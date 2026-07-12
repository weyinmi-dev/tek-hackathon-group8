using System.Text.Json;
using Application.Abstractions.Messaging;
using MediatR;
using SharedKernel;

namespace Modules.Ai.Agents.Tools;

/// <summary>
/// Shared dispatch for agent tools: sends a MediatR query and returns the result serialized for
/// the model. Every tool is a one-line call to this, so tool calls ride the same application
/// pipeline (logging, validation, exceptions) as any use case (Phase 2 §6.1, D8). Failures come
/// back as a readable error string rather than throwing, so a bad tool call doesn't abort the run.
/// </summary>
internal static class ToolResult
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static async Task<string> DispatchAsync<TResult>(ISender sender, IQuery<TResult> query, CancellationToken cancellationToken)
        where TResult : notnull
    {
        Result<TResult> result = await sender.Send(query, cancellationToken);
        return result.IsSuccess
            ? JsonSerializer.Serialize(result.Value, SerializerOptions)
            : $"ERROR {result.Error.Code}: {result.Error.Description}";
    }
}
