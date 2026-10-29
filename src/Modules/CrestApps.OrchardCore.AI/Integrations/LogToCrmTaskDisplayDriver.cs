using CrestApps.OrchardCore.AI.Core.Integrations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Liquid;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.AI.Integrations;

/// <summary>
/// The editor of the Log to CRM task.
/// </summary>
public class LogToCrmTaskViewModel
{
    /// <summary>Gets or sets the CRM connection.</summary>
    public string ConnectionId { get; set; }

    /// <summary>Gets or sets the phone number expression.</summary>
    public string Phone { get; set; }

    /// <summary>Gets or sets the field lines.</summary>
    public string Fields { get; set; }

    /// <summary>Gets or sets the title expression.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets the notes expression.</summary>
    public string Notes { get; set; }

    /// <summary>Gets or sets a value indicating whether a missing customer is added.</summary>
    public bool CreateIfMissing { get; set; }

    /// <summary>Gets or sets the CRM connections to choose from.</summary>
    public IList<SelectListItem> Connections { get; set; } = [];
}

/// <summary>
/// Edits the Log to CRM task.
/// </summary>
public sealed class LogToCrmTaskDisplayDriver : ActivityDisplayDriver<LogToCrmTask, LogToCrmTaskViewModel>
{
    private readonly IAIIntegrationConnectionStore _store;
    private readonly ILiquidTemplateManager _liquidTemplateManager;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogToCrmTaskDisplayDriver"/> class.
    /// </summary>
    /// <param name="store">Lists the CRM connections.</param>
    /// <param name="liquidTemplateManager">Validates the templates.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LogToCrmTaskDisplayDriver(IAIIntegrationConnectionStore store, ILiquidTemplateManager liquidTemplateManager, IStringLocalizer<LogToCrmTaskDisplayDriver> stringLocalizer)
    {
        _store = store;
        _liquidTemplateManager = liquidTemplateManager;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override async ValueTask EditActivityAsync(LogToCrmTask activity, LogToCrmTaskViewModel model)
    {
        model.ConnectionId = activity.ConnectionId;
        model.Phone = activity.Phone;
        model.Fields = activity.Fields;
        model.Title = activity.Title;
        model.Notes = activity.Notes;
        model.CreateIfMissing = activity.CreateIfMissing;
        model.Connections = (await _store.ListByProvidersAsync([AIIntegrationProviders.HubSpot, AIIntegrationProviders.Salesforce]))
            .Select(connection => new SelectListItem($"{connection.Name} ({connection.Provider})", connection.ItemId, connection.ItemId == activity.ConnectionId))
            .ToList();
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(LogToCrmTask activity, UpdateEditorContext context)
    {
        var model = new LogToCrmTaskViewModel();
        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.ConnectionId))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ConnectionId), S["Choose the CRM connection."]);
        }

        if (string.IsNullOrWhiteSpace(model.Phone))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Phone), S["Enter the customer's phone number, as Liquid."]);
        }

        foreach (var (name, value) in new[] { (nameof(model.Phone), model.Phone), (nameof(model.Fields), model.Fields), (nameof(model.Title), model.Title), (nameof(model.Notes), model.Notes) })
        {
            if (!string.IsNullOrWhiteSpace(value) && !_liquidTemplateManager.Validate(value, out _))
            {
                context.Updater.ModelState.AddModelError(Prefix, name, S["{0} is not a valid Liquid template.", name]);
            }
        }

        activity.ConnectionId = model.ConnectionId;
        activity.Phone = model.Phone?.Trim();
        activity.Fields = model.Fields?.Trim();
        activity.Title = model.Title?.Trim();
        activity.Notes = model.Notes?.Trim();
        activity.CreateIfMissing = model.CreateIfMissing;

        return Edit(activity, context);
    }
}
