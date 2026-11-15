using System.Security.Claims;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Controllers;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Drivers;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using CrestApps.OrchardCore.Payroll.ViewModels;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Disbursements;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Payroll;

/// <summary>
/// My Pay: a person sees only their own statements and stubs, changes only their own elections and net pay split, and
/// is told when those change.
/// </summary>
public sealed class MyPayTests
{
    [Fact]
    public async Task MyPay_ShowsOnlyThePersonsOwnStatements()
    {
        var context = new DisbursementsTestContext();
        var origin = await context.Checks.BusinessAccountAsync();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => PayRunProcessingTests.AddPayments(services, context, deposits: false, checks: true));
        var setup = await PayRunTests.SetUpAsync(harness, employerSetup: employer => employer.FundingBankAccountId = origin.ItemId);
        var sam = await AddSecondPersonAsync(harness, setup);
        await LinkAsync(harness, setup.StaffMemberId, "lee-user");
        await LinkAsync(harness, sam, "sam-user");

        var lee = CalendarTestHarness.User("lee-user", PayrollPermissions.ViewOwnPay.Name);
        var stranger = CalendarTestHarness.User("visitor");
        var notStaff = CalendarTestHarness.User("nobody-user", PayrollPermissions.ViewOwnPay.Name);

        // Drafts are not shown: before processing, nothing.
        await PayRunProcessingTests.ApproveAsync(harness, setup);
        var (draftIndex, draftId) = await harness.RunAsync(async services =>
        {
            var index = Assert.IsType<ViewResult>(await Controller(services, lee).Index());
            var statements = await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId));

            return ((MyPayIndexViewModel)index.Model, statements.Single(statement => statement.StaffMemberId == setup.StaffMemberId).ItemId);
        }, commit: false);
        var draftPage = await harness.RunAsync(async services => await Controller(services, lee).Statement(draftId), commit: false);

        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().ProcessAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), CalendarTestHarness.User("processor", PayrollPermissions.ProcessPayRuns.Name))).Succeeded));

        var (index, own, others, ownPdf, othersPdf, strangerIndex, notStaffIndex) = await harness.RunAsync(async services =>
        {
            var statements = await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId));
            var leeStatement = statements.Single(statement => statement.StaffMemberId == setup.StaffMemberId);
            var samStatement = statements.Single(statement => statement.StaffMemberId == sam);
            var stubs = new PayStubService(services.GetRequiredService<IPayCodeManager>(), services.GetRequiredService<IPayAccumulatorService>(), new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayStubService>());

            return (
                (MyPayIndexViewModel)Assert.IsType<ViewResult>(await Controller(services, lee).Index()).Model,
                await Controller(services, lee).Statement(leeStatement.ItemId),
                await Controller(services, lee).Statement(samStatement.ItemId),
                await Controller(services, lee).StatementPdf(leeStatement.ItemId, stubs),
                await Controller(services, lee).StatementPdf(samStatement.ItemId, stubs),
                await Controller(services, stranger).Index(),
                (MyPayIndexViewModel)Assert.IsType<ViewResult>(await Controller(services, notStaff).Index()).Model);
        }, commit: false);

        Assert.True(draftIndex.IsStaff);
        Assert.Empty(Rows(draftIndex));
        Assert.IsType<NotFoundResult>(draftPage);

        var row = Assert.Single(Rows(index));
        Assert.Equal(setup.StaffMemberId, row.Statement.StaffMemberId);
        Assert.Equal("Contoso", row.EmployerName);
        Assert.Equal(setup.StaffMemberId, Assert.IsType<PayStatementViewModel>(Assert.IsType<ViewResult>(own).Model).Statement.StaffMemberId);
        Assert.IsType<NotFoundResult>(others);
        Assert.Equal("application/pdf", Assert.IsType<FileContentResult>(ownPdf).ContentType);
        Assert.IsType<NotFoundResult>(othersPdf);
        Assert.IsType<ForbidResult>(strangerIndex);
        Assert.False(notStaffIndex.IsStaff);
        Assert.Single(index.Profiles);
    }

    [Fact]
    public async Task PayDetails_AreTheirOwnOnly_NeedTheirPermission_AndTellTheObservers()
    {
        var observer = new RecordingProfileObserver();
        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddSingleton<IPayProfileObserver>(observer));
        var setup = await PayRunTests.SetUpAsync(harness);
        var sam = await AddSecondPersonAsync(harness, setup);
        await LinkAsync(harness, setup.StaffMemberId, "lee-user");

        var lee = CalendarTestHarness.User("lee-user", PayrollPermissions.ViewOwnPay.Name, PayrollPermissions.ManageOwnPayDetails.Name);
        var viewer = CalendarTestHarness.User("lee-user", PayrollPermissions.ViewOwnPay.Name);
        var (leeProfile, samProfile) = await harness.RunAsync(async services =>
        {
            var profiles = services.GetRequiredService<IPayProfileManager>();

            return ((await profiles.ListByStaffMemberAsync(setup.StaffMemberId)).Single().ItemId, (await profiles.ListByStaffMemberAsync(sam)).Single().ItemId);
        }, commit: false);

        var (others, viewOnly, othersPost) = await harness.RunAsync(async services => (
            await Controller(services, lee).Details(samProfile),
            await Controller(services, viewer).Details(leeProfile),
            await Controller(services, lee).DetailsPost(samProfile)), commit: false);

        // The editor (a stand-in for the profile driver's My Pay group) moves net pay to a check of a fixed amount.
        var saved = await harness.RunAsync(async services => await Controller(services, lee, (profile, tracker) =>
        {
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder, Amount = null }];
            tracker.Changes.Add(PayProfileChangeKind.DistributionChanged);
        }).DetailsPost(leeProfile));
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<IPayProfileManager>().FindByIdAsync(leeProfile), commit: false);

        Assert.IsType<NotFoundResult>(others);
        Assert.IsType<ForbidResult>(viewOnly);
        Assert.IsType<NotFoundResult>(othersPost);
        Assert.IsType<RedirectToActionResult>(saved);
        Assert.Equal(NetPayMethod.Check, Assert.Single(stored.Distribution).Method);
        var change = Assert.Single(observer.Changes, entry => entry.Kind == PayProfileChangeKind.DistributionChanged);
        Assert.Equal(leeProfile, change.Profile.ItemId);
        Assert.Equal("Changed in My Pay", change.Reason);
    }

    [Fact]
    public async Task ProfileEditor_InMyPay_ReadsOnlyTheElectionsAndTheNetPaySplit()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var setup = await PayRunTests.SetUpAsync(harness);

        var (profile, tracker) = await harness.RunAsync(async services =>
        {
            var found = (await services.GetRequiredService<IPayProfileManager>().ListByStaffMemberAsync(setup.StaffMemberId)).Single();
            var editTracker = new PayProfileEditTracker();
            var driver = Driver(services, editTracker);
            var updater = new MultiModelUpdater(
                new PayProfileViewModel { EmployerId = "someone-else", PayScheduleId = "another" },
                new PayProfileElectionsViewModel { NewJurisdiction = "US", NewEffectiveFrom = new DateOnly(2026, 12, 1) },
                new PayProfileDistributionViewModel { Rows = [new NetPayRowViewModel { Method = "Check", Split = "Remainder" }] });

            await driver.UpdateAsync(found, new UpdateEditorContext(null, PayrollConstants.MyPayEditorGroup, false, string.Empty, null, null, updater));

            return (found, editTracker);
        }, commit: false);

        Assert.Equal(setup.EmployerId, profile.EmployerId);
        Assert.Contains(profile.Elections, election => election.EffectiveFrom == new DateOnly(2026, 12, 1));
        Assert.Equal(NetPayMethod.Check, Assert.Single(profile.Distribution).Method);
        Assert.Contains(PayProfileChangeKind.ElectionAdded, tracker.Changes);
    }

    [Fact]
    public async Task SecurityNotice_TellsThePersonWhereTheirPayGoesChanged_WithoutAnAccountNumber()
    {
        var sent = new List<(object To, global::OrchardCore.Notifications.INotificationMessage Message)>();
        var notifications = new Mock<global::OrchardCore.Notifications.INotificationService>();
        var lee = new global::OrchardCore.Users.Models.User { UserId = "lee-user", UserName = "lee" };
        var users = new Mock<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>(Mock.Of<Microsoft.AspNetCore.Identity.IUserStore<global::OrchardCore.Users.IUser>>(), null, null, null, null, null, null, null, null);

        notifications
            .Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<global::OrchardCore.Notifications.INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<object, global::OrchardCore.Notifications.INotificationMessage, CancellationToken>((to, message, _) => sent.Add((to, message)))
            .ReturnsAsync(new global::OrchardCore.Notifications.NotificationSendResult());
        users.Setup(manager => manager.FindByIdAsync("lee-user")).ReturnsAsync(lee);

        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddSingleton(notifications.Object);
            services.AddSingleton(users.Object);
            services.AddScoped<IPayProfileObserver, PayDetailsChangedNotificationObserver>();
        });
        var setup = await PayRunTests.SetUpAsync(harness, profileSetup: profile =>
        {
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.BankAccount, BankAccountId = "account-123456789", Split = NetPaySplit.Remainder }];

            return Task.CompletedTask;
        });
        await LinkAsync(harness, setup.StaffMemberId, "lee-user");

        await harness.RunAsync(async services =>
        {
            var profile = (await services.GetRequiredService<IPayProfileManager>().ListByStaffMemberAsync(setup.StaffMemberId)).Single();
            var recorder = services.GetRequiredService<DefaultPayProfileService>();

            await recorder.TellAsync(profile, PayProfileChangeKind.DistributionChanged, "Changed on the pay profile page");
            await recorder.TellAsync(profile, PayProfileChangeKind.Changed, "Saved on the pay profile page");
        });

        var (to, message) = Assert.Single(sent);
        Assert.Same(lee, to);
        Assert.Equal(PayrollConstants.PayDetailsChangedNotificationType, message.Type);
        Assert.Equal("~/Admin/my-pay", message.Url);
        Assert.DoesNotContain("123456789", message.Subject + message.Summary + message.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PayrollMenu_ShowsAPersonWithOnlyTheirOwnPay_NothingOfTheirEmployers()
    {
        await using var harness = await PayRunTests.CreateHarnessAsync();
        var setup = await PayRunTests.SetUpAsync(harness);

        var (employee, clerk) = await harness.RunAsync(async services => (
            await MenuIdsAsync(services, CalendarTestHarness.User("lee-user", PayrollPermissions.ViewOwnPay.Name, PayrollPermissions.ManageOwnPayDetails.Name)),
            await MenuIdsAsync(services, CalendarTestHarness.User("clerk", PayrollPermissionTemplates.For(PayrollPermissions.PreparePayRuns, setup.EmployerId).Name))), commit: false);

        Assert.DoesNotContain("payroll", employee);
        Assert.DoesNotContain("payrollPayRuns", employee);
        Assert.Contains("payrollPayRuns", clerk);
        Assert.DoesNotContain("payrollEmployees", clerk);
        Assert.DoesNotContain("payrollEmployers", clerk);
    }

    private static async Task<List<string>> MenuIdsAsync(IServiceProvider services, ClaimsPrincipal user)
    {
        var menu = new CrestApps.OrchardCore.Payroll.Navigation.PayrollAdminMenu(
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } },
            services.GetRequiredService<IPayrollScopeService>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.Payroll.Navigation.PayrollAdminMenu>());
        var builder = new global::OrchardCore.Navigation.NavigationBuilder();

        await menu.BuildNavigationAsync(global::OrchardCore.Navigation.NavigationConstants.AdminId, builder);

        static IEnumerable<global::OrchardCore.Navigation.MenuItem> Flatten(IEnumerable<global::OrchardCore.Navigation.MenuItem> items)
            => items.SelectMany(item => new[] { item }.Concat(Flatten(item.Items)));

        return [.. Flatten(builder.Build()).Select(item => item.Id).Where(id => !string.IsNullOrEmpty(id))];
    }

    [Fact]
    public async Task Review_ShowsTheComingPay_TakesAnAnswer_TellsThePreparer_AndGoesOutOfDateWhenPayChanges()
    {
        var sent = new List<(string To, global::OrchardCore.Notifications.INotificationMessage Message)>();
        var notifications = new Mock<global::OrchardCore.Notifications.INotificationService>();
        var users = new Mock<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>(Mock.Of<Microsoft.AspNetCore.Identity.IUserStore<global::OrchardCore.Users.IUser>>(), null, null, null, null, null, null, null, null);
        var site = new global::OrchardCore.Settings.SiteSettings { TimeZoneId = "America/New_York" };
        var siteService = new Mock<global::OrchardCore.Settings.ISiteService>();

        global::OrchardCore.Entities.EntityExtensions.Put(site, new PayrollSettings { EmployeeReview = true });
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);
        notifications
            .Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<global::OrchardCore.Notifications.INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback<object, global::OrchardCore.Notifications.INotificationMessage, CancellationToken>((to, message, _) => sent.Add((((global::OrchardCore.Users.Models.User)to).UserId, message)))
            .ReturnsAsync(new global::OrchardCore.Notifications.NotificationSendResult());
        users.Setup(manager => manager.FindByIdAsync("lee-user")).ReturnsAsync(new global::OrchardCore.Users.Models.User { UserId = "lee-user", UserName = "lee" });
        users.Setup(manager => manager.FindByIdAsync("preparer")).ReturnsAsync(new global::OrchardCore.Users.Models.User { UserId = "preparer", UserName = "preparer" });

        await using var harness = await PayRunTests.CreateHarnessAsync(services =>
        {
            services.AddSingleton(notifications.Object);
            services.AddSingleton(users.Object);
            services.AddSingleton(siteService.Object);
            services.AddScoped<IPayRunObserver, PayReviewNotificationObserver>();
        });
        var setup = await PayRunTests.SetUpAsync(harness);
        await LinkAsync(harness, setup.StaffMemberId, "lee-user");

        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);
        var lee = CalendarTestHarness.User("lee-user", PayrollPermissions.ViewOwnPay.Name);
        var sam = CalendarTestHarness.User("sam-user", PayrollPermissions.ViewOwnPay.Name);

        async Task CalculateAsync()
            => await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().CalculateAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), preparer)).Succeeded));

        Task<PayStatement> DraftAsync()
            => harness.RunAsync(async services => (await services.GetRequiredService<IPayRunService>().ListStatementsAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId))).Single(), commit: false);

        await CalculateAsync();
        var draft = await DraftAsync();

        // Calculated: Lee is asked once, and sees the coming pay in My Pay; someone else can't open it.
        var asked = Assert.Single(sent);
        Assert.Equal(("lee-user", PayrollConstants.PayReviewRequestedNotificationType, MyPayUrls.Review(draft.ItemId)), (asked.To, asked.Message.Type, asked.Message.Url));

        var (index, page, strangerPage) = await harness.RunAsync(async services =>
        {
            var reviews = services.GetRequiredService<PayStatementReviewService>();

            return (
                (MyPayIndexViewModel)Assert.IsType<ViewResult>(await Controller(services, lee).Index()).Model,
                await Controller(services, lee).Review(draft.ItemId, reviews),
                await Controller(services, sam).Review(draft.ItemId, reviews));
        }, commit: false);

        Assert.Equal(draft.ItemId, Assert.Single(index.ToReview).Statement.ItemId);
        Assert.Equal(draft.Net, ((MyPayReviewViewModel)Assert.IsType<ViewResult>(page).Model).Statement.Statement.Net);
        Assert.IsType<NotFoundResult>(strangerPage);

        // A question needs a note; with one it is kept and the preparer is told.
        var (noNote, asking) = await harness.RunAsync(async services =>
        {
            var reviews = services.GetRequiredService<PayStatementReviewService>();
            var controller = Controller(services, lee);
            var refused = await controller.ReviewPost(draft.ItemId, confirmed: false, note: " ", reviews);

            return (refused is ViewResult && !controller.ModelState.IsValid, await Controller(services, lee).ReviewPost(draft.ItemId, confirmed: false, note: "Two hours are missing on Monday.", reviews));
        });

        Assert.True(noNote);
        Assert.IsType<RedirectToActionResult>(asking);

        var questioned = await DraftAsync();

        Assert.Equal((PayStatementReviewStatus.Questioned, "Two hours are missing on Monday."), (questioned.Review.Status, questioned.Review.Note));
        Assert.True(questioned.Review.IsCurrentFor(questioned.Net));
        Assert.Contains(sent, item => item.To == "preparer" && item.Message.Type == PayrollConstants.PayReviewQuestionedNotificationType && item.Message.ReferenceId == draft.ItemId);

        // Calculated again with the same pay: Lee isn't asked again and the question stays.
        sent.Clear();
        await CalculateAsync();
        Assert.Empty(sent);
        Assert.True((await DraftAsync()).Review.IsCurrentFor(questioned.Net));

        // Two more hours change the net pay: Lee is asked again and the answer is out of date.
        await harness.RunAsync(async services =>
        {
            var inputs = services.GetRequiredService<IPayRunInputManager>();
            var input = await inputs.NewAsync();
            var reg = (await services.GetRequiredService<IPayCodeManager>().GetAllAsync()).Single(code => code.Code == "REG");

            input.RunId = setup.RunId;
            input.StaffMemberId = setup.StaffMemberId;
            input.PayCodeId = reg.ItemId;
            input.Hours = 2;
            await inputs.CreateAsync(input);
        });
        await CalculateAsync();

        var changed = await DraftAsync();

        Assert.NotEqual(questioned.Net, changed.Net);
        Assert.False(changed.Review.IsCurrentFor(changed.Net));
        Assert.Equal(PayrollConstants.PayReviewRequestedNotificationType, Assert.Single(sent).Message.Type);

        // Lee says it is right; once the run is submitted the pay can't be reviewed any more.
        await harness.RunAsync(async services => Assert.IsType<RedirectToActionResult>(await Controller(services, lee).ReviewPost(draft.ItemId, confirmed: true, note: null, services.GetRequiredService<PayStatementReviewService>())));
        Assert.Equal(PayStatementReviewStatus.Confirmed, (await DraftAsync()).Review.Status);
        await harness.RunAsync(async services => Assert.True((await services.GetRequiredService<IPayRunService>().SubmitAsync(await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId), preparer)).Succeeded));

        var (afterSubmit, closed) = await harness.RunAsync(async services =>
        {
            var reviews = services.GetRequiredService<PayStatementReviewService>();

            return ((MyPayIndexViewModel)Assert.IsType<ViewResult>(await Controller(services, lee).Index()).Model, await Controller(services, lee).ReviewPost(draft.ItemId, confirmed: false, note: "Late.", reviews));
        }, commit: false);

        Assert.Empty(afterSubmit.ToReview);
        Assert.IsType<NotFoundResult>(closed);

        // With the setting off nobody is asked and nothing is shown.
        global::OrchardCore.Entities.EntityExtensions.Put(site, new PayrollSettings { EmployeeReview = false });
        Assert.Empty(await harness.RunAsync(services => services.GetRequiredService<PayStatementReviewService>().ListOpenAsync(setup.StaffMemberId), commit: false));
    }

    [Fact]
    public async Task Calculation_StoresTheBalancesOfEverySource_OnTheStatement_AndTheStubPrintsThem()
    {
        var source = new Mock<IPayStatementBalanceSource>();

        source.Setup(value => value.GetBalancesAsync(It.IsAny<string>(), new DateOnly(2026, 11, 20), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PayStatementBalance { Name = "Vacation", Unit = PayStatementBalance.Hours, Balance = 24 }]);

        await using var harness = await PayRunTests.CreateHarnessAsync(services => services.AddSingleton(source.Object));
        var setup = await PayRunTests.SetUpAsync(harness);
        var preparer = CalendarTestHarness.User("preparer", PayrollPermissions.PreparePayRuns.Name);

        var (statement, pdf) = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<IPayRunService>();
            var run = await services.GetRequiredService<IPayRunManager>().FindByIdAsync(setup.RunId);

            Assert.True((await service.CalculateAsync(run, preparer)).Succeeded);

            var draft = (await service.ListStatementsAsync(run)).Single();

            return (draft, PayStubDocument.Write(draft, new Dictionary<string, string>(), null, new PayStubTexts()));
        });

        Assert.Equal(("Vacation", 24m), (Assert.Single(statement.Balances).Name, statement.Balances[0].Balance));
        Assert.NotEmpty(pdf);
    }

    private static IEnumerable<MyPayStatementRow> Rows(MyPayIndexViewModel model)
        => ((global::OrchardCore.Admin.AdminListContext<MyPayStatementRow>)model.Statements).Source;

    private static async Task<string> AddSecondPersonAsync(CalendarTestHarness harness, PayRunTests.Setup setup)
    {
        var sam = await WorkforceTests.AddWorkerAsync(harness, "Sam Ito", worker =>
        {
            worker.EmployeeNumber = "1002";
            worker.Put(new StaffPayRates { Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2020, 1, 1), HourlyRate = 20 }] });
        });

        await harness.RunAsync(async services =>
        {
            var profiles = services.GetRequiredService<IPayProfileManager>();
            var lee = (await profiles.ListByStaffMemberAsync(setup.StaffMemberId)).Single();
            var profile = await profiles.NewAsync();

            profile.StaffMemberId = sam.ItemId;
            profile.EmployerId = setup.EmployerId;
            profile.PayScheduleId = lee.PayScheduleId;
            profile.Distribution = [new NetPayRow { Method = NetPayMethod.Check, Split = NetPaySplit.Remainder }];
            profile.Elections = [new TaxElection { Jurisdiction = "US", EffectiveFrom = new DateOnly(2020, 1, 1) }];
            await services.GetRequiredService<IPayProfileService>().SetTaxIdAsync(profile, "987654321");
            await profiles.CreateAsync(profile);

            var inputs = services.GetRequiredService<IPayRunInputManager>();
            var hours = await inputs.NewAsync();

            hours.RunId = setup.RunId;
            hours.StaffMemberId = sam.ItemId;
            hours.PayCodeId = (await services.GetRequiredService<IPayCodeManager>().FindByCodeAsync("REG")).ItemId;
            hours.Hours = 40;
            await inputs.CreateAsync(hours);
        });

        return sam.ItemId;
    }

    private static Task LinkAsync(CalendarTestHarness harness, string staffMemberId, string userId)
        => harness.RunAsync(async services =>
        {
            var staff = services.GetRequiredService<IStaffMemberManager>();
            var member = await staff.FindByIdAsync(staffMemberId);

            member.UserId = userId;
            await staff.UpdateAsync(member);
        });

    private static MyPayController Controller(IServiceProvider services, ClaimsPrincipal user, Action<EmployeePayProfile, PayProfileEditTracker> edit = null)
    {
        var tracker = new PayProfileEditTracker();
        var displayManager = new Mock<IDisplayManager<EmployeePayProfile>>();
        var updater = new MultiModelUpdater();
        var accessor = new Mock<IUpdateModelAccessor>();

        accessor.SetupGet(entry => entry.ModelUpdater).Returns(updater);
        displayManager
            .Setup(manager => manager.BuildEditorAsync(It.IsAny<EmployeePayProfile>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Mock.Of<IShape>());
        displayManager
            .Setup(manager => manager.UpdateEditorAsync(It.IsAny<EmployeePayProfile>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<EmployeePayProfile, IUpdateModel, bool, string, string>((profile, _, _, group, _) =>
            {
                Assert.Equal(PayrollConstants.MyPayEditorGroup, group);
                edit?.Invoke(profile, tracker);
            })
            .ReturnsAsync(Mock.Of<IShape>());

        return new MyPayController(
            services.GetRequiredService<IStaffDirectory>(),
            services.GetRequiredService<IPayProfileManager>(),
            services.GetRequiredService<DefaultPayProfileService>(),
            services.GetRequiredService<PayStatementStore>(),
            services.GetRequiredService<EmployerStore>(),
            services.GetRequiredService<IPayCodeManager>(),
            tracker,
            services.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>(),
            accessor.Object,
            displayManager.Object,
            Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughHtmlLocalizer<MyPayController>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<MyPayController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = user,
                    RequestServices = new ServiceCollection()
                        .AddSingleton(Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionaryFactory>())
                        .AddSingleton(services.GetRequiredService<PayStatementReviewService>())
                        .AddSingleton<Microsoft.AspNetCore.Mvc.ModelBinding.IModelMetadataProvider, Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider>()
                        .BuildServiceProvider(),
                },
            },
            Url = Mock.Of<IUrlHelper>(),
        };
    }

    private static PayProfileDisplayDriver Driver(IServiceProvider services, PayProfileEditTracker tracker)
    {
        var driver = new PayProfileDisplayDriver(
            services.GetRequiredService<IPayProfileService>(),
            services.GetRequiredService<IPayrollScopeService>(),
            services.GetRequiredService<EmployerStore>(),
            services.GetRequiredService<IPayScheduleManager>(),
            services.GetRequiredService<IStaffDirectory>(),
            Mock.Of<CrestApps.OrchardCore.Addresses.Core.Editing.IAddressEditorService>(),
            Mock.Of<IShapeFactory>(),
            services,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = CalendarTestHarness.User("lee-user") } },
            Microsoft.Extensions.Options.Options.Create(new PayrollElectionOptions()),
            Mock.Of<Microsoft.Extensions.Localization.IStringLocalizerFactory>(),
            tracker,
            services.GetRequiredService<IClock>(),
            new CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<PayProfileDisplayDriver>());

        return driver;
    }

    /// <summary>
    /// Fills each model type the driver asks for from the object given for it.
    /// </summary>
    private sealed class MultiModelUpdater : IUpdateModel
    {
        private readonly Dictionary<Type, object> _sources;

        public MultiModelUpdater(params object[] sources)
        {
            _sources = sources.ToDictionary(source => source.GetType());
        }

        public Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model)
            where TModel : class
            => TryUpdateModelAsync(model, string.Empty);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix)
            where TModel : class
        {
            if (!_sources.TryGetValue(typeof(TModel), out var source))
            {
                return Task.FromResult(false);
            }

            foreach (var property in typeof(TModel).GetProperties().Where(property => property.CanRead && property.CanWrite))
            {
                property.SetValue(model, property.GetValue(source));
            }

            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params System.Linq.Expressions.Expression<Func<TModel, object>>[] includeExpressions)
            where TModel : class
            => TryUpdateModelAsync(model, prefix);

        public bool TryValidateModel(object model)
            => true;

        public bool TryValidateModel(object model, string prefix)
            => true;
    }

    /// <summary>
    /// Records profile changes.
    /// </summary>
    private sealed class RecordingProfileObserver : IPayProfileObserver
    {
        public List<PayProfileChange> Changes { get; } = [];

        public Task ChangedAsync(PayProfileChange change, CancellationToken cancellationToken = default)
        {
            Changes.Add(change);

            return Task.CompletedTask;
        }
    }
}
