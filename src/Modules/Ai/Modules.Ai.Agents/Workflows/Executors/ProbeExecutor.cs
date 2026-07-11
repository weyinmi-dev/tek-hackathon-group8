using Microsoft.Agents.AI.Workflows;

namespace Modules.Ai.Agents.Workflows.Executors;

// Temporary probe to pin the partial-Executor + [MessageHandler] source-gen pattern by compiling
// and running. A handler that returns a value declares that type as the executor's output and
// forwards it along the workflow edges. Deleted once the real executors are in.
public sealed partial class ProbeExecutor() : Executor("probe")
{
    [MessageHandler]
    public ValueTask<string> HandleAsync(string message, IWorkflowContext context)
        => ValueTask.FromResult(message.ToUpperInvariant());
}
