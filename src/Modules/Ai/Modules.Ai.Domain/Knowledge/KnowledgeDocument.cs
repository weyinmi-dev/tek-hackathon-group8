using SharedKernel;

namespace Modules.Ai.Domain.Knowledge;

/// <summary>
/// A single piece of indexed telco knowledge — incident write-up, runbook,
/// post-mortem, etc. The full text is kept here for traceability; the
/// retriever returns chunk-level matches and joins back to the parent
/// document for citations.
/// </summary>
public sealed class KnowledgeDocument : Entity
{
    private KnowledgeDocument(
        Guid id,
        string sourceKey,
        KnowledgeCategory category,
        string title,
        string region,
        string body,
        string tags,
        DateTime occurredAtUtc,
        DateTime indexedAtUtc) : base(id)
    {
        SourceKey = sourceKey;
        Category = category;
        Title = title;
        Region = region;
        Body = body;
        Tags = tags;
        OccurredAtUtc = occurredAtUtc;
        IndexedAtUtc = indexedAtUtc;
    }

    private KnowledgeDocument() { }

    /// <summary>Stable identifier supplied by the source system (e.g. "INC-2841", "SOP-FIBER-CUT-V3"). Idempotency key for re-indexing.</summary>
    public string SourceKey { get; private set; } = null!;
    public KnowledgeCategory Category { get; private set; }
    public string Title { get; private set; } = null!;
    public string Region { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string Tags { get; private set; } = null!;
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime IndexedAtUtc { get; private set; }

    public static KnowledgeDocument Create(
        string sourceKey,
        KnowledgeCategory category,
        string title,
        string region,
        string body,
        string tags,
        DateTime occurredAtUtc) =>
        new(Guid.NewGuid(), sourceKey, category, title, region, body, tags, occurredAtUtc, DateTime.UtcNow);

    public void Replace(string title, string region, string body, string tags, DateTime occurredAtUtc)
    {
        Title = title;
        Region = region;
        Body = body;
        Tags = tags;
        OccurredAtUtc = occurredAtUtc;
        IndexedAtUtc = DateTime.UtcNow;
    }
}
