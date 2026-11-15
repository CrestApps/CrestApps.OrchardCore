using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Payroll.Core;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.DataSources;
using CrestApps.OrchardCore.Payroll.Deployments;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Recipes;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DataSources;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// Pay profiles and pay items: the protected tax id with its keyed hash and recorded reads, the election history, what
/// keeps a profile from being paid, the rules on the split of net pay and on items, records kept while in use, nobody
/// changing their own pay, and pay data leaving only for those who may export it.
/// </summary>
public sealed class PayProfileTests
{
    [Fact]
    public async Task TaxId_IsProtected_HashedTheSameHoweverTyped_FoundWhenUsedTwice_AndEveryReadIsRecorded()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, _) = await AddEmployersAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var kim = await WorkforceTests.AddWorkerAsync(harness, "Kim Diaz");
        var observer = new RecordingObserver();

        var (leeProfile, kimProfile, same, revealed) = await harness.RunAsync(async services =>
        {
            var service = ActivatorUtilities.CreateInstance<DefaultPayProfileService>(services, new List<IPayProfileObserver> { observer }.AsEnumerable());
            var manager = services.GetRequiredService<IPayProfileManager>();
            var first = await NewProfileAsync(manager, lee.ItemId, contoso.ItemId);
            var second = await NewProfileAsync(manager, kim.ItemId, contoso.ItemId);

            await service.SetTaxIdAsync(first, "123-45-6789");
            await service.SetTaxIdAsync(second, " 123 45 6789 ");
            await manager.CreateAsync(first);
            await manager.CreateAsync(second);
            await services.GetRequiredService<global::YesSql.ISession>().FlushAsync();

            return (first, second, await service.FindSameTaxIdAsync(first), await service.RevealTaxIdAsync(first, "a test"));
        });

        Assert.Equal("6789", leeProfile.TaxId.Hint);
        Assert.DoesNotContain("6789", leeProfile.TaxId.Cipher, StringComparison.Ordinal);
        Assert.Equal(leeProfile.TaxIdHash, kimProfile.TaxIdHash);
        Assert.Equal([kimProfile.ItemId], same.Select(profile => profile.ItemId));
        Assert.Equal("123-45-6789", revealed);

        var read = Assert.Single(observer.Changes);
        Assert.Equal((PayProfileChangeKind.TaxIdRevealed, "a test"), (read.Kind, read.Reason));
    }

    [Fact]
    public void Elections_KeepTheirHistory_AndTheOneInEffectOnADayIsUsed()
    {
        var profile = new EmployeePayProfile
        {
            Elections =
            [
                new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 1, 1), Values = new() { ["FilingStatus"] = "Single" } },
                new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 7, 1), Values = new() { ["FilingStatus"] = "Married" } },
                new TaxElection { Jurisdiction = "US-CA", EffectiveFrom = new DateOnly(2026, 1, 1) },
            ],
        };

        Assert.Equal("Single", profile.ElectionOn("us", new DateOnly(2026, 6, 30)).Values["FilingStatus"]);
        Assert.Equal("Married", profile.ElectionOn("US", new DateOnly(2026, 7, 1)).Values["FilingStatus"]);
        Assert.Null(profile.ElectionOn("US", new DateOnly(2025, 12, 31)));
        Assert.Null(profile.ElectionOn("US-NY", new DateOnly(2026, 7, 1)));
    }

    [Fact]
    public async Task Blockers_ListWhatIsMissing_AndGoAwayOnceItIsThere()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, _) = await AddEmployersAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var schedule = await AddScheduleAsync(harness);

        var (before, after) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayProfileService>();
            var profile = await NewProfileAsync(services.GetRequiredService<IPayProfileManager>(), lee.ItemId, contoso.ItemId);
            var missing = (await service.GetBlockersAsync(profile)).Select(blocker => blocker.Code).ToList();

            profile.PayScheduleId = schedule.ItemId;
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];
            profile.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2020, 1, 1) }];
            await service.SetTaxIdAsync(profile, "123456789");

            var staff = await services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>().FindByIdAsync(lee.ItemId);

            staff.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2020, 1, 1), HourlyRate = 20 }] });
            await services.GetRequiredService<CrestApps.OrchardCore.Staff.Services.IStaffMemberManager>().UpdateAsync(staff);
            await services.GetRequiredService<global::YesSql.ISession>().FlushAsync();

            return (missing, (await service.GetBlockersAsync(profile)).Select(blocker => blocker.Code).ToList());
        });

        Assert.Equal(["NoPaySchedule", "NoTaxId", "NoElection", "NoDistribution", "NoPayRate"], before);
        Assert.Empty(after);
    }

    [Fact]
    public async Task Profile_IsOnePerStaffMemberAndEmployer_AndTheSplitMustEndWithWhatIsLeft()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, fabrikam) = await AddEmployersAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");

        var (duplicate, otherEmployer, badSplit, goodSplit) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();

            await manager.CreateAsync(await NewProfileAsync(manager, lee.ItemId, contoso.ItemId));
            await services.GetRequiredService<global::YesSql.ISession>().FlushAsync();

            var again = await NewProfileAsync(manager, lee.ItemId, contoso.ItemId);
            var other = await NewProfileAsync(manager, lee.ItemId, fabrikam.ItemId);
            var bad = await NewProfileAsync(manager, lee.ItemId, fabrikam.ItemId);
            var good = await NewProfileAsync(manager, lee.ItemId, fabrikam.ItemId);

            bad.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }, new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Percent, Percent = 60 }];
            good.Distribution = [new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = "acct", Split = NetPaySplit.Percent, Percent = 60 }, new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];

            return (
                (await manager.ValidateAsync(again, TestContext.Current.CancellationToken)).Succeeded,
                (await manager.ValidateAsync(other)).Succeeded,
                (await manager.ValidateAsync(bad)).Succeeded,
                (await manager.ValidateAsync(good)).Succeeded);
        }, commit: false);

        Assert.False(duplicate);
        Assert.True(otherEmployer);
        Assert.False(badSplit);
        Assert.True(goodSplit);
    }

    [Fact]
    public async Task PayItems_NeedAnAmountOrAPercent_CopyTheProfile_AndKeepWhatTheyUse()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, _) = await AddEmployersAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var profile = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();
            var created = await NewProfileAsync(manager, lee.ItemId, contoso.ItemId);

            await manager.CreateAsync(created);

            return created;
        });

        var (both, neither, valid, item) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayItemManager>();
            var code = await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG");

            async Task<(bool Valid, EmployeePayItem Item)> TryAsync(decimal? amount, decimal? percent)
            {
                var candidate = await manager.NewAsync();

                candidate.ProfileId = profile.ItemId;
                candidate.PayCodeId = code.ItemId;
                candidate.Amount = amount;
                candidate.Percent = percent;
                candidate.StartDate = new DateOnly(2026, 1, 1);

                return ((await manager.ValidateAsync(candidate)).Succeeded, candidate);
            }

            var good = await TryAsync(50, null);

            await manager.CreateAsync(good.Item);

            return ((await TryAsync(50, 5)).Valid, (await TryAsync(null, null)).Valid, good.Valid, good.Item);
        });

        var (codeUses, employerUses, profileDelete) = await harness.RunAsync(async services =>
        {
            var usage = services.GetRequiredService<PayProfileUsage>();
            var code = await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG");
            var failure = await Record.ExceptionAsync(async () => await services.GetRequiredService<IPayProfileManager>().DeleteAsync(profile, TestContext.Current.CancellationToken));

            return (await usage.GetUsagesAsync(code), await usage.GetUsagesAsync(contoso), failure);
        }, commit: false);

        Assert.False(both);
        Assert.False(neither);
        Assert.True(valid);
        Assert.Equal((lee.ItemId, contoso.ItemId), (item.StaffMemberId, item.EmployerId));
        Assert.Single(codeUses);
        Assert.Single(employerUses);
        Assert.IsType<InvalidOperationException>(profileDelete);
    }

    [Fact]
    public void PayItem_StopsAfterItsLastDay_OrOnceItsGoalIsPaid()
    {
        var loan = new EmployeePayItem { StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 12, 31), Goal = 500, PaidTowardGoal = 200 };

        Assert.True(loan.AppliesOn(new DateOnly(2026, 6, 1)));
        Assert.False(loan.AppliesOn(new DateOnly(2027, 1, 1)));
        Assert.False(loan.AppliesOn(new DateOnly(2025, 12, 31)));

        loan.PaidTowardGoal = 500;

        Assert.False(loan.AppliesOn(new DateOnly(2026, 6, 1)));
    }

    [Fact]
    public async Task NobodyChangesTheirOwnPay_UnlessTheSettingsAllowIt()
    {
        await using var harness = await CreateHarnessAsync();
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", worker => worker.UserId = "lee-user");
        var kim = await WorkforceTests.AddWorkerAsync(harness, "Kim Diaz");
        var leeUser = CalendarTestHarness.User("lee-user", PayrollPermissions.ManagePayProfiles.Name);

        var (own, other, anonymous) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayProfileService>();

            return (
                await service.MayChangePayOfAsync(leeUser, lee.ItemId),
                await service.MayChangePayOfAsync(leeUser, kim.ItemId),
                await service.MayChangePayOfAsync(new ClaimsPrincipal(new ClaimsIdentity()), kim.ItemId));
        }, commit: false);

        Assert.False(own);
        Assert.True(other);
        Assert.False(anonymous);
    }

    [Fact]
    public async Task EmployeesDataSet_AReaderOfOneEmployer_ReadsOnlyItsProfiles_WithTheTaxIdsLastCharacters()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, fabrikam) = await AddEmployersAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var kim = await WorkforceTests.AddWorkerAsync(harness, "Kim Diaz");

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();
            var service = services.GetRequiredService<IPayProfileService>();
            var first = await NewProfileAsync(manager, lee.ItemId, contoso.ItemId);
            var second = await NewProfileAsync(manager, kim.ItemId, fabrikam.ItemId);

            await service.SetTaxIdAsync(first, "123456789");
            await manager.CreateAsync(first);
            await manager.CreateAsync(second);
        });

        var clerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ViewPayrollReports, contoso.ItemId).Name);
        var nobody = CalendarTestHarness.User("nobody", PayrollPermissions.PreparePayRuns.Name);

        var clerkRows = await ReadAsync(harness, clerk);
        var nobodyRows = await ReadAsync(harness, nobody);

        Assert.Equal(["Lee Park|6789"], clerkRows);
        Assert.Empty(nobodyRows);
    }

    [Fact]
    public async Task Deployment_WritesNothingWithoutAUser_LeavesTaxIdsOut_AndWritesThemOnlyWhenAskedByOneWhoMaySeeThem()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, _) = await AddEmployersAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();
            var profile = await NewProfileAsync(manager, lee.ItemId, contoso.ItemId);

            await services.GetRequiredService<IPayProfileService>().SetTaxIdAsync(profile, "123456789");
            await manager.CreateAsync(profile);
        });

        var exporter = CalendarTestHarness.User("exporter", PayrollPermissions.ExportPayrollData.Name);
        var revealer = CalendarTestHarness.User("revealer", PayrollPermissions.ExportPayrollData.Name, PayrollPermissions.ViewTaxIds.Name);

        var none = await ExportAsync(harness, null, includeSensitive: true);
        var structure = await ExportAsync(harness, exporter, includeSensitive: true);
        var sensitive = await ExportAsync(harness, revealer, includeSensitive: true);

        Assert.Empty(none);
        Assert.Null(structure.Single(step => step["name"]!.GetValue<string>() == PayrollConstants.PayProfilesRecipeStepName)["Profiles"]![0]!["TaxId"]);
        Assert.Equal("123456789", sensitive.Single(step => step["name"]!.GetValue<string>() == PayrollConstants.PayProfilesRecipeStepName)["Profiles"]![0]!["TaxId"]!.GetValue<string>());
        Assert.DoesNotContain("TaxIdHash", sensitive[0].ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Recipe_ImportsAProfile_ByEmployeeNumberAndEmployerName_AndProtectsTheTaxId()
    {
        await using var harness = await CreateHarnessAsync();

        await AddEmployersAsync(harness);
        await AddScheduleAsync(harness);

        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", worker => worker.EmployeeNumber = "1001");
        var step = JsonNode.Parse("""
            {
              "name": "PayrollPayProfiles",
              "Profiles": [
                {
                  "EmployeeNumber": "1001",
                  "EmployerName": "Contoso",
                  "PayScheduleName": "Every two weeks",
                  "WorkerType": "Employee",
                  "TaxId": "987-65-4321",
                  "Elections": [ { "Jurisdiction": "us", "EffectiveFrom": "2026-01-01", "Values": { "FilingStatus": "Single" } } ],
                  "Distribution": [ { "Method": "Check", "Split": "Remainder" } ]
                }
              ]
            }
            """)!.AsObject();

        var errors = await harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = PayrollConstants.PayProfilesRecipeStepName, Step = step };

            await ActivatorUtilities.CreateInstance<PayProfilesRecipeStep>(services).ExecuteAsync(context);

            return context.Errors.ToList();
        });

        var profile = await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<IPayProfileManager>().ListByStaffMemberAsync(lee.ItemId)), commit: false);

        Assert.Empty(errors);
        Assert.Equal("4321", profile.TaxId.Hint);
        Assert.NotNull(profile.PayScheduleId);
        Assert.Equal("US", Assert.Single(profile.Elections).Jurisdiction);

        // An election written by hand has no entry time: the import stamps one, so the editor can show it.
        Assert.NotEqual(default, profile.Elections[0].EnteredUtc);
        Assert.Equal("Recipe", profile.Elections[0].EnteredBy);
    }

    [Fact]
    public async Task Recipe_BringsBackEveryFieldOfAProfileItsDeploymentWrote()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, _) = await AddEmployersAsync(harness);
        var schedule = await AddScheduleAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var ignored = new HashSet<string>(StringComparer.Ordinal) { nameof(EmployeePayProfile.CreatedUtc), nameof(EmployeePayProfile.ModifiedUtc), nameof(EmployeePayProfile.TaxId), nameof(EmployeePayProfile.TaxIdHash), "Properties" };

        var original = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();
            var profile = await NewProfileAsync(manager, lee.ItemId, contoso.ItemId);

            profile.PayScheduleId = schedule.ItemId;
            profile.WorkerType = PayWorkerType.Contractor;
            profile.OvertimeStatus = OvertimeStatus.Exempt;
            profile.Tipped = true;
            profile.BackupWithholding = true;
            profile.LegalName = "Lee A. Park";
            profile.HomeAddress = new CrestApps.OrchardCore.Addresses.Models.Address { AddressLine1 = "1 Main St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "US" };
            profile.WorkSiteTeamId = "site1";
            profile.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2026, 1, 1), Values = new() { ["FilingStatus"] = "Single" }, EnteredBy = "admin", EnteredUtc = new DateTime(2026, 1, 2, 15, 0, 0, DateTimeKind.Utc) }];
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];
            profile.DefaultAllocation = new PayInputAllocation { DepartmentTeamId = "dept1", ActivityCode = "A1", Job = "J1" };
            profile.RetroPayFrom = new DateOnly(2026, 3, 1);
            profile.Status = PayProfileStatus.OnLeave;
            await services.GetRequiredService<IPayProfileService>().SetTaxIdAsync(profile, "123456789");
            await manager.CreateAsync(profile);

            return profile;
        });

        // Every field the test covers is set to something other than a new profile's.
        var blank = new EmployeePayProfile();
        var properties = typeof(EmployeePayProfile).GetProperties().Where(property => property.CanRead && property.CanWrite && !ignored.Contains(property.Name)).ToList();

        string Json(object value)
            => System.Text.Json.JsonSerializer.Serialize(value);

        Assert.All(properties.Where(property => property.Name != nameof(EmployeePayProfile.ItemId)), property => Assert.NotEqual(Json(property.GetValue(blank)), Json(property.GetValue(original))));

        var revealer = CalendarTestHarness.User("revealer", PayrollPermissions.ExportPayrollData.Name, PayrollPermissions.ViewTaxIds.Name);
        var step = (await ExportAsync(harness, revealer, includeSensitive: true)).Single(candidate => candidate["name"]!.GetValue<string>() == PayrollConstants.PayProfilesRecipeStepName);

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();

            await manager.DeleteAsync(await manager.FindByIdAsync(original.ItemId));
        });

        var errors = await harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = PayrollConstants.PayProfilesRecipeStepName, Step = step };

            await ActivatorUtilities.CreateInstance<PayProfilesRecipeStep>(services).ExecuteAsync(context);

            return context.Errors.ToList();
        });

        var imported = await harness.RunAsync(async services => await services.GetRequiredService<IPayProfileManager>().FindByIdAsync(original.ItemId), commit: false);

        Assert.Empty(errors);
        Assert.All(properties, property => Assert.Equal(Json(property.GetValue(original)), Json(property.GetValue(imported))));
        Assert.Equal((original.TaxId.Hint, original.TaxIdHash), (imported.TaxId.Hint, imported.TaxIdHash));
    }

    [Fact]
    public void Editor_ReadsTheSplit_AndElectionValues_WithADotForDecimals()
    {
        var driver = (PayProfileDisplayDriver)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(PayProfileDisplayDriver));

        typeof(PayProfileDisplayDriver).GetField("S", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(driver, RecipeRoundTrip.Localizer<PayProfileDisplayDriver>());

        var rows = driver.ReadDistribution(
        [
            new NetPayRowViewModel { Method = "BankAccount", BankAccountId = "acct", Split = "Amount", Value = "250.5" },
            new NetPayRowViewModel { Method = "Check", BankAccountId = "ignored", Split = "Remainder" },
            new NetPayRowViewModel(),
        ], null);
        var values = driver.ReadElectionValues(GenericElectionFields.Create().Fields, new Dictionary<string, string> { ["FilingStatus"] = " Single ", ["Allowances"] = "2", ["Exempt"] = "true" }, null);

        Assert.Equal(2, rows.Count);
        Assert.Equal(250.5m, rows[0].Amount);
        Assert.Null(rows[1].BankAccountId);
        Assert.Equal("Single", values["FilingStatus"]);
        Assert.Equal("true", values["Exempt"]);
        Assert.False(values.ContainsKey("ExtraWithholding"));
    }

    [Fact]
    public async Task ProfilePages_AClerkOfOneEmployer_SeesOnlyItsPeople_RevealsNoTaxIdWithoutLeave_AndNobodyChangesTheirOwnPay()
    {
        await using var harness = await CreateHarnessAsync();
        var (contoso, fabrikam) = await AddEmployersAsync(harness);
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", worker => worker.UserId = "lee-user");
        var kim = await WorkforceTests.AddWorkerAsync(harness, "Kim Diaz");
        var (leeProfile, kimProfile) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayProfileManager>();
            var first = await NewProfileAsync(manager, lee.ItemId, contoso.ItemId);
            var second = await NewProfileAsync(manager, kim.ItemId, fabrikam.ItemId);

            await services.GetRequiredService<IPayProfileService>().SetTaxIdAsync(first, "123456789");
            await manager.CreateAsync(first);
            await manager.CreateAsync(second);

            return (first, second);
        });

        var clerk = CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.ManagePayProfiles, contoso.ItemId).Name);
        var revealer = CalendarTestHarness.User("revealer", PayrollPermissionTemplates.For(PayrollPermissions.ViewTaxIds, contoso.ItemId).Name);
        var self = CalendarTestHarness.User("lee-user", PayrollPermissions.ManagePayProfiles.Name);

        var (list, other, own, hidden, shown, shownHeaders, selfDelete) = await harness.RunAsync(async services =>
        {
            var revealing = Controller(services, revealer);

            return (
                await Controller(services, clerk).Index(null),
                await Controller(services, clerk).Edit(kimProfile.ItemId),
                await Controller(services, clerk).Edit(leeProfile.ItemId),
                await Controller(services, clerk).TaxId(leeProfile.ItemId),
                await revealing.TaxId(leeProfile.ItemId),
                revealing.Response.Headers.CacheControl.ToString(),
                await Controller(services, self).Delete(leeProfile.ItemId));
        }, commit: false);

        var listed = Assert.IsType<PayProfilesIndexViewModel>(Assert.IsType<ViewResult>(list).Model);

        Assert.Equal([leeProfile.ItemId], listed.List.Source.Cast<EmployeePayProfile>().Select(profile => profile.ItemId));
        Assert.True(listed.CanAdd);
        Assert.IsType<ForbidResult>(other);
        Assert.IsType<ViewResult>(own);
        Assert.IsType<ForbidResult>(hidden);
        Assert.Equal("123456789", Assert.IsType<PayProfileTaxIdViewModel>(Assert.IsType<ViewResult>(shown).Model).TaxId);
        Assert.Equal("no-store", shownHeaders);
        Assert.IsType<ForbidResult>(selfDelete);
    }

    [Fact]
    public async Task AuditTrail_RecordsWhoWasShownATaxId_WithItsLastCharactersOnly()
    {
        var recorded = new List<global::OrchardCore.AuditTrail.Services.Models.AuditTrailContext<CrestApps.OrchardCore.Payroll.AuditTrail.PayrollAuditData>>();
        var manager = new Mock<global::OrchardCore.AuditTrail.Services.IAuditTrailManager>();

        manager
            .Setup(audit => audit.RecordEventAsync(It.IsAny<global::OrchardCore.AuditTrail.Services.Models.AuditTrailContext<CrestApps.OrchardCore.Payroll.AuditTrail.PayrollAuditData>>()))
            .Callback<global::OrchardCore.AuditTrail.Services.Models.AuditTrailContext<CrestApps.OrchardCore.Payroll.AuditTrail.PayrollAuditData>>(recorded.Add)
            .Returns(Task.CompletedTask);

        var observer = new CrestApps.OrchardCore.Payroll.AuditTrail.AuditTrailPayProfileObserver(manager.Object);
        var profile = new EmployeePayProfile { ItemId = "profile1", StaffMemberId = "lee", EmployerId = "contoso", TaxId = new() { Cipher = "secret", Hint = "6789" } };

        await observer.ChangedAsync(new PayProfileChange { Profile = profile, Kind = PayProfileChangeKind.TaxIdRevealed, Actor = "clerk", ActorId = "clerk-id", Reason = "Shown on the pay profile page" }, TestContext.Current.CancellationToken);

        var context = Assert.Single(recorded);

        Assert.Equal(("PayrollTaxIdRevealed", "Payroll", "profile1", "clerk-id", "clerk"), (context.Name, context.Category, context.CorrelationId, context.UserId, context.UserName));
        Assert.Equal(("lee", "contoso", "6789"), (context.AuditTrailEventItem.StaffMemberId, context.AuditTrailEventItem.EmployerId, context.AuditTrailEventItem.TaxIdHint));
    }

    private static Task<CalendarTestHarness> CreateHarnessAsync()
        => WorkforceTests.CreateAsync(
            services =>
            {
                services.AddDataProtection();
                services.AddPayrollCore();
                services.AddPayrollHandlers();
                services.AddScoped<PayrollNames>();
                services.AddSingleton<global::OrchardCore.Modules.ILocalClock>(new FixedOffsetLocalClock(() => DateTime.UtcNow, TimeSpan.FromHours(-5)));
            },
            PayrollIndexSchema.CreateAsync,
            PayrollIndexSchema.Providers());

    private static async Task<(Employer Contoso, Employer Fabrikam)> AddEmployersAsync(CalendarTestHarness harness)
    {
        async Task<Employer> AddAsync(string name)
            => await harness.RunAsync(async services =>
            {
                var manager = services.GetRequiredService<IEmployerManager>();
                var employer = await manager.NewAsync();

                employer.Name = name;
                employer.Currency = "USD";
                employer.Registrations = [new EmployerTaxRegistration { Jurisdiction = "US" }];
                await manager.CreateAsync(employer);

                return employer;
            });

        await harness.RunAsync(services => services.GetRequiredService<PayCodeSeeder>().SeedAsync());

        return (await AddAsync("Contoso"), await AddAsync("Fabrikam"));
    }

    private static Task<PaySchedule> AddScheduleAsync(CalendarTestHarness harness)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IPayScheduleManager>();
            var schedule = await manager.NewAsync();

            schedule.Name = "Every two weeks";
            await manager.CreateAsync(schedule);

            return schedule;
        });

    private static async Task<EmployeePayProfile> NewProfileAsync(IPayProfileManager manager, string staffMemberId, string employerId)
    {
        var profile = await manager.NewAsync();

        profile.StaffMemberId = staffMemberId;
        profile.EmployerId = employerId;

        return profile;
    }

    private static PayProfilesController Controller(IServiceProvider services, ClaimsPrincipal user)
    {
        var displayManager = new Moq.Mock<global::OrchardCore.DisplayManagement.IDisplayManager<EmployeePayProfile>>();

        displayManager
            .Setup(manager => manager.BuildEditorAsync(Moq.It.IsAny<EmployeePayProfile>(), Moq.It.IsAny<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModel>(), Moq.It.IsAny<bool>(), Moq.It.IsAny<string>(), Moq.It.IsAny<string>()))
            .ReturnsAsync(Moq.Mock.Of<global::OrchardCore.DisplayManagement.IShape>());

        var shapeFactory = new Moq.Mock<global::OrchardCore.DisplayManagement.IShapeFactory>();

        shapeFactory
            .Setup(factory => factory.CreateAsync(Moq.It.IsAny<string>(), Moq.It.IsAny<Func<ValueTask<global::OrchardCore.DisplayManagement.IShape>>>(), Moq.It.IsAny<Action<global::OrchardCore.DisplayManagement.Implementation.ShapeCreatingContext>>(), Moq.It.IsAny<Action<global::OrchardCore.DisplayManagement.Implementation.ShapeCreatedContext>>()))
            .ReturnsAsync(Moq.Mock.Of<global::OrchardCore.DisplayManagement.IShape>());

        return new PayProfilesController(
            services.GetRequiredService<IPayProfileManager>(),
            services.GetRequiredService<IPayProfileService>(),
            services.GetRequiredService<DefaultPayProfileService>(),
            services.GetRequiredService<IPayrollScopeService>(),
            services.GetRequiredService<EmployerStore>(),
            services.GetRequiredService<PayrollNames>(),
            new PayProfileEditTracker(),
            Moq.Mock.Of<global::OrchardCore.DisplayManagement.ModelBinding.IUpdateModelAccessor>(),
            displayManager.Object,
            Moq.Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            shapeFactory.Object,
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<PayProfilesController>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayProfilesController>())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                    RequestServices = new ServiceCollection()
                        .AddSingleton(Moq.Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>())
                        .AddSingleton<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider, Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider>()
                        .BuildServiceProvider(),
                },
            },
        };
    }

    private static Task<string[]> ReadAsync(CalendarTestHarness harness, ClaimsPrincipal user)
        => harness.RunAsync(async services =>
        {
            var source = new PayrollDataSource([ActivatorUtilities.CreateInstance<EmployeesDataSet>(services)]);
            var table = await source.ReadRowsAsync(new DataSourceQuery
            {
                DataSet = PayrollConstants.EmployeesDataSetName,
                Fields = new HashSet<string>(StringComparer.Ordinal),
                MaxRows = 100,
                Context = new DataSourceContext { User = user },
            }, 100, TestContext.Current.CancellationToken);
            var fields = table.Fields.Select(field => field.Name).ToList();

            return table.Rows.Select(row => $"{row[fields.IndexOf("StaffMemberName")]}|{row[fields.IndexOf("TaxIdLastFour")]}").ToArray();
        }, commit: false);

    private static Task<List<JsonObject>> ExportAsync(CalendarTestHarness harness, ClaimsPrincipal user, bool includeSensitive)
        => harness.RunAsync(async services =>
        {
            var accessor = new HttpContextAccessor { HttpContext = user is null ? null : new DefaultHttpContext { User = user } };
            var source = ActivatorUtilities.CreateInstance<PayProfilesDeploymentSource>(services, accessor, new Moq.Mock<global::OrchardCore.DisplayManagement.Notify.INotifier>().Object, new EchoHtmlLocalizer<PayProfilesDeploymentSource>());
            var result = new DeploymentPlanResult(null, new RecipeDescriptor());

            await source.ProcessDeploymentStepAsync(new PayProfilesDeploymentStep { IncludeSensitiveData = includeSensitive }, result);

            return result.Steps.ToList();
        }, commit: false);

    private sealed class RecordingObserver : IPayProfileObserver
    {
        public List<PayProfileChange> Changes { get; } = [];

        public Task ChangedAsync(PayProfileChange change, CancellationToken cancellationToken = default)
        {
            Changes.Add(change);

            return Task.CompletedTask;
        }
    }
}
