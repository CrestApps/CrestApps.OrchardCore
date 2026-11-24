using System.Net;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.AI;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Modules.Telephony.AITools;

/// <summary>
/// The Telnyx tools call the same API client as the Telnyx numbers page, never return a key, and change only a number
/// the account owns. Every tool refuses a caller who is not signed in or lacks <b>Manage telephony settings</b> before
/// Telnyx is called.
/// </summary>
public sealed class TelnyxAIToolTests
{
    private static readonly DateTime _now = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    private readonly RecordingHttpMessageHandler _telnyx = new();
    private readonly Mock<ICatalogManager<OmnichannelChannelEndpoint>> _addresses = new();
    private readonly TelnyxCallerNumberReputationDocument _reputation = new();
    private readonly TelnyxSettings _settings = new();

    public static TheoryData<string> ToolNames
        => new()
        {
            GetTelnyxConnectionStatusTool.TheName,
            ListTelnyxNumbersTool.TheName,
            SearchTelnyxAvailableNumbersTool.TheName,
            SetTelnyxNumberCallerNameTool.TheName,
        };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), Build(signedIn: false), Arguments());

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(_telnyx.Requests);
        Assert.Empty(_addresses.Invocations);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutItsPermission(string toolName)
    {
        var result = await AgentToolTestContext.InvokeAsync(CreateTool(toolName), Build(authorize: (_, _) => false), Arguments());

        Assert.Equal(AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(_telnyx.Requests);
        Assert.Empty(_addresses.Invocations);
    }

    [Fact]
    public async Task GetTelnyxConnectionStatus_ReportsTheConnectionWithoutAnyKey()
    {
        _settings.IsEnabled = true;
        _settings.ApiKey = "protected-api-key-value";
        _settings.WebhookPublicKey = "protected-webhook-key";
        _settings.ConnectionId = "call-control-1";
        _settings.SipConnectionId = "sip-1";
        _settings.DefaultOutboundCallerId = "+17025550100";

        var text = await AgentToolTestContext.InvokeAsync(new GetTelnyxConnectionStatusTool(), Build());
        var result = AgentToolTestContext.ReadJson(text);

        Assert.True(result.GetProperty("connected").GetBoolean());
        Assert.True(result.GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("+17025550100", result.GetProperty("defaultCallerId").GetString());
        Assert.DoesNotContain("protected-api-key-value", text);
        Assert.DoesNotContain("protected-webhook-key", text);
        Assert.DoesNotContain("call-control-1", text);
    }

    [Fact]
    public async Task GetTelnyxConnectionStatus_SaysWhatIsMissing()
    {
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new GetTelnyxConnectionStatusTool(), Build()));

        Assert.False(result.GetProperty("connected").GetBoolean());
        Assert.Contains("not ready", result.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ListTelnyxNumbers_ListsTheAccountsNumbersWithTheirSpamRejections()
    {
        _telnyx.RespondWith(HttpStatusCode.OK, OwnedNumbers());
        _reputation.Numbers["+17025550144"] = new TelnyxCallerNumberRejections
        {
            Total = 2,
            Days = { [_now.AddDays(-1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)] = 2 },
            LastRejectedUtc = _now.AddDays(-1),
        };

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListTelnyxNumbersTool(), Build(), new Dictionary<string, object>
        {
            ["contains"] = "0144",
        }));

        var number = Assert.Single(result.GetProperty("items").EnumerateArray());
        Assert.Equal("pn-1", number.GetProperty("id").GetString());
        Assert.Equal(2, number.GetProperty("spamRejections").GetInt32());
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.StartsWith("/v2/phone_numbers?page[number]=1&page[size]=250", Uri.UnescapeDataString(_telnyx.Requests[0].Path));
    }

    [Fact]
    public async Task SearchTelnyxAvailableNumbers_AsksForTextCapableNumbers()
    {
        _telnyx.RespondWith(HttpStatusCode.OK, """
            { "data": [ { "phone_number": "+17025550199", "features": [ { "name": "voice" }, { "name": "sms" } ], "cost_information": { "monthly_cost": "1.00", "upfront_cost": "1.00", "currency": "USD" } } ] }
            """);

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SearchTelnyxAvailableNumbersTool(), Build(), new Dictionary<string, object>
        {
            ["areaCode"] = "702",
            ["sms"] = true,
        }));

        Assert.Equal("+17025550199", result.GetProperty("items")[0].GetProperty("phoneNumber").GetString());
        var path = Uri.UnescapeDataString(_telnyx.Requests[0].Path);
        Assert.Contains("filter[national_destination_code]=702", path);
        Assert.Contains("filter[features][]=sms", path);
    }

    [Fact]
    public async Task SetTelnyxNumberCallerName_RefusesANumberTheAccountDoesNotOwn()
    {
        _telnyx.RespondWith(HttpStatusCode.OK, OwnedNumbers());

        var result = await AgentToolTestContext.InvokeAsync(new SetTelnyxNumberCallerNameTool(), Build(), new Dictionary<string, object>
        {
            ["number"] = "+19995550000",
            ["callerName"] = "Acme Support",
        });

        Assert.StartsWith("Nothing was changed: the Telnyx account does not own", result);
        Assert.Single(_telnyx.Requests);
    }

    [Fact]
    public async Task SetTelnyxNumberCallerName_SetsTheNormalizedNameOnTheOwnedNumber()
    {
        _telnyx.RespondWith(HttpStatusCode.OK, OwnedNumbers()).RespondWith(HttpStatusCode.OK);

        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetTelnyxNumberCallerNameTool(), Build(), new Dictionary<string, object>
        {
            ["number"] = "+1 (702) 555-0144",
            ["callerName"] = "Acme Support!",
        }));

        Assert.Equal("Acme Support", result.GetProperty("callerName").GetString());
        Assert.Equal(HttpMethod.Patch, _telnyx.Requests[1].Method);
        Assert.Equal("/v2/phone_numbers/pn-1/voice", _telnyx.Requests[1].Path);
    }

    private static string OwnedNumbers()
        => """
            { "data": [
              { "id": "pn-1", "phone_number": "+17025550144", "status": "active", "connection_id": "call-control-1" },
              { "id": "pn-2", "phone_number": "+17025550155", "status": "active" }
            ] }
            """;

    private static AIFunction CreateTool(string toolName)
        => toolName switch
        {
            GetTelnyxConnectionStatusTool.TheName => new GetTelnyxConnectionStatusTool(),
            ListTelnyxNumbersTool.TheName => new ListTelnyxNumbersTool(),
            SearchTelnyxAvailableNumbersTool.TheName => new SearchTelnyxAvailableNumbersTool(),
            _ => new SetTelnyxNumberCallerNameTool(),
        };

    private static Dictionary<string, object> Arguments()
        => new()
        {
            ["phoneNumber"] = "+17025550199",
            ["number"] = "pn-1",
            ["callerName"] = "Acme",
            ["areaCode"] = "702",
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(new TelnyxApiClient(
            new HttpClient(_telnyx) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
            new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions
            {
                ApiBaseUrl = "https://api.telnyx.com/v2/",
                ApiKey = "test-api-key",
            }),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance));

        var documents = new Mock<IDocumentManager<TelnyxCallerNumberReputationDocument>>();
        documents.Setup(manager => manager.GetOrCreateImmutableAsync(It.IsAny<Func<Task<TelnyxCallerNumberReputationDocument>>>())).ReturnsAsync(_reputation);
        var clock = Mock.Of<IClock>(value => value.UtcNow == _now);
        services.AddSingleton(new TelnyxCallerNumberReputation(documents.Object, clock, NullLogger<TelnyxCallerNumberReputation>.Instance));

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<TelnyxSettings>()).Returns(_settings);
        services.AddSingleton(Mock.Of<ISiteService>(value => value.GetSiteSettingsAsync() == Task.FromResult(site.Object)));

        services.AddSingleton(_addresses.Object);

        return services.BuildServiceProvider();
    }
}
