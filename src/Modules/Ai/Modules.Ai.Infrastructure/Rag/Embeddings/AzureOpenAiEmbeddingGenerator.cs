using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Logging;
using Modules.Ai.Application.Rag.Embeddings;
using OpenAI.Embeddings;
using Pgvector;

namespace Modules.Ai.Infrastructure.Rag.Embeddings;

/// <summary>
/// Embeds text via the Azure OpenAI <c>embeddings</c> deployment. Uses the
/// official <c>Azure.AI.OpenAI</c> client (already pulled in transitively by
/// the SK Azure connector) so the AI module doesn't need to reinvent retry,
/// auth, or response parsing.
///
/// Wired only when AzureOpenAi is the active provider AND an embedding
/// deployment is configured — see Modules.Ai.Infrastructure.DependencyInjection.
/// </summary>
internal sealed class AzureOpenAiEmbeddingGenerator(
    AzureOpenAIClient client,
    string deploymentName,
    int dimensions,
    ILogger<AzureOpenAiEmbeddingGenerator> logger) : IEmbeddingGenerator
{
    public int Dimensions => dimensions;
    public string ModelName => $"azure-openai/{deploymentName}";

    public async Task<Vector> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        EmbeddingClient embeddingClient = client.GetEmbeddingClient(deploymentName);
        EmbeddingGenerationOptions opts = new() { Dimensions = dimensions };

        ClientResult<OpenAIEmbedding> result = await embeddingClient
            .GenerateEmbeddingAsync(text, opts, cancellationToken);

        ReadOnlyMemory<float> floats = result.Value.ToFloats();
        return new Vector(floats);
    }

    public async Task<IReadOnlyList<Vector>> GenerateBatchAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (inputs.Count == 0)
        {
            return [];
        }

        EmbeddingClient embeddingClient = client.GetEmbeddingClient(deploymentName);
        EmbeddingGenerationOptions opts = new() { Dimensions = dimensions };

        try
        {
            ClientResult<OpenAIEmbeddingCollection> result = await embeddingClient
                .GenerateEmbeddingsAsync(inputs, opts, cancellationToken);

            return result.Value
                .Select(e => new Vector(e.ToFloats()))
                .ToList();
        }
        catch (ClientResultException ex)
        {
            logger.LogError(ex, "Azure OpenAI embeddings call failed (status {Status}); deployment={Deployment}",
                ex.Status, deploymentName);
            throw;
        }
    }
}
