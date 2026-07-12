using global::Application.Abstractions.Events;
using global::Application.Abstractions.Storage;
using MediatR;
using Microsoft.Extensions.Logging;
using Modules.Network.Application.Ingestion.Pipeline;
using SharedKernel;

namespace Modules.Network.Application.Ingestion;

/// <summary>
/// Network's own reaction to a document upload: if the file looks like a network log, it runs the
/// analysis pipeline. Network decides this for ITSELF (Phase 2 D9) — the Ai module no longer reaches
/// into Network to dispatch it. The file content is read through the shared document-storage
/// abstraction, so Network depends on the shared storage interface, never on the Ai module.
/// </summary>
internal sealed class DocumentUploadedHandler(
    IDocumentStorageRegistry storage,
    ISender sender,
    ILogger<DocumentUploadedHandler> logger) : INotificationHandler<DocumentUploaded>
{
    private static readonly HashSet<string> NetworkLogExtensions =
        new([".csv", ".json", ".jsonl", ".xlsx", ".txt", ".log"], StringComparer.OrdinalIgnoreCase);

    public async Task Handle(DocumentUploaded notification, CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(notification.FileName);
        if (!NetworkLogExtensions.Contains(extension))
        {
            return;
        }

        logger.LogInformation(
            "Document {DocumentId} ({FileName}) looks like a network log — running the analysis pipeline.",
            notification.DocumentId, notification.FileName);

        IDocumentStorageProvider provider = storage.For((DocumentSource)notification.Source);
        await using Stream content = await provider.OpenReadAsync(notification.StorageKey, cancellationToken);

        await sender.Send(
            new ProcessNetworkLogCommand(
                FileName: notification.FileName,
                ContentType: notification.ContentType,
                Content: content,
                SubmittedBy: notification.SubmittedBy),
            cancellationToken);
    }
}
