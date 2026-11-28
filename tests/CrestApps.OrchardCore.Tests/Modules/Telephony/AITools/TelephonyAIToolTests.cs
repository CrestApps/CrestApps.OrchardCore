using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.AI;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Records;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;
using QueryContext = CrestApps.Core.Models.QueryContext;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony.AITools;

/// <summary>
/// The telephony extensions are the <c>telephonyExtension</c> record kind of the generic record tools, read through the
/// extension catalog and validated by the catalog's rules, with the permission the Extensions page checks; the dial tool
/// only asks the caller's own soft phone to dial, and refuses a caller who is not signed in or lacks its permission
/// before anything is sent.
/// </summary>
public sealed class TelephonyAIToolTests
{
    private readonly Mock<ITelephonyExtensionManager> _extensions = new();
    private readonly Mock<ISoftPhoneClientNotifier> _softPhones = new();
    private readonly Mock<UserManager<IUser>> _users = new(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

    [Fact]
    public async Task DialFromSoftPhone_RefusesACallerWhoIsNotSignedIn()
    {
        var result = await AgentToolTestContext.InvokeAsync(new DialFromSoftPhoneTool(), Build(signedIn: false), Arguments());

        Assert.Equal(TelephonyToolBase.NotAllowedMessage, result);
        AssertNothingReached();
    }

    [Fact]
    public async Task DialFromSoftPhone_RefusesACallerWithoutItsPermission()
    {
        var result = await AgentToolTestContext.InvokeAsync(new DialFromSoftPhoneTool(), Build(authorize: (_, _) => false), Arguments());

        Assert.Equal(TelephonyToolBase.NotAllowedMessage, result);
        AssertNothingReached();
    }

    [Fact]
    public void ExtensionKind_IsReadAndWrittenWithTheExtensionsPagesPermission()
    {
        var kind = GetExtensionKind();

        Assert.True(kind.CanWrite);
        Assert.Equal(TelephonyPermissions.ManageExtensions.Name, kind.Permission.Name);
        Assert.Equal(TelephonyPermissions.ManageExtensions.Name, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
    }

    [Fact]
    public async Task ExtensionKind_PagesTheCatalogWithTheSearch_AndReturnsTheAllowListedFields()
    {
        _extensions.Setup(manager => manager.PageAsync(2, 10, It.IsAny<QueryContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<TelephonyExtension>
            {
                Count = 11,
                Entries = [new TelephonyExtension { ItemId = "ext-1", Number = "204", DisplayName = "Maria", UserId = "user-maria", UserName = "maria", Name = "204 Maria" }],
            });
        var kind = GetExtensionKind();

        var page = await kind.ListAsync(Build(), new AIRecordQuery { Text = "maria", Page = 2, PageSize = 10 }, CancellationToken.None);
        var projected = JsonSerializer.SerializeToElement(kind.Projection(Assert.Single(page.Entries)));

        Assert.Equal(11, page.Count);
        Assert.Equal("204", projected.GetProperty("number").GetString());
        Assert.Equal(new[] { "displayName", "id", "number", "userId", "userName" }, projected.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        _extensions.Verify(manager => manager.PageAsync(2, 10, It.Is<QueryContext>(context => context.Name == "maria" && context.Sorted), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtensionKind_ReportsTheCatalogsValidation()
    {
        var failed = new ValidationResultDetails();
        failed.Fail(new ValidationResult("Extension 205 is already assigned."));
        _extensions.Setup(manager => manager.ValidateAsync(It.IsAny<TelephonyExtension>(), It.IsAny<CancellationToken>())).ReturnsAsync(failed);

        var result = await GetExtensionKind().ValidateAsync(Build(), new TelephonyExtension { ItemId = "ext-1", Number = "205" }, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("Extension 205 is already assigned.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public async Task DialFromSoftPhone_AsksOnlyTheCallersOwnSoftPhoneToDial()
    {
        var result = await AgentToolTestContext.InvokeAsync(new DialFromSoftPhoneTool(), Build(), new Dictionary<string, object>
        {
            ["number"] = "+15550103344",
        });

        Assert.Equal("+15550103344", AgentToolTestContext.ReadJson(result).GetProperty("requested").GetString());
        _softPhones.Verify(notifier => notifier.DialRequestedAsync(AgentToolTestContext.UserId, It.Is<TelephonyDialRequest>(request => request.Number == "+15550103344")), Times.Once);
    }

    [Fact]
    public async Task DialFromSoftPhone_RefusesAMissingNumber()
    {
        var result = await AgentToolTestContext.InvokeAsync(new DialFromSoftPhoneTool(), Build(), []);

        Assert.Equal("Nothing was dialed: give the number to call.", result);
        Assert.Empty(_softPhones.Invocations);
    }

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["number"] = "204",
        };

    private static AIRecordKind<TelephonyExtension> GetExtensionKind()
    {
        var services = new ServiceCollection();

        new AIToolsStartup(new RecordTestLocalizer<AIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return (AIRecordKind<TelephonyExtension>)provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds["telephonyExtension"];
    }

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(_extensions.Object);
        services.AddSingleton(_softPhones.Object);
        services.AddSingleton(_users.Object);

        return services.BuildServiceProvider();
    }

    private void AssertNothingReached()
    {
        Assert.Empty(_extensions.Invocations);
        Assert.Empty(_softPhones.Invocations);
        _users.Verify(manager => manager.FindByIdAsync(It.IsAny<string>()), Times.Never);
        _users.Verify(manager => manager.FindByNameAsync(It.IsAny<string>()), Times.Never);
    }
}
