using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Validation;

/// <summary>
/// Pins the queue rules to the handler so a recipe cannot store a queue the editor would have refused.
/// </summary>
public class ActivityQueueHandlerValidationTests
{
    [Fact]
    public async Task ValidatingAsync_WhenTheQueueNamesAKnownGroup_Succeeds()
    {
        // Arrange
        var queue = new ActivityQueue
        {
            Name = "Support",
            QueueGroupId = "known-group",
        };

        // Act
        var context = await ValidateAsync(queue, knownGroupId: "known-group");

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheQueueNamesAnUnknownGroup_Fails()
    {
        // Arrange
        var queue = new ActivityQueue
        {
            Name = "Support",
            QueueGroupId = "missing-group",
        };

        // Act
        var context = await ValidateAsync(queue, knownGroupId: "known-group");

        // Assert
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(ActivityQueue.QueueGroupId)));
    }

    [Fact]
    public async Task ValidatingAsync_WhenTheQueueNamesNoGroup_DoesNotConsultTheGroupCatalog()
    {
        // Arrange
        var queue = new ActivityQueue
        {
            Name = "Support",
            QueueGroupId = null,
        };

        var groupManager = new Mock<IActivityQueueGroupManager>(MockBehavior.Strict);
        var context = new ValidatingContext<ActivityQueue>(queue);

        // Act
        await CreateHandler(groupManager.Object).ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
        groupManager.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidatingAsync_WhenTheNameIsMissing_Fails(string name)
    {
        // Arrange
        var queue = new ActivityQueue
        {
            Name = name,
        };

        // Act
        var context = await ValidateAsync(queue, knownGroupId: "known-group");

        // Assert
        Assert.False(context.Result.Succeeded);
        Assert.Contains(context.Result.Errors, error => error.MemberNames.Contains(nameof(ActivityQueue.Name)));
    }

    private static async Task<ValidatingContext<ActivityQueue>> ValidateAsync(ActivityQueue queue, string knownGroupId)
    {
        var groupManager = new Mock<IActivityQueueGroupManager>();

        groupManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(
                id == knownGroupId
                    ? new ActivityQueueGroup { ItemId = id }
                    : null));

        var context = new ValidatingContext<ActivityQueue>(queue);

        await CreateHandler(groupManager.Object).ValidatingAsync(context, TestContext.Current.CancellationToken);

        return context;
    }

    private static ActivityQueueHandler CreateHandler(IActivityQueueGroupManager groupManager)
    {
        var services = new ServiceCollection();

        services.AddSingleton(groupManager);

        return new ActivityQueueHandler(
            new Mock<IClock>().Object,
            services.BuildServiceProvider(),
            new PassThroughStringLocalizer<ActivityQueueHandler>());
    }
}
