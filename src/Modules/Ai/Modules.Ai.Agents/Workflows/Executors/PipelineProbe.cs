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

// ── Fan-out / stateful fan-in join (NetworkLogAnalysisWorkflow's shape) ──────────
// split → (a ∥ b ∥ c) → join → sink. AddFanInBarrierEdge delivers a, b, c to the join
// as three SEPARATE invocations, so the join must accumulate them. This probe answers the
// open question of WHERE that accumulation can live: MAF is a superstep/BSP model, and it
// determines whether IWorkflowContext state written in one invocation is visible to the next
// invocation in the same superstep. The harness join smoke checks the emitted total against
// the arithmetic, which only holds if accumulation actually works.
public sealed record Partial(int Value);

public sealed record Joined(int Total);

public sealed partial class SplitExecutor() : Executor("split")
{
    [MessageHandler]
    public ValueTask<int> HandleAsync(int seed, IWorkflowContext context)
        => ValueTask.FromResult(seed);
}

public sealed partial class WorkerAExecutor() : Executor("worker-a")
{
    [MessageHandler]
    public ValueTask<Partial> HandleAsync(int n, IWorkflowContext context)
        => ValueTask.FromResult(new Partial(n));
}

public sealed partial class WorkerBExecutor() : Executor("worker-b")
{
    [MessageHandler]
    public ValueTask<Partial> HandleAsync(int n, IWorkflowContext context)
        => ValueTask.FromResult(new Partial(n * 2));
}

public sealed partial class WorkerCExecutor() : Executor("worker-c")
{
    [MessageHandler]
    public ValueTask<Partial> HandleAsync(int n, IWorkflowContext context)
        => ValueTask.FromResult(new Partial(n * 3));
}

// The join: accumulate `expected` partials via context state, then emit once. The handler RETURNS
// the message (a nullable type) rather than calling SendMessageAsync — a void handler's send is not
// declared to the workflow and gets dropped, whereas the return type declares the output. Returning
// null on the partial invocations emits nothing; the final invocation returns the joined result.
// Context state IS visible to later invocations within the same superstep (verified 2026-07-11), so
// the running count/sum survive across the three barrier deliveries.
public sealed partial class JoinExecutor(int expected) : Executor("join")
{
    [MessageHandler]
    public async ValueTask<Joined?> HandleAsync(Partial part, IWorkflowContext context)
    {
        int count = await context.ReadStateAsync<int>("count") + 1;
        int sum = await context.ReadStateAsync<int>("sum") + part.Value;
        await context.QueueStateUpdateAsync("count", count);
        await context.QueueStateUpdateAsync("sum", sum);
        if (Environment.GetEnvironmentVariable("WF_DEBUG") == "1")
        {
            Console.WriteLine($"  [join] part={part.Value} -> count={count} sum={sum} (need {expected})");
        }
        return count >= expected ? new Joined(sum) : null;
    }
}
