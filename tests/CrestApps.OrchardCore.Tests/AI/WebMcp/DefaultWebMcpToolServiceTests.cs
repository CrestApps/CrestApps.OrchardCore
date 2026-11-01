using System.Security.Claims;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.WebMcp.Models;
using CrestApps.OrchardCore.AI.WebMcp.Services;
using CrestApps.OrchardCore.Tests.AI.WebMcp.Doubles;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.AI.WebMcp;

public sealed class DefaultWebMcpToolServiceTests
{
    private static readonly ClaimsPrincipal _user = new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test"));

    [Fact]
    public async Task GetToolsAsync_ListsNothingByDefault()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions());

        // Act
        var tools = await service.GetToolsAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(tools);
    }

    [Fact]
    public async Task GetToolsAsync_ListsTheAllowedTools()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            Tools = [EchoTestTool.TheName],
        });

        // Act
        var tools = await service.GetToolsAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        var tool = Assert.Single(tools);
        Assert.Equal(EchoTestTool.TheName, tool.Name);
        Assert.Equal("Echo", tool.Title);
        Assert.Equal("Answers the text it receives.", tool.Description);
        Assert.Equal("object", tool.InputSchema.GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetToolsAsync_MatchesTheAllowListWithoutCaringForCase()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            Tools = ["ECHO_TOOL"],
        });

        // Act
        var tools = await service.GetToolsAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([EchoTestTool.TheName], tools.Select(tool => tool.Name));
    }

    [Fact]
    public async Task GetToolsAsync_NeverListsSystemOrHiddenTools_EvenWhenExposingAll()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            ExposeAllTools = true,
            Tools = [SystemTestTool.TheName, HiddenTestTool.TheName],
        });

        // Act
        var tools = await service.GetToolsAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [EchoTestTool.TheName, FailingTestTool.TheName, ServicesProbeTestTool.TheName],
            tools.Select(tool => tool.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetToolsAsync_LeavesOutToolsTheUserMayNotUse()
    {
        // Arrange
        var service = CreateService(
            new WebMcpOptions
            {
                ExposeAllTools = true,
            },
            authorize: name => name != FailingTestTool.TheName);

        // Act
        var tools = await service.GetToolsAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(tools, tool => tool.Name == FailingTestTool.TheName);
        Assert.Contains(tools, tool => tool.Name == EchoTestTool.TheName);
    }

    [Fact]
    public async Task GetToolsAsync_ListsAllowedToolInstances()
    {
        // Arrange
        var instance = new AIToolInstance
        {
            ItemId = "instance-1",
            Name = "docs",
            Source = "test-source",
        };

        var service = CreateService(
            new WebMcpOptions
            {
                Tools = ["docs"],
            },
            configure: services => AddToolInstance(services, instance));

        // Act
        var tools = await service.GetToolsAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        var tool = Assert.Single(tools);
        Assert.Equal(instance.GetFunctionName(), tool.Name);
        Assert.Equal("docs", tool.Title);
    }

    [Fact]
    public async Task GetToolsAsync_LeavesOutToolInstancesTheUserMayNotUse()
    {
        // Arrange
        var instance = new AIToolInstance
        {
            ItemId = "instance-1",
            Name = "docs",
            Source = "test-source",
        };

        var service = CreateService(
            new WebMcpOptions
            {
                ExposeAllTools = true,
            },
            configure: services => AddToolInstance(services, instance),
            authorize: name => name != instance.GetFunctionName());

        // Act
        var tools = await service.GetToolsAsync(_user, TestContext.Current.CancellationToken);

        // Assert
        Assert.DoesNotContain(tools, tool => tool.Name == instance.GetFunctionName());
    }

    [Fact]
    public async Task InvokeAsync_CallsAnAllowedToolWithTheArguments()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            Tools = [EchoTestTool.TheName],
        });

        var arguments = new Dictionary<string, object>
        {
            ["text"] = "hi",
        };

        // Act
        var result = await service.InvokeAsync(_user, EchoTestTool.TheName, arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.Succeeded, result.Status);
        Assert.False(result.IsError);
        Assert.Equal("echo: hi", result.Text);
    }

    [Fact]
    public async Task InvokeAsync_GivesTheToolTheRequestServices()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            Tools = [ServicesProbeTestTool.TheName],
        });

        // Act
        var result = await service.InvokeAsync(_user, ServicesProbeTestTool.TheName, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("services", result.Text);
    }

    [Theory]
    [InlineData("missing_tool")]
    [InlineData(SystemTestTool.TheName)]
    [InlineData(HiddenTestTool.TheName)]
    [InlineData(FailingTestTool.TheName)]
    [InlineData("")]
    [InlineData(null)]
    public async Task InvokeAsync_AnswersNotFoundForToolsThatAreNotGiven(string name)
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            Tools = [EchoTestTool.TheName, SystemTestTool.TheName, HiddenTestTool.TheName],
        });

        // Act
        var result = await service.InvokeAsync(_user, name, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.NotFound, result.Status);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task InvokeAsync_AnswersNotFoundWhenTheUserMayNotUseTheTool()
    {
        // Arrange
        var service = CreateService(
            new WebMcpOptions
            {
                Tools = [EchoTestTool.TheName],
            },
            authorize: _ => false);

        // Act
        var result = await service.InvokeAsync(_user, EchoTestTool.TheName, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task InvokeAsync_HidesTheDetailsOfAFailure()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions
        {
            Tools = [FailingTestTool.TheName],
        });

        // Act
        var result = await service.InvokeAsync(_user, FailingTestTool.TheName, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.Failed, result.Status);
        Assert.True(result.IsError);
        Assert.DoesNotContain("Secret", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_CallsAnAllowedToolInstance()
    {
        // Arrange
        var instance = new AIToolInstance
        {
            ItemId = "instance-1",
            Name = "docs",
            Source = "test-source",
        };

        var service = CreateService(
            new WebMcpOptions
            {
                Tools = ["docs"],
            },
            configure: services => AddToolInstance(services, instance));

        // Act
        var result = await service.InvokeAsync(_user, instance.GetFunctionName(), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebMcpToolInvocationStatus.Succeeded, result.Status);
        Assert.Equal("instance answer", result.Text);
    }

    [Fact]
    public async Task InvokeAsync_RequiresAUser()
    {
        // Arrange
        var service = CreateService(new WebMcpOptions());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.InvokeAsync(null, EchoTestTool.TheName, null, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetToolsAsync(null, TestContext.Current.CancellationToken));
    }

    private static void AddToolInstance(IServiceCollection services, AIToolInstance instance)
    {
        var catalog = new Mock<INamedCatalog<AIToolInstance>>();

        catalog
            .Setup(c => c.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIToolInstance> { instance });

        var source = new Mock<IAIToolInstanceSource>();

        source
            .Setup(s => s.CreateTool(instance))
            .Returns(() => AIFunctionFactory.Create(() => "instance answer", instance.GetFunctionName(), "Searches the docs."));

        services.AddSingleton(catalog.Object);
        services.AddKeyedSingleton(instance.Source, source.Object);
    }

    private static DefaultWebMcpToolService CreateService(
        WebMcpOptions options,
        Action<IServiceCollection> configure = null,
        Func<string, bool> authorize = null)
    {
        var services = new ServiceCollection();

        services.AddCoreAITool<EchoTestTool>(EchoTestTool.TheName)
            .WithTitle("Echo")
            .Selectable();
        services.AddCoreAITool<FailingTestTool>(FailingTestTool.TheName)
            .WithTitle("Fails")
            .Selectable();
        services.AddCoreAITool<ServicesProbeTestTool>(ServicesProbeTestTool.TheName)
            .WithTitle("Services probe")
            .Selectable();
        services.AddCoreAITool<SystemTestTool>(SystemTestTool.TheName);
        services.AddCoreAITool<HiddenTestTool>(HiddenTestTool.TheName)
            .Selectable()
            .Hidden();

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();

        var evaluator = new Mock<IAIToolAccessEvaluator>();

        evaluator
            .Setup(e => e.IsAuthorizedAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>()))
            .ReturnsAsync((ClaimsPrincipal _, string name) => authorize?.Invoke(name) ?? true);

        var snapshot = new Mock<IOptionsSnapshot<WebMcpOptions>>();

        snapshot
            .Setup(s => s.Value)
            .Returns(options);

        return new DefaultWebMcpToolService(
            provider,
            provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>(),
            snapshot.Object,
            evaluator.Object,
            NullLogger<DefaultWebMcpToolService>.Instance);
    }
}
