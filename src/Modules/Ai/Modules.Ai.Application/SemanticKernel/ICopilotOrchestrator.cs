using Modules.Ai.Application.Copilot.AskCopilot;

namespace Modules.Ai.Application.SemanticKernel;

/// <summary>
/// Orchestrates the Semantic Kernel skill chain that turns a natural-language
/// engineer query into a structured Copilot answer (root cause, impact, actions,
/// confidence). Implemented in Infrastructure with either Azure OpenAI or a
/// deterministic mock provider — controlled by the <c>Ai:Provider</c> config key.
/// </summary>
public interface ICopilotOrchestrator
{
    Task<CopilotAnswer> AskAsync(string query, CancellationToken cancellationToken = default);
}
