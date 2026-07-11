using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Modules.Ai.Agents.Infrastructure;

/// <summary>
/// Offline-mode chat client (Phase 2 D7). Sits <em>below</em> the agent as a swapped
/// <see cref="IChatClient"/>, so the offline path exercises the same agents, tools and
/// context providers as production — only the model call is replaced with a deterministic
/// stand-in. This dissolves the Phase 1 §4.6 divergence where Mock and Azure ran different
/// retrieval algorithms behind one interface.
///
/// The response is intentionally simple: it echoes the latest user turn inside a fixed
/// envelope. Per-agent offline fidelity (structured JSON for the analysis agents, a grounded
/// answer for the copilot) is refined when each agent is wired (M9/M11/M12); M6 only needs a
/// valid client the agents can run against without a network or database.
/// </summary>
public sealed class DeterministicChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var reply = new ChatMessage(ChatRole.Assistant, BuildReply(messages, options));
        return Task.FromResult(new ChatResponse(reply));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, BuildReply(messages, options));
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // Nothing to release — the deterministic client holds no resources.
    }

    private static string BuildReply(IEnumerable<ChatMessage> messages, ChatOptions? options)
    {
        List<ChatMessage> all = messages.ToList();

        // Document-intake gate: when the instructions are the knowledge-base gatekeeper prompt,
        // answer as that gatekeeper so offline uploads still reach Indexed instead of being
        // rejected for want of a model. Without this, DocumentIngestionWorkflow's ValidateRelevance
        // reads "OFFLINE MODE" as not-relevant and rejects every document in an offline stack.
        // AsAIAgent supplies the agent's instructions via ChatOptions.Instructions, NOT as a
        // message, so the gate marker is searched there as well as in the messages.
        string messageText = string.Join("\n", all.Select(m => m.Text));
        string context = messageText + "\n" + (options?.Instructions ?? string.Empty);
        // The gatekeeper markers live in the agent instructions (via ChatOptions.Instructions); the
        // "File: … Category: …" shape is what ValidateRelevanceExecutor puts in the user message and
        // is always present regardless of how instructions are threaded — either is enough.
        bool isIntakeGate =
            context.Contains("RELEVANT or IRRELEVANT", StringComparison.OrdinalIgnoreCase)
            || context.Contains("quality-control gatekeeper", StringComparison.OrdinalIgnoreCase)
            || (messageText.Contains("File:", StringComparison.OrdinalIgnoreCase)
                && messageText.Contains("Category:", StringComparison.OrdinalIgnoreCase));
        if (isIntakeGate)
        {
            return "RELEVANT\nOffline mode: accepted without model review.";
        }

        string? lastUser = all.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        return string.IsNullOrWhiteSpace(lastUser)
            ? "OFFLINE MODE: deterministic response (no model configured)."
            : $"OFFLINE MODE: deterministic response to \"{lastUser.Trim()}\".";
    }
}
