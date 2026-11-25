using System.Reflection;
using CrestApps.OrchardCore.Core.ModelBinding;
using CrestApps.OrchardCore.Customers.Controllers;
using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.WorkOrders.Controllers;
using CrestApps.OrchardCore.Workforce.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using OrchardCore.Modules;
using CustomersStartup = CrestApps.OrchardCore.Customers.Startup;
using OrdersStartup = CrestApps.OrchardCore.Orders.Startup;
using WorkforceStartup = CrestApps.OrchardCore.Workforce.Startup;
using WorkOrdersStartup = CrestApps.OrchardCore.WorkOrders.Startup;

namespace CrestApps.OrchardCore.Tests.Core.ModelBinding;

/// <summary>
/// Found while fixing F4 (third fix verification): MVC binds a form value in the request's culture, while a number input
/// and script always write a dot. In de-DE "12.50" bound as 1250 and in fr-FR it did not bind, in every admin and staff
/// action that takes a number from a form. These tests post through MVC's own value providers and model binders, with the
/// services each module's <c>Startup</c> registers, under the request culture, as the actions are called in a site.
/// </summary>
public sealed class InvariantNumberModelBindingTests
{
    public static TheoryData<string, string> Posts => new()
    {
        { "en-US", "12.50" },
        { "de-DE", "12.50" },
        { "fr-FR", "12.50" },
        { "nl-NL", "12.50" },
        { "de-DE", "12,50" },
        { "fr-FR", "12,50" },
    };

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task StoreCreditChange_BindsTheAmountTheFormSent(string culture, string posted)
    {
        var result = await BindAsync<StoreCreditAdminController>(new CustomersStartup(), nameof(StoreCreditAdminController.Change), culture, new() { ["amount"] = posted });

        Assert.Equal(12.50m, result.Arguments[1]);
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task GiftCardAdjust_BindsTheAmountTheFormSent(string culture, string posted)
    {
        var result = await BindAsync<GiftCardsAdminController>(new OrdersStartup(), nameof(GiftCardsAdminController.Adjust), culture, new() { ["amount"] = posted });

        Assert.Equal(12.50m, result.Arguments[1]);
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task ReturnRefund_BindsTheAmountTheFormSent(string culture, string posted)
    {
        var result = await BindAsync<ReturnsAdminController>(new OrdersStartup(), nameof(ReturnsAdminController.Refund), culture, new() { ["amount"] = posted });

        Assert.Equal(12.50m, result.Arguments[1]);
    }

    // A property of a CrestApps view model binds the same way as an action parameter.
    [Theory]
    [MemberData(nameof(Posts))]
    public async Task OrderRefund_BindsTheAmountPropertyTheFormSent(string culture, string posted)
    {
        var result = await BindAsync<OrdersAdminController>(new OrdersStartup(), nameof(OrdersAdminController.Refund), culture, new() { ["Amount"] = posted, ["Reason"] = "Damaged" });

        Assert.Equal(12.50m, Assert.IsType<OrderRefundInputModel>(result.Arguments[1]).Amount);
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task WorkOrderUse_BindsTheQuantityTheFormSent(string culture, string posted)
    {
        var result = await BindAsync<WorkOrdersController>(new WorkOrdersStartup(), nameof(WorkOrdersController.Use), culture, new() { ["quantity"] = posted });

        Assert.Equal(12.50m, result.Arguments[2]);
    }

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task TimesheetAddLine_BindsTheHoursTheFormSent(string culture, string posted)
    {
        var result = await BindAsync<TimesheetsController>(new WorkforceStartup(), nameof(TimesheetsController.AddLine), culture, new() { ["hours"] = posted, ["date"] = "2026-11-16" });

        Assert.Equal(12.50m, result.Arguments[3]);
    }

    // The time clock's script writes the browser's coordinates, which always have a dot.
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public async Task TimeClockPunch_BindsTheCoordinatesTheScriptSent(string culture)
    {
        var result = await BindAsync<MyTimeController>(new WorkforceStartup(), nameof(MyTimeController.Punch), culture, new()
        {
            ["kind"] = "In",
            ["latitude"] = "52.520008",
            ["longitude"] = "-13.404954",
        });

        Assert.Equal(52.520008d, result.Arguments[1]);
        Assert.Equal(-13.404954d, result.Arguments[2]);
    }

    // The bug the binding fixes, as MVC alone reads a form value: the dot is a group separator in de-DE and nl-NL, and not a
    // number in fr-FR.
    [Theory]
    [InlineData("de-DE", 1250)]
    [InlineData("nl-NL", 1250)]
    [InlineData("fr-FR", null)]
    public async Task WithoutTheInvariantBinding_TheFrameworkReadsTheFormValueInTheRequestCulture(string culture, int? expected)
    {
        var result = await BindAsync<StoreCreditAdminController>(startup: null, nameof(StoreCreditAdminController.Change), culture, new() { ["amount"] = "12.50" });

        Assert.Equal((decimal?)expected, (decimal?)result.Arguments[1]);
    }

    // A group separator is never taken: "1,250" is not twelve hundred and fifty in one culture and 1.25 in another.
    [Theory]
    [InlineData("en-US", "12,50")]
    [InlineData("en-US", "1,250")]
    [InlineData("de-DE", "1.250,00")]
    [InlineData("de-DE", "twelve")]
    [InlineData("en-US", "Infinity")]
    public async Task AValueThatIsNotANumber_IsAModelError(string culture, string posted)
    {
        var result = await BindAsync<StoreCreditAdminController>(new CustomersStartup(), nameof(StoreCreditAdminController.Change), culture, new() { ["amount"] = posted });

        Assert.Null(result.Arguments[1]);
        Assert.False(result.ModelState.IsValid);
        Assert.Equal(posted, result.ModelState["amount"]?.AttemptedValue);
    }

    [Fact]
    public async Task AnEmptyValue_BindsNothing()
    {
        var result = await BindAsync<StoreCreditAdminController>(new CustomersStartup(), nameof(StoreCreditAdminController.Change), "de-DE", new() { ["amount"] = string.Empty });

        Assert.Null(result.Arguments[1]);
        Assert.True(result.ModelState.IsValid);
    }

    [Fact]
    public async Task ANegativeAmount_KeepsItsSign()
    {
        var result = await BindAsync<GiftCardsAdminController>(new OrdersStartup(), nameof(GiftCardsAdminController.Adjust), "de-DE", new() { ["amount"] = "-5.25" });

        Assert.Equal(-5.25m, result.Arguments[1]);
    }

    // An asp-for number input writes its value invariantly and marks it with an __Invariant field.
    [Fact]
    public void AddInvariantNumberInputs_WritesNumberInputsInvariantly()
    {
        using var services = new ServiceCollection().AddLogging().AddMvcCore().Services.AddInvariantNumberInputs().BuildServiceProvider();

        Assert.Equal(FormInputRenderMode.DetectCultureFromInputType, services.GetRequiredService<IOptions<MvcViewOptions>>().Value.HtmlHelperOptions.FormInputRenderMode);
    }

    [Fact]
    public void AddInvariantNumberInputs_CalledTwice_RegistersTheBinderOnce()
    {
        using var services = new ServiceCollection().AddLogging().AddMvcCore().Services
            .AddInvariantNumberInputs()
            .AddInvariantNumberInputs()
            .BuildServiceProvider();

        var providers = services.GetRequiredService<IOptions<MvcOptions>>().Value.ModelBinderProviders;

        Assert.Single(providers, provider => provider.GetType().Name == "InvariantNumberModelBinderProvider");
        Assert.Equal("InvariantNumberModelBinderProvider", providers[0].GetType().Name);
    }

    private static Task<FormPostBinder.Result> BindAsync<TController>(IStartup startup, string action, string culture, Dictionary<string, StringValues> form)
    {
        var method = typeof(TController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(candidate => candidate.Name == action && candidate.GetCustomAttributes<HttpPostAttribute>().Any());

        return FormPostBinder.BindAsync(startup, method, culture, form);
    }
}
