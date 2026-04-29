using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Documents.ReindexDocument;

public sealed record ReindexDocumentCommand(Guid DocumentId) : ICommand;
