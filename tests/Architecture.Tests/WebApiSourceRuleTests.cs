using FluentAssertions;
using Xunit;

namespace Architecture.Tests;

/// <summary>
/// The two Phase 2 §4.2 rules that constrain Web.Api. They are checked against Web.Api's source and
/// project file rather than its compiled assembly: referencing the host from a test project forces a
/// rebuild of it on every test run, which fails while the app is running (the live process holds a
/// lock on its bin directory). A rule nobody can run is not enforcement.
///
/// Source is a fair proxy here. To use a type you must name it — in a using directive or inline — so
/// scanning for the forbidden namespaces catches both. The csproj check closes the other door: it
/// stops someone from adding the reference "for later" and inviting the drift back in.
/// </summary>
public sealed class WebApiSourceRuleTests
{
    private static readonly string[] ForbiddenNamespaces = ["Modules.Ai.Domain", "Modules.Ai.Agents"];

    // Kills Phase 1 §4.2 #3 and #4: an endpoint reaching into Ai.Infrastructure.Mcp.Osm for geo
    // enrichment, and IManagedDocumentRepository injected straight into an endpoint. Endpoints send
    // commands; they do not touch AI entities, repositories, or agents.
    [Fact]
    public void WebApiSource_NamesNoAiDomainOrAiAgentType()
    {
        string webApi = Path.Combine(RepoRoot(), "src", "Web.Api");

        var offenders = Directory
            .EnumerateFiles(webApi, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => ForbiddenNamespaces.Any(ns => File.ReadAllText(f).Contains(ns, StringComparison.Ordinal)))
            .Select(f => Path.GetRelativePath(webApi, f))
            .ToList();

        offenders.Should().BeEmpty(
            "endpoints reach AI through Ai.Application commands and queries — never its entities, " +
            "repositories, or agent builders");
    }

    [Fact]
    public void WebApiProject_ReferencesNeitherAiDomainNorAiAgents()
    {
        string csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "Web.Api", "Web.Api.csproj"));

        foreach (string forbidden in ForbiddenNamespaces)
        {
            csproj.Should().NotContain($"{forbidden}.csproj",
                $"Web.Api must not take a project reference on {forbidden}");
        }
    }

    /// <summary>
    /// Walks up from the test binary until it finds the solution file. Keeps the tests independent of
    /// build configuration and of where the runner is invoked from.
    /// </summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TelcoPilot.slnx")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the architecture tests must be able to locate the repository root");
        return dir!.FullName;
    }
}
