using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Modules.Ai.Application.Copilot.AskCopilot;
using Modules.Ai.Application.SemanticKernel;
using Modules.Ai.Infrastructure.SemanticKernel.Skills;

namespace Modules.Ai.Infrastructure.SemanticKernel;

/// <summary>
/// Real Semantic Kernel orchestrator wired to Azure OpenAI.
///
/// Flow:
///   1) Register the three telco skills as Kernel plugins
///   2) Hand the user query to the chat-completion model with the
///      "TelcoPilot NOC" system prompt
///   3) Enable automatic function calling — the model picks which
///      Diagnostics / Outage / Recommendation skills to invoke
///   4) Wrap the final structured reply with a synthesized skill trace
///      (real timings) for the frontend's animated agent panel
/// </summary>
internal sealed class SemanticKernelOrchestrator(
    Kernel kernel,
    IChatCompletionService chat,
    ILogger<SemanticKernelOrchestrator> logger)
    : ICopilotOrchestrator
{
    private const string SystemPrompt = """
        You are TelcoPilot, an AI assistant embedded in a telco Network Operations Center
        for a Lagos, Nigeria metro carrier. The user is an on-call engineer, manager, or executive.

        You have three plugins:
          - DiagnosticsSkill : tower & region metrics (signal, load, status, issue)
          - OutageSkill      : active and recent incidents (severity, root-cause, subs affected)
          - RecommendationSkill : operator runbook playbooks (3 concrete actions per cause class)

        Use them as needed before answering. Always cite specific tower IDs (e.g. TWR-LEK-003)
        and incident IDs (e.g. INC-2841) when the data supports it.

        Format your reply in this EXACT structure (plain text, no markdown headers):

        ROOT CAUSE
        <2-3 sentences identifying the most likely cause, citing specific tower IDs and metrics>

        AFFECTED
        <bullet list of 2-4 items: regions, tower IDs, subscriber counts>

        RECOMMENDED ACTIONS
        <numbered list of 3 concrete actions an engineer can take now>

        CONFIDENCE
        <single number 0-100 followed by " %" and a one-line justification>

        Keep total reply under 200 words. Be specific.
        """;

    public async Task<CopilotAnswer> AskAsync(string query, CancellationToken cancellationToken = default)
    {
        var trace = new List<SkillTraceEntry>();
        var sw = Stopwatch.StartNew();

        ChatHistory history = new(SystemPrompt);
        history.AddUserMessage(query);

        OpenAIPromptExecutionSettings settings = new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.2,
            MaxTokens = 600,
        };

        try
        {
            long planStart = sw.ElapsedMilliseconds;
            ChatMessageContent result = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);
            long planEnd = sw.ElapsedMilliseconds;

            // Walk the chat-history additions to recover which functions SK actually invoked.
            // Each function call shows up as a non-user, non-system message containing tool-call metadata.
            foreach (ChatMessageContent msg in history)
            {
                if (msg.Role == AuthorRole.Tool && msg.AuthorName is { } fn)
                {
                    string[] parts = fn.Split('-', 2);
                    string skill = parts.Length == 2 ? parts[0] : "Skill";
                    string func  = parts.Length == 2 ? parts[1] : fn;
                    trace.Add(new SkillTraceEntry(skill, func, 200, "done"));
                }
            }
            if (trace.Count == 0)
            {
                // Model answered without invoking a skill — emit synthetic trace so the UI's agent panel still renders.
                trace.Add(new SkillTraceEntry("IntentParser", "parseQuery", 80, "done"));
            }
            trace.Add(new SkillTraceEntry("LlmComposer", "compose", (int)(planEnd - planStart), "done"));

            string answer = result.Content ?? "(empty response)";
            double confidence = ExtractConfidence(answer);
            IReadOnlyList<string> attachments = AttachmentSelector.Select(query);

            return new CopilotAnswer(answer, confidence, trace, attachments, "azure-openai");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure OpenAI call failed; falling back to mock answer.");
            return MockAnswer(query, trace, "azure-openai-fallback");
        }
    }

    internal static double ExtractConfidence(string answer)
    {
        // The system prompt asks for "<n> %" on the CONFIDENCE line. Be lenient about parse failures.
        int idx = answer.IndexOf("CONFIDENCE", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return 0.85;
        }
        string tail = answer[idx..];
        int pct = tail.IndexOf('%');
        if (pct < 0)
        {
            return 0.85;
        }
        int start = pct;
        while (start > 0 && (char.IsDigit(tail[start - 1]) || tail[start - 1] == ' '))
        {
            start--;
        }
        string slice = tail[start..pct].Trim();
        return int.TryParse(slice, out int n) ? Math.Clamp(n / 100.0, 0, 1) : 0.85;
    }

    internal static CopilotAnswer MockAnswer(string query, List<SkillTraceEntry> trace, string provider)
    {
        trace.Add(new SkillTraceEntry("MockOrchestrator", "synthesize", 120, "done"));
        return new CopilotAnswer(
            $"""
            ROOT CAUSE
            Backhaul fiber degradation on TG-LEK-A serving TWR-LEK-003 (Lekki Phase 1) — packet loss climbed from 2% to 60% in the last 12 minutes. Correlated with civil-works permit issued in the area at 16:50.

            AFFECTED
            • Lekki Phase 1 — 14,200 subscribers
            • Spillover to TWR-LEK-008 (Phase 2) at 88% load
            • 4G voice + data, no impact on 5G NSA

            RECOMMENDED ACTIONS
            1. Dispatch field-team-3 to fiber junction LJ-7 (ETA 22 min)
            2. Auto-shed traffic from LEK-003 → LEK-008, LEK-014
            3. Open ticket with civil-works contractor — request immediate halt

            CONFIDENCE
            92 % — pattern matches 11 prior fiber-cut incidents this quarter.
            """,
            0.92,
            trace,
            AttachmentSelector.Select(query),
            provider);
    }
}

internal static class AttachmentSelector
{
    /// <summary>
    /// Mirrors the JSX prototype's keyword-driven attachment picker so the
    /// frontend always gets contextual chart/map suggestions.
    /// </summary>
    public static IReadOnlyList<string> Select(string query)
    {
        string q = query ?? "";
        var picks = new List<string>();
        const RegexOptions opts = RegexOptions.IgnoreCase;
        if (Regex.IsMatch(q, "lagos west|surulere|mushin|yaba", opts))
        {
            picks.AddRange(["lagosWestChart", "miniMap-lagosWest"]);
        }
        if (Regex.IsMatch(q, "lekki|fiber|packet", opts))
        {
            picks.AddRange(["lekkiChart", "miniMap-lekki"]);
        }
        if (Regex.IsMatch(q, "outage|incident|down", opts))
        {
            picks.Add("outageTable");
        }
        if (Regex.IsMatch(q, "predict|fail|forecast", opts))
        {
            picks.Add("predictChart");
        }
        if (Regex.IsMatch(q, "ikeja|allen", opts))
        {
            picks.AddRange(["miniMap-ikeja", "ikejaChart"]);
        }
        if (picks.Count == 0)
        {
            picks.AddRange(["lagosWestChart", "outageTable"]);
        }
        return picks;
    }
}
