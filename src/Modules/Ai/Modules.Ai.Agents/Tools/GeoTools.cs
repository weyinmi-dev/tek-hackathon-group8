using System.ComponentModel;
using MediatR;
using Modules.Ai.Application.Tools;

namespace Modules.Ai.Agents.Tools;

/// <summary>Geo capability tools (Phase 2 §6.2). Two composed tools, not five OSM primitives.</summary>
public sealed class GeoTools(ISender sender)
{
    [Description("Return the geographic context for a tower or site: coordinates, place name, whether the area is urban/suburban/rural/remote, how accessible it is, and the distance to the nearest fuel station. Use this for questions about where a site is, how hard it is to reach, or refuelling logistics.")]
    public Task<string> GetSiteGeoContext(
        [Description("The tower or site code, e.g. LOS-T-014.")] string siteCode,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new GetSiteGeoContextQuery(siteCode), cancellationToken);

    [Description("Classify the area around a tower or site as urban, suburban, rural or remote, with an accessibility score. Use this when only the character of the area matters and the full geo context is not needed.")]
    public Task<string> ClassifyRegion(
        [Description("The tower or site code, e.g. LOS-T-014.")] string siteCode,
        CancellationToken cancellationToken = default)
        => ToolResult.DispatchAsync(sender, new ClassifyRegionQuery(siteCode), cancellationToken);
}
