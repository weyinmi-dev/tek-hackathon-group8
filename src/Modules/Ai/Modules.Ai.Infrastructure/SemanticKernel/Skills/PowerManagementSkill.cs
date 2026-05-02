using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Modules.Network.Domain.Towers;

namespace Modules.Ai.Infrastructure.SemanticKernel.Skills;

/// <summary>
/// Provides the AI with the ability to query real-time power and fuel data for base stations.
/// </summary>
public sealed class PowerManagementSkill(IServiceProvider serviceProvider, ILogger<PowerManagementSkill> logger)
{
    [KernelFunction("get_critical_fuel_sites")]
    [Description("Retrieves a list of network towers that are critically low on generator fuel and require immediate dispatch.")]
    public async Task<string> GetCriticalFuelSitesAsync(
        [Description("The fuel threshold in liters to consider critical (default is 200)")] double thresholdLiters = 200)
    {
        logger.LogInformation("AI querying critical fuel sites with threshold {Threshold}L", thresholdLiters);

        using var scope = serviceProvider.CreateScope();
        var towerRepo = scope.ServiceProvider.GetRequiredService<ITowerRepository>();

        var towers = await towerRepo.GetLowFuelTowersAsync(thresholdLiters);

        if (!towers.Any())
        {
            return "All sites have adequate fuel levels currently.";
        }

        var lines = towers.Select(t => 
            $"- {t.Code} ({t.Name}, {t.Region}): {t.FuelLevelLiters}L remaining. Currently on {(t.ActivePowerSource == PowerSource.Generator ? "Generator" : t.ActivePowerSource.ToString())}");
            
        return $"Found {towers.Count} sites with critically low fuel:\n" + string.Join("\n", lines);
    }
}
