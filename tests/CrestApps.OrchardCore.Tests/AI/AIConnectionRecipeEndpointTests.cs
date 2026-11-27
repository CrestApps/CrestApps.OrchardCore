using System.Net;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.A2A.Recipes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Recipes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// C13 (review 11): the editor refuses an MCP or A2A connection whose endpoint, or OAuth2 token endpoint, is on the
/// site's own network, and the recipe steps are held to the same rule, as the provider connection, deployment and tool
/// instance steps already are. The steps run with the real <see cref="AIEndpointPolicy"/> over a fixed DNS answer.
/// </summary>
public sealed class AIConnectionRecipeEndpointTests
{
    [Theory]
    [InlineData("http://10.0.0.5/mcp", "https://login.example.com/token")]
    [InlineData("https://mcp.example.com/", "http://169.254.169.254/token")]
    [InlineData("https://mcp.example.com/", "https://inside.example.com/token")]
    public async Task McpConnectionStep_RefusesAnEndpointOnTheSiteOwnNetwork(string endpoint, string tokenEndpoint)
    {
        var manager = new Mock<ISourceCatalogManager<McpConnection>>();
        manager.Setup(x => x.NewAsync(McpConstants.TransportTypes.Sse, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var connection = new McpConnection { Source = McpConstants.TransportTypes.Sse };
                connection.Put(new SseMcpConnectionMetadata { Endpoint = new Uri(endpoint), OAuth2TokenEndpoint = tokenEndpoint });

                return ValueTask.FromResult(connection);
            });
        manager.Setup(x => x.ValidateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var step = CreateMcpStep(manager.Object);
        var context = Context(McpConnectionStep.StepKey, "Connections", new JsonObject { [nameof(McpConnection.Source)] = McpConstants.TransportTypes.Sse });

        await step.ExecuteAsync(context);

        Assert.Contains(context.Errors, error => error.Contains("site's own network", StringComparison.Ordinal));
        manager.Verify(x => x.CreateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task McpConnectionStep_ImportsAPublicConnection()
    {
        var manager = new Mock<ISourceCatalogManager<McpConnection>>();
        manager.Setup(x => x.NewAsync(McpConstants.TransportTypes.Sse, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var connection = new McpConnection { Source = McpConstants.TransportTypes.Sse };
                connection.Put(new SseMcpConnectionMetadata { Endpoint = new Uri("https://mcp.example.com/"), OAuth2TokenEndpoint = "https://login.example.com/token" });

                return ValueTask.FromResult(connection);
            });
        manager.Setup(x => x.ValidateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var step = CreateMcpStep(manager.Object);
        var context = Context(McpConnectionStep.StepKey, "Connections", new JsonObject { [nameof(McpConnection.Source)] = McpConstants.TransportTypes.Sse });

        await step.ExecuteAsync(context);

        Assert.Empty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.IsAny<McpConnection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("http://192.168.1.20/a2a", null)]
    [InlineData("https://agent.example.com/", "http://127.0.0.1:8080/token")]
    public async Task A2AConnectionStep_RefusesAnEndpointOnTheSiteOwnNetwork(string endpoint, string tokenEndpoint)
    {
        var manager = new Mock<ICatalogManager<A2AConnection>>();
        manager.Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var connection = new A2AConnection { Endpoint = endpoint };

                if (tokenEndpoint is not null)
                {
                    connection.Put(new A2AConnectionMetadata { OAuth2TokenEndpoint = tokenEndpoint });
                }

                return ValueTask.FromResult(connection);
            });
        manager.Setup(x => x.ValidateAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var step = CreateStep<A2AConnectionStep>(services => services.AddSingleton(manager.Object));
        var context = Context(A2AConnectionStep.StepKey, "Connections", new JsonObject { [nameof(A2AConnection.Endpoint)] = endpoint });

        await step.ExecuteAsync(context);

        Assert.Contains(context.Errors, error => error.Contains("site's own network", StringComparison.Ordinal));
        manager.Verify(x => x.CreateAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A2AConnectionStep_AnUpdateToTheSiteOwnNetwork_IsRefused_BeforeItIsSaved()
    {
        // Round 2 (Mild): an update was saved by the catalog before the step checked the endpoint.
        var existing = new A2AConnection { ItemId = "a2a-1", Endpoint = "https://agent.example.com/" };
        var manager = new Mock<ICatalogManager<A2AConnection>>();
        manager.Setup(x => x.FindByIdAsync("a2a-1", It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(existing));
        manager.Setup(x => x.ValidateAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var step = CreateStep<A2AConnectionStep>(services => services.AddSingleton(manager.Object));
        var context = Context(A2AConnectionStep.StepKey, "Connections", new JsonObject { [nameof(A2AConnection.ItemId)] = "a2a-1", [nameof(A2AConnection.Endpoint)] = "http://10.0.0.5/a2a" });

        var refused = false;

        try
        {
            await step.ExecuteAsync(context);
            refused = context.Errors.Count > 0;
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("site's own network", StringComparison.Ordinal))
        {
            refused = true;
        }

        Assert.True(refused);
        manager.Verify(x => x.UpdateAsync(It.IsAny<A2AConnection>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A2AConnectionStep_ImportsAPublicConnection()
    {
        var manager = new Mock<ICatalogManager<A2AConnection>>();
        manager.Setup(x => x.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(new A2AConnection { Endpoint = "https://agent.example.com/" }));
        manager.Setup(x => x.ValidateAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>())).Returns(() => ValueTask.FromResult(new ValidationResultDetails()));

        var step = CreateStep<A2AConnectionStep>(services => services.AddSingleton(manager.Object));
        var context = Context(A2AConnectionStep.StepKey, "Connections", new JsonObject { [nameof(A2AConnection.Endpoint)] = "https://agent.example.com/" });

        await step.ExecuteAsync(context);

        Assert.Empty(context.Errors);
        manager.Verify(x => x.CreateAsync(It.IsAny<A2AConnection>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static McpConnectionStep CreateMcpStep(ISourceCatalogManager<McpConnection> manager)
    {
        var mcpOptions = new McpClientAIOptions();
        mcpOptions.AddTransportType(McpConstants.TransportTypes.Sse);

        return CreateStep<McpConnectionStep>(services => services
            .AddSingleton(manager)
            .AddSingleton(Options.Create(mcpOptions))
            .AddSingleton(new ShellSettings { Name = ShellSettings.DefaultShellName })
            .AddSingleton(Mock.Of<IAuthorizationService>())
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("test")) } }));
    }

    // The step is built from services, as Orchard Core builds it.
    private static TStep CreateStep<TStep>(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton<IAIEndpointPolicy>(EndpointPolicy());
        configure(services);

        return ActivatorUtilities.CreateInstance<TStep>(services.BuildServiceProvider());
    }

    // inside.example.com answers with an address on the site's own network; every other name with a public one.
    private static AIEndpointPolicy EndpointPolicy()
        => new(Mock.Of<IHostEnvironment>(), Options.Create(new AIEgressOptions()), host => host.StartsWith("inside.", StringComparison.Ordinal)
            ? [IPAddress.Parse("10.0.0.9")]
            : [IPAddress.Parse("203.0.113.10")]);

    private static RecipeExecutionContext Context(string stepName, string entriesName, JsonObject entry)
        => new()
        {
            Name = stepName,
            Step = new JsonObject { [entriesName] = new JsonArray { entry } },
        };
}
