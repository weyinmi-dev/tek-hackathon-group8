using System.Security.Cryptography;
using System.Text;
using Application.Abstractions.Caching;
using Modules.Ai.Application.Rag.Embeddings;
using Pgvector;

namespace Modules.Ai.Infrastructure.Rag.Embeddings;

/// <summary>
/// Caches embeddings by content hash (Phase 3 M14). Embedding the same text twice is a wasted
/// network round-trip and a wasted token spend, and this system does it constantly: the energy
/// indexer re-runs every five minutes over sites whose text has usually not changed, and re-indexing
/// a document re-embeds every unchanged chunk.
/// </summary>
/// <remarks>
/// A decorator over the real generator, so the caller (RagIndexer, workflow embed step) is unaware.
/// The cache key includes the model and dimensions: swapping either invalidates every entry rather
/// than silently mixing vector spaces, which would corrupt retrieval.
/// </remarks>
internal sealed class CachingEmbeddingGenerator(
    IEmbeddingGenerator inner,
    ICacheService cache) : IEmbeddingGenerator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(7);

    public int Dimensions => inner.Dimensions;

    public string ModelName => inner.ModelName;

    public async Task<Vector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        string key = CacheKey(text);

        float[]? cached = await cache.GetAsync<float[]>(key, cancellationToken);
        if (IsUsable(cached))
        {
            return new Vector(cached!);
        }

        Vector embedding = await inner.GenerateAsync(text, cancellationToken);
        await cache.SetAsync(key, embedding.ToArray(), CacheTtl, cancellationToken);
        return embedding;
    }

    public async Task<IReadOnlyList<Vector>> GenerateBatchAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        var results = new Vector?[inputs.Count];
        var missIndexes = new List<int>();
        var missTexts = new List<string>();

        for (int i = 0; i < inputs.Count; i++)
        {
            float[]? cached = await cache.GetAsync<float[]>(CacheKey(inputs[i]), cancellationToken);
            if (IsUsable(cached))
            {
                results[i] = new Vector(cached!);
            }
            else
            {
                missIndexes.Add(i);
                missTexts.Add(inputs[i]);
            }
        }

        if (missTexts.Count > 0)
        {
            // One batched call for everything that missed — never per-item round-trips.
            IReadOnlyList<Vector> fresh = await inner.GenerateBatchAsync(missTexts, cancellationToken);

            for (int m = 0; m < missIndexes.Count && m < fresh.Count; m++)
            {
                int target = missIndexes[m];
                results[target] = fresh[m];
                await cache.SetAsync(CacheKey(inputs[target]), fresh[m].ToArray(), CacheTtl, cancellationToken);
            }
        }

        // Any slot still null means the generator returned fewer vectors than inputs; surface that
        // rather than silently shifting vectors onto the wrong chunks.
        var ordered = new List<Vector>(inputs.Count);
        for (int i = 0; i < results.Length; i++)
        {
            ordered.Add(results[i]
                ?? throw new InvalidOperationException(
                    $"Embedding generator returned no vector for input {i} of {inputs.Count}."));
        }

        return ordered;
    }

    private bool IsUsable(float[]? cached) => cached is not null && cached.Length == inner.Dimensions;

    private string CacheKey(string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return $"emb:{inner.ModelName}:{inner.Dimensions}:{Convert.ToHexString(hash)}";
    }
}
