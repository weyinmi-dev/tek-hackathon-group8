using Application.Abstractions.Caching;
using Application.Abstractions.Messaging;
using Modules.Energy.Domain.Events;
using SharedKernel;

namespace Modules.Energy.Application.Anomalies.GetAnomalies;

public sealed record GetAnomaliesQuery(int Take = 50) : IQuery<AnomaliesResponse>, ICachedQuery
{
    public string CacheKey => $"energy:anomalies:{Take}";
    public TimeSpan? Expiration => TimeSpan.FromSeconds(5);
}

public sealed record AnomaliesResponse(IReadOnlyList<AnomalyDto> Anomalies);

public sealed record AnomalyDto(
    Guid Id,
    string Site,
    string Kind,
    string Sev,
    string T,
    string Detail,
    double Conf,
    string Model,
    bool Acknowledged);

internal sealed class GetAnomaliesQueryHandler(IAnomalyEventRepository anomalies)
    : IQueryHandler<GetAnomaliesQuery, AnomaliesResponse>
{
    public async Task<Result<AnomaliesResponse>> Handle(GetAnomaliesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<AnomalyEvent> rows = await anomalies.ListAsync(request.Take, cancellationToken);
        IReadOnlyList<AnomalyDto> dtos = rows
            .Select(a => new AnomalyDto(
                a.Id,
                a.SiteCode,
                a.Kind.ToWire(),
                a.Severity.ToWire(),
                a.DetectedAtUtc.ToString("HH:mm"),
                a.Detail,
                a.Confidence,
                a.ModelName,
                a.Acknowledged))
            .ToList();
        return Result.Success(new AnomaliesResponse(dtos));
    }
}
