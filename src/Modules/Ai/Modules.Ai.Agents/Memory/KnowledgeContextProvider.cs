using MediatR;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Modules.Ai.Application.Knowledge;
using SharedKernel;

namespace Modules.Ai.Agents.Memory;

/// <summary>
/// A MAF <see cref="AIContextProvider"/> that grounds an agent turn in the knowledge base: it takes
/// the user's latest message, runs a RAG search through <see cref="SearchKnowledgeQuery"/> (Phase 2
/// D8 — via ISender, never a repository), and returns the hits as extra instructions for the turn.
/// </summary>
/// <remarks>
/// Stateless like every provider — one instance serves all sessions, and the only thing that varies
/// per turn (the query) comes from the invocation context, not a field.
/// </remarks>
public sealed class KnowledgeContextProvider(ISender sender) : AIContextProvider
{
    private const int TopK = 5;

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        string? query = context.AIContext.Messages?
            .LastOrDefault(m => m.Role == ChatRole.User)?
            .Text;

        if (string.IsNullOrWhiteSpace(query))
        {
            return new AIContext();
        }

        Result<IReadOnlyList<KnowledgeHitDto>> result =
            await sender.Send(new SearchKnowledgeQuery(query, TopK), cancellationToken);

        if (result.IsFailure || result.Value.Count == 0)
        {
            return new AIContext();
        }

        string knowledge = string.Join(
            "\n\n",
            result.Value.Select(h => $"[{h.Title} · {h.Region}] {h.Content}"));

        return new AIContext
        {
            Instructions = "Relevant knowledge base excerpts (cite when used):\n\n" + knowledge,
        };
    }
}
