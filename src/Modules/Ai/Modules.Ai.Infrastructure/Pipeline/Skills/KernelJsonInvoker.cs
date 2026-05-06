using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using SharedKernel;

namespace Modules.Ai.Infrastructure.Pipeline.Skills;

internal static class KernelJsonInvoker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // MaxTokens is intentionally absent: SK 1.74+ maps it to the obsolete max_tokens wire
    // parameter which o-series / GPT-4.1+ models reject. Omitting it lets the model use its
    // own limit and avoids HTTP 400 unsupported_parameter errors.
    private static readonly AzureOpenAIPromptExecutionSettings PipelineSettings = new()
    {
        Temperature = 0.9,
        ResponseFormat = "json_object"
    };

    public static async Task<Result<T>> InvokeAsync<T>(
        Kernel kernel,
        string prompt,
        KernelArguments args,
        CancellationToken cancellationToken = default)
    {
        try
        {
            KernelFunction function = KernelFunctionFactory.CreateFromPrompt(prompt);
            var invokeArgs = new KernelArguments(args, new Dictionary<string, PromptExecutionSettings>
            {
                { PromptExecutionSettings.DefaultServiceId, PipelineSettings }
            });
            FunctionResult functionResult = await kernel.InvokeAsync(function, invokeArgs, cancellationToken);
            string json = functionResult.ToString().Trim();

            if (typeof(T) == typeof(string))
            {
                return Result.Success((T)(object)json);
            }

            if (IsListType(typeof(T)))
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("items", out JsonElement itemsEl))
                {
                    T? items = JsonSerializer.Deserialize<T>(itemsEl.GetRawText(), JsonOptions);
                    return Result.Success(items!);
                }
            }

            T? value = JsonSerializer.Deserialize<T>(json, JsonOptions);
            return Result.Success(value!);
        }
        catch (Exception ex)
        {
            return Result.Failure<T>(Error.Failure("KernelJsonInvoker.Failed", ex.Message));
        }
    }

    private static bool IsListType(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() is var def && (
            def == typeof(List<>) ||
            def == typeof(IReadOnlyList<>) ||
            def == typeof(IList<>) ||
            def == typeof(IEnumerable<>));
}
