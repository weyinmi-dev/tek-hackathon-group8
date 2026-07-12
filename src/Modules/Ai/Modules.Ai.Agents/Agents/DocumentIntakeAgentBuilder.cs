using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Agents.Configuration;
using Modules.Ai.Agents.Prompts;

namespace Modules.Ai.Agents.Agents;

/// <summary>
/// Builds the document-intake agent (Phase 2 §5.1): filename + text preview → a relevance decision
/// and extracted metadata. Toolless classification, invoked by the document-ingestion workflow's
/// validation step (Phase 2 §7.1).
/// </summary>
public sealed class DocumentIntakeAgentBuilder(IChatClient chatClient)
{
    public AIAgent Build() => chatClient.AsAIAgent(
        instructions: AgentPrompts.DocumentIntake,
        name: AgentNames.DocumentIntake);
}
