using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.Drivers;

internal sealed class McpConnectionDisplayDriver : DisplayDriver<McpConnection>
{
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

        connection.DisplayText = model.DisplayText;

        return Edit(connection, context);
    }
}
