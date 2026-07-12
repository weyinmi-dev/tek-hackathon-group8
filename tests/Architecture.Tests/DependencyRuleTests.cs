using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// Executable form of the dependency rules in docs/PHASE2_AI_ARCHITECTURE_DESIGN.md §4.2. Each rule
/// there was written to kill a specific Phase 1 finding; each test here fails if that finding comes
/// back. Rules enforced only by review decay — this is the enforcement.
///
/// Mechanism: the C# compiler records an assembly reference in metadata only when a type from that
/// assembly is actually used. So <see cref="Assembly.GetReferencedAssemblies"/> reports real coupling,
/// not merely a &lt;ProjectReference&gt; someone forgot to delete. A dead project reference passes
/// (harmless); the moment code uses a forbidden type, the reference appears and the test fails.
/// </summary>
public sealed class DependencyRuleTests
{
    private static readonly Assembly AiDomain = typeof(Modules.Ai.Domain.Conversations.Conversation).Assembly;
    private static readonly Assembly AiApplication = typeof(Modules.Ai.Application.Analysis.AnomalyThresholdPolicy).Assembly;
    private static readonly Assembly AiAgents = typeof(Modules.Ai.Agents.Agents.OperationsCopilotAgentBuilder).Assembly;
    private static readonly Assembly AiInfrastructure = typeof(Modules.Ai.Infrastructure.DependencyInjection).Assembly;

    private static readonly Assembly[] AiAssemblies = [AiDomain, AiApplication, AiAgents, AiInfrastructure];

    /// <summary>Assemblies whose names mean "someone else's Application layer".</summary>
    private static readonly string[] ForeignApplicationLayers =
        ["Modules.Network.Application", "Modules.Alerts.Application", "Modules.Analytics.Application"];

    private static IReadOnlyList<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

    // ── Rule: Ai.Domain references no vector or AI package ───────────────────
    // Kills Phase 1 §4.2 #1: `using Pgvector;` inside the KnowledgeChunk entity. The domain models
    // an embedding as float[]; the storage type is Infrastructure's problem.
    [Fact]
    public void AiDomain_ReferencesNoVectorOrAiPackage()
    {
        string[] banned = ["Pgvector", "Microsoft.Agents.AI", "Microsoft.Extensions.AI",
                           "Microsoft.Extensions.AI.Abstractions", "Azure.AI.OpenAI", "Microsoft.SemanticKernel"];

        IReadOnlyList<string> references = ReferencesOf(AiDomain);

        references.Should().NotContain(banned,
            "Ai.Domain must stay free of vector and model packages — an embedding is a float[] to the domain");
    }

    // ── Rule: Ai.Agents may not reference Ai.Infrastructure, Ai.Domain, or any repository ────
    // The agent layer talks to ports, never to persistence. Otherwise the agents become a second,
    // parallel data-access path with no transaction story.
    [Fact]
    public void AiAgents_DoesNotReferenceInfrastructureOrDomain()
    {
        IReadOnlyList<string> references = ReferencesOf(AiAgents);

        references.Should().NotContain("Modules.Ai.Infrastructure",
            "the dependency points Infrastructure → Agents, never back");
        references.Should().NotContain("Modules.Ai.Domain",
            "agents work with Application-layer contracts, not entities");
    }

    [Fact]
    public void AiAgents_DependsOnNoRepository()
    {
        var offenders = AiAgents.GetTypes()
            .SelectMany(t => t.GetConstructors())
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Where(IsRepository)
            .Select(t => t.FullName ?? t.Name)
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            "agents reach data through Application ports; injecting a repository re-opens the AI → repositories path");
    }

    private static bool IsRepository(Type type) =>
        type.Name.EndsWith("Repository", StringComparison.Ordinal)
        || type.Name.StartsWith('I') && type.Name.EndsWith("Repository", StringComparison.Ordinal);

    // ── Rule: Ai.* may not reference another module's .Application ───────────
    // Kills Phase 1 §4.2 #2 (Ai.Infrastructure → Network.Application) and, with it, the runtime cycle
    // in Phase 1 §4.3: if AI cannot see another module's Application layer, it cannot send that
    // module's commands. Cross-module traffic goes through the thin .Api projects.
    [Theory]
    [InlineData("Modules.Ai.Domain")]
    [InlineData("Modules.Ai.Application")]
    [InlineData("Modules.Ai.Agents")]
    [InlineData("Modules.Ai.Infrastructure")]
    public void AiLayer_DoesNotReferenceAnotherModulesApplication(string layer)
    {
        Assembly assembly = AiAssemblies.Single(a => a.GetName().Name == layer);

        IReadOnlyList<string> references = ReferencesOf(assembly);

        references.Should().NotContain(ForeignApplicationLayers,
            $"{layer} must reach other modules through their .Api projects — a direct .Application reference " +
            "is what let AI dispatch another module's commands and close the cycle");
    }

    // ── Rule: MAF types stay inside Ai.Agents ────────────────────────────────
    // The whole point of the ICopilotAgent / INetworkBatchAnalyzer ports. If Ai.Application could see
    // Microsoft.Agents.AI, the framework would be back in the layer that every other module consumes,
    // and swapping it out would again be a cross-cutting rewrite.
    [Fact]
    public void AiApplication_DoesNotReferenceAgentFramework()
    {
        IReadOnlyList<string> references = ReferencesOf(AiApplication);

        references.Where(r => r.StartsWith("Microsoft.Agents.AI", StringComparison.Ordinal))
            .Should().BeEmpty(
                "Ai.Application defines ports (ICopilotAgent); the framework that implements them lives in Ai.Agents");
    }

    [Theory]
    [InlineData("Modules.Network.Application")]
    [InlineData("Modules.Alerts.Application")]
    [InlineData("Modules.Analytics.Application")]
    public void OtherModules_DoNotReferenceAgentFramework(string layer)
    {
        Assembly assembly = ForeignApplicationAssemblies().Single(a => a.GetName().Name == layer);

        IReadOnlyList<string> references = ReferencesOf(assembly);

        references.Where(r => r.StartsWith("Microsoft.Agents.AI", StringComparison.Ordinal))
            .Should().BeEmpty($"{layer} consumes AI through module-neutral contracts, never the framework itself");
    }

    private static Assembly[] ForeignApplicationAssemblies() =>
    [
        typeof(Modules.Network.Application.Ingestion.Stage2_Analyze.AnalyzeNetworkBatchCommand).Assembly,
        typeof(Modules.Alerts.Application.DependencyInjection).Assembly,
        typeof(Modules.Analytics.Application.DependencyInjection).Assembly,
    ];

    // ── Rule: no namespace is named after a vendor framework ─────────────────
    // Kills Phase 1 §4.2 #6: `Modules.Ai.Application.SemanticKernel`. Namespaces name capabilities
    // ("Copilot", "Knowledge"), not the library that happens to implement them this quarter — a
    // vendor-named namespace makes every swap a rename across the codebase.
    [Fact]
    public void NoNamespaceIsNamedAfterAVendorFramework()
    {
        string[] vendors = ["SemanticKernel", "AgentFramework", "Pgvector", "OpenAi", "OpenAI"];

        var offenders = AiAssemblies
            .SelectMany(a => a.GetTypes())
            .Select(t => t.Namespace)
            .Where(ns => ns is not null)
            .Distinct()
            .Where(ns => ns!.Split('.').Any(segment => vendors.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            .Select(ns => ns!)
            .ToList();

        offenders.Should().BeEmpty("namespaces are named after what the code does, not the vendor that powers it");
    }
}
