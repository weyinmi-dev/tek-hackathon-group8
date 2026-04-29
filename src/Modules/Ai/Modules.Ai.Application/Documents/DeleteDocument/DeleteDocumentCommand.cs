using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Documents.DeleteDocument;

public sealed record DeleteDocumentCommand(Guid DocumentId) : ICommand;
