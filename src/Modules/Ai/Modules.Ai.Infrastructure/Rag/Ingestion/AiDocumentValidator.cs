using Microsoft.SemanticKernel;
using Modules.Ai.Application.Rag.Ingestion;

namespace Modules.Ai.Infrastructure.Rag.Ingestion;

public sealed class AiDocumentValidator(Kernel kernel) : IDocumentValidator
{
    private const string SystemPrompt = 
        """
        You are the Quality Control gatekeeper for TelcoPilot, an AI system for Telecommunications Network Operations Centers (NOC).
        Your job is to decide if a document is RELEVANT or IRRELEVANT to the project.

        RELEVANT topics:
        - Network maintenance, outages, and incident reports.
        - Energy consumption at tower sites (diesel, battery, solar).
        - Engineering SOPs (Standard Operating Procedures) for telco equipment.
        - Network performance metrics (latency, jitter, subscriber counts).
        - Telco-specific regulatory or environmental reports.

        IRRELEVANT topics:
        - Recipes, personal letters, generic entertainment, sports.
        - Random marketing materials not related to infrastructure.
        - Anything not related to running a telco network.

        RESPONSE FORMAT:
        Return a JSON-like string (but just text) in two lines:
        Line 1: RELEVANT or IRRELEVANT
        Line 2: A short explanation (1 sentence) why.
        """;

    public async Task<(bool IsValid, string Reason)> ValidateAsync(string fileName, string textPreview, CancellationToken ct = default)
    {
        string prompt = 
            $"""
            Document FileName: {fileName}
            Preview Content:
            ---
            {textPreview}
            ---
            """;

        var result = await kernel.InvokePromptAsync(
            $"{SystemPrompt}\n\nUser Request: Analyze this document:\n{prompt}",
            new KernelArguments(),
            cancellationToken: ct);

        string response = result.ToString().Trim();
        string[] lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length > 0 && lines[0].Contains("RELEVANT", StringComparison.OrdinalIgnoreCase))
        {
            return (true, lines.Length > 1 ? lines[1] : "Document is relevant.");
        }

        return (false, lines.Length > 1 ? lines[1] : "Document does not appear relevant to Telco Operations.");
    }
}
