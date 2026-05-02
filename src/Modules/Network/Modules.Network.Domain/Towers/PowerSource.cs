namespace Modules.Network.Domain.Towers;

/// <summary>
/// Represents the active power source currently driving the telecom tower.
/// </summary>
public enum PowerSource
{
    /// <summary>Commercial electrical grid.</summary>
    Grid = 0,
    /// <summary>On-site diesel generator.</summary>
    Generator = 1,
    /// <summary>Solar panel array.</summary>
    Solar = 2,
    /// <summary>Backup battery / UPS.</summary>
    Battery = 3
}
