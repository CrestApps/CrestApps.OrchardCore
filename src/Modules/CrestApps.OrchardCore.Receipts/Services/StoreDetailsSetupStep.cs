using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Receipts.Core;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Receipts.Services;

/// <summary>
/// The setup guide's first step: the store's name and how customers reach it, printed on receipts, invoices and
/// emails.
/// </summary>
internal sealed class StoreDetailsSetupStep : IStoreSetupStep
{
    private const string NameField = "name";
    private const string EmailField = "email";
    private const string PhoneField = "phone";
    private const string AddressField = "address";

    private readonly ISiteService _siteService;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public StoreDetailsSetupStep(ISiteService siteService, IOptions<AdminOptions> adminOptions, IStringLocalizer<StoreDetailsSetupStep> stringLocalizer)
    {
        _siteService = siteService;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    public string Name
        => "store-details";

    public int Order
        => 10;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<ReceiptSettings>();
        var done = !string.IsNullOrWhiteSpace(settings.BusinessName) && !string.IsNullOrWhiteSpace(settings.ContactEmail);

        return new StoreSetupStepDescription
        {
            Title = S["Your store's details"],
            Description = S["Your store's name and how customers reach you. They're printed on receipts, invoices and the emails customers get."],
            IsDone = done,
            Status = done ? S["{0}, {1}", settings.BusinessName, settings.ContactEmail] : null,
            Url = $"~/{_adminOptions.AdminUrlPrefix}/Settings/{ReceiptsConstants.SettingsGroupId}",
            UrlText = S["Receipt settings, with your logo"],
            ActionText = S["Save the details"],
            Fields =
            [
                new StoreSetupField { Name = NameField, Label = S["Store name"], Value = settings.BusinessName, Required = true },
                new StoreSetupField { Name = EmailField, Label = S["Email for customers"], Value = settings.ContactEmail, Type = "email", Required = true, Hint = S["Where customers write to you. Shown on receipts and emails."] },
                new StoreSetupField { Name = PhoneField, Label = S["Phone (optional)"], Value = settings.ContactPhone, Type = "tel" },
                new StoreSetupField { Name = AddressField, Label = S["Address (optional)"], Value = settings.BusinessAddress, Type = "textarea", Hint = S["Printed on receipts and invoices, where some countries require it."] },
            ],
        };
    }

    public async Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
    {
        var name = context.GetValue(NameField);
        var email = context.GetValue(EmailField);

        if (name is null || email is null || !email.Contains('@', StringComparison.Ordinal))
        {
            return StoreSetupActionResult.Failure(S["Enter the store's name and an email address customers can write to."]);
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<ReceiptSettings>();

        settings.BusinessName = name;
        settings.ContactEmail = email;
        settings.ContactPhone = context.GetValue(PhoneField);
        settings.BusinessAddress = context.GetValue(AddressField);

        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);

        return StoreSetupActionResult.Success(S["Saved the store's details."]);
    }
}
