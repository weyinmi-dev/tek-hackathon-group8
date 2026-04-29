namespace Modules.Ai.Application.Rag.Chunking;

/// <summary>
/// Splits a long body of text into retrieval-sized windows. Implementations
/// trade off recall (smaller chunks) versus context (larger chunks); the
/// default recursive splitter prefers paragraph boundaries.
/// </summary>
public interface IChunker
{
    IReadOnlyList<TextChunk> Split(string text);
}

public sealed record TextChunk(int Ordinal, string Content, int TokenEstimate);
