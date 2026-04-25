using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.SemanticKernel;
using Modules.Alerts.Api;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill — surfaces active incidents and outage history to the LLM.
/// </summary>
public sealed class OutageSkill(IAlertsApi alerts)
{
    [KernelFunction("get_active_outages")]
    [Description("List all currently active or under-investigation incidents. Returns code, severity, region, tower, root-cause hypothesis, subscribers affected, and confidence.")]
    public async Task<string> GetActiveOutagesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AlertSnapshot> rows = await alerts.ListActiveAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return "No active outages.";
        }

        var sb = new StringBuilder($"Active outages ({rows.Count}):\n");
        foreach (AlertSnapshot a in rows)
        {
            sb.Append("  ").Append(a.Code).Append("  sev=").Append(a.Severity)
              .Append("  region=").Append(a.Region).Append("  tower=").Append(a.TowerCode)
              .Append("  users=").Append(a.SubscribersAffected)
              .Append("  conf=").Append(a.Confidence.ToString("F2", CultureInfo.InvariantCulture))
              .Append("  cause=\"").Append(a.Cause).Append('"').AppendLine();
        }
        return sb.ToString();
    }

    [KernelFunction("get_outages_in_region")]
    [Description("List active or recent outages limited to a specific region.")]
    public async Task<string> GetOutagesInRegionAsync(
        [Description("Region name, e.g. 'Lagos West', 'Lekki'")] string region,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AlertSnapshot> all = await alerts.ListAllAsync(cancellationToken);
        var rows = all
            .Where(a => string.Equals(a.Region, region, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();

        if (rows.Count == 0)
        {
            return $"No outages found in region '{region}'.";
        }

        var sb = new StringBuilder($"Outages in {region} ({rows.Count}):\n");
        foreach (AlertSnapshot a in rows)
        {
            sb.Append("  ").Append(a.Code).Append("  sev=").Append(a.Severity)
              .Append("  status=").Append(a.Status).Append("  cause=\"").Append(a.Cause).Append('"').AppendLine();
        }
        return sb.ToString();
    }
}
