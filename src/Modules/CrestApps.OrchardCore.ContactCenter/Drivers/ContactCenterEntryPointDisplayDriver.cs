using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

internal sealed class ContactCenterEntryPointDisplayDriver : DisplayDriver<ContactCenterEntryPoint>
{
    private readonly ContactCenterAdminFormOptionsProvider _optionsProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterEntryPointDisplayDriver"/> class.
    /// </summary>
    /// <param name="optionsProvider">The admin form options provider.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterEntryPointDisplayDriver(
        ContactCenterAdminFormOptionsProvider optionsProvider,
        IStringLocalizer<ContactCenterEntryPointDisplayDriver> stringLocalizer)
    {
        _optionsProvider = optionsProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(ContactCenterEntryPoint entryPoint, BuildDisplayContext context)
    {
        return CombineAsync(
            View("ContactCenterEntryPoint_Fields_SummaryAdmin", entryPoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("ContactCenterEntryPoint_Buttons_SummaryAdmin", entryPoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("ContactCenterEntryPoint_DefaultMeta_SummaryAdmin", entryPoint)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> EditAsync(ContactCenterEntryPoint entryPoint, BuildEditorContext context)
    {
        var viewModel = new EntryPointViewModel
        {
            Id = entryPoint.ItemId,
            Name = entryPoint.Name,
            Description = entryPoint.Description,
            DialedNumbersText = entryPoint.DialedNumbers is { Count: > 0 }
                ? string.Join(Environment.NewLine, entryPoint.DialedNumbers)
                : null,
            TargetQueueId = entryPoint.TargetQueueId,
            Priority = entryPoint.Priority,
            BusinessHoursCalendarId = entryPoint.BusinessHoursCalendarId,
            ClosedAction = entryPoint.ClosedAction,
            OverflowQueueId = entryPoint.OverflowQueueId,
            WelcomeMessage = entryPoint.WelcomeMessage,
            ClosedMessage = entryPoint.ClosedMessage,
            Enabled = entryPoint.Enabled,
        };

        await _optionsProvider.PopulateEntryPointEditorAsync(viewModel);

        return Initialize<EntryPointViewModel>("ContactCenterEntryPointFields_Edit", model =>
        {
            model.Id = viewModel.Id;
            model.Name = viewModel.Name;
            model.Description = viewModel.Description;
            model.DialedNumbersText = viewModel.DialedNumbersText;
            model.TargetQueueId = viewModel.TargetQueueId;
            model.TargetQueueOptions = viewModel.TargetQueueOptions;
            model.Priority = viewModel.Priority;
            model.BusinessHoursCalendarId = viewModel.BusinessHoursCalendarId;
            model.BusinessHoursCalendarOptions = viewModel.BusinessHoursCalendarOptions;
            model.ClosedAction = viewModel.ClosedAction;
            model.OverflowQueueId = viewModel.OverflowQueueId;
            model.OverflowQueueOptions = viewModel.OverflowQueueOptions;
            model.WelcomeMessage = viewModel.WelcomeMessage;
            model.ClosedMessage = viewModel.ClosedMessage;
            model.Enabled = viewModel.Enabled;
        }).Location("Content:1");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(ContactCenterEntryPoint entryPoint, UpdateEditorContext context)
    {
        var model = new EntryPointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Name), S["Name is a required field."]);
        }

        entryPoint.Name = model.Name?.Trim();
        entryPoint.Description = model.Description?.Trim();
        entryPoint.DialedNumbers = ParseLines(model.DialedNumbersText);
        entryPoint.TargetQueueId = string.IsNullOrWhiteSpace(model.TargetQueueId) ? null : model.TargetQueueId.Trim();
        entryPoint.Priority = model.Priority;
        entryPoint.BusinessHoursCalendarId = string.IsNullOrWhiteSpace(model.BusinessHoursCalendarId) ? null : model.BusinessHoursCalendarId.Trim();
        entryPoint.ClosedAction = model.ClosedAction;
        entryPoint.OverflowQueueId = string.IsNullOrWhiteSpace(model.OverflowQueueId) ? null : model.OverflowQueueId.Trim();
        entryPoint.WelcomeMessage = model.WelcomeMessage?.Trim();
        entryPoint.ClosedMessage = model.ClosedMessage?.Trim();
        entryPoint.Enabled = model.Enabled;

        return await EditAsync(entryPoint, context);
    }

    private static List<string> ParseLines(string text)
    {
        var values = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
        {
            return values;
        }

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length > 0 && !values.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(trimmed);
            }
        }

        return values;
    }
}
