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

    /// <summary>Serializer options for StateBag get/set — the caller and this provider must share them.</summary>
    public static readonly System.Text.Json.JsonSerializerOptions StateBagJson = new(System.Text.Json.JsonSerializerDefaults.Web);

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

    protected override ValueTask StoreChatHistoryAsync(
        InvokedContext context,
        CancellationToken cancellationToken = default)
    {
        // Intentionally a no-op. In the copilot flow the caller (AskCopilotCommandHandler) persists
        // each turn itself: the assistant message carries provider/confidence/skill-trace metadata
        // the raw model response doesn't, and the caller owns the conversation activity + the EF
        // concurrency workaround. Storing here too would double-write. This provider's role is to
        // LOAD prior turns (ProvideChatHistoryAsync) so history reaches the model; the caller persists.
        return ValueTask.CompletedTask;
    }

    private static bool TryGetConversationId(AgentSession? session, out Guid conversationId)
    {
        conversationId = Guid.Empty;
        if (session is not null
            && session.StateBag.TryGetValue(ConversationIdKey, out string? raw, StateBagJson)
            && Guid.TryParse(raw, out conversationId))
        {
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
