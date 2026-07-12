namespace Modules.Ai.Application.Geo;

/// <summary>
/// Where a site is and what is around it, in terms the model can quote directly.
/// </summary>
/// <param name="RegionType">urban | suburban | rural | remote.</param>
/// <param name="AccessibilityScore">0–100; higher means easier to reach and closer to dense infrastructure.</param>
/// <param name="NearestFuelStationKm">Straight-line distance, null when none was found — the number a diesel-logistics question turns on.</param>
public sealed record SiteGeoSummary(
    string SiteCode,
    double Latitude,
    double Longitude,
    string? PlaceName,
    string RegionType,
    int AccessibilityScore,
    string Reasoning,
    string? NearestFuelStationName,
    double? NearestFuelStationKm);

/// <summary>
/// The region's character, on its own — the cheap answer when the caller only needs to know whether
/// a site is urban or remote and does not care where the nearest fuel station is.
/// </summary>
public sealed record RegionClassification(
    string SiteCode,
    string RegionType,
    int AccessibilityScore,
    string Reasoning);

/// <summary>
/// The geo capability behind <c>GeoTools</c>, as an Application port.
///
/// The implementation is an OpenStreetMap client living in Infrastructure, and this interface exists
/// so the agent layer never has to know that. The contract is deliberately expressed in domain terms
/// (site code, region type, distance to fuel) rather than OSM's — swapping OSM for a commercial geo
/// provider should be a registration change, not a change to any tool or prompt.
/// </summary>
public interface IGeoContextProvider
{
    /// <summary>Full context for one site. Null when the site is unknown or the provider is unavailable.</summary>
    Task<SiteGeoSummary?> GetSiteContextAsync(string siteCode, CancellationToken cancellationToken = default);

    /// <summary>Just the classification. Null when the site is unknown or the provider is unavailable.</summary>
    Task<RegionClassification?> ClassifyRegionAsync(string siteCode, CancellationToken cancellationToken = default);
}
