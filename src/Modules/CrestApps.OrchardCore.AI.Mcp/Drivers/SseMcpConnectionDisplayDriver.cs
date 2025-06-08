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

internal sealed class SseMcpConnectionDisplayDriver : DisplayDriver<McpConnection>
{
    internal readonly IStringLocalizer S;

    public SseMcpConnectionDisplayDriver(IStringLocalizer<SseMcpConnectionDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(McpConnection connection, BuildEditorContext context)
    {
        if (connection.Source != McpConstants.TransportTypes.Sse)
        {
            return null;
        }

        return Initialize<SseConnectionFieldsViewModel>("SseMcpConnectionFields_Edit", model =>
        {
            var metadata = connection.As<SseMcpConnectionMetadata>();
            model.Endpoint = metadata.Endpoint?.ToString();

            if (metadata.AdditionalHeaders is not null)
            {
                model.AdditionalHeaders = JsonSerializer.Serialize(metadata.AdditionalHeaders, McpJOptions.SchemaSerializerOptions);
            }

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
        if (connection.Source != McpConstants.TransportTypes.Sse)
        {
            return null;
        }

        var model = new SseConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        Uri endpoint = null;
        Dictionary<string, string> additionalHeaders = null;

        if (string.IsNullOrEmpty(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint field is required."]);
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Invalid Endpoint value."]);
        }

        if (!string.IsNullOrWhiteSpace(model.AdditionalHeaders))
        {
            try
            {
                additionalHeaders = JsonSerializer.Deserialize<Dictionary<string, string>>(model.AdditionalHeaders);
            }
            catch
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.AdditionalHeaders), S["Invalid additional headers format."]);
            }
        }

        connection.Alter<SseMcpConnectionMetadata>(metadata =>
        {
            metadata.Endpoint = endpoint;
            metadata.AdditionalHeaders = additionalHeaders ?? [];
        });

        return Edit(connection, context);
    }
}
