using SharedKernel;

namespace Modules.Network.Domain.Maintenance;

/// <summary>
/// A field engineer, as reported by the provider's maintenance feed and identified by their
/// <see cref="EngineerId"/>.
///
/// Engineers are only created when the feed gives an actual id. Completed-work history names an
/// engineer in free text with no id attached ("Grace Okafor"), and inventing an id for that name
/// would fabricate identity — two different Graces would collapse into one person, and a later
/// feed carrying the real id would create a duplicate. Those names are kept as text on the ticket
/// that recorded them instead.
/// </summary>
public sealed class Engineer : Entity
{
    private Engineer(Guid id, string engineerId, string name, DateTime firstSeenAtUtc) : base(id)
    {
        EngineerId = engineerId;
        Name = name;
        FirstSeenAtUtc = firstSeenAtUtc;
        LastSeenAtUtc = firstSeenAtUtc;
        IsActive = true;
    }

    private Engineer() { }

    public string EngineerId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTime FirstSeenAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }

    public static Engineer Register(string engineerId, string name, DateTime seenAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Engineer(Guid.NewGuid(), engineerId.Trim(), name.Trim(), seenAtUtc);
    }

    /// <summary>Re-reports the engineer from a fresh snapshot. True when the record actually changed.</summary>
    public bool Observe(string name, DateTime seenAtUtc)
    {
        bool changed = !string.Equals(Name, name, StringComparison.Ordinal) || !IsActive;

        Name = name;
        LastSeenAtUtc = seenAtUtc;
        IsActive = true;

        return changed;
    }
}

public interface IEngineerRepository
{
    Task<Engineer?> GetByEngineerIdAsync(string engineerId, CancellationToken ct = default);
    Task<IReadOnlyList<Engineer>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Engineer engineer, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
