using Modules.Ai.Domain.Knowledge;

namespace Modules.Ai.Application.Rag.Models;

/// <summary>Plain record handed to the indexer — keeps the domain entity hidden behind a simple input shape.</summary>
public sealed record KnowledgeDocumentInput(
    string SourceKey,
    KnowledgeCategory Category,
    string Title,
    string Region,
    string Body,
    IReadOnlyList<string> Tags,
    DateTime OccurredAtUtc);
