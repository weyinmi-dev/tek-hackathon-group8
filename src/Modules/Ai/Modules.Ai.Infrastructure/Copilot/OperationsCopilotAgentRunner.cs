using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Modules.Ai.Agents.Agents;
using Modules.Ai.Agents.Configuration;
using Modules.Ai.Agents.Memory;
using Modules.Ai.Application.Copilot.AskCopilot;

namespace Modules.Ai.Infrastructure.Copilot;

/// <summary>
/// Drives the MAF <see cref="OperationsCopilotAgentBuilder">operations copilot agent</see> for the
/// Application layer (Phase 3 M11), replacing the Semantic Kernel orchestrator. It opens an
/// <see cref="AgentSession"/> bound to the conversation via the StateBag key the
/// <see cref="PostgresChatHistoryProvider"/> reads, so the history + knowledge providers load prior
/// turns and persist the new exchange; the agent's answer and tool calls are mapped back into the
/// existing <see cref="CopilotAnswer"/> shape so the API contract is unchanged.
/// </summary>
internal sealed class OperationsCopilotAgentRunner(
    OperationsCopilotAgentBuilder builder,
    ILogger<OperationsCopilotAgentRunner> logger) : ICopilotAgent
{
    public async Task<CopilotAnswer> AskAsync(
        string query,
        Guid conversationId,
        string actorRole,
        CancellationToken cancellationToken = default)
    {
        AIAgent agent = builder.Build();

        // Bind the session to this conversation so PostgresChatHistoryProvider loads its prior turns
        // (history reaches the model) and stores the new user + assistant exchange afterwards.
        AgentSession session = await agent.CreateSessionAsync(cancellationToken);
        // Stored as a string: the StateBag serializes values as JSON, and a string round-trips
        // cleanly on both sides (the provider parses it back). See PostgresChatHistoryProvider.
        session.StateBag.SetValue(
            PostgresChatHistoryProvider.ConversationIdKey,
            conversationId.ToString(),
            PostgresChatHistoryProvider.StateBagJson);

        AgentResponse response = await agent.RunAsync(query, session, cancellationToken: cancellationToken);
        string answer = response.ToString() ?? string.Empty;

        List<SkillTraceEntry> skillTrace = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Select(call => new SkillTraceEntry(
                Skill: AgentNames.OperationsCopilot,
                Function: call.Name,
                DurationMs: 0,
                Status: "invoked"))
            .ToList();

        logger.LogInformation(
            "Copilot agent answered conversation {ConversationId}: {ToolCount} tool call(s), {AnswerLength} chars.",
            conversationId, skillTrace.Count, answer.Length);

        return new CopilotAnswer(
            Answer: answer,
            Confidence: 0.9,
            SkillTrace: skillTrace,
            Attachments: [],
            Provider: AgentNames.OperationsCopilot,
            ConversationId: conversationId);
    }
}
