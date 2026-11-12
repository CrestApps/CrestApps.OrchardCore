using System.Security.Claims;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Controllers;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Subscriptions;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using CrestApps.OrchardCore.Reports.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.Reports;

/// <summary>
/// A report's optional export permission: the report page hides its export buttons and refuses the export without
/// it, and a report subscription can't attach the export for a person who lacks it.
/// </summary>
public sealed class ReportExportPermissionTests
{
    private static readonly Permission _viewPermission = new("ViewGuardedReport", LocalizationSource.Create<ReportExportPermissionTests>("View guarded report"));
    private static readonly Permission _exportPermission = new("ExportGuardedReport", LocalizationSource.Create<ReportExportPermissionTests>("Export guarded report"));

    private readonly Mock<IReportRunner> _runner = new();
    private readonly List<ReportRunMode> _runs = [];

    public ReportExportPermissionTests()
    {
        _runner
            .Setup(runner => runner.RunAsync(It.IsAny<ReportContext>(), It.IsAny<CancellationToken>()))
            .Callback((ReportContext context, CancellationToken _) => _runs.Add(context.Mode))
            .ReturnsAsync((ReportContext context, CancellationToken _) => new ReportDocument()
                .Add(ReportSection.ForTable(context.Mode == ReportRunMode.Export ? "Rows" : "Summary", [new ReportColumn("Mode")], [new ReportRow([context.Mode.ToString()])])));
    }

    [Fact]
    public async Task TheExport_IsRefused_WithoutTheExportPermission()
    {
        var result = await Controller(_viewPermission.Name).Export("guarded", ReportsConstants.CsvExportFormat);

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(_runs);
    }

    [Fact]
    public async Task TheExport_IsMade_WithBothPermissions()
    {
        var result = await Controller(_viewPermission.Name, _exportPermission.Name).Export("guarded", ReportsConstants.CsvExportFormat);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("guarded-all-time.csv", file.FileDownloadName);
        Assert.Equal([ReportRunMode.Export], _runs);
    }

    [Fact]
    public async Task ThePage_HidesTheExportButtons_WithoutTheExportPermission()
    {
        var denied = Assert.IsType<ReportDisplayViewModel>(Assert.IsType<ViewResult>(await Controller(_viewPermission.Name).Display("guarded")).Model);
        var allowed = Assert.IsType<ReportDisplayViewModel>(Assert.IsType<ViewResult>(await Controller(_viewPermission.Name, _exportPermission.Name).Display("guarded")).Model);

        Assert.Empty(denied.ExportFormats);
        Assert.Single(allowed.ExportFormats);
    }

    [Theory]
    [InlineData(null, null, "guarded-all-time")]
    [InlineData("2026-11-01", null, "guarded-from-20261101")]
    [InlineData(null, "2026-11-07", "guarded-to-20261107")]
    [InlineData("2026-11-01", "2026-11-07", "guarded-20261101-to-20261107")]
    public void TheFileName_NamesTheBoundsTheRangeHas(string from, string to, string expected)
    {
        var range = new ReportDateRange
        {
            FromUtc = from is null ? null : DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture),
            ToUtc = to is null ? null : DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture),
        };

        Assert.Equal(expected, ReportsController.GetFileName("guarded", range));
    }

    private static readonly string Manage = CrestApps.OrchardCore.Reports.Subscriptions.ReportSubscriptionPermissions.ManageReportSubscriptions.Name;

    // SEC-A13: a subscription kept sending the report after the person who saved it lost access to it. At send time the
    // author must still exist, be enabled, and hold the report's permission and Manage report subscriptions; otherwise
    // the subscription is turned off and says why.
    [Theory]
    [InlineData("gone")]
    [InlineData("no-view")]
    [InlineData("no-manage")]
    public async Task ASubscription_StopsAndSaysWhy_WhenItsAuthorLostAccess(string author)
    {
        var email = new Mock<IEmailService>();
        var subscription = Subscription(modifiedBy: author);
        subscription.AttachmentFormat = null;

        var service = SubscriptionService(email, grants: new()
        {
            ["no-view"] = [Manage],
            ["no-manage"] = [_viewPermission.Name],
        });

        var sent = await service.SendAsync(subscription, DateTime.UtcNow, reschedule: true, TestContext.Current.CancellationToken);

        Assert.False(sent);
        Assert.False(subscription.IsEnabled);
        Assert.Null(subscription.NextRunUtc);
        Assert.Contains(author, subscription.LastError);
        Assert.Empty(_runs);
        email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ASubscription_DoesNotSendTheExport_WhileWhoSavedItLacksTheExportPermission()
    {
        var email = new Mock<IEmailService>();
        var subscription = Subscription(modifiedBy: "viewer");

        var sent = await SubscriptionService(email, grants: new() { ["viewer"] = [_viewPermission.Name, Manage] }).SendAsync(subscription, DateTime.UtcNow, reschedule: false, TestContext.Current.CancellationToken);

        Assert.False(sent);
        Assert.Contains("viewer may not export this report", subscription.LastError);
        Assert.Empty(_runs);
        email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ASubscription_SendsTheExport_WhileWhoSavedItHasTheExportPermission_WithTheScreenInTheEmail()
    {
        var email = new Mock<IEmailService>();
        MailMessage message = null;
        string attachment = null;

        email
            .Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((MailMessage mail, string _, CancellationToken _) =>
            {
                message = mail;
                attachment = new StreamReader(Assert.Single(mail.Attachments).Stream).ReadToEnd();
            })
            .ReturnsAsync(Result.Success());

        // The creator could only view it; the person who last saved it may export it.
        var subscription = Subscription(modifiedBy: "exporter");

        var sent = await SubscriptionService(email, grants: new() { ["viewer"] = [_viewPermission.Name, Manage], ["exporter"] = [_viewPermission.Name, _exportPermission.Name, Manage] })
            .SendAsync(subscription, DateTime.UtcNow, reschedule: false, TestContext.Current.CancellationToken);

        Assert.True(sent, subscription.LastError);
        Assert.Equal([ReportRunMode.Display, ReportRunMode.Export], _runs);
        Assert.Contains("Display", message.HtmlBody);
        Assert.Contains("Export", attachment);
    }

    [Fact]
    public async Task ASubscription_CannotBeSavedWithAFileFormat_ByAPersonWithoutTheExportPermission()
    {
        var store = new Mock<IReportSubscriptionStore>();
        var email = new Mock<IEmailService>();
        var grants = new Dictionary<string, string[]> { ["person"] = [_viewPermission.Name, ReportSubscriptionPermissions.ManageReportSubscriptions.Name] };
        var controller = new ReportSubscriptionsController(
            store.Object,
            SubscriptionService(email, grants),
            ReportManager().Object,
            ExportManager(),
            new SourceTextLocalizerFactory(),
            Authorization(grants),
            Mock.Of<INotifier>(),
            Mock.Of<IClock>(),
            Mock.Of<IHtmlLocalizer<ReportSubscriptionsController>>(),
            new SourceTextStringLocalizer<ReportSubscriptionsController>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal("person") },
            },
        };

        var withFile = await controller.CreatePost(new ReportSubscriptionEditViewModel { ReportName = "guarded", Recipients = "team@example.com", AttachmentFormat = ReportsConstants.CsvExportFormat }, submit: null);

        Assert.IsType<ViewResult>(withFile);
        Assert.Contains(controller.ModelState[nameof(ReportSubscriptionEditViewModel.AttachmentFormat)].Errors, error => error.ErrorMessage.Contains("You may not export this report", StringComparison.Ordinal));
        store.Verify(value => value.CreateAsync(It.IsAny<ReportSubscription>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    private ReportsController Controller(params string[] grants)
    {
        var reportManager = ReportManager();

        var filterDisplayManager = new Mock<IDisplayManager<ReportFilter>>();
        filterDisplayManager
            .Setup(manager => manager.UpdateEditorAsync(It.IsAny<ReportFilter>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Mock.Of<IShape>());
        filterDisplayManager
            .Setup(manager => manager.BuildEditorAsync(It.IsAny<ReportFilter>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Mock.Of<IShape>());

        var updateModelAccessor = new Mock<IUpdateModelAccessor>();
        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(Mock.Of<IUpdateModel>());

        return new ReportsController(
            reportManager.Object,
            ExportManager(),
            filterDisplayManager.Object,
            Authorization(new() { ["person"] = grants }),
            updateModelAccessor.Object,
            new ReportDisplayValueResolver(Mock.Of<IShapeFactory>(), Mock.Of<IDisplayHelper>()),
            new SourceTextLocalizerFactory())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal("person") },
            },
        };
    }

    private ReportSubscriptionService SubscriptionService(Mock<IEmailService> email, Dictionary<string, string[]> grants)
    {
        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(DateTime.UtcNow);

        var users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        users.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((string userName) => grants.ContainsKey(userName) ? new TestUser(userName) : null);

        var principals = new Mock<IUserClaimsPrincipalFactory<IUser>>();
        principals.Setup(factory => factory.CreateAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => Principal(user.UserName));

        return new ReportSubscriptionService(
            Mock.Of<IReportSubscriptionStore>(),
            ReportManager().Object,
            ExportManager(),
            new ReportDisplayValueResolver(Mock.Of<IShapeFactory>(), Mock.Of<IDisplayHelper>()),
            new SourceTextLocalizerFactory(),
            siteService.Object,
            clock.Object,
            Authorization(grants),
            new ReportOwnerPrincipalResolver(users.Object, principals.Object),
            NullLogger<ReportSubscriptionService>.Instance,
            new SourceTextStringLocalizer<ReportSubscriptionService>(),
            email.Object);
    }

    // The CSV format, as the Reports feature registers it.
    private static ReportExportManager ExportManager()
    {
        var services = new ServiceCollection().AddReportExportFormat<CsvReportExportFormat>().BuildServiceProvider();

        return new ReportExportManager(services.GetRequiredService<IOptions<ReportExportFormatOptions>>(), services);
    }

    private Mock<IReportManager> ReportManager()
    {
        var descriptor = new ReportDescriptor(
            "guarded",
            LocalizationSource.Create<ReportExportPermissionTests>("Guarded"),
            LocalizationSource.Create<ReportExportPermissionTests>("A report with an export permission."),
            ReportsConstants.Categories.General,
            _viewPermission,
            typeof(IReportRunner),
            _ => _runner.Object)
        {
            ExportPermission = _exportPermission,
            DefaultDateRange = ReportDefaultDateRange.AllTime,
        };

        var reportManager = new Mock<IReportManager>();
        reportManager.Setup(manager => manager.GetReports()).Returns([descriptor]);
        reportManager.Setup(manager => manager.GetDescriptor("guarded")).Returns(descriptor);
        reportManager.Setup(manager => manager.GetReport("guarded")).Returns(_runner.Object);

        return reportManager;
    }

    private static ReportSubscription Subscription(string modifiedBy)
        => new()
        {
            ItemId = "subscription-1",
            ReportName = "guarded",
            Recipients = "team@example.com",
            AttachmentFormat = ReportsConstants.CsvExportFormat,
            Frequency = ReportSubscriptionFrequency.Daily,
            CreatedBy = "viewer",
            ModifiedBy = modifiedBy,
        };

    private static ClaimsPrincipal Principal(string userName)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, userName)], "Test"));

    // Grants each user, by name, the permissions listed for them.
    private static IAuthorizationService Authorization(Dictionary<string, string[]> grants)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                grants.TryGetValue(user.Identity?.Name ?? string.Empty, out var granted) &&
                requirements.OfType<PermissionRequirement>().All(requirement => granted.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        return authorizationService.Object;
    }

    private sealed class TestUser : IUser
    {
        public TestUser(string userName)
        {
            UserName = userName;
        }

        public string UserName { get; }
    }

    private sealed class SourceTextStringLocalizer<T> : Microsoft.Extensions.Localization.IStringLocalizer<T>
    {
        public Microsoft.Extensions.Localization.LocalizedString this[string name]
            => new(name, name);

        public Microsoft.Extensions.Localization.LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<Microsoft.Extensions.Localization.LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
