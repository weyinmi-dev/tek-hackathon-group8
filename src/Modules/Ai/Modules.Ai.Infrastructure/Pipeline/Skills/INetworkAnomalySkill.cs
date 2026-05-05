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
