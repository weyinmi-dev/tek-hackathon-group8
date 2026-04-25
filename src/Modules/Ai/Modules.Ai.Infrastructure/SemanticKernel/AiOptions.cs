namespace Modules.Ai.Infrastructure.SemanticKernel;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"AzureOpenAi" | "Mock". Defaults to Mock when AzureOpenAi creds are missing.</summary>
    public string Provider { get; init; } = "Mock";

    public AzureOpenAiOptions AzureOpenAi { get; init; } = new();
}

public sealed class AzureOpenAiOptions
{
    public string Endpoint   { get; init; } = "";
    public string ApiKey     { get; init; } = "";
    public string Deployment { get; init; } = "gpt-4o-mini";
}
