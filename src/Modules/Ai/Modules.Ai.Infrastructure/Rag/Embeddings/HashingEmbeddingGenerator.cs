using System.Text.RegularExpressions;
using Modules.Ai.Application.Rag.Embeddings;
using Pgvector;

namespace Modules.Ai.Infrastructure.Rag.Embeddings;

/// <summary>
/// Deterministic, dependency-free fallback used when Azure OpenAI is not
/// configured. Implements a hashing trick: tokenize, hash each token to a
/// dimension index, accumulate counts, then L2-normalize. Cosine similarity
/// over these vectors approximates token-overlap relevance — not as good as
/// a real embedding model, but it lets the entire RAG pipeline (chunking,
/// retrieval, prompt injection) run end-to-end in offline / Mock mode.
/// </summary>
internal sealed partial class HashingEmbeddingGenerator(int dimensions) : IEmbeddingGenerator
{
    public int Dimensions => dimensions;
    public string ModelName => $"mock-hashing/d={dimensions}";

    public Task<Vector> GenerateAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(Embed(text ?? string.Empty));

    public Task<IReadOnlyList<Vector>> GenerateBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        IReadOnlyList<Vector> result = inputs.Select(t => Embed(t ?? string.Empty)).ToList();
        return Task.FromResult(result);
    }

    private Vector Embed(string text)
    {
        float[] vector = new float[dimensions];
        if (string.IsNullOrWhiteSpace(text))
        {
            // Return a unit vector on dim 0 so we never produce a NaN cosine.
            vector[0] = 1f;
            return new Vector(vector);
        }

        foreach (Match m in TokenRegex().Matches(text.ToUpperInvariant()))
        {
            string token = m.Value;
            if (token.Length < 2)
            {
                continue;
            }

            // 64-bit FNV-1a → mix → modulo. Distinct hash from sign so positive/negative
            // contributions both occur and partial cancellation is possible (mirrors how
            // a real embedding has both axes of meaning).
            ulong h = Fnv1a(token);
            int idx = (int)(h % (ulong)dimensions);
            int sign = (int)(h >> 63) == 0 ? 1 : -1;
            vector[idx] += sign;
        }

        double norm = 0;
        for (int i = 0; i < dimensions; i++)
        {
            norm += vector[i] * vector[i];
        }
        norm = Math.Sqrt(norm);
        if (norm < 1e-9)
        {
            vector[0] = 1f;
            return new Vector(vector);
        }

        float inv = (float)(1.0 / norm);
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] *= inv;
        }
        return new Vector(vector);
    }

    private static ulong Fnv1a(string token)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong h = offset;
        for (int i = 0; i < token.Length; i++)
        {
            h ^= token[i];
            h *= prime;
        }
        return h;
    }

    [GeneratedRegex(@"[A-Z0-9][A-Z0-9\-]+")]
    private static partial Regex TokenRegex();
}
