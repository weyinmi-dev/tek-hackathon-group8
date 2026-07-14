using SharedKernel;

namespace Modules.Network.Domain.Assets;

/// <summary>
/// A physical unit installed at a site — baseband unit, radio unit, generator, battery bank.
/// Identified by the vendor's own <see cref="EquipmentId"/> scoped to a site, which is what makes
/// synchronisation idempotent: the same snapshot uploaded twice updates one row rather than
/// creating a second.
///
/// Equipment is never deleted. A unit that stops appearing in a site's snapshot has been
/// decommissioned or swapped out, not erased — the history of what was installed and when is the
/// point. It is soft-retired instead, and un-retires automatically if it shows up again.
/// </summary>
public sealed class SiteEquipment : Entity
{
    private SiteEquipment(
        Guid id,
        string siteCode,
        string equipmentId,
        string type,
        string? model,
        string? status,
        DateTime firstSeenAtUtc) : base(id)
    {
        SiteCode = siteCode;
        EquipmentId = equipmentId;
        Type = type;
        Model = model;
        Status = status;
        FirstSeenAtUtc = firstSeenAtUtc;
        LastSeenAtUtc = firstSeenAtUtc;
        IsActive = true;
    }

    private SiteEquipment() { }

    public string SiteCode { get; private set; } = null!;

    /// <summary>The vendor's identifier ("BBU-001"). Unique within a site, not globally.</summary>
    public string EquipmentId { get; private set; } = null!;

    public string Type { get; private set; } = null!;
    public string? Model { get; private set; }

    /// <summary>Vendor-reported condition ("Healthy", "Running", "Charging"). Free text — vendors differ.</summary>
    public string? Status { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime FirstSeenAtUtc { get; private set; }
    public DateTime LastSeenAtUtc { get; private set; }
    public DateTime? RetiredAtUtc { get; private set; }

    public static SiteEquipment Install(
        string siteCode, string equipmentId, string type, string? model, string? status, DateTime seenAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(siteCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(equipmentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(type);

        return new SiteEquipment(
            Guid.NewGuid(),
            siteCode.Trim().ToUpperInvariant(),
            equipmentId.Trim(),
            type,
            model,
            status,
            seenAtUtc);
    }

    /// <summary>
    /// Re-reports the unit from a fresh snapshot. Returns true when something actually changed, so
    /// the sync report can distinguish a real update from a no-op re-upload of the same document.
    /// </summary>
    public bool Observe(string type, string? model, string? status, DateTime seenAtUtc)
    {
        bool changed =
            !string.Equals(Type, type, StringComparison.Ordinal) ||
            !string.Equals(Model, model, StringComparison.Ordinal) ||
            !string.Equals(Status, status, StringComparison.Ordinal) ||
            !IsActive;

        Type = type;
        Model = model;
        Status = status;
        LastSeenAtUtc = seenAtUtc;

        // A unit that reappears after being retired was swapped back in or was missing from one
        // bad feed. Either way the live document wins.
        if (!IsActive)
        {
            IsActive = true;
            RetiredAtUtc = null;
        }

        return changed;
    }

    /// <summary>Soft-retire: the unit was absent from the site's latest snapshot.</summary>
    public bool Retire(DateTime retiredAtUtc)
    {
        if (!IsActive)
        {
            return false;
        }

        IsActive = false;
        RetiredAtUtc = retiredAtUtc;
        return true;
    }
}

public interface ISiteEquipmentRepository
{
    Task<IReadOnlyList<SiteEquipment>> ListForSiteAsync(string siteCode, CancellationToken ct = default);
    Task AddAsync(SiteEquipment equipment, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
