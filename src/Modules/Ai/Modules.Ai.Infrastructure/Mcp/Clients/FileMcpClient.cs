using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace Modules.Ai.Infrastructure.Mcp.Clients;

/// <summary>
/// Singleton that starts the @modelcontextprotocol/server-filesystem process ONCE at host
/// startup and caches the resulting <see cref="KernelFunction"/> list. The kernel factory
/// calls <see cref="AddToKernelBuilder"/> on each request — it is synchronous and cheap
/// because the tools list is already resolved by then.
///
/// If npx or the MCP package is unavailable, initialization is silently skipped and the
/// FS plugin is simply absent from the kernel — nothing else breaks.
/// </summary>
internal sealed class FileMcpClient(ILogger<FileMcpClient> logger)
{
    private volatile IReadOnlyList<KernelFunction> _tools = [];

    internal async Task InitializeAsync(CancellationToken ct)
    {
        try
        {
            McpClient mcpClient = await McpClient.CreateAsync(new StdioClientTransport(new()
            {
                Name = "FileSystem",
                Command = "npx",
                Arguments = ["-y", "@modelcontextprotocol/server-filesystem",
                    Path.Combine("src", "Web.Api", ".telcopilot")]
            }), cancellationToken: ct);

            IList<McpClientTool> tools = await mcpClient.ListToolsAsync(cancellationToken: ct);
            _tools = tools.Select(t => t.AsKernelFunction()).ToList();

            logger.LogInformation(
                "Filesystem MCP server initialized with {ToolCount} tools.", _tools.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Filesystem MCP server failed to initialize; FS plugin will be unavailable.");
            _tools = [];
        }
    }

    /// <summary>
    /// Adds the cached filesystem tools to the builder as the "FS" plugin.
    /// No-op when initialization failed or has not yet completed.
    /// </summary>
    internal void AddToKernelBuilder(IKernelBuilder kb)
    {
        IReadOnlyList<KernelFunction> tools = _tools;
        if (tools.Count > 0)
            kb.Plugins.AddFromFunctions("FS", tools);
    }
}

/// <summary>
/// Triggers <see cref="FileMcpClient.InitializeAsync"/> at application startup so the MCP
/// subprocess is ready before the first request arrives.
/// </summary>
internal sealed class FileMcpClientInitializer(FileMcpClient client) : IHostedService
{
    public Task StartAsync(CancellationToken ct) => client.InitializeAsync(ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
