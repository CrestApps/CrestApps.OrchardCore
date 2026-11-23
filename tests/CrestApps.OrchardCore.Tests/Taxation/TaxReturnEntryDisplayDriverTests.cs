using CrestApps.OrchardCore.Taxation.Drivers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Taxation.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// The drivers of the payment form of a tax return and of the form that files a document electronically, which packs
/// extend with their own drivers.
/// </summary>
public sealed class TaxReturnEntryDisplayDriverTests
{
    [Fact]
    public async Task PaymentEntry_ReadsTheAmountDayReferenceAndMethod()
    {
        // Arrange
        var driver = new TaxReturnPaymentEntryDisplayDriver(Mock.Of<ILocalClock>(), new PassThroughStringLocalizer<TaxReturnPaymentEntryDisplayDriver>());
        var entry = new TaxReturnPaymentEntry { Return = new TaxReturn() };
        var context = UpdateContext(new TaxReturnPaymentEntryViewModel
        {
            Amount = " 69.50 ",
            PaidOn = new DateOnly(2026, 11, 18),
            Reference = "ACH-1",
            Method = "ACH debit",
        });

        // Act
        await driver.UpdateAsync(entry, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal((69.50m, new DateOnly(2026, 11, 18), "ACH-1", "ACH debit"), (entry.Payment.Amount, entry.Payment.PaidOn, entry.Payment.Reference, entry.Payment.Method));
    }

    [Fact]
    public async Task PaymentEntry_RefusesAnAmountItCantRead()
    {
        // Arrange
        var driver = new TaxReturnPaymentEntryDisplayDriver(Mock.Of<ILocalClock>(), new PassThroughStringLocalizer<TaxReturnPaymentEntryDisplayDriver>());
        var entry = new TaxReturnPaymentEntry { Return = new TaxReturn() };
        var context = UpdateContext(new TaxReturnPaymentEntryViewModel { Amount = "sixty", PaidOn = new DateOnly(2026, 11, 18) });

        // Act
        await driver.UpdateAsync(entry, context);

        // Assert
        Assert.False(context.Updater.ModelState.IsValid);
        Assert.Equal(0m, entry.Payment.Amount);
    }

    [Theory]
    [InlineData("TaxReturnJson|Gateway", true, "TaxReturnJson", "Gateway", true)]
    [InlineData("TaxReturnJson|Manual", false, "TaxReturnJson", "Manual", true)]
    [InlineData("", false, null, null, false)]
    [InlineData("TaxReturnJson", false, null, null, false)]
    public async Task SubmissionRequest_ReadsTheFormatChannelAndTestFlag(string option, bool isTest, string format, string transport, bool valid)
    {
        // Arrange
        var driver = new EFileSubmissionRequestDisplayDriver(Mock.Of<IEFilingService>(), Mock.Of<IStringLocalizerFactory>(), new PassThroughStringLocalizer<EFileSubmissionRequestDisplayDriver>());
        var request = new EFileSubmissionRequest { Target = new EFileTarget { Type = "TaxReturn", Id = "r1" } };
        var context = UpdateContext(new EFileSubmissionRequestViewModel { Option = option, IsTest = isTest });

        // Act
        await driver.UpdateAsync(request, context);

        // Assert
        Assert.Equal(valid, context.Updater.ModelState.IsValid);
        Assert.Equal((format, transport, isTest), (request.Format, request.Transport, request.IsTest));
    }

    private static UpdateEditorContext UpdateContext(object submitted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(submitted));
}
