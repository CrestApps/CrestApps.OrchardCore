using System.Text.Json;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class McpConnectionDisplayDriver : DisplayDriver<McpConnection>
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true,
    };

    internal readonly IStringLocalizer S;

    public McpConnectionDisplayDriver(IStringLocalizer<McpConnectionDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(McpConnection connection, BuildDisplayContext context)
    {
        return CombineAsync(
            View("McpConnection_Fields_SummaryAdmin", connection).Location("Content:1"),
            View("McpConnection_Buttons_SummaryAdmin", connection).Location("Actions:5"),
            View("McpConnection_DefaultMeta_SummaryAdmin", connection).Location("Meta:5")
        );
    }

    public override IDisplayResult Edit(McpConnection connection, BuildEditorContext context)
    {
        return Initialize<McpConnectionFieldsViewModel>("McpConnectionFields_Edit", model =>
        {
            model.DisplayText = connection.DisplayText;
            model.TransportType = connection.TransportType;
            model.Location = connection.Location;

            model.TransportOptions = JsonSerializer.Serialize(connection.TransportOptions ?? [], _jsonSerializerOptions);

            model.TransportTypes =
            [
                new(S["Standard IO transport"], McpConstants.TransportTypes.StdIo),
                new(S["Server side events transport"], McpConstants.TransportTypes.Sse),
            ];

            model.Schema =
            """
            {
              "$schema": "http://json-schema.org/draft-04/schema#",
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
        var model = new McpConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The Display text is required."]);
        }

        if (string.IsNullOrEmpty(model.TransportType))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TransportType), S["The Transport Type is required."]);
        }
        else if (model.TransportType != McpConstants.TransportTypes.StdIo && model.TransportType != McpConstants.TransportTypes.Sse)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.TransportType), S["Unsupported Transport Type."]);
        }

        if (model.TransportType == McpConstants.TransportTypes.StdIo && string.IsNullOrEmpty(model.Location))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Location), S["The Location is required when using StdIo transport type."]);
        }

        if (!string.IsNullOrEmpty(model.TransportOptions))
        {
            try
            {
                connection.TransportOptions = JsonSerializer.Deserialize<Dictionary<string, string>>(model.TransportOptions);
            }
            catch
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.TransportOptions), S["Invalid transport options."]);
            }
        }
        else
        {
            connection.TransportOptions = null;
        }

        connection.DisplayText = model.DisplayText;
        connection.TransportType = model.TransportType;
        connection.Location = model.Location;

        return Edit(connection, context);
    }
}
