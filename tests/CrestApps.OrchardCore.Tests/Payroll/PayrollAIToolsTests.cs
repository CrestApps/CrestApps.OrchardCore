using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.AI;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Disbursements;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// The Payroll AI tools: a person's own statement explained with what changed since their pay before it, and a pay run
/// compared with the run before it, each refused without the permission of the page it mirrors and never with a tax id
/// or an account number.
/// </summary>
public sealed class PayrollAIToolsTests
{
    [Fact]
    public void Comparison_SumsEachKindOfLine_LeavesOutReversals_AndPutsTheLargestChangeFirst()
    {
        var codes = new Dictionary<string, PayCode>(StringComparer.Ordinal)
        {
            ["reg"] = new PayCode { ItemId = "reg", Code = "REG" },
            ["bonus"] = new PayCode { ItemId = "bonus", Code = "BONUS" },
        };
        var before = new PayStatement
        {
            Lines =
            [
                new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Description = "Regular", Amount = 1600 },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS", Description = "Social Security", Amount = 99.20m },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS", Description = "Social Security", Payer = PayLinePayer.Employer, Amount = 99.20m },
            ],
        };
        var now = new PayStatement
        {
            Lines =
            [
                new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Description = "Regular", Amount = 1800 },
                new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "bonus", Description = "Bonus", Amount = 500 },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS", Description = "Social Security", Amount = 142.60m },
                new PayStatementLine { Type = PayLineType.Tax, TaxCode = "US-SS", Description = "Social Security", Payer = PayLinePayer.Employer, Amount = 99.20m },
            ],
        };
        var reversal = new PayStatement { VoidOf = "x", Lines = [new PayStatementLine { Type = PayLineType.Earning, PayCodeId = "reg", Amount = -1800 }] };

        var changes = PayStatementComparison.Compare([now, reversal], [before], codes);

        Assert.Equal([("BONUS", 0m, 500m), ("REG", 1600m, 1800m), ("US-SS", 99.20m, 142.60m)], changes.Select(change => (change.Key, change.Previous, change.Current)));
        Assert.False(PayStatementComparison.Counts(reversal));
    }

    [Fact]
    public async Task Tools_ExplainOwnPayAndTheRunsChange_OnlyWithThePagesPermission()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => PayRunProcessingTests.AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);

        await harness.RunAsync(async services =>
        {
            var staff = services.GetRequiredService<IStaffMemberManager>();
            var member = await staff.FindByIdAsync(setup.StaffMemberId);

            member.UserId = "lee-user";
            await staff.UpdateAsync(member);
        });
        await PayRunProcessingTests.ApproveAsync(harness, setup);
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name))).Succeeded));

        // The next period: 90 hours instead of 80, calculated and not yet approved.
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);
        var nextNumber = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var first = await runs.FindByIdAsync(setup.RunId);
            var run = await runs.NewAsync();

            run.EmployerId = first.EmployerId;
            run.PayScheduleId = first.PayScheduleId;
            run.PeriodStart = new DateOnly(2026, 11, 15);
            run.PeriodEnd = new DateOnly(2026, 11, 28);
            run.PayDate = new DateOnly(2026, 12, 4);
            await runs.CreateAsync(run);

            var inputs = services.GetRequiredService<IPayRunInputManager>();
            var hours = await inputs.NewAsync();

            hours.RunId = run.ItemId;
            hours.StaffMemberId = setup.StaffMemberId;
            hours.PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG")).ItemId;
            hours.Hours = 90;
            await inputs.CreateAsync(hours);

            return run.ItemId;
        });
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().CalculateAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(nextNumber), preparer)).Succeeded));

        var lee = CalendarTestHarness.User("lee-user", PayrollPermissions.ViewOwnPay.Name);
        var noPermission = CalendarTestHarness.User("lee-user");
        var notStaff = CalendarTestHarness.User("nobody-user", PayrollPermissions.ViewOwnPay.Name);
        var viewer = CalendarTestHarness.User("viewer", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, setup.EmployerId).Name);
        var stranger = CalendarTestHarness.User("stranger", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayRuns, "fabrikam").Name);

        var (own, refused, unlinked, changes, hidden, first, missing) = await harness.RunAsync(async services =>
        {
            var runs = services.GetRequiredService<IPayRunManager>();
            var number = (await runs.FindByIdAsync(nextNumber)).Number;

            return (
                await InvokeAsync(new MyPayStatementTool(), services, lee),
                await InvokeAsync(new MyPayStatementTool(), services, noPermission),
                await InvokeAsync(new MyPayStatementTool(), services, notStaff),
                await InvokeAsync(new PayRunChangesTool(), services, viewer, ("run", number)),
                await InvokeAsync(new PayRunChangesTool(), services, stranger, ("run", number)),
                await InvokeAsync(new PayRunChangesTool(), services, viewer, ("run", setup.RunNumber)),
                await InvokeAsync(new PayRunChangesTool(), services, viewer));
        }, commit: false);

        // Lee's own statement: the lines and how it was paid, never the tax id.
        using (var document = JsonDocument.Parse(own))
        {
            var root = document.RootElement;

            Assert.Equal((1600m, 1500.80m), (root.GetProperty("gross").GetDecimal(), root.GetProperty("net").GetDecimal()));
            Assert.Equal("Check", root.GetProperty("payments")[0].GetProperty("method").GetString());
            Assert.Contains(root.GetProperty("lines").EnumerateArray(), line => line.GetProperty("code").GetString() == "REG" && line.GetProperty("amount").GetDecimal() == 1600m);
            Assert.Equal(JsonValueKind.Null, root.GetProperty("previous").ValueKind);
        }

        Assert.DoesNotContain("123456789", own, StringComparison.Ordinal);
        Assert.DoesNotContain("6789", own, StringComparison.Ordinal);
        Assert.Equal("You may not see pay statements.", refused);
        Assert.Contains("isn't linked to a staff member", unlinked, StringComparison.Ordinal);

        // The new run against the paid one: 10 more hours at 20.00.
        using (var document = JsonDocument.Parse(changes))
        {
            var root = document.RootElement;
            var person = root.GetProperty("netChanged")[0];

            Assert.Equal(200m, root.GetProperty("change").GetProperty("gross").GetDecimal());
            Assert.Equal("Lee Park", person.GetProperty("name").GetString());
            Assert.Equal(("REG", 200m), (person.GetProperty("lines")[0].GetProperty("code").GetString(), person.GetProperty("lines")[0].GetProperty("change").GetDecimal()));
            Assert.Empty(root.GetProperty("joined").EnumerateArray());
        }

        Assert.StartsWith("There's no pay run numbered", hidden, StringComparison.Ordinal);
        Assert.Contains("nothing to compare it with", first, StringComparison.Ordinal);
        Assert.Equal("Say which pay run, by its number.", missing);
    }

    private static async Task<string> InvokeAsync(AIFunction tool, IServiceProvider services, ClaimsPrincipal user, params (string Name, object Value)[] arguments)
    {
        var values = arguments.ToDictionary(argument => argument.Name, argument => argument.Value);
        var result = await tool.InvokeAsync(new AIFunctionArguments(values) { Services = new UserServices(services, user) }, TestContext.Current.CancellationToken);

        return result?.ToString();
    }

    // The scope's services, with the signed-in user the tools read.
    private sealed class UserServices : IServiceProvider
    {
        private readonly IServiceProvider _services;
        private readonly HttpContextAccessor _accessor;

        public UserServices(IServiceProvider services, ClaimsPrincipal user)
        {
            _services = services;
            _accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user, RequestServices = services } };
        }

        public object GetService(Type serviceType)
            => serviceType == typeof(IHttpContextAccessor) ? _accessor : _services.GetService(serviceType);
    }
}
