using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterAssistServiceTests
{
    [Fact]
    public void IsAvailable_ReflectsRegisteredProviders()
    {
        // Arrange & Act
        var withProvider = new ContactCenterAssistService([new Mock<IContactCenterAssistProvider>().Object]);
        var without = new ContactCenterAssistService([]);

        // Assert
        Assert.True(withProvider.IsAvailable);
        Assert.False(without.IsAvailable);
    }

    [Fact]
    public async Task SuggestDispositionAsync_ReturnsFirstProviderSuggestionByOrder()
    {
        // Arrange
        var context = new AssistContext { InteractionId = "int1" };

        var lowPriority = new Mock<IContactCenterAssistProvider>();
        lowPriority.SetupGet(p => p.Order).Returns(100);
        lowPriority.Setup(p => p.SuggestDispositionAsync(context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DispositionSuggestion { DispositionId = "low" });

        var highPriority = new Mock<IContactCenterAssistProvider>();
        highPriority.SetupGet(p => p.Order).Returns(10);
        highPriority.Setup(p => p.SuggestDispositionAsync(context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DispositionSuggestion { DispositionId = "high" });

        var service = new ContactCenterAssistService([lowPriority.Object, highPriority.Object]);

        // Act
        var suggestion = await service.SuggestDispositionAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("high", suggestion.DispositionId);
    }

    [Fact]
    public async Task SuggestDispositionAsync_WhenNoProviderHasSuggestion_ReturnsNull()
    {
        // Arrange
        var context = new AssistContext { InteractionId = "int1" };
        var provider = new Mock<IContactCenterAssistProvider>();
        provider.Setup(p => p.SuggestDispositionAsync(context, It.IsAny<CancellationToken>())).ReturnsAsync((DispositionSuggestion)null);

        var service = new ContactCenterAssistService([provider.Object]);

        // Act
        var suggestion = await service.SuggestDispositionAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(suggestion);
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsFirstNonEmptySummary()
    {
        // Arrange
        var context = new AssistContext { InteractionId = "int1" };

        var empty = new Mock<IContactCenterAssistProvider>();
        empty.SetupGet(p => p.Order).Returns(10);
        empty.Setup(p => p.SummarizeAsync(context, It.IsAny<CancellationToken>())).ReturnsAsync((string)null);

        var withSummary = new Mock<IContactCenterAssistProvider>();
        withSummary.SetupGet(p => p.Order).Returns(20);
        withSummary.Setup(p => p.SummarizeAsync(context, It.IsAny<CancellationToken>())).ReturnsAsync("A concise summary.");

        var service = new ContactCenterAssistService([empty.Object, withSummary.Object]);

        // Act
        var summary = await service.SummarizeAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("A concise summary.", summary);
    }
}
