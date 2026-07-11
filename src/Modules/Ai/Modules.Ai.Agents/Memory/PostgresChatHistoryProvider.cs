using MediatR;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Application.Copilot.Conversations;
using SharedKernel;

namespace Modules.Ai.Agents.Memory;

/// <summary>
/// A MAF <see cref="ChatHistoryProvider"/> that persists conversation turns through the Ai module's
/// CQRS layer (Phase 2 D8 — the provider never touches a repository, only ISender).
/// </summary>
/// <remarks>
/// One instance serves every session concurrently, so it holds NO per-session state in fields —
/// that would leak one user's history into another's (Phase 2 D5; MAF's own documented requirement).
/// The conversation a session maps to is read from <see cref="AgentSession.StateBag"/>, set by the
/// caller that opens the session (M10 wiring).
/// </remarks>
public sealed class PostgresChatHistoryProvider(ISender sender) : ChatHistoryProvider
{
    /// <summary>StateBag key under which the caller stores this session's conversation id.</summary>
    public const string ConversationIdKey = "ai.conversationId";

    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetConversationId(context.Session, out Guid conversationId))
        {
            return [];
        }

        Result<IReadOnlyList<ConversationMessageDto>> result =
            await sender.Send(new GetConversationMessagesQuery(conversationId), cancellationToken);

        return result.IsSuccess
            ? result.Value.Select(m => new ChatMessage(ToRole(m.Role), m.Content)).ToList()
            : [];
    }

    protected override async ValueTask StoreChatHistoryAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        // ResponseMessages is null when the invocation failed — nothing to persist.
        if (context.ResponseMessages is null || !TryGetConversationId(context.Session, out Guid conversationId))
        {
            return;
        }

        // Persist the new exchange only. ProvideChatHistoryAsync already loaded the prior turns, so
        // the delta is: the user message that triggered this turn (the last user message in the
        // accumulated input) plus the assistant response(s). The M10 call site must not also persist
        // the user turn, or it would double-store.
        var turns = new List<ConversationTurn>();
        ChatMessage? newUserMessage = context.RequestMessages.LastOrDefault(m => m.Role == ChatRole.User);
        if (newUserMessage is not null && !string.IsNullOrWhiteSpace(newUserMessage.Text))
        {
            turns.Add(new ConversationTurn(newUserMessage.Role.Value, newUserMessage.Text));
        }

        turns.AddRange(context.ResponseMessages
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            .Select(m => new ConversationTurn(m.Role.Value, m.Text)));

        if (turns.Count > 0)
        {
            await sender.Send(new AppendMessagesCommand(conversationId, turns), cancellationToken);
        }
    }

    private static bool TryGetConversationId(AgentSession? session, out Guid conversationId)
    {
        conversationId = Guid.Empty;
        if (session?.StateBag.TryGetValue(ConversationIdKey, out object? raw) == true && raw is Guid id)
        {
            conversationId = id;
            return true;
        }

        return false;
    }

    private static ChatRole ToRole(string role) => role.ToLowerInvariant() switch
    {
        "assistant" => ChatRole.Assistant,
        "system" => ChatRole.System,
        "tool" => ChatRole.Tool,
        _ => ChatRole.User,
    };
}
