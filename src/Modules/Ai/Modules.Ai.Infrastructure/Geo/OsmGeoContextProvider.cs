using Modules.Ai.Application.Geo;
using Modules.Ai.Infrastructure.Mcp.Osm;

namespace Modules.Ai.Infrastructure.Geo;

/// <summary>
/// Implements the Application's geo port over the OpenStreetMap layer. This is the whole point of the
/// port: everything OSM-shaped stops here. <c>SiteGeoLookup</c> already caches the composite context,
/// so both tools are cheap after the first call for a site.
/// </summary>
internal sealed class OsmGeoContextProvider(ISiteGeoLookup lookup) : IGeoContextProvider
{
    public async Task<SiteGeoSummary?> GetSiteContextAsync(string siteCode, CancellationToken cancellationToken = default)
    {
        SiteGeoContext? context = await lookup.GetAsync(siteCode, cancellationToken);
        if (context is null)
        {
            return null;
        }

        OsmFuelStationDistance fuel = context.NearestFuelStation;

        return new SiteGeoSummary(
            SiteCode: context.SiteCode,
            Latitude: context.Coordinates.Latitude,
            Longitude: context.Coordinates.Longitude,
            PlaceName: context.Place?.DisplayName,
            RegionType: context.Classification.RegionType,
            AccessibilityScore: (int)Math.Round(context.Classification.AccessibilityScore),
            Reasoning: context.Classification.Reasoning,
            NearestFuelStationName: fuel.Found ? fuel.Name : null,
            // Metres are the OSM layer's unit; kilometres are what an operator asks in. Convert once,
            // here, rather than leaving the model to divide by 1000 and hope.
            NearestFuelStationKm: fuel is { Found: true, StraightLineMetres: { } metres }
                ? Math.Round(metres / 1000, 2)
                : null);
    }

    public async Task<RegionClassification?> ClassifyRegionAsync(string siteCode, CancellationToken cancellationToken = default)
    {
        SiteGeoContext? context = await lookup.GetAsync(siteCode, cancellationToken);
        if (context is null)
        {
            return null;
        }

        OsmRegionClassification classification = context.Classification;

        return new RegionClassification(
            SiteCode: context.SiteCode,
            RegionType: classification.RegionType,
            AccessibilityScore: (int)Math.Round(classification.AccessibilityScore),
            Reasoning: classification.Reasoning);
    }
}
