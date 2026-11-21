using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Tests.Core.ModelBinding;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Found with the fifth fix verification (outside the PR, on main from #1332): a tax deposit's amount was read with
/// <c>NumberStyles.AllowDecimalPoint</c> and the invariant culture, and anything it couldn't read became 0, so a German
/// admin's "140,50" was refused as "the amount is more than zero". The liabilities page's form is posted here through
/// MVC's own binder, under the request culture, into the real controller.
/// </summary>
public sealed class PayrollDepositAmountInputCultureTests
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

    [Theory]
    [MemberData(nameof(Posts))]
    public async Task RecordDeposit_SavesTheAmountTheFormSent(string culture, string posted)
    {
        var deposits = CreateDeposits(out var created, out _);

        await PostAsync(deposits.Object, Mock.Of<INotifier>(), culture, posted);

        Assert.Equal(140.50m, Assert.Single(created).Amount);
    }

    // A value that can't be read is refused as such, never turned into 0.
    [Theory]
    [InlineData("en-US", "1,250.00")]
    [InlineData("de-DE", "1.250,00")]
    [InlineData("fr-FR", "douze")]
    public async Task RecordDeposit_WithAnAmountThatIsNotANumber_IsRefused(string culture, string posted)
    {
        var deposits = CreateDeposits(out var created, out var validated);
        var notifier = new Mock<INotifier>();

        var result = await PostAsync(deposits.Object, notifier.Object, culture, posted);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Empty(created);
        Assert.Empty(validated);
        Assert.Single(notifier.Invocations, invocation => invocation.Arguments.Count > 0 && Equals(invocation.Arguments[0], NotifyType.Error));
    }

    private static async Task<IActionResult> PostAsync(ITaxDepositManager deposits, INotifier notifier, string culture, string amount)
    {
        var employers = new Mock<IEmployerManager>();
        employers.Setup(manager => manager.FindByIdAsync("employer-1", It.IsAny<CancellationToken>())).ReturnsAsync(new Employer { ItemId = "employer-1", Currency = "USD" });

        var scope = new Mock<IPayrollScopeService>();
        scope.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<Permission>(), "employer-1")).ReturnsAsync(true);

        var controller = new PayrollFilingsController(
            Mock.Of<ITaxLiabilityService>(),
            deposits,
            Mock.Of<IPayrollFilingManager>(),
            Mock.Of<IPayrollFilingService>(),
            employers.Object,
            scope.Object,
            documents: null,
            Mock.Of<ILocalClock>(),
            notifier,
            Mock.Of<IStringLocalizerFactory>(),
            new EchoHtmlLocalizer<PayrollFilingsController>(),
            new PassThroughStringLocalizer<PayrollFilingsController>());

        var method = typeof(PayrollFilingsController).GetMethod(nameof(PayrollFilingsController.RecordDeposit), BindingFlags.Public | BindingFlags.Instance);
        var bound = await FormPostBinder.BindAsync(new PayrollStartup(), method, culture, new Dictionary<string, StringValues>
        {
            ["NewDeposit.EmployerId"] = "employer-1",
            ["NewDeposit.Jurisdiction"] = "US",
            ["NewDeposit.Group"] = "Withholding",
            ["NewDeposit.PeriodStart"] = "2026-10-01",
            ["NewDeposit.PeriodEnd"] = "2026-10-31",
            ["NewDeposit.Amount"] = amount,
            ["NewDeposit.PaidOn"] = "2026-11-10",
        }, controller: controller);

        controller.HttpContext.User = TestSecurity.User("admin");

        return await (Task<IActionResult>)method.Invoke(controller, bound.Arguments);
    }

    // Validates as the real manager does: the amount is more than zero.
    private static Mock<ITaxDepositManager> CreateDeposits(out List<TaxDeposit> created, out List<TaxDeposit> validated)
    {
        var createdDeposits = new List<TaxDeposit>();
        var validatedDeposits = new List<TaxDeposit>();
        var deposits = new Mock<ITaxDepositManager>();

        deposits.Setup(manager => manager.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new TaxDeposit { ItemId = "deposit-1" });
        deposits
            .Setup(manager => manager.ValidateAsync(It.IsAny<TaxDeposit>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaxDeposit deposit, CancellationToken _) =>
            {
                validatedDeposits.Add(deposit);

                var result = new ValidationResultDetails();

                if (deposit.Amount <= 0)
                {
                    result.Fail(new ValidationResult("The amount is more than zero.", [nameof(TaxDeposit.Amount)]));
                }

                return result;
            });
        deposits.Setup(manager => manager.CreateAsync(It.IsAny<TaxDeposit>(), It.IsAny<CancellationToken>())).Callback((TaxDeposit deposit, CancellationToken _) => createdDeposits.Add(deposit));

        created = createdDeposits;
        validated = validatedDeposits;

        return deposits;
    }
}
