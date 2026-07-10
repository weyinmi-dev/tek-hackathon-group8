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
        var reply = new ChatMessage(ChatRole.Assistant, BuildReply(messages));
        return Task.FromResult(new ChatResponse(reply));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatResponseUpdate(ChatRole.Assistant, BuildReply(messages));
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

    private static string BuildReply(IEnumerable<ChatMessage> messages)
    {
        string? lastUser = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        return string.IsNullOrWhiteSpace(lastUser)
            ? "OFFLINE MODE: deterministic response (no model configured)."
            : $"OFFLINE MODE: deterministic response to \"{lastUser.Trim()}\".";
    }
}
