using Application.Abstractions.Messaging;

namespace Modules.Ai.Application.Documents.DownloadDocument;

/// <summary>
/// Retrieves the original file behind a managed document. Deliberately a plain <see cref="IQuery{T}"/>
/// and not an <c>ICachedQuery</c> — the response carries an open stream, which must never be cached.
/// </summary>
public sealed record DownloadDocumentQuery(Guid DocumentId) : IQuery<DocumentDownload>;

/// <summary>
/// The file, ready to be written to the response. <paramref name="Content"/> is an open stream owned
/// by the caller (the endpoint hands it to <c>Results.File</c>, which disposes it).
/// </summary>
public sealed record DocumentDownload(Stream Content, string ContentType, string FileName);
