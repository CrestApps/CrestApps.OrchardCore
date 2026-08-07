using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class InteractionHandlerTests
{
    private static readonly DateTime _now = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InitializedAsync_SetsCreatedUtc_AndGeneratesCorrelationId()
    {
        // Arrange
        var handler = CreateHandler();
        var interaction = new Interaction();

        // Act
        await handler.InitializedAsync(new InitializedContext<Interaction>(interaction), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, interaction.CreatedUtc);
        Assert.False(string.IsNullOrEmpty(interaction.CorrelationId));
    }

    [Fact]
    public async Task InitializedAsync_WhenCorrelationIdIsProvided_DoesNotOverwriteIt()
    {
        // Arrange
        var handler = CreateHandler();
        var interaction = new Interaction
        {
            CorrelationId = "correlation-1",
        };

        // Act
        await handler.InitializedAsync(new InitializedContext<Interaction>(interaction), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("correlation-1", interaction.CorrelationId);
    }

    [Fact]
    public async Task UpdatingAsync_SetsModifiedUtc()
    {
        // Arrange
        var handler = CreateHandler();
        var interaction = new Interaction();

        // Act
        await handler.UpdatingAsync(new UpdatingContext<Interaction>(interaction, new JsonObject()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_now, interaction.ModifiedUtc);
    }

    private static InteractionHandler CreateHandler()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new InteractionHandler(Mock.Of<IHttpContextAccessor>(), clock.Object);
    }
}
