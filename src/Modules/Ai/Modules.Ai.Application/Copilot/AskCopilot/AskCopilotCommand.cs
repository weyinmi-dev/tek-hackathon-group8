using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Copilot.AskCopilot;

/// <summary>
/// Ask the Copilot a question. <c>UserId</c> + <c>ConversationId</c> drive durable
/// chat persistence — when ConversationId is null the handler creates a new
/// conversation owned by UserId; the caller gets the resulting id back so the
/// UI can pin the active session.
/// </summary>
public sealed record AskCopilotCommand(
    string Query,
    Guid UserId,
    string ActorHandle,
    string ActorRole,
    Guid? ConversationId) : ICommand<CopilotAnswer>;

/// <summary>
/// Orchestrator output enriched with persistence identifiers by the command handler.
/// Orchestrators leave the three Guid fields at <c>Guid.Empty</c>; the handler
/// rewrites them with <c>with { ... }</c> after writing to the conversations table.
/// </summary>
public sealed record CopilotAnswer(
    string Answer,
    double Confidence,
    IReadOnlyList<SkillTraceEntry> SkillTrace,
    IReadOnlyList<string> Attachments,
    string Provider,
    Guid ConversationId = default,
    Guid UserMessageId = default,
    Guid AssistantMessageId = default);

public sealed record SkillTraceEntry(string Skill, string Function, int DurationMs, string Status);
