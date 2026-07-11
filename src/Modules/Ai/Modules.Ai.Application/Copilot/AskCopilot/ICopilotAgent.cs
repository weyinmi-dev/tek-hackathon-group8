namespace Modules.Ai.Application.Copilot.AskCopilot;

/// <summary>
/// The copilot seam (Phase 3 M11): answers a query as the operations copilot, grounded in the given
/// conversation's history. Replaces <c>ICopilotOrchestrator</c>. The implementation lives in the
/// Agents/Infrastructure layer and drives the MAF <c>OperationsCopilotAgent</c> with an
/// <c>AgentSession</c> bound to <paramref name="conversationId"/>, so the chat-history and knowledge
/// context providers load prior turns and persist the new exchange — the Application layer never
/// references the AI framework.
/// </summary>
public interface ICopilotAgent
{
    Task<CopilotAnswer> AskAsync(
        string query,
        Guid conversationId,
        string actorRole,
        CancellationToken cancellationToken = default);
}
