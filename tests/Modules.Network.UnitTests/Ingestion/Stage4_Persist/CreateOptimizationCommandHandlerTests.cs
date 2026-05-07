using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Network.Application.Ingestion.Stage4_Persist;
using Modules.Network.Domain.Optimizations;
using SharedKernel;
using Xunit;

namespace Modules.Network.UnitTests.Ingestion.Stage4_Persist;

public sealed class CreateOptimizationCommandHandlerTests
{
    [Fact]
    public async Task Handle_HappyPath_PersistsOptimizationAndReturnsId()
    {
        var repo = new InMemoryOptimizationRepo();
        CreateOptimizationCommandHandler handler = NewHandler(repo);

        Result<Guid> result = await handler.Handle(NewCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Optimization persisted = repo.Stored.Should().ContainSingle().Subject;
        persisted.Id.Should().Be(result.Value);
        persisted.TowerCode.Should().Be("LOS-T-014");
        persisted.Type.Should().Be(OptimizationType.LoadBalance);
    }

    [Fact]
    public async Task Handle_InvalidArguments_AreTranslatedToProblemResult()
    {
        // Decision engine + AI validator should already have caught these, but the handler
        // defends the invariant — a malformed action shouldn't crash the whole stage.
        var repo = new InMemoryOptimizationRepo();
        CreateOptimizationCommandHandler handler = NewHandler(repo);

        var command = NewCommand() with { TowerCode = "" };
        Result<Guid> result = await handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Network.Ingestion.InvalidOptimization");
        repo.Stored.Should().BeEmpty();
    }

    private static CreateOptimizationCommand NewCommand() => new(
        IngestionRunId: Guid.NewGuid(),
        TowerCode: "LOS-T-014",
        AnomalyFingerprint: "fp-abc",
        Type: OptimizationType.LoadBalance,
        EstimatedImpact: 0.7m,
        Rationale: "load spike sustained",
        ProposedAt: DateTimeOffset.UtcNow);

    private static CreateOptimizationCommandHandler NewHandler(IOptimizationRepository repo) =>
        new(repo, NullLogger<CreateOptimizationCommandHandler>.Instance);

    private sealed class InMemoryOptimizationRepo : IOptimizationRepository
    {
        public List<Optimization> Stored { get; } = [];

        public Task AddAsync(Optimization optimization, CancellationToken _ = default)
        {
            Stored.Add(optimization);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Optimization>> ListByRunAsync(Guid id, CancellationToken _ = default) =>
            Task.FromResult<IReadOnlyList<Optimization>>(Stored.Where(o => o.IngestionRunId == id).ToList());

        public Task<int> CountAsync(CancellationToken _ = default) => Task.FromResult(Stored.Count);
    }
}
