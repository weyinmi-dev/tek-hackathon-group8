using Application.Abstractions.Events;

namespace Modules.Ai.Application.Abstractions;

/// <summary>
/// Enqueues an integration event into the transactional outbox. The event is added to the current
/// unit of work but NOT saved — the caller's <c>IUnitOfWork.SaveChangesAsync</c> commits it in the
/// same transaction as the aggregate that raised it, so the event and the state change are atomic
/// (the transactional-outbox pattern). The <c>OutboxProcessor</c> then publishes it asynchronously.
/// </summary>
public interface IOutboxWriter
{
    void Enqueue(IIntegrationEvent integrationEvent);
}
