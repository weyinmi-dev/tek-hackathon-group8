using Application.Abstractions.Messaging;
using Modules.Ai.Application.SemanticKernel;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using Modules.Analytics.Api;
using SharedKernel;

namespace Modules.Ai.Application.Copilot.AskCopilot;

internal sealed class AskCopilotCommandHandler(
    ICopilotOrchestrator orchestrator,
    IChatLogRepository chatLog,
    IUnitOfWork uow,
    IAnalyticsApi analytics)
    : ICommandHandler<AskCopilotCommand, CopilotAnswer>
{
    public async Task<Result<CopilotAnswer>> Handle(AskCopilotCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Result.Failure<CopilotAnswer>(Error.NotFound("Copilot.QueryRequired", "Query is required."));
        }

        CopilotAnswer answer = await orchestrator.AskAsync(request.Query, cancellationToken);

        // Persist the conversation for the audit log + cross-module analytics.
        await chatLog.AddAsync(ChatLog.Record(
            userId: null,
            actor: request.ActorHandle,
            question: request.Query,
            answer: answer.Answer,
            skillTrace: string.Join(" → ", answer.SkillTrace.Select(s => $"{s.Skill}.{s.Function}")),
            confidence: answer.Confidence), cancellationToken);
        await uow.SaveChangesAsync(cancellationToken);

        // Audit trail entry — appears in /audit page immediately.
        await analytics.RecordAsync(
            actor: request.ActorHandle,
            role: request.ActorRole,
            action: "copilot.query",
            target: request.Query.Length > 200 ? request.Query[..200] : request.Query,
            sourceIp: "-",
            cancellationToken);

        return Result.Success(answer);
    }
}
