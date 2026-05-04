using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Modules.Ai.Infrastructure.SemanticKernel;

namespace Modules.Ai.Infrastructure.Mcp.Osm;

/// <summary>
/// HTTP-backed OSM client. Talks to Nominatim (forward / reverse geocoding) and the
/// Overpass API (POI / infrastructure queries) — the same public endpoints the
/// jagan-shanmugam open-streetmap-mcp Python server wraps. Routing distance is
/// computed via great-circle (haversine) rather than calling OSRM, since OSRM
/// would require either another sidecar or a paid provider; haversine is enough
/// signal for the "remote vs accessible" reasoning the Copilot does.
///
/// Per OSM's usage policy this client:
///   * Sends a descriptive User-Agent (configurable per deployment)
///   * Caches results aggressively via the <see cref="CachedOsmClient"/> decorator
///   * Issues one request at a time per logical operation (no parallel POI sweeps)
/// </summary>
internal sealed class OsmClient : IOsmClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly OsmOptions _opts;
    private readonly ILogger<OsmClient> _log;

    public OsmClient(HttpClient http, IOptions<AiOptions> ai, ILogger<OsmClient> log)
    {
        _http = http;
        _opts = ai.Value.Osm;
        _log = log;
    }

    public async Task<OsmPlace?> ReverseGeocodeAsync(double lat, double lon, CancellationToken ct = default)
    {
        string url = $"{_opts.NominatimBaseUrl.TrimEnd('/')}/reverse" +
                     $"?lat={Fmt(lat)}&lon={Fmt(lon)}&format=jsonv2&addressdetails=1&zoom=18";

        try
        {
            NominatimReverseResponse? r = await _http.GetFromJsonAsync<NominatimReverseResponse>(url, JsonOpts, ct);
            if (r is null || string.IsNullOrEmpty(r.DisplayName))
            {
                return null;
            }
            return new OsmPlace(
                DisplayName: r.DisplayName,
                Country: r.Address?.Country,
                State: r.Address?.State,
                City: r.Address?.City ?? r.Address?.Town ?? r.Address?.Village,
                Suburb: r.Address?.Suburb ?? r.Address?.Neighbourhood,
                Road: r.Address?.Road,
                Coordinates: new OsmCoordinates(lat, lon));
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Nominatim reverse geocode failed for ({Lat}, {Lon})", lat, lon);
            return null;
        }
    }

    public async Task<OsmNearbyInfrastructure> GetNearbyInfrastructureAsync(
        double lat, double lon, int radiusMetres, CancellationToken ct = default)
    {
        // Overpass QL: pull amenities, shops, healthcare, fuel, and roads within radius.
        // The "out center" suffix gives a representative point even for ways/relations.
        string ql = $"""
        [out:json][timeout:25];
        (
          node(around:{radiusMetres},{Fmt(lat)},{Fmt(lon)})[amenity];
          node(around:{radiusMetres},{Fmt(lat)},{Fmt(lon)})[shop];
          node(around:{radiusMetres},{Fmt(lat)},{Fmt(lon)})[healthcare];
          way(around:{radiusMetres},{Fmt(lat)},{Fmt(lon)})[highway~"^(motorway|trunk|primary|secondary|tertiary)$"];
        );
        out center 80;
        """;

        OverpassResponse? data = await PostOverpassAsync(ql, ct);
        if (data is null)
        {
            return new OsmNearbyInfrastructure(
                new OsmCoordinates(lat, lon), radiusMetres, 0, Array.Empty<OsmInfrastructureItem>());
        }

        List<OsmInfrastructureItem> items = new();
        foreach (OverpassElement el in data.Elements)
        {
            double? itemLat = el.Lat ?? el.Center?.Lat;
            double? itemLon = el.Lon ?? el.Center?.Lon;
            if (itemLat is null || itemLon is null) continue;

            (string category, string? subtype) = ClassifyTags(el.Tags);
            if (category == "ignore") continue;

            string name = el.Tags.TryGetValue("name", out string? n) && !string.IsNullOrWhiteSpace(n)
                ? n
                : (subtype ?? category);

            double distance = HaversineMetres(lat, lon, itemLat.Value, itemLon.Value);
            items.Add(new OsmInfrastructureItem(
                Category: category,
                Name: name,
                Subtype: subtype,
                Coordinates: new OsmCoordinates(itemLat.Value, itemLon.Value),
                DistanceMetres: Math.Round(distance, 1)));
        }

        items.Sort((a, b) => a.DistanceMetres.CompareTo(b.DistanceMetres));
        return new OsmNearbyInfrastructure(
            new OsmCoordinates(lat, lon),
            radiusMetres,
            items.Count,
            items);
    }

    public async Task<OsmFuelStationDistance> GetDistanceToFuelStationAsync(
        double lat, double lon, CancellationToken ct = default)
    {
        // Search a generous 15km radius — fuel stations may be scarce around remote sites.
        const int searchRadius = 15_000;
        string ql = $"""
        [out:json][timeout:25];
        (
          node(around:{searchRadius},{Fmt(lat)},{Fmt(lon)})[amenity=fuel];
          way(around:{searchRadius},{Fmt(lat)},{Fmt(lon)})[amenity=fuel];
        );
        out center 50;
        """;

        OverpassResponse? data = await PostOverpassAsync(ql, ct);
        if (data is null || data.Elements.Count == 0)
        {
            return new OsmFuelStationDistance(
                new OsmCoordinates(lat, lon), Found: false, Name: null,
                StationCoordinates: null, StraightLineMetres: null, Brand: null);
        }

        OsmInfrastructureItem? nearest = null;
        string? brand = null;
        foreach (OverpassElement el in data.Elements)
        {
            double? itemLat = el.Lat ?? el.Center?.Lat;
            double? itemLon = el.Lon ?? el.Center?.Lon;
            if (itemLat is null || itemLon is null) continue;

            string name = el.Tags.TryGetValue("name", out string? n) ? n ?? "Fuel station" : "Fuel station";
            string? thisBrand = el.Tags.TryGetValue("brand", out string? b) ? b : null;
            double dist = HaversineMetres(lat, lon, itemLat.Value, itemLon.Value);

            if (nearest is null || dist < nearest.DistanceMetres)
            {
                nearest = new OsmInfrastructureItem("fuel", name, thisBrand,
                    new OsmCoordinates(itemLat.Value, itemLon.Value), Math.Round(dist, 1));
                brand = thisBrand;
            }
        }

        if (nearest is null)
        {
            return new OsmFuelStationDistance(
                new OsmCoordinates(lat, lon), Found: false, Name: null,
                StationCoordinates: null, StraightLineMetres: null, Brand: null);
        }

        return new OsmFuelStationDistance(
            Origin: new OsmCoordinates(lat, lon),
            Found: true,
            Name: nearest.Name,
            StationCoordinates: nearest.Coordinates,
            StraightLineMetres: nearest.DistanceMetres,
            Brand: brand);
    }

    public async Task<OsmRegionClassification> ClassifyRegionAsync(
        double lat, double lon, CancellationToken ct = default)
    {
        // 1km square footprint — gives a stable urban/rural read without over-counting.
        const int sampleRadius = 1_000;
        string ql = $"""
        [out:json][timeout:25];
        (
          way(around:{sampleRadius},{Fmt(lat)},{Fmt(lon)})[building];
          node(around:{sampleRadius},{Fmt(lat)},{Fmt(lon)})[amenity];
          way(around:{sampleRadius},{Fmt(lat)},{Fmt(lon)})[highway];
        );
        out tags 600;
        """;

        OverpassResponse? data = await PostOverpassAsync(ql, ct);
        int buildings = 0, amenities = 0, roads = 0;
        if (data is not null)
        {
            foreach (OverpassElement el in data.Elements)
            {
                if (el.Tags.ContainsKey("building")) buildings++;
                else if (el.Tags.ContainsKey("amenity")) amenities++;
                else if (el.Tags.ContainsKey("highway")) roads++;
            }
        }

        // Heuristic thresholds calibrated against Lagos-area samples — densely built blocks
        // routinely return 200+ buildings/km², semi-suburban 50-200, rural <50, remote ~0.
        (string regionType, string reasoning) = (buildings, roads) switch
        {
            ( > 200, _) => ("urban",    $"{buildings} buildings + {roads} road segments in 1km — dense urban fabric"),
            ( > 50, > 20) => ("suburban", $"{buildings} buildings + {roads} road segments in 1km — suburban"),
            ( > 5, _)  => ("rural",    $"{buildings} buildings in 1km — rural with sparse infrastructure"),
            _           => ("remote",   $"{buildings} buildings + {roads} roads in 1km — remote / off-grid corridor"),
        };

        // Composite 0-100 score: buildings dominate (0-60), roads add up to 30, amenities up to 10.
        double buildingScore = Math.Min(60, buildings / 5.0);
        double roadScore     = Math.Min(30, roads * 1.5);
        double amenityScore  = Math.Min(10, amenities / 2.0);
        double score = Math.Round(buildingScore + roadScore + amenityScore, 1);

        return new OsmRegionClassification(
            Origin: new OsmCoordinates(lat, lon),
            RegionType: regionType,
            BuildingCount: buildings,
            AmenityCount: amenities,
            RoadSegmentCount: roads,
            AccessibilityScore: score,
            Reasoning: reasoning);
    }

    public Task<OsmRouteDistance> CalculateRouteDistanceAsync(
        double originLat, double originLon,
        double destinationLat, double destinationLon,
        CancellationToken ct = default)
    {
        // Real road-network routing would require OSRM (self-hosted) or a paid provider.
        // Haversine (great-circle) is enough signal for "is this a 5-min hop or a 60-km haul"
        // — the Copilot uses it to reason about dispatch reach, not to draw turn-by-turn.
        double metres = HaversineMetres(originLat, originLon, destinationLat, destinationLon);
        var result = new OsmRouteDistance(
            Origin: new OsmCoordinates(originLat, originLon),
            Destination: new OsmCoordinates(destinationLat, destinationLon),
            StraightLineMetres: Math.Round(metres, 1),
            Method: "haversine");
        return Task.FromResult(result);
    }

    private async Task<OverpassResponse?> PostOverpassAsync(string ql, CancellationToken ct)
    {
        try
        {
            using HttpRequestMessage req = new(HttpMethod.Post, _opts.OverpassBaseUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = ql }),
            };
            using HttpResponseMessage res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("Overpass request failed: {Status}", res.StatusCode);
                return null;
            }
            return await res.Content.ReadFromJsonAsync<OverpassResponse>(JsonOpts, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Overpass query threw");
            return null;
        }
    }

    private static (string Category, string? Subtype) ClassifyTags(IReadOnlyDictionary<string, string?> tags)
    {
        if (tags.TryGetValue("amenity", out string? a) && !string.IsNullOrEmpty(a))
        {
            string cat = a switch
            {
                "fuel" => "fuel",
                "hospital" or "clinic" or "doctors" or "pharmacy" => "healthcare",
                "school" or "kindergarten" or "college" or "university" => "education",
                "police" or "fire_station" => "emergency",
                "bank" or "atm" => "finance",
                "restaurant" or "cafe" or "fast_food" or "bar" => "food",
                _ => "amenity",
            };
            return (cat, a);
        }
        if (tags.TryGetValue("shop", out string? s) && !string.IsNullOrEmpty(s))
        {
            return ("shop", s);
        }
        if (tags.TryGetValue("healthcare", out string? h) && !string.IsNullOrEmpty(h))
        {
            return ("healthcare", h);
        }
        if (tags.TryGetValue("highway", out string? hw) && !string.IsNullOrEmpty(hw))
        {
            return ("road", hw);
        }
        return ("ignore", null);
    }

    internal static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000.0; // earth radius in metres
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;

    private static string Fmt(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    // ---- raw OSM response shapes ----

    private sealed record NominatimReverseResponse(
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("address")] NominatimAddress? Address);

    private sealed record NominatimAddress(
        [property: JsonPropertyName("country")] string? Country,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("village")] string? Village,
        [property: JsonPropertyName("suburb")] string? Suburb,
        [property: JsonPropertyName("neighbourhood")] string? Neighbourhood,
        [property: JsonPropertyName("road")] string? Road);

    private sealed class OverpassResponse
    {
        [JsonPropertyName("elements")]
        public List<OverpassElement> Elements { get; set; } = new();
    }

    private sealed class OverpassElement
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("lat")]
        public double? Lat { get; set; }

        [JsonPropertyName("lon")]
        public double? Lon { get; set; }

        [JsonPropertyName("center")]
        public OverpassCenter? Center { get; set; }

        // STJ leaves missing properties at their default — initialise so the consumer can
        // call TryGetValue without a null check on every element.
        [JsonPropertyName("tags")]
        public Dictionary<string, string?> Tags { get; set; } = new();
    }

    private sealed class OverpassCenter
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lon")]
        public double Lon { get; set; }
    }
}
