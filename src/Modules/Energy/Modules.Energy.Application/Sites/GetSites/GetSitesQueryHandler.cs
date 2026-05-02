using Application.Abstractions.Messaging;
using Modules.Energy.Domain.Sites;
using SharedKernel;

namespace Modules.Energy.Application.Sites.GetSites;

internal sealed class GetSitesQueryHandler(ISiteRepository sites)
    : IQueryHandler<GetSitesQuery, SitesResponse>
{
    public async Task<Result<SitesResponse>> Handle(GetSitesQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Site> rows = await sites.ListAsync(cancellationToken);
        IReadOnlyList<SiteDto> dtos = rows
            .Select(s => new SiteDto(
                s.Code, s.Name, s.Region,
                s.Source.ToWire(),
                s.BattPct, s.DieselPct, s.SolarKw, s.GridUp,
                s.DailyDieselLitres, s.DailyCostNgn, s.UptimePct, s.HasSolar,
                s.Health.ToWire(), s.AnomalyNote))
            .ToList();
        return Result.Success(new SitesResponse(dtos));
    }
}
