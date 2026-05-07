using System.Text.Json;
using System.Text.Json.Serialization;
using Modules.Network.Domain.Ingestion;

namespace Modules.Ai.Infrastructure.Pipeline;

/// <summary>
/// Shared JSON configuration for the Stage 2 AI path.
/// camelCase + string-encoded enums match what the prompts ask the model to produce,
/// so the model's output deserializes into our typed records without casing tricks.
/// </summary>
internal static class AiPipelineJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Compact JSON array of events suitable for a prompt: one object per event,
    /// camelCase keys, nulls omitted. Keeps the prompt small — token cost matters
    /// when the batch can run to thousands of rows.
    /// </summary>
    public static string SerializeEvents(IReadOnlyList<NetworkEvent> events) =>
        JsonSerializer.Serialize(
            events.Select(e => new
            {
                timestamp = e.OccurredAt,
                towerCode = e.TowerCode,
                signalPct = e.SignalPct,
                loadPct = e.LoadPct,
                latencyMs = e.LatencyMs,
                status = e.RawStatus
            }),
            Options);
}
