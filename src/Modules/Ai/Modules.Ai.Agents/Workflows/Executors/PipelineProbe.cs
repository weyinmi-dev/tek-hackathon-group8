using Microsoft.Agents.AI.Workflows;

namespace Modules.Ai.Agents.Workflows.Executors;

// A throwaway probe that pins the orchestration shape DocumentIngestionWorkflow needs: a linear
// chain (extract → validate → chunk → …) with a conditional split (validate → chunk on accept,
// → reject on reject), plus resume from a non-terminal mid-pipeline checkpoint. Toy int math keeps
// it dependency-free so it runs in the harness without ports, a database, or a chat client. The
// single-executor foundation it builds on only proved run + output + one terminal checkpoint.
//
// Fan-in note (for NetworkLogAnalysisWorkflow at M12, not proved here): AddFanInBarrierEdge waits
// for all sources but delivers each upstream message to the target SEPARATELY (target handler fires
// once per message), so a join that aggregates must accumulate across invocations via IWorkflowContext
// state — it does NOT receive an IReadOnlyList in one call. Verified empirically 2026-07-11.
//
// Deleted once the real executors exercise these patterns for real.
public sealed record IngestOutcome(string Label);

// Chain step 1: double the seed and pass it on.
public sealed partial class IngestExecutor() : Executor("ingest")
{
    [MessageHandler]
    public ValueTask<int> HandleAsync(int seed, IWorkflowContext context)
        => ValueTask.FromResult(seed * 2);
}

// Chain step 2: the split point. The value flows out unchanged; the two downstream edges carry the
// accept/reject predicates, so routing is data-driven exactly like validate → chunk | reject.
public sealed partial class GateExecutor() : Executor("gate")
{
    [MessageHandler]
    public ValueTask<int> HandleAsync(int n, IWorkflowContext context)
        => ValueTask.FromResult(n);
}

// Accept branch — reached only when the gate edge predicate (n >= 20) holds.
public sealed partial class KeepExecutor() : Executor("keep")
{
    [MessageHandler]
    public ValueTask<IngestOutcome> HandleAsync(int n, IWorkflowContext context)
        => ValueTask.FromResult(new IngestOutcome($"KEPT:{n}"));
}

// Reject branch — reached only when (n < 20) holds.
public sealed partial class DropExecutor() : Executor("drop")
{
    [MessageHandler]
    public ValueTask<IngestOutcome> HandleAsync(int n, IWorkflowContext context)
        => ValueTask.FromResult(new IngestOutcome($"DROPPED:{n}"));
}
