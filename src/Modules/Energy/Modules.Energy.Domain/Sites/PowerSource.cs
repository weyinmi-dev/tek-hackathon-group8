namespace Modules.Energy.Domain.Sites;

/// <summary>Active power source feeding a site at a point in time.</summary>
public enum PowerSource
{
    Grid = 0,
    Generator = 1,
    Battery = 2,
    Solar = 3,
}

public static class PowerSourceExtensions
{
    public static string ToWire(this PowerSource s) => s switch
    {
        PowerSource.Grid => "grid",
        PowerSource.Generator => "generator",
        PowerSource.Battery => "battery",
        PowerSource.Solar => "solar",
        _ => "grid",
    };

    public static PowerSource FromWire(string s) => s?.ToLowerInvariant() switch
    {
        "grid" => PowerSource.Grid,
        "generator" => PowerSource.Generator,
        "battery" => PowerSource.Battery,
        "solar" => PowerSource.Solar,
        _ => PowerSource.Grid,
    };
}
