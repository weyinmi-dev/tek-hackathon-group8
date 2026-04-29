using Modules.Ai.Application.Rag;
using Modules.Ai.Application.Rag.Chunking;

namespace Modules.Ai.Infrastructure.Rag.Chunking;

/// <summary>
/// Greedy paragraph-aware splitter. Walks the source text, accumulates
/// paragraphs into a window until the configured max size, then emits a
/// chunk and starts the next window with a configurable character overlap
/// so that thoughts straddling a boundary still embed inside both chunks.
///
/// Token estimate is intentionally a heuristic (chars/4) — good enough for
/// recall-tuning. If we ever swap in a real BPE tokenizer the interface stays.
/// </summary>
internal sealed class RecursiveTextChunker(RagOptions options) : IChunker
{
    private static readonly string[] ParagraphSeparators = ["\r\n\r\n", "\n\n"];

    public IReadOnlyList<TextChunk> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (normalized.Length == 0)
        {
            return [];
        }

        int max = Math.Max(120, options.ChunkSize);
        int overlap = Math.Clamp(options.ChunkOverlap, 0, max / 2);

        // Short documents — single chunk, no work.
        if (normalized.Length <= max)
        {
            return [new TextChunk(0, normalized, EstimateTokens(normalized))];
        }

        var chunks = new List<TextChunk>();
        string[] paragraphs = normalized.Split(ParagraphSeparators, StringSplitOptions.RemoveEmptyEntries);

        var current = new System.Text.StringBuilder();
        int ordinal = 0;

        foreach (string p in paragraphs)
        {
            string paragraph = p.Trim();
            if (paragraph.Length == 0)
            {
                continue;
            }

            if (current.Length + paragraph.Length + 2 <= max)
            {
                if (current.Length > 0)
                {
                    current.Append("\n\n");
                }
                current.Append(paragraph);
                continue;
            }

            // Flush whatever we've accumulated, then either start a new chunk with this
            // paragraph or — if the paragraph itself is bigger than max — slice it.
            if (current.Length > 0)
            {
                FlushChunk(chunks, ref ordinal, current.ToString());
                current.Clear();
                if (overlap > 0 && chunks.Count > 0)
                {
                    string previous = chunks[^1].Content;
                    string tail = previous.Length > overlap ? previous[^overlap..] : previous;
                    current.Append(tail).Append("\n\n");
                }
            }

            if (paragraph.Length <= max)
            {
                current.Append(paragraph);
            }
            else
            {
                SliceLongParagraph(paragraph, max, overlap, chunks, ref ordinal);
            }
        }

        if (current.Length > 0)
        {
            FlushChunk(chunks, ref ordinal, current.ToString());
        }

        return chunks;
    }

    private static void FlushChunk(List<TextChunk> chunks, ref int ordinal, string content)
    {
        string trimmed = content.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }
        chunks.Add(new TextChunk(ordinal++, trimmed, EstimateTokens(trimmed)));
    }

    private static void SliceLongParagraph(string paragraph, int max, int overlap, List<TextChunk> chunks, ref int ordinal)
    {
        int step = max - overlap;
        if (step <= 0)
        {
            step = max;
        }

        for (int start = 0; start < paragraph.Length; start += step)
        {
            int len = Math.Min(max, paragraph.Length - start);
            FlushChunk(chunks, ref ordinal, paragraph.Substring(start, len));
            if (start + len >= paragraph.Length)
            {
                break;
            }
        }
    }

    private static int EstimateTokens(string text) => Math.Max(1, text.Length / 4);
}
