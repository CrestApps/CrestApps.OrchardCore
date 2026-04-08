using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Memory;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tools;
using CrestApps.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Tools;

public sealed class SaveUserMemoryToolTests
{
    [Fact]
    public async Task InvokeAsync_WhenDescriptionDuplicatesContent_PreservesDescription()
    {
        var store = new Mock<IAIMemoryStore>();
        store.Setup(x => x.FindByUserAndNameAsync("user-1", "user_identity_and_preference"))
            .ReturnsAsync((AIMemoryEntry)null);

        var manager = new Mock<ICatalogManager<AIMemoryEntry>>();
        AIMemoryEntry createdEntry = null;
        manager.Setup(x => x.CreateAsync(It.IsAny<AIMemoryEntry>()))
            .Callback<AIMemoryEntry>(entry => createdEntry = entry)
            .Returns(ValueTask.CompletedTask);

        var tool = new SaveUserMemoryTool();
        var arguments = CreateArguments(
            store.Object,
            manager.Object,
            new Dictionary<string, object>
            {
                ["name"] = "user_identity_and_preference",
                ["description"] = "User's name is Mike Alhayek and he loves OrchardCore",
                ["content"] = "User's name is Mike Alhayek and he loves OrchardCore",
            });

        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.NotNull(createdEntry);
        Assert.Equal("User's name is Mike Alhayek and he loves OrchardCore", createdEntry.Description);

        using var document = JsonDocument.Parse(result.ToString());
        Assert.Equal("User's name is Mike Alhayek and he loves OrchardCore", document.RootElement.GetProperty("Description").GetString());
    }

    [Fact]
    public async Task InvokeAsync_WhenDescriptionIsSemantic_PreservesDescription()
    {
        var store = new Mock<IAIMemoryStore>();
        store.Setup(x => x.FindByUserAndNameAsync("user-1", "user_identity_and_preference"))
            .ReturnsAsync((AIMemoryEntry)null);

        var manager = new Mock<ICatalogManager<AIMemoryEntry>>();
        AIMemoryEntry createdEntry = null;
        manager.Setup(x => x.CreateAsync(It.IsAny<AIMemoryEntry>()))
            .Callback<AIMemoryEntry>(entry => createdEntry = entry)
            .Returns(ValueTask.CompletedTask);

        var tool = new SaveUserMemoryTool();
        var arguments = CreateArguments(
            store.Object,
            manager.Object,
            new Dictionary<string, object>
            {
                ["name"] = "user_identity_and_preference",
                ["description"] = "The user's identity and stated preferences.",
                ["content"] = "User's name is Mike Alhayek and he loves OrchardCore",
            });

        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        Assert.NotNull(createdEntry);
        Assert.Equal("The user's identity and stated preferences.", createdEntry.Description);
    }

    private static AIFunctionArguments CreateArguments(
        IAIMemoryStore store,
        ICatalogManager<AIMemoryEntry> manager,
        Dictionary<string, object> values)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
            ], "Test")),
        };

        var services = new ServiceCollection()
            .AddSingleton<IAIMemorySafetyService, DefaultAIMemorySafetyService>()
            .AddSingleton(store)
            .AddSingleton(manager)
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor
            {
                HttpContext = httpContext,
            })
            .AddSingleton<ILogger<SaveUserMemoryTool>>(_ => NullLogger<SaveUserMemoryTool>.Instance)
            .BuildServiceProvider();

        return new AIFunctionArguments(values)
        {
            Services = services,
        };
    }
}
