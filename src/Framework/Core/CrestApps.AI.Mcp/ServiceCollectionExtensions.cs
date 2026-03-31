using CrestApps.AI.Mcp.Handlers;
using CrestApps.AI.Mcp.Models;
using CrestApps.AI.Mcp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;

namespace CrestApps.AI.Mcp;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MCP client services including transport providers, OAuth2, and the core
    /// <see cref="McpService"/> that manages connections to remote MCP servers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrestAppsMcpClient(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient();

        services.TryAddScoped<McpService>();
        services.TryAddScoped<IOAuth2TokenService, DefaultOAuth2TokenService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMcpClientTransportProvider, SseClientTransportProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IMcpClientTransportProvider, StdioClientTransportProvider>());

        services.Configure<McpClientAIOptions>(options =>
        {
            options.AddTransportType(McpConstants.TransportTypes.Sse, entry =>
            {
                entry.DisplayName = new LocalizedString("Server-Sent Events", "Server-Sent Events");
                entry.Description = new LocalizedString("Server-Sent Events Description", "Uses a remote MCP server over HTTP.");
            });
            options.AddTransportType(McpConstants.TransportTypes.StdIo, entry =>
            {
                entry.DisplayName = new LocalizedString("Standard Input/Output", "Standard Input/Output");
                entry.Description = new LocalizedString("Standard Input/Output Description", "Uses a local MCP process over standard input/output.");
            });
        });

        return services;
    }

    /// <summary>
    /// Adds MCP server services for serving prompts and resources through the
    /// Model Context Protocol. Call this when your application acts as an MCP server.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrestAppsMcpServer(this IServiceCollection services)
    {
        services.TryAddScoped<IMcpServerPromptService, DefaultMcpServerPromptService>();
        services.TryAddScoped<IMcpServerResourceService, DefaultMcpServerResourceService>();

        services.AddMcpResourceType<FtpResourceTypeHandler>(FtpResourceConstants.Type, entry =>
        {
            entry.DisplayName = new LocalizedString("FTP", "FTP/FTPS");
            entry.Description = new LocalizedString("FTP Description", "Reads content from FTP/FTPS servers.");
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = new LocalizedString("FTP Path", "The remote file path on the FTP server.") },
            ];
        });

        services.AddMcpResourceType<SftpResourceTypeHandler>(SftpResourceConstants.Type, entry =>
        {
            entry.DisplayName = new LocalizedString("SFTP", "SFTP");
            entry.Description = new LocalizedString("SFTP Description", "Reads content from SFTP servers.");
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = new LocalizedString("SFTP Path", "The remote file path on the SFTP server.") },
            ];
        });

        return services;
    }

    /// <summary>
    /// Registers an MCP resource type with its handler.
    /// </summary>
    /// <typeparam name="THandler">The type of handler for this resource type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="type">The unique type identifier for this resource type.</param>
    /// <param name="configure">Optional configuration action for the resource type entry.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMcpResourceType<THandler>(
        this IServiceCollection services,
        string type,
        Action<McpResourceTypeEntry> configure = null)
        where THandler : class, IMcpResourceTypeHandler
    {
        services.Configure<McpOptions>(options =>
        {
            options.AddResourceType(type, configure);
        });

        // Register the handler implementation
        services.AddScoped<THandler>();

        // Register by interface for enumeration
        services.AddScoped<IMcpResourceTypeHandler>(sp => sp.GetRequiredService<THandler>());

        // Register as keyed service for direct lookup by type
        services.AddKeyedScoped<IMcpResourceTypeHandler>(type, (sp, key) => sp.GetRequiredService<THandler>());

        return services;
    }
}
