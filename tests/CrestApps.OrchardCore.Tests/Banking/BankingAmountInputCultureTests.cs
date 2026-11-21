using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Banking.Checks.Services;
using CrestApps.OrchardCore.Banking.Checks.Drivers;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Drivers;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Core.ModelBinding;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using BankingStartup = CrestApps.OrchardCore.Banking.Startup;

namespace CrestApps.OrchardCore.Tests.Banking;

/// <summary>
/// Found with the fifth fix verification (outside the PR, on main from #1332): the disbursement and check editors read
/// their amount text with <c>NumberStyles.Number</c> and the invariant culture, where the comma is a group separator, so
/// a German admin's "140,50" was a payment of 14050. Each editor is posted here through MVC's own binder, under the
/// request culture, into the real driver.
/// </summary>
public sealed class BankingAmountInputCultureTests
{
    public static TheoryData<string, string> Posts => new()
    {
        { "en-US", "140.50" },
        { "de-DE", "140.50" },
        { "de-DE", "140,50" },
        { "fr-FR", "140,50" },
        { "nl-NL", "140.50" },
        { "nl-NL", "140,50" },
    };

    public static TheoryData<string, string> Unreadable => new()
    {
        { "en-US", "1,250.00" },
        { "de-DE", "1.250,00" },
        { "fr-FR", "douze" },
    };

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task Disbursement_SavesTheAmountTheFormSent(string culture, string posted)
    {
        var disbursement = new Disbursement();
        var updater = Post(culture, "Disbursement.Amount", posted);

        await ((IDisplayDriver<Disbursement>)CreateDisbursementDriver()).UpdateEditorAsync(disbursement, updater.CreateContext(isNew: true));

        Assert.True(updater.ModelState.IsValid);
        Assert.Equal(140.50m, disbursement.Amount);
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public async Task Disbursement_WithAnAmountThatIsNotANumber_IsRefused(string culture, string posted)
    {
        var disbursement = new Disbursement();
        var updater = Post(culture, "Disbursement.Amount", posted);

        await ((IDisplayDriver<Disbursement>)CreateDisbursementDriver()).UpdateEditorAsync(disbursement, updater.CreateContext(isNew: true));

        Assert.False(updater.ModelState.IsValid);
        Assert.Equal(0m, disbursement.Amount);
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task Check_SavesTheAmountTheFormSent(string culture, string posted)
    {
        var check = new CheckRecord();
        var updater = Post(culture, "CheckRecord.Amount", posted, ("CheckRecord.CheckDate", "2026-11-17"));

        await ((IDisplayDriver<CheckRecord>)CreateCheckDriver()).UpdateEditorAsync(check, updater.CreateContext(isNew: true));

        Assert.True(updater.ModelState.IsValid);
        Assert.Equal(140.50m, check.Amount);
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public async Task Check_WithAnAmountThatIsNotANumber_IsRefused(string culture, string posted)
    {
        var check = new CheckRecord();
        var updater = Post(culture, "CheckRecord.Amount", posted, ("CheckRecord.CheckDate", "2026-11-17"));

        await ((IDisplayDriver<CheckRecord>)CreateCheckDriver()).UpdateEditorAsync(check, updater.CreateContext(isNew: true));

        Assert.False(updater.ModelState.IsValid);
        Assert.Equal(0m, check.Amount);
    }

    // The amount above which a check prints a second signature line, a control on money going out.
    [Theory]
    [MemberData(nameof(Posts))]
    public async Task CheckLayout_SavesTheSecondSignatureAmountTheFormSent(string culture, string posted)
    {
        var layout = new CheckLayout { ItemId = "layout-1" };
        var updater = Post(culture, "CheckLayout.SecondSignatureAbove", posted, ("CheckLayout.Name", "Standard"));

        await ((IDisplayDriver<CheckLayout>)CreateCheckLayoutDriver()).UpdateEditorAsync(layout, updater.CreateContext());

        Assert.True(updater.ModelState.IsValid);
        Assert.Equal(140.50m, layout.SecondSignatureAbove);
    }

    [Theory]
    [MemberData(nameof(Unreadable))]
    public async Task CheckLayout_WithASecondSignatureAmountThatIsNotANumber_IsRefused(string culture, string posted)
    {
        var layout = new CheckLayout { ItemId = "layout-1", SecondSignatureAbove = 5000m };
        var updater = Post(culture, "CheckLayout.SecondSignatureAbove", posted, ("CheckLayout.Name", "Standard"));

        await ((IDisplayDriver<CheckLayout>)CreateCheckLayoutDriver()).UpdateEditorAsync(layout, updater.CreateContext());

        Assert.False(updater.ModelState.IsValid);
        Assert.Equal(5000m, layout.SecondSignatureAbove);
    }

    // An empty amount still means no second signature line.
    [Fact]
    public async Task CheckLayout_WithAnEmptySecondSignatureAmount_HasNone()
    {
        var layout = new CheckLayout { ItemId = "layout-1", SecondSignatureAbove = 5000m };
        var updater = Post("de-DE", "CheckLayout.SecondSignatureAbove", string.Empty, ("CheckLayout.Name", "Standard"));

        await ((IDisplayDriver<CheckLayout>)CreateCheckLayoutDriver()).UpdateEditorAsync(layout, updater.CreateContext());

        Assert.True(updater.ModelState.IsValid);
        Assert.Null(layout.SecondSignatureAbove);
    }

    private static FormPostUpdateModel Post(string culture, string field, string value, params (string Field, string Value)[] others)
    {
        var form = new Dictionary<string, StringValues> { [field] = value };

        foreach (var (otherField, otherValue) in others)
        {
            form[otherField] = otherValue;
        }

        return new FormPostUpdateModel(new BankingStartup(), culture, form);
    }

    private static DisbursementDisplayDriver CreateDisbursementDriver()
    {
        var disbursements = new Mock<IDisbursementService>();
        disbursements.SetupGet(service => service.Providers).Returns([]);

        return new DisbursementDisplayDriver(CreateBankAccounts(), disbursements.Object, new PassThroughStringLocalizer<DisbursementDisplayDriver>());
    }

    private static CheckRecordDisplayDriver CreateCheckDriver()
    {
        var clock = new Mock<ILocalClock>();
        clock.Setup(value => value.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero));

        return new CheckRecordDisplayDriver(CreateBankAccounts(), clock.Object, CreateAddressEditor(), new ModelShapeFactory(), new PassThroughStringLocalizer<CheckRecordDisplayDriver>());
    }

    private static CheckLayoutDisplayDriver CreateCheckLayoutDriver()
        => new(Mock.Of<ICheckFileStore>(), CreateAddressEditor(), new ModelShapeFactory(), new PassThroughStringLocalizer<CheckLayoutDisplayDriver>());

    private static IAddressEditorService CreateAddressEditor()
    {
        var addressEditor = new Mock<IAddressEditorService>();
        addressEditor
            .Setup(service => service.BuildAsync(It.IsAny<CrestApps.OrchardCore.Addresses.Models.Address>(), It.IsAny<AddressEditorOptions>()))
            .ReturnsAsync(new AddressEditorViewModel());

        return addressEditor.Object;
    }

    private static IBankAccountManager CreateBankAccounts()
    {
        var accounts = new Mock<IBankAccountManager>();
        accounts.Setup(manager => manager.ListByOwnersAsync(It.IsAny<IEnumerable<PartyRef>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        accounts.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<BankAccount>());

        return accounts.Object;
    }
}
