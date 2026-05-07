using Application.Abstractions.Messaging;

namespace Modules.Network.Application.Ingestion.Pipeline;

/// <summary>
/// Orchestrator entry point. The handler walks Stages 1→5 sequentially via MediatR,
/// owns all status transitions, and is the single place where the IngestionRun
/// aggregate's lifecycle is mutated. Returns an <see cref="IngestionRunSummary"/>
/// either describing the just-completed run or echoing a prior run when the file's
/// content hash matches an existing one (idempotent re-upload).
///
/// This command is intentionally <b>not</b> an <c>IIngestionPipelineRequest</c> —
/// the IngestionRunId doesn't exist until the handler creates it. The per-stage
/// commands inside DO carry the marker, so the tracing behavior fires once per stage.
/// </summary>
public sealed record ProcessNetworkLogCommand(
    string FileName,
    string ContentType,
    Stream Content,
    string SubmittedBy,
    /// <summary>
    /// Path of the staged file relative to the telcopilot root (e.g.
    /// <c>uploads/a1b2c3d4/events.csv</c>). Set by the endpoint before dispatch via
    /// <c>IFileStagingService</c> so the MCP filesystem server and the Stage-2 AI
    /// analyzer can reference the raw bytes. Null when staging was not available.
    /// </summary>
    string? McpFilePath = null) : ICommand<IngestionRunSummary>;
