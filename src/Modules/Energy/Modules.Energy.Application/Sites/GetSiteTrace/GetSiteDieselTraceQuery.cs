using Application.Abstractions.Messaging;
using Modules.Energy.Domain.Telemetry;
using SharedKernel;

namespace Modules.Energy.Application.Sites.GetSiteTrace;

public sealed record GetSiteDieselTraceQuery(string SiteCode, int Hours = 24) : IQuery<DieselTraceResponse>;

public sealed record DieselTraceResponse(string SiteCode, IReadOnlyList<DieselTracePointDto> Points);

public sealed record DieselTracePointDto(DateTime At, int DieselPct, int LitresDelta);

internal sealed class GetSiteDieselTraceQueryHandler(ISiteEnergyLogRepository logs)
    : IQueryHandler<GetSiteDieselTraceQuery, DieselTraceResponse>
{
    public async Task<Result<DieselTraceResponse>> Handle(GetSiteDieselTraceQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<SiteEnergyLog> rows = await logs.ListForSiteAsync(request.SiteCode, request.Hours, cancellationToken);
        IReadOnlyList<DieselTracePointDto> points = rows
            .OrderBy(r => r.RecordedAtUtc)
            .Select(r => new DieselTracePointDto(r.RecordedAtUtc, r.DieselPct, 0))
            .ToList();
        return Result.Success(new DieselTraceResponse(request.SiteCode, points));
    }
}
