using System.Text.Json;
using Application.Abstractions.Messaging;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.SemanticKernel;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using Modules.Analytics.Api;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.AskCopilot;

internal sealed class AskCopilotCommandHandler(
    ICopilotOrchestrator orchestrator,
    IConversationRepository conversations,
    IChatLogRepository chatLog,
    IUnitOfWork uow,
    IAnalyticsApi analytics,
    ILogger<AskCopilotCommandHandler> logger)
    : ICommandHandler<AskCopilotCommand, CopilotAnswer>
{
    private static readonly JsonSerializerOptions MetadataJson = new() { WriteIndented = false };

    public async Task<Result<CopilotAnswer>> Handle(AskCopilotCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Copilot query received from {ActorHandle} (user {UserId}, conversation {ConversationId}), query length {QueryLength}",
            request.ActorHandle, request.UserId, request.ConversationId, request.Query.Length);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Result.Failure<CopilotAnswer>(Error.Problem("Copilot.QueryRequired", "Query is required."));
        }

        // Resolve or create the conversation, append the user's question, and call the AI.
        //
        // Why we don't just SaveChanges over the tracked Conversation entity: when the
        // conversation already exists, EF Core was reproducibly throwing
        // DbUpdateConcurrencyException ("expected 1 row, got 0") on the UPDATE that flushes
        // the activity scalars (MessageCount, LastMessageAtUtc, UpdatedAtUtc) — even though
        // the row demonstrably existed and there's no concurrency token configured. We have
        // not pinned down the underlying EF/Npgsql cause; instead we sidestep it by detaching
        // the existing entity before SaveChanges and writing those scalars via a direct
        // ExecuteUpdate after the inserts succeed. Newly-created conversations stay on the
        // normal INSERT path (the bug only affects UPDATE).
        (Conversation conversation, bool isExisting) = await ResolveConversationAsync(request, cancellationToken);
        Message userMessage = conversation.AppendMessage(MessageRole.User, request.Query);

        CopilotAnswer answer;
        try
        {
            answer = await orchestrator.AskAsync(request.Query, request.ActorRole, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI orchestrator failed for query from {ActorHandle}", request.ActorHandle);
            // Persist the user question even though the AI never answered, so a refresh shows
            // the question (and the user can retry) instead of silently dropping the turn.
            try
            {
                await PersistAndUpdateActivityAsync(conversation, isExisting, cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx, "Failed to persist user question after AI failure for {ActorHandle}", request.ActorHandle);
            }
            return Result.Failure<CopilotAnswer>(Error.Problem("Copilot.AiFailure", "AI service is temporarily unavailable."));
        }

        logger.LogInformation("Copilot response: confidence {Confidence}, provider {Provider}, skills {SkillCount}",
            answer.Confidence, answer.Provider, answer.SkillTrace.Count);

        // Persist the assistant turn with the provider/trace/confidence carried in metadata
        // so on session restore the UI can render the same answer card without re-asking.
        string metadata = JsonSerializer.Serialize(new MessageMetadata(
            Provider: answer.Provider,
            Confidence: answer.Confidence,
            SkillTrace: [.. answer.SkillTrace],
            Attachments: [.. answer.Attachments]), MetadataJson);

        Message assistantMessage = conversation.AppendMessage(MessageRole.Assistant, answer.Answer, metadata);

        // ChatLog stays as a flat audit row for cross-module analytics — kept for backward
        // compat with the audit page and any external dashboards. Conversations are the new
        // user-facing source of truth.
        await chatLog.AddAsync(ChatLog.Record(
            userId: request.UserId == Guid.Empty ? null : request.UserId,
            actor: request.ActorHandle,
            question: request.Query,
            answer: answer.Answer,
            skillTrace: string.Join(" → ", answer.SkillTrace.Select(s => $"{s.Skill}.{s.Function}")),
            confidence: answer.Confidence), cancellationToken);

        await PersistAndUpdateActivityAsync(conversation, isExisting, cancellationToken);

        await analytics.RecordAsync(
            actor: request.ActorHandle,
            role: request.ActorRole,
            action: "copilot.query",
            target: request.Query.Length > 200 ? request.Query[..200] : request.Query,
            sourceIp: "-",
            cancellationToken);

        return Result.Success(answer with
        {
            ConversationId = conversation.Id,
            UserMessageId = userMessage.Id,
            AssistantMessageId = assistantMessage.Id,
        });
    }

    private async Task<(Conversation conversation, bool isExisting)> ResolveConversationAsync(AskCopilotCommand request, CancellationToken ct)
    {
        if (request.ConversationId is { } existingId)
        {
            Conversation? existing = await conversations.GetAsync(existingId, ct);
            if (existing is not null && existing.UserId == request.UserId)
            {
                return (existing, true);
            }
            // Falls through: stale/foreign id → new conversation. Don't 404 — UX is better.
            logger.LogInformation("Conversation {ConversationId} not found or not owned by {UserId}; starting fresh.", existingId, request.UserId);
        }

        var fresh = Conversation.Start(request.UserId, request.ActorHandle, initialTitle: request.Query);
        await conversations.AddAsync(fresh, ct);
        return (fresh, false);
    }

    /// <summary>
    /// For new conversations: a normal SaveChanges INSERTs everything (conversation + messages + chat_log).
    /// For existing conversations: detach the conversation BEFORE SaveChanges so EF doesn't emit the
    /// failing UPDATE on it, let SaveChanges INSERT just the new messages + chat_log, then write the
    /// activity scalars with a direct ExecuteUpdate (which bypasses the change tracker).
    /// </summary>
    private async Task PersistAndUpdateActivityAsync(Conversation conversation, bool isExisting, CancellationToken ct)
    {
        if (isExisting)
        {
            // Snapshot the values we need before detaching — once detached, the entity is no
            // longer special, but we still hold the reference, so reading is fine.
            int messageCount = conversation.MessageCount;
            DateTime updatedAtUtc = conversation.UpdatedAtUtc;
            DateTime lastMessageAtUtc = conversation.LastMessageAtUtc ?? conversation.UpdatedAtUtc;
            Guid conversationId = conversation.Id;

            conversations.Detach(conversation);
            await uow.SaveChangesAsync(ct);
            await conversations.UpdateActivityAsync(conversationId, messageCount, updatedAtUtc, lastMessageAtUtc, ct);
            return;
        }

        await uow.SaveChangesAsync(ct);
    }
}

/// <summary>
/// JSON shape stored in <c>messages.metadata</c> for assistant turns. Keeps the answer
/// card fully reconstructible without re-running the LLM.
/// </summary>
internal sealed record MessageMetadata(
    string Provider,
    double Confidence,
    IReadOnlyList<SkillTraceEntry> SkillTrace,
    IReadOnlyList<string> Attachments);
