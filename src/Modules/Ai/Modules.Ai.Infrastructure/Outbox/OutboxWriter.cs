using System.Text.Json;
using Application.Abstractions.Events;
using Modules.Ai.Application.Abstractions;
using Modules.Ai.Infrastructure.Database;

namespace Modules.Ai.Infrastructure.Outbox;

/// <summary>
/// Writes integration events into <c>ai.outbox_messages</c> on the shared <see cref="AiDbContext"/>,
/// so they commit in the same transaction as the aggregate (transactional outbox). The type name is
/// stored assembly-qualified because <c>OutboxProcessor</c> rehydrates it with <c>Type.GetType</c>.
/// </summary>
internal sealed class OutboxWriter(AiDbContext db) : IOutboxWriter
{
    // Default options — must match OutboxProcessor's deserializer so the event round-trips.
    private static readonly JsonSerializerOptions SerializerOptions = new();

    public void Enqueue(IIntegrationEvent integrationEvent)
    {
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = integrationEvent.GetType().AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions),
            OccurredAtUtc = DateTime.UtcNow,
        });
    }
}
