using SharedKernel;
namespace Application.Abstractions.Events;

/// <summary>
/// Raised (through the outbox) when a document has been stored and its <c>ManagedDocument</c> row
/// committed. It is the seam that makes ingestion asynchronous and breaks the Ai→Network runtime
/// cycle (Phase 2 D9): the Ai module reacts by running <c>DocumentIngestionWorkflow</c>, and the
/// Network module reacts — independently, deciding for itself — by checking whether the file is a
/// network log. Neither module calls the other; both subscribe to this shared event.
/// </summary>
/// <param name="Id">Event identity (idempotency / tracing).</param>
/// <param name="DocumentId">The managed document that was uploaded.</param>
/// <param name="FileName">Original file name — carries the extension the Network module gates on.</param>
/// <param name="ContentType">MIME type as stored.</param>
/// <param name="StorageKey">Key to read the file back from document storage.</param>
/// <param name="Source">The <c>DocumentSource</c> enum value as an int (the shared kernel does not reference Ai.Domain).</param>
/// <param name="SubmittedBy">The uploading actor, forwarded to any downstream pipeline.</param>
public sealed record DocumentUploaded(
    Guid Id,
    Guid DocumentId,
    string FileName,
    string ContentType,
    string StorageKey,
    int Source,
    string SubmittedBy) : IIntegrationEvent;
