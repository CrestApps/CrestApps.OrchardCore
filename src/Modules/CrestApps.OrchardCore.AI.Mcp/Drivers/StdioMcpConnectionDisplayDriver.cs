using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class StdioMcpConnectionDisplayDriver : DisplayDriver<McpConnection>
{
    internal readonly IStringLocalizer S;

    public StdioMcpConnectionDisplayDriver(IStringLocalizer<StdioMcpConnectionDisplayDriver> stringLocalizer)
    {
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
            var metadata = connection.As<StdioMcpConnectionMetadata>();
            model.Command = metadata.Command;

            if (metadata.Arguments is not null)
            {
                model.Arguments = JsonSerializer.Serialize(metadata.Arguments, McpJOptions.SchemaSerializerOptions);
            }
            else
            {
                model.Arguments = "[]";
            }

            model.Schema =
            """
            {
              "$schema": "http://json-schema.org/draft-04/schema#",
              "type": "array",
              "items": {
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

        var model = new StdioConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        string[] arguments = null;

        if (string.IsNullOrWhiteSpace(model.Command))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Command), S["Command is required."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Arguments))
        {
            try
            {
                arguments = JsonSerializer.Deserialize<string[]>(model.Arguments);
            }
            catch
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Arguments), S["Invalid arguments headers format."]);
            }
        }

        connection.Alter<StdioMcpConnectionMetadata>(metadata =>
        {
            metadata.Command = model.Command;
            metadata.Arguments = arguments ?? [];
        });

        return Edit(connection, context);
    }
}
