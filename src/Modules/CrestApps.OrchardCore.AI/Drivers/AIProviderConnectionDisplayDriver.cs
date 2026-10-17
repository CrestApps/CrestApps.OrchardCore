using CrestApps.Core.AI.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Drivers;

internal sealed class AIProviderConnectionDisplayDriver : DisplayDriver<AIProviderConnection>
{
    private readonly INamedSourceCatalog<AIProviderConnection> _connectionsCatalog;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionDisplayDriver"/> class.
    /// </summary>
    /// <param name="connectionsCatalog">The catalog for retrieving AI provider connections by name.</param>
    /// <param name="shellReleaseManager">The shell release manager for requesting tenant restarts.</param>
    /// <param name="stringLocalizer">The string localizer for this driver.</param>
    public AIProviderConnectionDisplayDriver(
        INamedSourceCatalog<AIProviderConnection> connectionsCatalog,
        IShellReleaseManager shellReleaseManager,
        IStringLocalizer<AIProviderConnectionDisplayDriver> stringLocalizer)
    {
        _connectionsCatalog = connectionsCatalog;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(AIProviderConnection connection, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = connection.DisplayText?.ToLowerInvariant() ?? string.Empty;
        }

        var results = new List<IDisplayResult>();

        // A connection defined in configuration is listed apart from the editable ones and cannot be selected,
        // edited or deleted.
        if (!connection.IsReadOnly)
        {
            results.Add(View("AIProviderConnection_Checkbox_SummaryAdmin", connection)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"));
            results.Add(View("AIProviderConnection_Buttons_SummaryAdmin", connection).Location("Actions:5"));
        }

        results.Add(View("AIProviderConnection_Fields_SummaryAdmin", connection)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"));
        results.Add(View("AIProviderConnection_DefaultTags_SummaryAdmin", connection)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"));
        results.Add(View("AIProviderConnection_DefaultMeta_SummaryAdmin", connection)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
            .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5"));

        return CombineAsync(results);
    }

    public override IDisplayResult Edit(AIProviderConnection connection, BuildEditorContext context)
    {
        context.AddTenantReloadWarningWrapper();

        return Initialize<AIProviderConnectionFieldsViewModel>("AIProviderConnectionFields_Edit", model =>
        {
            model.DisplayText = connection.DisplayText;
            model.Name = connection.Name;
            model.IsNew = context.IsNew;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIProviderConnection connection, UpdateEditorContext context)
    {
        var model = new AIProviderConnectionFieldsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            if (string.IsNullOrEmpty(model.Name))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is required."]);
            }
            else if (await _connectionsCatalog.FindByNameAsync(model.Name) is not null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Another connection with the same name exists."]);
            }

            connection.Name = model.Name;
        }

        if (string.IsNullOrWhiteSpace(model.DisplayText))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.DisplayText), S["The Title is required."]);
        }

        connection.DisplayText = model.DisplayText;

        return Edit(connection, context);
    }
}
