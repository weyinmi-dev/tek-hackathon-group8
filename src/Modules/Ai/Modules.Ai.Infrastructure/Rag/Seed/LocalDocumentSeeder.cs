using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Documents;
using Modules.Ai.Application.Rag.Ingestion;
using Modules.Ai.Domain.Documents;
using Modules.Ai.Domain;
using Modules.Ai.Domain.Knowledge;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Rag.Seed;

/// <summary>
/// Scans the local document store directory for PDFs that haven't been indexed yet.
/// Useful for "hot-loading" a knowledge base from a folder of reports/SOPs.
/// </summary>
public sealed class LocalDocumentSeeder(
    DocumentsOptions options,
    IManagedDocumentRepository documents,
    IDocumentIngestionService ingestion,
    IUnitOfWork uow,
    ILogger<LocalDocumentSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var roots = new List<string> { options.LocalRoot };
        roots.AddRange(options.AdditionalWatchFolders);

        foreach (string rawRoot in roots)
        {
            string root = Path.GetFullPath(rawRoot);
            if (!Directory.Exists(root))
            {
                logger.LogWarning("LocalDocumentSeeder: directory '{Root}' not found.", root);
                continue;
            }

            string[] files = Directory.GetFiles(root, "*.pdf");
            if (files.Length == 0)
            {
                continue;
            }

            IReadOnlyList<ManagedDocument> existing = await documents.ListAsync(ct);
            HashSet<string> existingKeys = existing.Select(d => d.StorageKey).ToHashSet();

            int added = 0;
            foreach (string path in files)
            {
                string fileName = Path.GetFileName(path);
                
                if (existingKeys.Contains(fileName))
                {
                    continue;
                }

                var info = new FileInfo(path);
                KnowledgeCategory category = ResolveCategory(fileName);
                string region = ResolveRegion(fileName);

                var doc = ManagedDocument.Create(
                    title: Path.GetFileNameWithoutExtension(fileName).Replace('_', ' '),
                    fileName: fileName,
                    contentType: "application/pdf",
                    sizeBytes: info.Length,
                    category: category,
                    region: region,
                    tags: [category.ToString().ToLowerInvariant(), region.ToLowerInvariant()],
                    source: DocumentSource.LocalUpload,
                    storageKey: fileName,
                    externalReference: null,
                    uploadedBy: "system-seeder");

                await documents.AddAsync(doc, ct);
                added++;
            }

            if (added > 0)
            {
                await uow.SaveChangesAsync(ct);
                logger.LogInformation("LocalDocumentSeeder: discovered and registered {Count} new documents in {Root}.", added, root);

                // Now trigger ingestion for any Pending documents (including the ones we just added).
                IReadOnlyList<ManagedDocument> pending = await documents.ListByStatusAsync(IndexingStatus.Pending, ct);
                foreach (ManagedDocument p in pending)
                {
                    try
                    {
                        logger.LogInformation("LocalDocumentSeeder: ingesting {FileName}...", p.FileName);
                        await ingestion.IngestAsync(p.Id, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "LocalDocumentSeeder: failed to ingest {FileName}", p.FileName);
                    }
                }
            }
        }
    }

    private static KnowledgeCategory ResolveCategory(string fileName)
    {
        string upper = fileName.ToUpperInvariant();
        if (upper.Contains("INC-") || upper.Contains("ICR-") || upper.Contains("INCIDENT")) return KnowledgeCategory.IncidentReport;
        if (upper.Contains("SOP-") || upper.Contains("RESPONSE") || upper.Contains("PROCEDURE")) return KnowledgeCategory.EngineeringSop;
        if (upper.Contains("OUTAGE") || upper.Contains("WSUM-")) return KnowledgeCategory.OutageSummary;
        if (upper.Contains("FUEL") || upper.Contains("EIR-")) return KnowledgeCategory.EnergySiteSnapshot;
        if (upper.Contains("BHR-") || upper.Contains("EAR-") || upper.Contains("ANOMAL")) return KnowledgeCategory.EnergyAnomaly;
        if (upper.Contains("TOWER-PERF") || upper.Contains("PAR-") || upper.Contains("QOS-") || upper.Contains("NCC-")) return KnowledgeCategory.TowerPerformance;
        if (upper.Contains("ALERT-") || upper.Contains("FAL-")) return KnowledgeCategory.AlertHistory;
        
        return KnowledgeCategory.NetworkDiagnostic;
    }

    private static string ResolveRegion(string fileName)
    {
        string upper = fileName.ToUpperInvariant();
        if (upper.Contains("LEK")) return "Lekki";
        if (upper.Contains("LAGW") || upper.Contains("LAGOS-WEST")) return "Lagos West";
        if (upper.Contains("IKJ") || upper.Contains("IKEJA")) return "Ikeja";
        if (upper.Contains("VI-") || upper.Contains("VICTORIA")) return "Victoria Island";
        if (upper.Contains("IKO") || upper.Contains("IKOYI")) return "Ikoyi";
        if (upper.Contains("OJO") || upper.Contains("FESTAC")) return "Festac";
        if (upper.Contains("SURULERE") || upper.Contains("SURU")) return "Surulere";
        if (upper.Contains("APP") || upper.Contains("APAPA")) return "Apapa";
        if (upper.Contains("AGE") || upper.Contains("AGEGE")) return "Agege";
        
        return "Lagos";
    }
}
