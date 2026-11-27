using CrestApps.Core;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.DirectDeposit;
using CrestApps.OrchardCore.Banking.Disbursements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Disbursements.Drivers;

/// <summary>
/// The Bank file (ACH) card on one of the business's own bank accounts: what the business's bank needs on the files
/// sent from it. Kept in the account's properties, so it travels with the account.
/// </summary>
internal sealed class NachaOriginSettingsDisplayDriver : DisplayDriver<BankAccount>
{
    internal readonly IStringLocalizer S;

    public NachaOriginSettingsDisplayDriver(IStringLocalizer<NachaOriginSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    // Its own prefix, so its fields never meet the bank account editor's.
    protected override void BuildPrefix(BankAccount model, string htmlFieldPrefix)
    {
        Prefix = string.IsNullOrEmpty(htmlFieldPrefix) ? nameof(NachaOriginSettings) : htmlFieldPrefix + "." + nameof(NachaOriginSettings);
    }

    public override IDisplayResult Edit(BankAccount account, BuildEditorContext context)
    {
        // Only on the business's own accounts in the admin editor, never on a person's own account page.
        if (!account.Owner.IsEmpty || !string.IsNullOrEmpty(context.GroupId))
        {
            return null;
        }

        var settings = account.TryGet<NachaOriginSettings>(out var stored) ? stored : new NachaOriginSettings();

        return Initialize<NachaOriginSettingsViewModel>("NachaOriginSettings_Edit", model =>
        {
            model.Enabled = settings.Enabled;
            model.ImmediateDestination = settings.ImmediateDestination;
            model.ImmediateDestinationName = settings.ImmediateDestinationName;
            model.ImmediateOrigin = settings.ImmediateOrigin;
            model.ImmediateOriginName = settings.ImmediateOriginName;
            model.CompanyName = settings.CompanyName;
            model.CompanyIdentification = settings.CompanyIdentification;
            model.CompanyEntryDescription = settings.CompanyEntryDescription;
            model.IncludeOffset = settings.IncludeOffset;
        }).Location(l => l.Zone("Content", "20").Card(S["Bank File (ACH)"], "50"));
    }

    public override async Task<IDisplayResult> UpdateAsync(BankAccount account, UpdateEditorContext context)
    {
        if (!account.Owner.IsEmpty || !string.IsNullOrEmpty(context.GroupId))
        {
            return null;
        }

        var model = new NachaOriginSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // The card was not on the form (the account became the business's in this save): keep what is stored.
        if (!model.Shown)
        {
            return Edit(account, context);
        }

        var settings = new NachaOriginSettings
        {
            Enabled = model.Enabled,
            ImmediateDestination = Clean(model.ImmediateDestination),
            ImmediateDestinationName = Clean(model.ImmediateDestinationName),
            ImmediateOrigin = Clean(model.ImmediateOrigin),
            ImmediateOriginName = Clean(model.ImmediateOriginName),
            CompanyName = Clean(model.CompanyName),
            CompanyIdentification = Clean(model.CompanyIdentification),
            CompanyEntryDescription = Clean(model.CompanyEntryDescription)?.ToUpperInvariant(),
            IncludeOffset = model.IncludeOffset,
        };

        if (settings.Enabled)
        {
            Require(context, settings.ImmediateOrigin, nameof(model.ImmediateOrigin), 10, S["Enter the origin your bank gave you, ten characters at most."]);
            Require(context, settings.CompanyName, nameof(model.CompanyName), 16, S["Enter the company name, sixteen characters at most."]);
            Require(context, settings.CompanyIdentification, nameof(model.CompanyIdentification), 10, S["Enter the company identification your bank gave you, ten characters at most."]);

            Require(context, settings.CompanyEntryDescription, nameof(model.CompanyEntryDescription), 10, S["Enter the company entry description, such as PAYROLL or VENDOR PAY, ten characters at most."]);

            if (settings.ImmediateDestination is { } destination && (destination.Length != 9 || !destination.All(char.IsAsciiDigit)))
            {
                context.Updater.ModelState.AddModelError(Prefix + "." + nameof(model.ImmediateDestination), S["Enter the routing number of the bank that receives the file, nine digits, or leave it empty for this account's bank."]);
            }
        }

        account.Put(settings);

        return Edit(account, context);
    }

    private void Require(UpdateEditorContext context, string value, string field, int maxLength, string message)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength)
        {
            context.Updater.ModelState.AddModelError(Prefix + "." + field, message);
        }
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
