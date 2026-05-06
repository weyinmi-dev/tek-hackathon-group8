using Modules.Network.Application.Ingestion.Stage2_Analyze.Contracts;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

/// <summary>
/// One of three SK-backed skills the batch analyzer composes. Lifted behind an
/// interface so the analyzer wrapper (validate + retry + combine logic) can be
/// unit-tested with stubs, while the live SK plumbing only runs in integration tests.
/// </summary>
internal interface INetworkAnomalySkill
{
    Task<Result<IReadOnlyList<DetectedAnomaly>>> InvokeAsync(string eventsJson, CancellationToken cancellationToken = default);
}

internal interface INetworkOptimizationSkill
{
    Task<Result<IReadOnlyList<ProposedOptimization>>> InvokeAsync(string eventsJson, CancellationToken cancellationToken = default);
}

internal interface INetworkTopologyMappingSkill
{
    Task<Result<TopologyDelta?>> InvokeAsync(string eventsJson, CancellationToken cancellationToken = default);
}

internal interface INetworkEnergySkill
{
    /// <summary>
    /// Returns a JSON blob containing energy-related observations or suggested
    /// energy-relevant findings derived from the provided events. The analyzer
    /// treats this as an opaque JSON string so the Network pipeline can optionally
    /// consume or ignore it without taking a compile-time dependency on the
    /// Energy module types.
    /// </summary>
    Task<Result<string>> InvokeAsync(string eventsJson, CancellationToken cancellationToken = default);
}
