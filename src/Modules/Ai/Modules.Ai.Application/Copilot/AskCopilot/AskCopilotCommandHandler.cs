using Application.Abstractions.Messaging;
using Modules.Ai.Application.SemanticKernel;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Conversations;
using Modules.Analytics.Api;
using SharedKernel;
using Microsoft.Extensions.Logging;

namespace Modules.Ai.Application.Copilot.AskCopilot;

internal sealed class AskCopilotCommandHandler(
    ICopilotOrchestrator orchestrator,
    IChatLogRepository chatLog,
    IUnitOfWork uow,
    IAnalyticsApi analytics,
    ILogger<AskCopilotCommandHandler> logger)
    : ICommandHandler<AskCopilotCommand, CopilotAnswer>
{
    public async Task<Result<CopilotAnswer>> Handle(AskCopilotCommand request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Copilot query received from {ActorHandle} with role {ActorRole}, query length {QueryLength}",
            request.ActorHandle, request.ActorRole, request.Query.Length);

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Result.Failure<CopilotAnswer>(Error.NotFound("Copilot.QueryRequired", "Query is required."));
        }

        CopilotAnswer answer;
        try
        {
            answer = await orchestrator.AskAsync(request.Query, request.ActorRole, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI orchestrator failed for query from {ActorHandle}", request.ActorHandle);
            return Result.Failure<CopilotAnswer>(Error.Problem("Copilot.AiFailure", "AI service is temporarily unavailable."));
        }

        logger.LogInformation("Copilot response generated: confidence {Confidence}, provider {Provider}, skill trace count {SkillTraceCount}",
            answer.Confidence, answer.Provider, answer.SkillTrace.Count);

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
