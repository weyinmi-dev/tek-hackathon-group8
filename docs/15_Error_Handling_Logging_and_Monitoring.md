# Error Handling, Logging, and Monitoring

This document describes TelcoPilot's complete error handling strategy, from the innermost domain layer through the API surface; its structured logging implementation with Serilog; OpenTelemetry distributed tracing via .NET Aspire ServiceDefaults; frontend error handling; and the production monitoring pathway.

---

## Design Philosophy

TelcoPilot treats error handling as a first-class architectural concern, not an afterthought. The design principle is: **every error must be representable, loggable, and recoverable without throwing an uncaught exception into the HTTP response pipeline**.

This is achieved through a layered approach:
1. **Result<T> monad** — domain and application layers never throw; they return typed error values
2. **Pipeline behaviors** — cross-cutting concerns (logging, exception catching) applied uniformly to every handler
3. **GlobalExceptionHandler** — last-resort catch for anything that escapes the pipeline
4. **Frontend structured errors** — the UI never shows a raw stack trace or generic "something went wrong"

---

## Pipeline Behavior Chain

Every command and query in TelcoPilot flows through a four-stage MediatR pipeline. The behaviors are registered in a specific order that is load-bearing — changing the order changes the semantics.

```
Request
  └── ExceptionHandlingPipelineBehavior  (outermost — catches everything)
        └── RequestLoggingPipelineBehavior  (logs entry + exit of every operation)
              └── ValidationPipelineBehavior  (rejects invalid input before handler)
                    └── QueryCachingPipelineBehavior  (cache-first for ICachedQuery)
                          └── Handler  (the actual business logic)
```

### ExceptionHandlingPipelineBehavior

```csharp
public async Task<TResponse> Handle(
    TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
{
    try
    {
        return await next();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Unhandled exception in handler {Handler} for request {@Request}",
            typeof(TRequest).Name, request);
        throw; // rethrow so GlobalExceptionHandler can format the HTTP response
    }
}
```

**Why outermost?** Because it must wrap all other behaviors. If ValidationPipelineBehavior throws (it should not, but defensive programming applies), ExceptionHandlingPipelineBehavior catches it. If the handler throws a database exception, ExceptionHandlingPipelineBehavior catches and logs it with the full request context before rethrowing to the GlobalExceptionHandler.

**Why rethrow rather than return an error result?** Because uncaught exceptions at this layer represent bugs — conditions that the Result<T> pattern was supposed to prevent but did not. Logging and rethrowing preserves the full stack trace for diagnostics while allowing the HTTP layer to format a clean error response.

### RequestLoggingPipelineBehavior

```csharp
// On entry
_logger.LogInformation("Handling {RequestName} {@Request}", 
    typeof(TRequest).Name, request);

// On exit (success)
_logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms",
    typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);

// On exit (failure Result<T>)
_logger.LogWarning("Handler {RequestName} returned failure: {Error}",
    typeof(TRequest).Name, result.Error);
```

This behavior provides a complete entry-exit trace for every operation without any handler-level instrumentation. A senior engineer investigating a production issue can reconstruct the exact sequence of operations by searching logs by correlation ID.

### ValidationPipelineBehavior

FluentValidation validators are discovered automatically via assembly scanning and registered with the DI container. The ValidationPipelineBehavior runs all registered validators for the request type before passing to the handler.

```csharp
var failures = await validator.ValidateAsync(request, ct);
if (!failures.IsValid)
{
    return Result.ValidationFailure(
        failures.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage)));
}
```

Validation failures are returned as typed `Result` values, not exceptions. The API layer maps `Result.ValidationFailure` to HTTP 400 ProblemDetails. No handler ever receives an invalid request.

### QueryCachingPipelineBehavior

Only applies to requests that implement `ICachedQuery<TResponse>`. It checks Redis first; on a miss it calls the handler and populates the cache.

```csharp
var cached = await _cache.GetAsync<TResponse>(request.CacheKey, ct);
if (cached is not null) return Result.Success(cached);

var result = await next();
if (result.IsSuccess)
    await _cache.SetAsync(request.CacheKey, result.Value, request.Expiration, ct);

return result;
```

Failures are never cached. If a handler returns an error result, the cache is not populated and the next request will attempt the handler again.

---

## Result<T> Pattern

TelcoPilot's SharedKernel defines `Result` and `Result<T>` as the universal return type for all handlers. No handler returns a raw domain object or throws a domain exception.

```csharp
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }        // only valid when IsSuccess
    public Error Error { get; }    // only valid when IsFailure

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
}

public sealed record Error(ErrorType Type, string Code, string Description);

public enum ErrorType
{
    NotFound,
    Validation,
    Unauthorized,
    Conflict,
    Failure  // catch-all for unexpected errors
}
```

**Why this matters for reliability**: In a traditional architecture, a handler that cannot find a record throws `NotFoundException`. This exception propagates up the stack, is caught by a middleware, and converted to a 404. Every layer between the handler and the middleware is a potential site for the exception to be swallowed, logged incorrectly, or converted to a 500.

With `Result<T>`, the handler returns `Result.Failure(Error.NotFound("Tower", id))`. The API layer reads the `Error.Type` and maps it to the appropriate HTTP status code. No exception is thrown. No exception can be accidentally swallowed. The error carries a structured `Code` and `Description` that can be serialised directly into a ProblemDetails response.

---

## Serilog Structured Logging

TelcoPilot uses Serilog for all application logging. Structured logging means log entries are not plain strings — they carry named properties that can be queried and aggregated.

### Configuration

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(new CompactJsonFormatter())
    .WriteTo.File("logs/telcopilot-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();
```

### Context Enrichment

`LogContext.PushProperty` is used in the request pipeline to attach the correlation ID, user handle, and role to every log entry within the scope of a request. This means every log line generated during processing of a Copilot query carries the queryId, userHandle, and role — without each log call having to specify them.

```csharp
using (LogContext.PushProperty("UserId", userId))
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    // all logs within this scope carry UserId and CorrelationId
    await next(context);
}
```

### Production Sinks

| Sink | Use Case |
|---|---|
| Console (JSON) | Docker log aggregation — Docker captures stdout; Azure Monitor can ingest Docker logs |
| File (rolling daily) | Local debugging and short-term retention |
| **Seq** (recommended) | Structured log search and alerting for production NOC |
| **Azure Application Insights** (recommended) | Azure-native log analytics, trace correlation, alert rules |

---

## OpenTelemetry and Aspire ServiceDefaults

TelcoPilot's .NET Aspire `ServiceDefaults` project (referenced by both the AppHost and the WebApi) registers OpenTelemetry instrumentation for:

- **Traces**: HTTP requests, EF Core queries, HttpClient calls, Redis operations
- **Metrics**: Request counts, latency histograms, error rates
- **Logs**: Correlation of Serilog log entries with OTel trace context

In the Aspire local development environment, all telemetry is collected by the Aspire dashboard's built-in OTLP collector and displayed at `https://localhost:17017`.

The Aspire dashboard provides:
- **Distributed traces**: Full request trace from NGINX → Backend → DB → Redis with timing for each span
- **Resource logs**: Interleaved log view across all services
- **Metrics graphs**: Request rate, error rate, EF Core query duration

### Production OTel Export

In production, the OTLP exporter is configured to send to Azure Application Insights or a self-hosted Jaeger/Grafana Tempo instance:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddRedisInstrumentation()
        .AddOtlpExporter(options =>
            options.Endpoint = new Uri(builder.Configuration["OTLP_ENDPOINT"])));
```

---

## Frontend Error Handling

The Next.js frontend implements graceful degradation at every API boundary.

### Copilot Error State

When a Copilot API call fails (network error, 500, 403), the UI renders a structured error message rather than a spinner that never resolves or a raw error object:

```
Unable to reach the Copilot service.
Error: [descriptive message]
Please check your connection or contact your NOC administrator.
```

The error message is formatted with the same visual structure as a successful answer — same card, same spacing, with a warning icon and error-coloured border. This ensures the UI remains legible under failure conditions.

### Dashboard Polling Resilience

The 30-second Dashboard polling loop catches fetch errors silently and retains the previously loaded data. A failed poll does not clear the KPI strip or display an error banner — the stale data remains with a subtle timestamp indicator showing when data was last successfully refreshed. This prevents the dashboard from becoming useless during a brief API hiccup.

### Authentication Error Handling

A 401 response from any API endpoint triggers immediate logout and redirect to the login page. A 403 response renders an "Access Denied" in-page component rather than a redirect — preserving the user's navigation context while clearly communicating the permission boundary.

---

## Health Check Endpoint

TelcoPilot exposes a `/health` endpoint via ASP.NET Core's built-in health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgsql(connectionString, name: "postgres")
    .AddRedis(redisConnectionString, name: "redis");

app.MapHealthChecks("/health");
```

The endpoint returns:
- **200 Healthy** — both PostgreSQL and Redis are reachable
- **503 Degraded / Unhealthy** — one or both dependencies are unreachable

In the Docker Compose deployment, this endpoint is used by load balancers and monitoring tools. In the Docker healthcheck context, `pg_isready` covers the database tier; the `/health` endpoint covers the full application tier.

---

## Audit Log as Operational Record

The audit trail (discussed in detail in [11_Executive_Dashboard_and_Analytics.md](11_Executive_Dashboard_and_Analytics.md)) is itself a monitoring instrument. Abnormal patterns in the audit log — a surge in Copilot queries for a specific region, repeated failed alert acknowledgments, bulk user role changes — are early-warning signals.

In production, audit log entries should be shipped to a SIEM (Security Information and Event Management) system for anomaly detection and compliance archival.

---

## Production Monitoring Pathway

| Tool | Purpose | When to Add |
|---|---|---|
| **Seq** | Structured log search and alerting | Immediately in production |
| **Azure Application Insights** | End-to-end tracing, exception tracking, availability tests | Azure deployment |
| **Azure Monitor** | Infrastructure metrics, alert rules, cost monitoring | Azure deployment |
| **Grafana + Prometheus** | Custom dashboards, SLA burn rate tracking | If self-hosted |
| **PagerDuty / OpsGenie** | On-call alerting from Azure Monitor or Seq alert rules | Production NOC |

---

## Cross-References

- Backend pipeline behavior implementation: [04_Backend_Architecture.md](04_Backend_Architecture.md)
- Aspire AppHost and local dev setup: [20_Setup_and_Local_Development.md](20_Setup_and_Local_Development.md)
- Infrastructure and health checks: [13_Infrastructure_and_Deployment.md](13_Infrastructure_and_Deployment.md)
