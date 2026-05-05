using Microsoft.EntityFrameworkCore;
using Modules.Network.Application.Ingestion.Stage3_Decide;
using Modules.Network.Domain.Towers;
using Modules.Network.Infrastructure.Database;

namespace Modules.Network.Infrastructure.Ingestion;

internal sealed class TowerSnapshotProvider(NetworkDbContext db) : ITowerSnapshotProvider
{
    public async Task<IReadOnlyDictionary<string, TowerSnapshot>> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        List<Tower> towers = await db.Towers
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var snapshot = new Dictionary<string, TowerSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (Tower tower in towers)
        {
            snapshot[tower.Code] = new TowerSnapshot(
                tower.Code,
                tower.Status.ToWire(),
                tower.SignalPct,
                tower.LoadPct);
        }

        return snapshot;
    }
}
