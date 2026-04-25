using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Copilot.AskCopilot;

public sealed record AskCopilotCommand(string Query, string ActorHandle, string ActorRole) : ICommand<CopilotAnswer>;

public sealed record CopilotAnswer(
    string Answer,
    double Confidence,
    IReadOnlyList<SkillTraceEntry> SkillTrace,
    IReadOnlyList<string> Attachments,
    string Provider);

public sealed record SkillTraceEntry(string Skill, string Function, int DurationMs, string Status);
