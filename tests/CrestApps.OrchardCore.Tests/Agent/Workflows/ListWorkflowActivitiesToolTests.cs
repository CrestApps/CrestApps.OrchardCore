using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Agent.Workflows;

public sealed class ListWorkflowActivitiesToolTests
{
    [Fact]
    public void JsonSchema_ShouldDeclareNoParameters()
    {
        // Arrange
        var tool = new ListWorkflowActivitiesTool();

        // Act
        var properties = tool.JsonSchema.GetProperty("properties");

        // Assert
        Assert.Empty(properties.EnumerateObject());
        Assert.False(tool.JsonSchema.TryGetProperty("required", out _));
    }

    [Fact]
    public async Task InvokeAsync_WithNoArguments_ShouldListActivities()
    {
        // Arrange
        var timerEvent = new Mock<IEvent>();
        timerEvent.Setup(x => x.Name).Returns("TimerEvent");
        timerEvent.Setup(x => x.DisplayText).Returns(new LocalizedString("Timer", "Timer"));
        timerEvent.Setup(x => x.Category).Returns(new LocalizedString("Background", "Background"));

        var notifyTask = new Mock<ITask>();
        notifyTask.Setup(x => x.Name).Returns("NotifyTask");
        notifyTask.Setup(x => x.DisplayText).Returns(new LocalizedString("Notify", "Notify"));
        notifyTask.Setup(x => x.Category).Returns(new LocalizedString("UI", "UI"));

        var services = CreateServiceProvider(timerEvent.Object, notifyTask.Object);

        var tool = new ListWorkflowActivitiesTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>())
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);

        using var document = JsonDocument.Parse(result.ToString());
        var activities = document.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, activities.Length);

        Assert.Equal("TimerEvent", activities[0].GetProperty("Name").GetString());
        Assert.Equal("Timer", activities[0].GetProperty("DisplayText").GetProperty("Value").GetString());
        Assert.Equal("Background", activities[0].GetProperty("Category").GetProperty("Value").GetString());
        Assert.True(activities[0].GetProperty("IsEvent").GetBoolean());
        Assert.False(activities[0].GetProperty("IsTask").GetBoolean());

        Assert.Equal("NotifyTask", activities[1].GetProperty("Name").GetString());
        Assert.False(activities[1].GetProperty("IsEvent").GetBoolean());
        Assert.True(activities[1].GetProperty("IsTask").GetBoolean());
    }

    [Fact]
    public async Task InvokeAsync_WithNoActivities_ShouldReturnMessage()
    {
        // Arrange
        var services = CreateServiceProvider();

        var tool = new ListWorkflowActivitiesTool();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>())
        {
            Services = services,
        };

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("There are no available activities.", result.ToString());
    }

    private static ServiceProvider CreateServiceProvider(params IActivity[] activities)
    {
        var activityLibrary = new Mock<IActivityLibrary>();
        activityLibrary.Setup(x => x.ListActivities()).Returns(activities);

        var services = new ServiceCollection();
        services.AddSingleton<ILogger<ListWorkflowActivitiesTool>>(NullLogger<ListWorkflowActivitiesTool>.Instance);
        services.AddSingleton(activityLibrary.Object);

        return services.BuildServiceProvider();
    }
}
