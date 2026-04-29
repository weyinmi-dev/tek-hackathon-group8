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

        // Resolve or create the conversation. We persist the user's message BEFORE the
        // orchestrator call so a slow/failed AI call still leaves the question in history;
        // the user can refresh, see their question, and retry.
        Conversation conversation = await ResolveConversationAsync(request, cancellationToken);
        Message userMessage = conversation.AppendMessage(MessageRole.User, request.Query);
        await uow.SaveChangesAsync(cancellationToken);

        CopilotAnswer answer;
        try
        {
            answer = await orchestrator.AskAsync(request.Query, request.ActorRole, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI orchestrator failed for query from {ActorHandle}", request.ActorHandle);
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

        await uow.SaveChangesAsync(cancellationToken);

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

    private async Task<Conversation> ResolveConversationAsync(AskCopilotCommand request, CancellationToken ct)
    {
        if (request.ConversationId is { } existingId)
        {
            Conversation? existing = await conversations.GetAsync(existingId, ct);
            if (existing is not null && existing.UserId == request.UserId)
            {
                return existing;
            }
            // Falls through: stale/foreign id → new conversation. Don't 404 — UX is better.
            logger.LogInformation("Conversation {ConversationId} not found or not owned by {UserId}; starting fresh.", existingId, request.UserId);
        }

        var fresh = Conversation.Start(request.UserId, request.ActorHandle, initialTitle: request.Query);
        await conversations.AddAsync(fresh, ct);
        return fresh;
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
