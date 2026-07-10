namespace Modules.Ai.Infrastructure.Outbox;

/// <summary>
/// A transactional-outbox row. Written in the same database transaction as the state change
/// that raised the event (Phase 2 §9.4), so a crash between commit and publish cannot lose it.
/// <see cref="OutboxProcessor"/> drains unprocessed rows and republishes them via MediatR.
///
/// Lives in the <c>ai</c> schema. A repository-wide outbox is deferred until a second module
/// needs one. Nothing writes rows here yet — the write path lands with the async document
/// pipeline (Phase 3 M9); until then the table stays empty and the processor idles.
/// </summary>
internal sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>Assembly-qualified name of the integration-event type, used to rehydrate the payload.</summary>
    public string Type { get; init; } = null!;

    /// <summary>JSON body of the integration event.</summary>
    public string Payload { get; init; } = null!;

    public DateTime OccurredAtUtc { get; init; }

    /// <summary>Set once the event has been published successfully. Null means pending.</summary>
    public DateTime? ProcessedAtUtc { get; set; }

    /// <summary>Number of failed delivery attempts. Drives future poison-message handling.</summary>
    public int Attempts { get; set; }

    /// <summary>Last failure message, if any.</summary>
    public string? Error { get; set; }
}
