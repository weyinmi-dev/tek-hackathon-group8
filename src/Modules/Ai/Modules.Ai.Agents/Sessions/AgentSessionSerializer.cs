using System.Text.Json;
using Microsoft.Agents.AI;

namespace Modules.Ai.Agents.Sessions;

/// <summary>
/// Serialises and restores an <see cref="AgentSession"/> so a copilot conversation can survive across
/// requests. MAF owns the wire format; this is the thin seam the hosting layer calls. Persisted state
/// is bound to the authenticated user at the storage layer (Phase 2 D5) — this type only converts.
/// </summary>
/// <remarks>The <see cref="JsonSerializerOptions"/> are shared, immutable configuration, not session
/// state, so a single serializer instance is safe to share across users.</remarks>
public sealed class AgentSessionSerializer(JsonSerializerOptions? jsonOptions = null)
{
    private readonly JsonSerializerOptions _jsonOptions = jsonOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public ValueTask<JsonElement> SerializeAsync(
        AIAgent agent,
        AgentSession session,
        CancellationToken cancellationToken = default) =>
        agent.SerializeSessionAsync(session, _jsonOptions, cancellationToken);

    public ValueTask<AgentSession> DeserializeAsync(
        AIAgent agent,
        JsonElement state,
        CancellationToken cancellationToken = default) =>
        agent.DeserializeSessionAsync(state, _jsonOptions, cancellationToken);
}
