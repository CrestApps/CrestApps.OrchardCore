using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Services;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class StdioMcpConnectionDisplayDriver : DisplayDriver<McpConnection>
{
    private readonly ShellSettings _shellSettings;
    private readonly IAuthorizationService _authorizationService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StdioMcpConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StdioMcpConnectionDisplayDriver(
        ShellSettings shellSettings,
        IAuthorizationService authorizationService,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<StdioMcpConnectionDisplayDriver> stringLocalizer)
    {
        _shellSettings = shellSettings;
        _authorizationService = authorizationService;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(McpConnection connection, BuildEditorContext context)
    {
        if (connection.Source != McpConstants.TransportTypes.StdIo)
        {
            return null;
        }

        return Initialize<StdioConnectionFieldsViewModel>("StdioMcpConnectionFields_Edit", model =>
        {
            var metadata = connection.GetOrCreate<StdioMcpConnectionMetadata>();
            model.Command = metadata.Command;
            model.WorkingDirectory = metadata.WorkingDirectory;

            if (metadata.Arguments is not null && metadata.Arguments.Length > 0)
            {
                model.Arguments = JsonSerializer.Serialize(metadata.Arguments, McpJOptions.SchemaSerializerOptions);
            }
            else
            {
                model.Arguments = "[]";
            }

            if (metadata.EnvironmentVariables is not null)
            {
                model.EnvironmentVariables = JsonSerializer.Serialize(metadata.EnvironmentVariables, McpJOptions.SchemaSerializerOptions);
            }
            else
            {
                model.EnvironmentVariables = "{}";
            }

            model.ArgumentsSchema =
            """
            {
              "$schema": "https://json-schema.org/draft-04/schema#",
              "type": "array",
              "items": {
                "type": "string"
              }
            }
            """;

            model.EnvironmentVariablesSchema =
            """
            {
              "$schema": "https://json-schema.org/draft-04/schema#",
              "type": "object",
              "additionalProperties": {
                "type": "string"
              }
            }
            """;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(McpConnection connection, UpdateEditorContext context)
    {
        if (connection.Source != McpConstants.TransportTypes.StdIo)
        {
            return null;
        }

        // The controller checks this too; the editor refuses on its own so no other caller can save a command.
        if (!await McpStdioAccess.CanManageAsync(_shellSettings, _authorizationService, _httpContextAccessor.HttpContext?.User))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(StdioConnectionFieldsViewModel.Command), S["You are not allowed to manage local (stdio) connections."]);

            return Edit(connection, context);
        }

        var model = new StdioConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Command))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Command), S["Command is required."]);
        }

        string[] arguments = null;

        if (!string.IsNullOrWhiteSpace(model.Arguments))
        {
            try
            {
                arguments = JsonSerializer.Deserialize<string[]>(model.Arguments);
            }
            catch
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Arguments), S["Invalid arguments format."]);
            }
        }

        Dictionary<string, string> environmentVariables = null;

        if (!string.IsNullOrWhiteSpace(model.EnvironmentVariables))
        {
            try
            {
                environmentVariables = JsonSerializer.Deserialize<Dictionary<string, string>>(model.EnvironmentVariables);
            }
            catch
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.EnvironmentVariables), S["Invalid environment variables format."]);
            }
        }

        connection.Alter<StdioMcpConnectionMetadata>(metadata =>
        {
            metadata.Command = model.Command;
            metadata.Arguments = arguments ?? [];
            metadata.WorkingDirectory = model.WorkingDirectory;
            metadata.EnvironmentVariables = environmentVariables ?? [];
        });

        return Edit(connection, context);
    }
}
