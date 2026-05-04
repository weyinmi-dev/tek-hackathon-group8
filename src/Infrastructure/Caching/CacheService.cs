using System.Buffers;
using System.Text.Json;
using Application.Abstractions.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace Infrastructure.Caching;

internal sealed class CacheService(IDistributedCache cache) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        byte[]? bytes = await cache.GetAsync(key, cancellationToken);
        if (bytes is null)
        {
            return default;
        }

        // Defensive: if a model shape changes (e.g. a record gets a new positional ctor
        // arg), pre-existing cache entries may no longer be deserializable into TResponse.
        // Treat any deserialization failure as a cache miss + evict the bad entry, so the
        // pipeline falls through to the live handler and overwrites the slot with a fresh
        // payload. Without this, an old blob silently 500s every request until its TTL.
        try
        {
            return Deserialize<T>(bytes);
        }
        catch (JsonException)
        {
            await cache.RemoveAsync(key, cancellationToken);
            return default;
        }
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes = Serialize(value);

        return cache.SetAsync(key, bytes, CacheOptions.Create(expiration), cancellationToken);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
        cache.RemoveAsync(key, cancellationToken);

    private static T Deserialize<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<T>(bytes)!;
    }

    private static byte[] Serialize<T>(T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, value);
        return buffer.WrittenSpan.ToArray();
    }
}
