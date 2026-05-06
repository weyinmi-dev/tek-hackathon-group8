using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

/// <summary>
/// Shared "ask the model for JSON, deserialize to T" helper used by the three Stage-2
/// skills. Centralises:
/// <list type="bullet">
///   <item>OpenAI JSON-mode execution settings (low temperature, json_object response format)</item>
///   <item>JSON-deserialization with the shared <see cref="AiPipelineJson"/> options</item>
///   <item>Translation of any failure to a typed <see cref="Result{T}"/> with a stable error code,
///   so the analyzer wrapper can decide whether to retry uniformly.</item>
/// </list>
/// </summary>
internal static class KernelJsonInvoker
{
    /// <summary>
    /// OpenAI JSON-mode requires the response to be a single top-level object — arrays
    /// are not allowed at the root. All Stage-2 prompts therefore ask the model to wrap
    /// the payload in <c>{"items": [...]}</c>; the helper unwraps before deserializing.
    /// </summary>
    public const string ItemsEnvelopeProperty = "items";

    public static async Task<Result<T>> InvokeAsync<T>(
        Kernel kernel,
        string promptTemplate,
        KernelArguments arguments,
        CancellationToken cancellationToken)
    {
        PromptExecutionSettings settings = kernel.Services.GetRequiredService<PromptExecutionSettings>();
        KernelFunction fn = kernel.CreateFunctionFromPrompt(promptTemplate, executionSettings: settings);

        string raw;
        try
        {
            FunctionResult result = await kernel
                .InvokeAsync(fn, arguments, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            raw = result.GetValue<string>() ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure<T>(Error.Failure(
                "Network.Ingestion.AiInvocationFailed",
                $"AI invocation failed: {ex.Message}"));
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result.Failure<T>(Error.Failure(
                "Network.Ingestion.AiEmptyResponse",
                "AI returned an empty response."));
        }

        return Deserialize<T>(raw);
    }

    public static Result<T> Deserialize<T>(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            JsonElement payload = doc.RootElement;

            // Unwrap {"items": …} when the inner type is a collection or nullable record.
            if (payload.ValueKind == JsonValueKind.Object &&
                payload.TryGetProperty(ItemsEnvelopeProperty, out JsonElement items))
            {
                payload = items;
            }

            T? value = payload.Deserialize<T>(AiPipelineJson.Options);
            if (value is null)
            {
                return Result.Failure<T>(Error.Failure(
                    "Network.Ingestion.AiNullPayload",
                    "AI response deserialized to null."));
            }

            return Result.Success(value);
        }
        catch (JsonException ex)
        {
            return Result.Failure<T>(Error.Failure(
                "Network.Ingestion.AiMalformedJson",
                $"AI response was not valid JSON: {ex.Message}"));
        }
    }
}
