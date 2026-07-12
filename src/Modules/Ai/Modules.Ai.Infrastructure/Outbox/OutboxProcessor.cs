using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Ai.Infrastructure.Database;

namespace Modules.Ai.Infrastructure.Outbox;

/// <summary>
/// Drains the transactional outbox: polls pending <see cref="OutboxMessage"/> rows, rehydrates
/// each integration event from its stored type + JSON, and republishes it via MediatR so the
/// existing notification handlers run — decoupled from the request that raised the event.
///
/// A failed message is left pending with its attempt count and error recorded, so it is retried
/// on the next poll rather than lost. Registered from M5; the table stays empty until the async
/// document pipeline (M9) starts writing, so until then this simply idles.
/// </summary>
internal sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;
    private static readonly JsonSerializerOptions SerializerOptions = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox drain failed; will retry next poll.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task DrainBatchAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        AiDbContext db = scope.ServiceProvider.GetRequiredService<AiDbContext>();
        IPublisher publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        List<OutboxMessage> batch = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAtUtc == null)
            .OrderBy(m => m.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (batch.Count == 0)
        {
            return;
        }

        foreach (OutboxMessage message in batch)
        {
            try
            {
                Type type = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"Outbox type '{message.Type}' could not be resolved.");

                if (JsonSerializer.Deserialize(message.Payload, type, SerializerOptions) is INotification notification)
                {
                    await publisher.Publish(notification, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Outbox payload for '{message.Type}' is not an INotification.");
                }

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.Error = ex.Message;
                logger.LogWarning(ex, "Outbox message {Id} ({Type}) failed (attempt {Attempts}).",
                    message.Id, message.Type, message.Attempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
