namespace Modules.Network.Domain.Towers;

/// <summary>
/// Projects real coordinates onto the 0–100% canvas the frontend's stylised network map draws on.
///
/// The map is an abstract diagram of Lagos, not a slippy map, and the towers that ship with the
/// seed data carry hand-placed positions chosen for legibility — there is no projection to recover
/// from them. Sites arriving from an OSS feed still need somewhere to land, so this defines one:
/// a straight linear fit of the Lagos metro bounding box onto the canvas, inset slightly so a site
/// at the edge of the box does not render half off-screen.
///
/// It is deliberately simple. The map conveys relative position and health, not survey accuracy;
/// anything fancier (web-mercator, clustering by real distance) would be precision the canvas
/// cannot display. Coordinates outside the box clamp to the edge rather than escaping the canvas.
/// </summary>
public static class LagosMapProjection
{
    // Lagos metro bounding box, wide enough to hold every site in the seeded fleet.
    private const double WestLongitude = 3.25;
    private const double EastLongitude = 3.65;
    private const double NorthLatitude = 6.65;
    private const double SouthLatitude = 6.40;

    // Inset so an edge-of-box site still renders fully inside the canvas.
    private const double MinPercent = 5.0;
    private const double MaxPercent = 95.0;

    /// <summary>Horizontal position, 5–95%. Longitude increases west-to-east, so does x.</summary>
    public static double MapX(double longitude) =>
        Interpolate(longitude, WestLongitude, EastLongitude);

    /// <summary>
    /// Vertical position, 5–95%. Latitude increases northward but screen y increases downward,
    /// so the axis is inverted: the northern edge of the box maps to the top of the canvas.
    /// </summary>
    public static double MapY(double latitude) =>
        Interpolate(latitude, NorthLatitude, SouthLatitude);

    private static double Interpolate(double value, double atMin, double atMax)
    {
        double fraction = (value - atMin) / (atMax - atMin);
        double percent = MinPercent + (fraction * (MaxPercent - MinPercent));
        return Math.Round(Math.Clamp(percent, MinPercent, MaxPercent), 2);
    }
}
