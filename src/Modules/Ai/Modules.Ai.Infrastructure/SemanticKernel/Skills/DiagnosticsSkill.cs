using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.SemanticKernel;
using Modules.Network.Api;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// SK skill (a.k.a. plugin) — surfaces network diagnostics to the LLM.
/// Each [KernelFunction] is auto-described to the model so it can decide when
/// to invoke it as part of an agentic flow. The LLM never talks to the DB
/// directly — every data path goes through INetworkApi (a same-process,
/// strongly-typed cross-module contract).
/// </summary>
public sealed class DiagnosticsSkill(INetworkApi network)
{
    [KernelFunction("get_region_metrics")]
    [Description("Get current signal strength, load and status for every tower in the named region. Use this when the user asks why a region is slow or asks for a diagnostic of an area like 'Lagos West'.")]
    public async Task<string> GetRegionMetricsAsync(
        [Description("Region name, e.g. 'Lagos West', 'Ikeja', 'Lekki'")] string region,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TowerSnapshot> towers = await network.ListByRegionAsync(region, cancellationToken);
        if (towers.Count == 0)
        {
            return $"No towers found in region '{region}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Region: {region} — {towers.Count} towers");
        foreach (TowerSnapshot t in towers.OrderBy(t => t.Code))
        {
            sb.Append("  ").Append(t.Code).Append("  ").Append(t.Name)
              .Append("  signal=").Append(t.SignalPct).Append('%')
              .Append("  load=").Append(t.LoadPct).Append('%')
              .Append("  status=").Append(t.Status);
            if (!string.IsNullOrEmpty(t.Issue))
            {
                sb.Append("  issue=\"").Append(t.Issue).Append('"');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    [KernelFunction("get_tower_metrics")]
    [Description("Get the current diagnostic snapshot for a single tower by its code, e.g. 'TWR-LEK-003'.")]
    public async Task<string> GetTowerMetricsAsync(
        [Description("The tower code")] string towerCode,
        CancellationToken cancellationToken = default)
    {
        TowerSnapshot? t = await network.GetByCodeAsync(towerCode, cancellationToken);
        if (t is null)
        {
            return $"Tower '{towerCode}' not found.";
        }
        return $"{t.Code} ({t.Name}, {t.Region}) signal={t.SignalPct}% load={t.LoadPct}% status={t.Status} issue={t.Issue ?? "none"}";
    }
}
