using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

/// <summary>
/// One of four SK-backed skills the batch analyzer composes. Lifted behind an
/// interface so the analyzer wrapper (validate + retry + combine logic) can be
/// unit-tested with stubs, while the live SK plumbing only runs in integration tests.
///
/// <paramref name="rawContext"/> is an optional excerpt (≤3000 chars) of the original
/// uploaded file read via <c>IFileStagingService</c>. When present it is included as
/// additional prompt context so the model can ground analysis in the source document
/// rather than only the parsed events array.
/// </summary>
internal interface INetworkAnomalySkill
{
    Task<Result<IReadOnlyList<DetectedAnomaly>>> InvokeAsync(
        string eventsJson,
        string? rawContext = null,
        CancellationToken cancellationToken = default);
}

internal interface INetworkOptimizationSkill
{
    Task<Result<IReadOnlyList<ProposedOptimization>>> InvokeAsync(
        string eventsJson,
        string? rawContext = null,
        CancellationToken cancellationToken = default);
}

internal interface INetworkTopologyMappingSkill
{
    Task<Result<TopologyDelta?>> InvokeAsync(
        string eventsJson,
        string? rawContext = null,
        CancellationToken cancellationToken = default);
}

internal interface INetworkEnergySkill
{
    /// <summary>
    /// Returns a JSON blob containing energy-related observations derived from the
    /// provided events. The analyzer treats this as an opaque JSON string so the
    /// Network pipeline can consume it without a compile-time dependency on Energy
    /// module types. <paramref name="rawContext"/> follows the same convention as
    /// the other skills — optional raw file excerpt for additional grounding.
    /// </summary>
    Task<Result<string>> InvokeAsync(
        string eventsJson,
        string? rawContext = null,
        CancellationToken cancellationToken = default);
}
