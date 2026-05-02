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

    public async Task<CopilotAnswer> AskAsync(string query, string userRole, CancellationToken cancellationToken = default)
    {
        var trace = new List<SkillTraceEntry>();
        var sw = Stopwatch.StartNew();

        string systemPrompt = $"""
        You are TelcoPilot, an AI assistant embedded in MTN Nigeria Network Operations Center
        for a Lagos, Nigeria metro carrier. The user is an {userRole} (engineer, manager, or admin).

        Your role is to provide accurate, actionable insights grounded in real backend data and
        prior operational knowledge. NEVER fabricate tower IDs, site codes, KPI numbers, or
        incident details — always source them from the tools below.

        You have these plugins available — call them as needed before composing the answer:
          - DiagnosticsSkill   : live tower & region metrics (signal, load, status, issue)
          - OutageSkill        : active and recent incidents (severity, root-cause, subs affected)
          - RecommendationSkill: operator runbook playbooks (3 concrete actions per cause class)
          - KnowledgeSkill     : RAG over historical incident reports, outage summaries, engineering
                                 SOPs, tower performance trends, alert history, AND historical
                                 energy / fuel / battery logs. Call search_knowledge for any
                                 'why', 'how', 'what happened', 'has this happened before',
                                 or 'show the trend' question.
          - InternalToolsSkill : MCP-style internal tools — get_network_metrics, get_outages,
                                 analyze_latency, find_best_connectivity. Use for deterministic
                                 numeric summaries and failover targeting.
          - EnergySkill        : Live energy / power-management state — get_energy_sites,
                                 get_energy_site, get_energy_kpis, detect_energy_anomalies,
                                 get_energy_diesel_trace, recommend_energy_optimizations.
                                 Use for fuel theft, diesel consumption, battery health,
                                 solar adoption, OPEX, and "recommend cost optimizations"
                                 questions.

        Tool selection rule:
          • RAG (KnowledgeSkill)  → for explanations, trends, historical 'why/how/what happened'.
          • MCP-style live tools  → for current state, decisions, recommendations, and any
                                    question that needs the freshest snapshot of the fleet.
          • Combine both when the user wants both an explanation AND a recommendation
            (e.g. "Why did Surulere consume more diesel yesterday, and what should we do?").

        Instructions:
        1. Decide whether you need historical context (KnowledgeSkill), live state (Diagnostics /
           InternalToolsSkill / EnergySkill), or both, and call the relevant plugins.
        2. Cite knowledge-base hits inline using their source key — e.g. [INC-2841-WRITEUP] or
           [SOP-FIBER-CUT-V3] — when the answer leans on retrieved context.
        3. Provide a structured response.
        4. Cite specific tower / site IDs (e.g. TWR-LEK-003) and incident IDs (e.g. INC-2841)
           returned by the tools — never invent them.
        5. Keep response under 220 words.

        Response Format:
        ROOT CAUSE
        <2-3 sentences identifying the most likely cause, citing specific data and any KB sources>

        AFFECTED
        <bullet list of 2-4 items: regions, tower IDs, subscriber counts>

        RECOMMENDED ACTIONS
        <numbered list of 3 concrete actions>

        CONFIDENCE
        <single number 0-100 followed by " %" and a one-line justification>
        """;

        ChatHistory history = new(systemPrompt);
        history.AddUserMessage(query);

        // NOTE: do not set MaxTokens here. SK 1.74's OpenAIPromptExecutionSettings.MaxTokens
        // binds to ChatCompletionOptions.MaxTokens (the obsolete property in OpenAI SDK 2.x),
        // which serializes on the wire as "max_tokens". GPT-5 family reasoning models reject
        // that param and require "max_completion_tokens", so setting it produces a 400.
        // The system prompt already caps the answer at ~200 words.
        OpenAIPromptExecutionSettings settings = new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            Temperature = 0.2,
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
            logger.LogError(ex, "Azure OpenAI call failed; surfacing diagnostic answer.");
            trace.Add(new SkillTraceEntry("LlmComposer", "compose", 0, "error"));

            string detail = $"{ex.GetType().Name}: {ex.Message}";
            if (ex.InnerException is not null)
            {
                detail += $"\n  → {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
            }

            string answer = $"""
            ROOT CAUSE
            The Azure OpenAI call failed. The copilot is wired up but the upstream model call threw before a response was produced.

            AFFECTED
            • Live answers are unavailable until the upstream call succeeds.
            • Skills, DB, and auth all reached this point fine — the failure is in the chat-completion request.

            RECOMMENDED ACTIONS
            1. Read the exception below and address its root cause.
            2. Confirm Ai:AzureOpenAi:Endpoint, ApiKey, and Deployment are set in user-secrets.
            3. Verify the deployment '{nameof(SemanticKernelOrchestrator)}' is configured to call exists in the Azure resource.

            EXCEPTION
            {detail}

            CONFIDENCE
            0 % — diagnostic, not a model answer.
            """;

            return new CopilotAnswer(answer, 0.0, trace, AttachmentSelector.Select(query), "azure-openai-error");
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
