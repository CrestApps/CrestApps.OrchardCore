using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.PayLater.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// The setup guide's offline payments: bank transfers, for customers who pay from their bank after ordering.
/// </summary>
internal sealed class OfflinePaymentsSetupStep : IStoreSetupStep
{
    private const string InstructionsField = "instructions";

    private readonly ISiteService _siteService;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public OfflinePaymentsSetupStep(ISiteService siteService, IOptions<AdminOptions> adminOptions, IStringLocalizer<OfflinePaymentsSetupStep> stringLocalizer)
    {
        _siteService = siteService;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    public string Name
        => "offline-payments";

    public int Order
        => 35;

    public async Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
    {
        var bankTransfer = await _siteService.GetSettingsAsync<BankTransferSettings>();
        var cashOnDelivery = await _siteService.GetSettingsAsync<CashOnDeliverySettings>();
        var offered = new List<string>();

        if (bankTransfer.Enabled && !string.IsNullOrWhiteSpace(bankTransfer.Instructions))
        {
            offered.Add(S["bank transfer"]);
        }

        if (cashOnDelivery.Enabled && !string.IsNullOrWhiteSpace(cashOnDelivery.Instructions))
        {
            offered.Add(S["cash on delivery"]);
        }

        return new StoreSetupStepDescription
        {
            Title = S["Take bank transfers"],
            Description = S["Lets customers order now and pay from their bank. The order waits until you mark it paid, when the money arrives. Enter what they need to pay you, such as your bank's name and your account number."],
            IsDone = offered.Count > 0,
            IsOptional = true,
            Status = offered.Count == 0 ? null : S["Customers can pay by {0}.", string.Join(", ", offered)],
            Url = $"~/{_adminOptions.AdminUrlPrefix}/Settings/{PayLaterConstants.SettingsGroupId}",
            UrlText = S["Offline payment settings"],
            ActionText = S["Take bank transfers"],
            Fields =
            [
                new StoreSetupField { Name = InstructionsField, Label = S["How to pay you"], Value = bankTransfer.Instructions, Type = "textarea", Required = true, Hint = S["Shown to the customer after they order, and in their order email."] },
            ],
        };
    }

    public async Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
    {
        if (context.GetValue(InstructionsField) is not { } instructions)
        {
            return StoreSetupActionResult.Failure(S["Enter how customers pay you, such as your bank's name and account number."]);
        }

        var site = await _siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<BankTransferSettings>();

        settings.Enabled = true;
        settings.Instructions = instructions;
        site.Put(settings);
        await _siteService.UpdateSiteSettingsAsync(site);

        return StoreSetupActionResult.Success(S["Customers can pay by bank transfer."]);
    }
}
