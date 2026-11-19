using CrestApps.OrchardCore.Commerce;
using CrestApps.OrchardCore.Commerce.AI;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Commerce.AI;

/// <summary>
/// The store setup tools need the Set up your store permission, list the steps as the guide page does, run a step's
/// quick action only with the fields it declares, and record what was run or skipped in the guide's progress.
/// </summary>
public sealed class StoreSetupToolTests
{
    private readonly StoreSetupSettings _progress = new();
    private readonly FakeStep _currency = new("currency", 20, isDone: null)
    {
        Fields = [new StoreSetupField { Name = "currency", Required = true, Type = "select", Options = [new("USD", "US dollar"), new("EUR", "Euro")] }],
        HasAction = true,
    };

    private readonly FakeStep _tax = new("tax", 30, isDone: false) { Url = "/Admin/tax" };
    private readonly FakeStep _roles = new("staff-roles", 90, isDone: null) { HasAction = true, IsOptional = true };

    public static TheoryData<string> Tools => new()
    {
        GetStoreSetupGuideTool.TheName,
        RunStoreSetupStepTool.TheName,
        SkipStoreSetupStepTool.TheName,
    };

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(CommerceAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_currency.Runs);
        Assert.Empty(_progress.Skipped);
    }

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task Tool_RefusesACallerWithoutSetUpStore(string toolName)
    {
        // Arrange
        var services = Build(authorize: (permission, _) => permission.Name != CommercePermissions.SetUpStore.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(CommerceAIToolBase.NotAllowedMessage, result);
        Assert.Equal(CommercePermissions.SetUpStore.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_currency.Runs);
        Assert.Empty(_progress.Skipped);
    }

    [Fact]
    public async Task GetStoreSetupGuide_ListsTheStepsInOrder_WithTheStepToDoNow()
    {
        // Arrange
        _progress.Skipped.Add("tax");

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(GetStoreSetupGuideTool.TheName, Build(), []));

        // Assert
        var steps = json.GetProperty("steps").EnumerateArray().ToArray();

        Assert.Equal(["currency", "tax", "staff-roles"], steps.Select(step => step.GetProperty("name").GetString()));
        Assert.Equal(["to do", "skipped", "to do"], steps.Select(step => step.GetProperty("state").GetString()));
        Assert.Equal("currency", json.GetProperty("current").GetString());
        Assert.Equal("EUR", steps[0].GetProperty("quickAction").GetProperty("fields")[0].GetProperty("options")[1].GetProperty("value").GetString());
        Assert.Equal("/Admin/tax", steps[1].GetProperty("page").GetString());
        Assert.False(steps[1].TryGetProperty("quickAction", out _));
    }

    [Fact]
    public async Task RunStoreSetupStep_RunsTheQuickAction_AndRecordsTheStepDone()
    {
        // Arrange
        var services = Build();

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(RunStoreSetupStepTool.TheName, services, new() { ["step"] = "currency", ["values"] = new Dictionary<string, object> { ["currency"] = "EUR" } }));
        var guide = AgentToolTestContext.ReadJson(await InvokeAsync(GetStoreSetupGuideTool.TheName, services, []));

        // Assert
        Assert.True(json.GetProperty("done").GetBoolean());
        Assert.Equal("EUR", Assert.Single(_currency.Runs)["currency"]);
        Assert.Contains("currency", _progress.Completed);
        Assert.True(guide.GetProperty("steps")[0].GetProperty("ranInThisMessage").GetBoolean());
        Assert.Equal("done", guide.GetProperty("steps")[0].GetProperty("state").GetString());
        Assert.Equal("tax", guide.GetProperty("current").GetString());
    }

    [Theory]
    [InlineData("currency", "{\"region\":\"EU\"}", "is not a field of this step")]
    [InlineData("currency", "{\"currency\":\"GBP\"}", "is not a choice of the field")]
    [InlineData("currency", null, "needs a value for currency")]
    [InlineData("tax", null, "has no quick action")]
    [InlineData("unknown", null, "there is no setup step named")]
    public async Task RunStoreSetupStep_RefusesWhatThePageCouldNotPost(string step, string values, string expected)
    {
        // Arrange
        var arguments = new Dictionary<string, object> { ["step"] = step };

        if (values is not null)
        {
            arguments["values"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(values);
        }

        // Act
        var result = await InvokeAsync(RunStoreSetupStepTool.TheName, Build(), arguments);

        // Assert
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(_currency.Runs);
        Assert.Empty(_progress.Completed);
    }

    [Fact]
    public async Task RunStoreSetupStep_ReportsAFailedQuickAction_AndLeavesTheStepToDo()
    {
        // Arrange
        _currency.Fail = true;

        // Act
        var result = await InvokeAsync(RunStoreSetupStepTool.TheName, Build(), new() { ["step"] = "currency", ["values"] = new Dictionary<string, object> { ["currency"] = "USD" } });

        // Assert
        Assert.Equal("The step 'currency' was not done: That did not work.", result);
        Assert.Empty(_progress.Completed);
    }

    [Fact]
    public async Task SkipStoreSetupStep_SkipsAStep_AndUndoesTheSkip()
    {
        // Arrange
        var services = Build();

        // Act
        await InvokeAsync(SkipStoreSetupStepTool.TheName, services, new() { ["step"] = "tax" });
        var skipped = _progress.Skipped.ToArray();
        await InvokeAsync(SkipStoreSetupStepTool.TheName, services, new() { ["step"] = "tax", ["undo"] = true });

        // Assert
        Assert.Equal(["tax"], skipped);
        Assert.Empty(_progress.Skipped);
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName == GetStoreSetupGuideTool.TheName ? [] : new() { ["step"] = "staff-roles" };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        var site = new Mock<ISite>();
        site.Setup(value => value.Properties).Returns(new System.Text.Json.Nodes.JsonObject());
        site.Setup(value => value.GetOrCreate<StoreSetupSettings>()).Returns(_progress);

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.LoadSiteSettingsAsync()).ReturnsAsync(site.Object);
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        services.AddSingleton<IStoreSetupStep>(_roles);
        services.AddSingleton<IStoreSetupStep>(_tax);
        services.AddSingleton<IStoreSetupStep>(_currency);
        services.AddScoped<StoreSetupToolScope>();

        return services.BuildServiceProvider();
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            GetStoreSetupGuideTool.TheName => new GetStoreSetupGuideTool(),
            RunStoreSetupStepTool.TheName => new RunStoreSetupStepTool(),
            _ => new SkipStoreSetupStepTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    private sealed class FakeStep : IStoreSetupStep
    {
        private readonly bool? _isDone;

        public FakeStep(string name, int order, bool? isDone)
        {
            Name = name;
            Order = order;
            _isDone = isDone;
        }

        public string Name { get; }

        public int Order { get; }

        public IReadOnlyList<StoreSetupField> Fields { get; init; } = [];

        public bool HasAction { get; init; }

        public bool IsOptional { get; init; }

        public string Url { get; init; }

        public bool Fail { get; set; }

        public List<IReadOnlyDictionary<string, string>> Runs { get; } = [];

        public Task<StoreSetupStepDescription> DescribeAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new StoreSetupStepDescription
            {
                Title = new LocalizedString(Name, "The " + Name + " step"),
                IsDone = _isDone,
                IsOptional = IsOptional,
                Url = Url,
                Fields = Fields,
                ActionText = HasAction ? new LocalizedString("Run", "Run") : null,
            });

        public Task<StoreSetupActionResult> RunAsync(StoreSetupActionContext context, CancellationToken cancellationToken = default)
        {
            if (!HasAction)
            {
                return Task.FromResult<StoreSetupActionResult>(null);
            }

            if (Fail)
            {
                return Task.FromResult(StoreSetupActionResult.Failure(new LocalizedString("Failed", "That did not work.")));
            }

            Runs.Add(context.Values);

            return Task.FromResult(StoreSetupActionResult.Success(new LocalizedString("Done", "Done.")));
        }
    }
}
