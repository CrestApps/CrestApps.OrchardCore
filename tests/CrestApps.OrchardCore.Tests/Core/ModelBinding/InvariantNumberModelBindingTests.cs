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
/// A number input and script always post a dot ("12.50"), while MVC reads a form value in the request culture: in de-DE
/// "12.50" binds as 1250 and in fr-FR it doesn't bind. ASP.NET Core's own answer is the <c>__Invariant</c> form field: a
/// field it names is read with the invariant culture. An <c>asp-for</c> number input adds it by itself once
/// <c>AddInvariantNumberInputs()</c> turns on <see cref="FormInputRenderMode.DetectCultureFromInputType"/>; a hand-written
/// input or a script adds it next to the value. These tests post through MVC's own value providers and model binders, with
/// the services each module's <c>Startup</c> registers, under the request culture, as the actions are called in a site.
/// </summary>
public sealed class InvariantNumberModelBindingTests
{
    public static TheoryData<string> Cultures => new() { "en-US", "de-DE", "fr-FR", "nl-NL" };

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task StoreCreditChange_BindsTheAmountTheFormSent(string culture)
    {
        var result = await BindAsync<StoreCreditAdminController>(new CustomersStartup(), nameof(StoreCreditAdminController.Change), culture, Invariant(new() { ["amount"] = "12.50" }));

        Assert.Equal(12.50m, result.Arguments[1]);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task GiftCardAdjust_BindsANegativeAmountTheFormSent(string culture)
    {
        var result = await BindAsync<GiftCardsAdminController>(new OrdersStartup(), nameof(GiftCardsAdminController.Adjust), culture, Invariant(new() { ["amount"] = "-5.25" }));

        Assert.Equal(-5.25m, result.Arguments[1]);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task ReturnRefund_BindsTheAmountTheFormSent(string culture)
    {
        var result = await BindAsync<ReturnsAdminController>(new OrdersStartup(), nameof(ReturnsAdminController.Refund), culture, Invariant(new() { ["amount"] = "12.50" }));

        Assert.Equal(12.50m, result.Arguments[1]);
    }

    // A property of a view model binds the same way as an action parameter.
    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task OrderRefund_BindsTheAmountPropertyTheFormSent(string culture)
    {
        var result = await BindAsync<OrdersAdminController>(new OrdersStartup(), nameof(OrdersAdminController.Refund), culture, Invariant(new() { ["Amount"] = "12.50" }, KeyValuePair.Create("Reason", new StringValues("Damaged"))));

        Assert.Equal(12.50m, Assert.IsType<OrderRefundInputModel>(result.Arguments[1]).Amount);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task WorkOrderUse_BindsTheQuantityTheFormSent(string culture)
    {
        var result = await BindAsync<WorkOrdersController>(new WorkOrdersStartup(), nameof(WorkOrdersController.Use), culture, Invariant(new() { ["quantity"] = "12.50" }));

        Assert.Equal(12.50m, result.Arguments[2]);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task TimesheetAddLine_BindsTheHoursTheFormSent(string culture)
    {
        var result = await BindAsync<TimesheetsController>(new WorkforceStartup(), nameof(TimesheetsController.AddLine), culture, Invariant(new() { ["hours"] = "12.50" }, KeyValuePair.Create("date", new StringValues("2026-11-16"))));

        Assert.Equal(12.50m, result.Arguments[3]);
    }

    // The time clock's script writes the browser's coordinates, which always have a dot, and names them in __Invariant.
    [Theory]
    [MemberData(nameof(Cultures))]
    public async Task TimeClockPunch_BindsTheCoordinatesTheScriptSent(string culture)
    {
        var result = await BindAsync<MyTimeController>(new WorkforceStartup(), nameof(MyTimeController.Punch), culture, Invariant(new()
        {
            ["latitude"] = "52.520008",
            ["longitude"] = "-13.404954",
        }, KeyValuePair.Create("kind", new StringValues("In"))));

        Assert.Equal(52.520008d, result.Arguments[1]);
        Assert.Equal(-13.404954d, result.Arguments[2]);
    }

    // The bug the __Invariant field fixes, as MVC reads a form value without it: the dot is a group separator in de-DE and
    // nl-NL, and not a number in fr-FR.
    [Theory]
    [InlineData("de-DE", 1250)]
    [InlineData("nl-NL", 1250)]
    [InlineData("fr-FR", null)]
    public async Task WithoutTheInvariantField_TheFrameworkReadsTheFormValueInTheRequestCulture(string culture, int? expected)
    {
        var result = await BindAsync<StoreCreditAdminController>(new CustomersStartup(), nameof(StoreCreditAdminController.Change), culture, new() { ["amount"] = "12.50" });

        Assert.Equal((decimal?)expected, (decimal?)result.Arguments[1]);
    }

    // An asp-for number input writes its value invariantly and marks it with an __Invariant field.
    [Fact]
    public void AddInvariantNumberInputs_WritesNumberInputsInvariantly()
    {
        using var services = new ServiceCollection().AddLogging().AddMvcCore().Services.AddInvariantNumberInputs().BuildServiceProvider();

        Assert.Equal(FormInputRenderMode.DetectCultureFromInputType, services.GetRequiredService<IOptions<MvcViewOptions>>().Value.HtmlHelperOptions.FormInputRenderMode);
    }

    [Fact]
    public void AddInvariantNumberInputs_CalledTwice_RegistersItOnce()
    {
        var services = new ServiceCollection()
            .AddInvariantNumberInputs()
            .AddInvariantNumberInputs();

        Assert.Single(services, descriptor => descriptor.ImplementationType?.Name == "InvariantNumberMvcViewOptionsSetup");
    }

    // Posts numbers as a number input or a script does, with the __Invariant field naming them, plus other fields as they are.
    private static Dictionary<string, StringValues> Invariant(Dictionary<string, StringValues> numbers, params KeyValuePair<string, StringValues>[] others)
    {
        var form = new Dictionary<string, StringValues>(numbers, StringComparer.OrdinalIgnoreCase)
        {
            ["__Invariant"] = new StringValues([.. numbers.Keys]),
        };

        foreach (var (key, value) in others)
        {
            form[key] = value;
        }

        return form;
    }

    private static Task<FormPostBinder.Result> BindAsync<TController>(IStartup startup, string action, string culture, Dictionary<string, StringValues> form)
    {
        var method = typeof(TController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(candidate => candidate.Name == action && candidate.GetCustomAttributes<HttpPostAttribute>().Any());

        return FormPostBinder.BindAsync(startup, method, culture, form);
    }
}
