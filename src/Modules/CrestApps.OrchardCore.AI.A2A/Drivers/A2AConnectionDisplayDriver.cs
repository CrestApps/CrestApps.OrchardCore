using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.OrchardCore.AI.A2A.ViewModels;
using CrestApps.OrchardCore.AI.Core.Drivers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.A2A.Drivers;

internal sealed class A2AConnectionDisplayDriver : ConnectionAuthDisplayDriverBase<A2AConnection>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="A2AConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public A2AConnectionDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<A2AConnectionDisplayDriver> stringLocalizer)
        : base(dataProtectionProvider, A2AConstants.DataProtectionPurpose, stringLocalizer)
    {
    }

    public override Task<IDisplayResult> DisplayAsync(A2AConnection connection, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = connection.DisplayText?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("A2AConnection_Checkbox_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("A2AConnection_Fields_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("A2AConnection_Buttons_SummaryAdmin", connection).Location("Actions:5"),
            View("A2AConnection_DefaultTags_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Endpoint:5"),
            View("A2AConnection_DefaultMeta_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
        );
    }

    public override IDisplayResult Edit(A2AConnection connection, BuildEditorContext context)
    {
        return Initialize<A2AConnectionFieldsViewModel>("A2AConnectionFields_Edit", model =>
        {
            model.DisplayText = connection.DisplayText;
            model.Endpoint = connection.Endpoint;

            PopulateAuthFields(connection.GetOrCreate<A2AConnectionMetadata>(), model, JSOptions.Indented);
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(A2AConnection connection, UpdateEditorContext context)
    {
        var model = new A2AConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The Title is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Endpoint))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["The Endpoint is required."]);
        }
        else if (!Uri.TryCreate(model.Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Endpoint), S["The Endpoint must be a valid HTTP or HTTPS URL."]);
        }

        connection.DisplayText = model.DisplayText;
        connection.Endpoint = model.Endpoint;

        var metadata = connection.GetOrCreate<A2AConnectionMetadata>();

        ApplyAuthFields(context, model, metadata);

        connection.Put(metadata);

        return Edit(connection, context);
    }
}
