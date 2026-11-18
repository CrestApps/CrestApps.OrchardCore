using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.AI;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Subscriptions;
using CrestApps.OrchardCore.Reports.Subscriptions.Models;
using CrestApps.OrchardCore.Reports.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.Email;
using OrchardCore.Infrastructure;
using OrchardCore.Modules;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using AgentToolTestContext = CrestApps.OrchardCore.Tests.Agent.AgentToolTestContext;
using ReportsAuthorizationService = CrestApps.OrchardCore.Tests.Modules.Reports.Designer.Module.FakeAuthorizationService;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.AI;

/// <summary>
/// The report subscription tools need <c>ManageReportSubscriptions</c>, save through the subscription editor's checks for
/// the caller (a report they may view, up to 50 valid addresses, an attachment they may export), record the caller as
/// the person who saved it, and send only after the same checks pass, through the real subscription service.
/// </summary>
public sealed class ReportSubscriptionToolTests
{
    private static readonly string _manage = ReportSubscriptionPermissions.ManageReportSubscriptions.Name;
    private static readonly string _viewSales = SalesTestReport.Permission.Name;
    private static readonly string _exportSales = SalesTestReport.ExportPermission.Name;

    private readonly Dictionary<string, ReportSubscription> _subscriptions = new(StringComparer.Ordinal);
    private readonly List<MailMessage> _emails = [];

    public static TheoryData<string> ToolNames => new()
    {
        ListReportSubscriptionsTool.TheName,
        SaveReportSubscriptionTool.TheName,
        SendReportSubscriptionTool.TheName,
        DeleteReportSubscriptionTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var subscription = AddSubscription();
        var services = Build(signedIn: false, _manage, _viewSales, _exportSales);

        // Act
        var result = await InvokeAsync(toolName, services, AllArguments());

        // Assert
        Assert.Equal(ReportsAIToolBase.NotAllowedMessage, result);
        AssertUnchanged(subscription);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageReportSubscriptions_EvenWithTheReportsPermissions(string toolName)
    {
        // Arrange
        var subscription = AddSubscription();
        var services = Build(true, _viewSales, _exportSales);

        // Act
        var result = await InvokeAsync(toolName, services, AllArguments());

        // Assert
        Assert.Equal(ReportsAIToolBase.NotAllowedMessage, result);
        AssertUnchanged(subscription);
    }

    [Fact]
    public async Task ListReportSubscriptions_ListsTheScheduleRecipientsAndLastError()
    {
        // Arrange
        var subscription = AddSubscription();
        subscription.LastError = "The email couldn't be sent: mailbox full";
        subscription.LastRunUtc = ReportToolHarness.Now.AddDays(-7);
        var services = Build(true, _manage);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListReportSubscriptionsTool.TheName, services, []));

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());

        Assert.Equal("s1", item.GetProperty("id").GetString());
        Assert.Equal("Sales test", item.GetProperty("reportTitle").GetString());
        Assert.Equal("Weekly", item.GetProperty("frequency").GetString());
        Assert.Equal("Monday", item.GetProperty("dayOfWeek").GetString());
        Assert.Equal(new[] { "finance@example.com", "ceo@example.com" }, item.GetProperty("recipients").EnumerateArray().Select(entry => entry.GetString()));
        Assert.Equal("The email couldn't be sent: mailbox full", item.GetProperty("lastError").GetString());
        Assert.StartsWith("2026-03-13T12:00:00", item.GetProperty("lastRun").GetString(), StringComparison.Ordinal);
        Assert.True(json.GetProperty("canEmail").GetBoolean());
    }

    [Fact]
    public async Task SaveReportSubscription_CreatesAScheduleSavedByTheCaller()
    {
        // Arrange
        var services = Build(true, _manage, _viewSales, _exportSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportSubscriptionTool.TheName, services, new()
        {
            ["report"] = SalesTestReport.Name,
            ["frequency"] = "Weekly",
            ["dayOfWeek"] = "Monday",
            ["hour"] = 7,
            ["recipients"] = new[] { "finance@example.com", "Finance@example.com", "ceo@example.com" },
            ["attachmentFormat"] = "csv",
        }));

        // Assert
        var subscription = Assert.Single(_subscriptions.Values);

        Assert.True(json.GetProperty("saved").GetBoolean(), json.ToString());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal("Sales test", subscription.Name);
        Assert.Equal(ReportToolHarness.CallerName, subscription.CreatedBy);
        Assert.Equal("finance@example.com, ceo@example.com", subscription.Recipients);
        Assert.Equal("csv", subscription.AttachmentFormat);

        // 20 March 2026 is a Friday; the next Monday at 7:00 in the site's time zone (UTC here) is 23 March.
        Assert.Equal(new DateTime(2026, 3, 23, 7, 0, 0, DateTimeKind.Utc), subscription.NextRunUtc);
    }

    [Fact]
    public async Task SaveReportSubscription_RefusesAnAttachment_WithoutTheReportsExportPermission()
    {
        // Arrange
        var services = Build(true, _manage, _viewSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportSubscriptionTool.TheName, services, new()
        {
            ["report"] = SalesTestReport.Name,
            ["recipients"] = new[] { "finance@example.com" },
            ["attachmentFormat"] = "csv",
        }));

        // Assert
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetString().StartsWith("You may not export this report", StringComparison.Ordinal));
        Assert.Empty(_subscriptions);
    }

    [Fact]
    public async Task SaveReportSubscription_RefusesAReportTheCallerMayNotView()
    {
        // Arrange
        var services = Build(true, _manage);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportSubscriptionTool.TheName, services, new()
        {
            ["report"] = SalesTestReport.Name,
            ["recipients"] = new[] { "finance@example.com" },
        }));

        // Assert
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetString() == "Choose a report.");
        Assert.Empty(_subscriptions);
    }

    [Theory]
    [InlineData(51)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SaveReportSubscription_RefusesRecipientsTheEditorRefuses(int count)
    {
        // Arrange: -1 stands for one address that is not an email address.
        var services = Build(true, _manage, _viewSales);
        var recipients = count < 0
            ? new[] { "not an address" }
            : Enumerable.Range(1, count).Select(index => $"person{index}@example.com").ToArray();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportSubscriptionTool.TheName, services, new()
        {
            ["report"] = SalesTestReport.Name,
            ["recipients"] = recipients,
        }));

        // Assert
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetString().StartsWith("Enter up to 50 email addresses", StringComparison.Ordinal));
        Assert.Empty(_subscriptions);
    }

    [Fact]
    public async Task SaveReportSubscription_ById_DisablesIt_AndRecordsTheCallerAsTheLastToSaveIt()
    {
        // Arrange
        var subscription = AddSubscription();
        var services = Build(true, _manage, _viewSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveReportSubscriptionTool.TheName, services, new() { ["id"] = "s1", ["enabled"] = false }));

        // Assert
        Assert.True(json.GetProperty("saved").GetBoolean(), json.ToString());
        Assert.False(subscription.IsEnabled);
        Assert.Null(subscription.NextRunUtc);
        Assert.Equal(ReportToolHarness.CallerName, subscription.ModifiedBy);
        Assert.Equal("finance@example.com, ceo@example.com", subscription.Recipients);
    }

    [Fact]
    public async Task SaveReportSubscription_WithAnUnknownId_SaysSo()
    {
        // Arrange
        var services = Build(true, _manage, _viewSales);

        // Act
        var result = await InvokeAsync(SaveReportSubscriptionTool.TheName, services, new() { ["id"] = "missing", ["enabled"] = false });

        // Assert
        Assert.Contains("no report subscription with the id 'missing'", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendReportSubscription_EmailsTheReportNow_WithoutMovingTheSchedule()
    {
        // Arrange
        var subscription = AddSubscription();
        var nextRun = subscription.NextRunUtc;
        var services = Build(true, _manage, _viewSales);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SendReportSubscriptionTool.TheName, services, new() { ["id"] = "s1" }));

        // Assert
        var email = Assert.Single(_emails);

        Assert.True(json.GetProperty("sent").GetBoolean(), json.ToString());
        Assert.Equal("finance@example.com, ceo@example.com", email.To);
        Assert.Equal("2026-03-13", json.GetProperty("period").GetProperty("from").GetString());
        Assert.Equal("2026-03-19", json.GetProperty("period").GetProperty("to").GetString());
        Assert.Equal(nextRun, subscription.NextRunUtc);
        Assert.Equal(ReportToolHarness.Now, subscription.LastRunUtc);
    }

    [Fact]
    public async Task SendReportSubscription_RefusesACallerWhoMayNotViewTheReport()
    {
        // Arrange
        var subscription = AddSubscription();
        var services = Build(true, _manage);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SendReportSubscriptionTool.TheName, services, new() { ["id"] = "s1" }));

        // Assert
        Assert.False(json.GetProperty("sent").GetBoolean());
        Assert.Empty(_emails);
        Assert.Null(subscription.LastRunUtc);
    }

    [Fact]
    public async Task DeleteReportSubscription_DeletesIt()
    {
        // Arrange
        AddSubscription();
        var services = Build(true, _manage);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteReportSubscriptionTool.TheName, services, new() { ["id"] = "s1" }));

        // Assert
        Assert.True(json.GetProperty("deleted").GetBoolean());
        Assert.Empty(_subscriptions);
    }

    [Fact]
    public async Task DeleteReportSubscription_WithAnUnknownId_DeletesNothing()
    {
        // Arrange
        AddSubscription();
        var services = Build(true, _manage);

        // Act
        var result = await InvokeAsync(DeleteReportSubscriptionTool.TheName, services, new() { ["id"] = "missing" });

        // Assert
        Assert.Contains("no report subscription with the id 'missing'", result, StringComparison.Ordinal);
        Assert.Single(_subscriptions);
    }

    private ReportSubscription AddSubscription()
    {
        var subscription = new ReportSubscription
        {
            ItemId = "s1",
            Name = "Weekly sales",
            ReportName = SalesTestReport.Name,
            Frequency = ReportSubscriptionFrequency.Weekly,
            DayOfWeek = DayOfWeek.Monday,
            Hour = 7,
            Recipients = "finance@example.com, ceo@example.com",
            IsEnabled = true,
            NextRunUtc = new DateTime(2026, 3, 23, 7, 0, 0, DateTimeKind.Utc),
            CreatedBy = "Owner",
            CreatedUtc = ReportToolHarness.Now.AddDays(-30),
        };

        _subscriptions[subscription.ItemId] = subscription;

        return subscription;
    }

    private void AssertUnchanged(ReportSubscription subscription)
    {
        Assert.Same(subscription, Assert.Single(_subscriptions.Values));
        Assert.True(subscription.IsEnabled);
        Assert.Equal("Weekly sales", subscription.Name);
        Assert.Null(subscription.ModifiedBy);
        Assert.Null(subscription.LastRunUtc);
        Assert.Empty(_emails);
    }

    private ServiceProvider Build(bool signedIn = true, params string[] granted)
    {
        var httpContext = new DefaultHttpContext();

        if (signedIn)
        {
            httpContext.User = ReportDesignerPrincipals.User(ReportToolHarness.CallerId, ReportToolHarness.CallerName);
        }

        var services = new ServiceCollection();
        var clock = Mock.Of<IClock>(value => value.UtcNow == ReportToolHarness.Now);

        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton<IAuthorizationService>(new ReportsAuthorizationService(granted));
        services.AddSingleton(AgentToolTestContext.CreateServices().BuildServiceProvider().GetRequiredService<ILocalClock>());
        services.AddSingleton(clock);
        services.AddSingleton<IStringLocalizerFactory>(new PassThroughStringLocalizerFactory());
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddSingleton<SalesTestReport>();
        services.AddSingleton(Options.Create(new ReportOptions().Add(ReportDescriptor.Create<SalesTestReport>())));
        services.AddSingleton(Options.Create(new ReportExportFormatOptions().Add(ReportExportFormatDescriptor.Create<CsvReportExportFormat>())));
        services.AddSingleton<IReportManager, ReportManager>();
        services.AddSingleton<IReportExportManager, ReportExportManager>();
        services.AddSingleton(new ReportDisplayValueResolver(Mock.Of<IShapeFactory>(), Mock.Of<IDisplayHelper>()));

        var site = new Mock<ISite>();
        site.SetupGet(value => value.TimeZoneId).Returns("UTC");
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager.Setup(manager => manager.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => new User { UserId = name, UserName = name, IsEnabled = true });
        var principals = new Mock<IUserClaimsPrincipalFactory<IUser>>();
        principals.Setup(factory => factory.CreateAsync(It.IsAny<IUser>()))
            .ReturnsAsync((IUser user) => ReportDesignerPrincipals.User(user.UserName, user.UserName));
        services.AddSingleton(new ReportOwnerPrincipalResolver(userManager.Object, principals.Object));

        var email = new Mock<IEmailService>();
        email.Setup(service => service.SendAsync(It.IsAny<MailMessage>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((MailMessage message, string _, CancellationToken _) => _emails.Add(message))
            .ReturnsAsync(Result.Success());
        services.AddSingleton(email.Object);

        services.AddSingleton(Store());
        services.AddSingleton<ReportSubscriptionService>();
        services.AddSingleton<ReportSubscriptionEditor>();

        return services.BuildServiceProvider();
    }

    private IReportSubscriptionStore Store()
    {
        var store = new Mock<IReportSubscriptionStore>();

        store.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(id is not null && _subscriptions.TryGetValue(id, out var found) ? found : null));
        store.Setup(value => value.CreateAsync(It.IsAny<ReportSubscription>(), It.IsAny<CancellationToken>()))
            .Returns((ReportSubscription entry, CancellationToken _) =>
            {
                _subscriptions[entry.ItemId] = entry;

                return ValueTask.CompletedTask;
            });
        store.Setup(value => value.UpdateAsync(It.IsAny<ReportSubscription>(), It.IsAny<CancellationToken>()))
            .Returns((ReportSubscription entry, CancellationToken _) =>
            {
                _subscriptions[entry.ItemId] = entry;

                return ValueTask.CompletedTask;
            });
        store.Setup(value => value.DeleteAsync(It.IsAny<ReportSubscription>(), It.IsAny<CancellationToken>()))
            .Returns((ReportSubscription entry, CancellationToken _) => ValueTask.FromResult(_subscriptions.Remove(entry.ItemId)));
        store.Setup(value => value.PageAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string search, int skip, int take, CancellationToken _) =>
            {
                var all = _subscriptions.Values
                    .Where(entry => string.IsNullOrEmpty(search) || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(entry => entry.Name, StringComparer.Ordinal)
                    .ToList();

                return (all.Count, (IReadOnlyList<ReportSubscription>)all.Skip(skip).Take(take).ToList());
            });

        return store.Object;
    }

    // Arguments that reach every tool's permission check and would change something if it were passed.
    private static Dictionary<string, object> AllArguments()
        => new()
        {
            ["id"] = "s1",
            ["name"] = "Changed",
            ["enabled"] = false,
            ["recipients"] = new[] { "other@example.com" },
        };

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
        => AgentToolTestContext.InvokeAsync(CreateTool(toolName), services, arguments);

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            ListReportSubscriptionsTool.TheName => new ListReportSubscriptionsTool(),
            SaveReportSubscriptionTool.TheName => new SaveReportSubscriptionTool(),
            SendReportSubscriptionTool.TheName => new SendReportSubscriptionTool(),
            DeleteReportSubscriptionTool.TheName => new DeleteReportSubscriptionTool(),
            _ => throw new ArgumentOutOfRangeException(nameof(toolName), toolName, null),
        };
}
