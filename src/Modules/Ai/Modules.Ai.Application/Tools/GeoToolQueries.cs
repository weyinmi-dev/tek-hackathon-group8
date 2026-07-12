using Application.Abstractions.Messaging;
using Modules.Ai.Application.Geo;
using SharedKernel;

namespace Modules.Ai.Application.Tools;

// Canonical Geo tool queries (Phase 2 §6.2). The five OSM primitives the old system exposed collapse
// to these two composed tools — the old system prompt already told the model to "prefer
// osm_get_site_geocontext (one call, cached) over invoking the four primitives separately", which was
// prose compensating for a tool-design problem. Fixing the tools deletes the prose.

/// <summary>get_site_geocontext — where a site is, what is around it, and how far the nearest fuel is.</summary>
public sealed record GetSiteGeoContextQuery(string SiteCode) : IQuery<SiteGeoSummary>;

internal sealed class GetSiteGeoContextQueryHandler(IGeoContextProvider geo)
    : IQueryHandler<GetSiteGeoContextQuery, SiteGeoSummary>
{
    public async Task<Result<SiteGeoSummary>> Handle(GetSiteGeoContextQuery request, CancellationToken cancellationToken)
    {
        SiteGeoSummary? context = await geo.GetSiteContextAsync(request.SiteCode, cancellationToken);

        // A miss is a legitimate answer, not an exception: the site may be unknown, or the geo provider
        // may be switched off. Returning a failure means the model is told so, plainly, instead of being
        // handed an empty object it might read as "no fuel station nearby".
        return context is null
            ? Result.Failure<SiteGeoSummary>(Error.NotFound(
                "Geo.SiteNotFound",
                $"No geo context is available for site '{request.SiteCode}'."))
            : Result.Success(context);
    }
}

/// <summary>classify_region — urban / suburban / rural / remote, without the rest of the context.</summary>
public sealed record ClassifyRegionQuery(string SiteCode) : IQuery<RegionClassification>;

internal sealed class ClassifyRegionQueryHandler(IGeoContextProvider geo)
    : IQueryHandler<ClassifyRegionQuery, RegionClassification>
{
    public async Task<Result<RegionClassification>> Handle(ClassifyRegionQuery request, CancellationToken cancellationToken)
    {
        RegionClassification? classification = await geo.ClassifyRegionAsync(request.SiteCode, cancellationToken);

        return classification is null
            ? Result.Failure<RegionClassification>(Error.NotFound(
                "Geo.SiteNotFound",
                $"No geo context is available for site '{request.SiteCode}'."))
            : Result.Success(classification);
    }
}
