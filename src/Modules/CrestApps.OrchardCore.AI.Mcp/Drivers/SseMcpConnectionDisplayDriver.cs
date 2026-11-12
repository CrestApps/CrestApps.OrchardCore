using CrestApps.OrchardCore.AI.Core;
using CrestApps.Core;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Core.Drivers;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class SseMcpConnectionDisplayDriver : ConnectionAuthDisplayDriverBase<McpConnection>
{
    private readonly IAIEndpointPolicy _endpointPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseMcpConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="endpointPolicy">Decides whether an endpoint may be saved.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SseMcpConnectionDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<SseMcpConnectionDisplayDriver> stringLocalizer)
        : base(dataProtectionProvider, McpConstants.DataProtectionPurpose, stringLocalizer)
    {
        _endpointPolicy = endpointPolicy;
    }

    public override IDisplayResult Edit(McpConnection connection, BuildEditorContext context)
    {
        if (connection.Source != McpConstants.TransportTypes.Sse)
        {
            return null;
        }

        return Initialize<SseConnectionFieldsViewModel>("SseMcpConnectionFields_Edit", model =>
        {
            var metadata = connection.GetOrCreate<SseMcpConnectionMetadata>();
            model.Endpoint = metadata.Endpoint?.ToString();

            PopulateAuthFields(metadata, model, McpJOptions.SchemaSerializerOptions);
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

        if (string.IsNullOrEmpty(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Endpoint field is required."]);
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["Invalid Endpoint value."]);
        }
        else if (!_endpointPolicy.IsAllowed(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["This address is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections."]);
        }

        var metadata = connection.GetOrCreate<SseMcpConnectionMetadata>();

        metadata.Endpoint = endpoint;

        ApplyAuthFields(context, model, metadata);

        connection.Put(metadata);

        return Edit(connection, context);
    }
}
