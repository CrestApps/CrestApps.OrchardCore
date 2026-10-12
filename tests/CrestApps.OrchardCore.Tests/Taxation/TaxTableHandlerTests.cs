using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Taxation.Handlers;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Modules;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxTableHandlerTests
{
    [Fact]
    public async Task ValidatingAsync_WhenNameMissing_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable { Name = "   " });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenEffectiveEndBeforeStart_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "Valid",
            EffectiveFromUtc = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenRowMaximumNotGreaterThanMinimum_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "Valid",
            Rows =
            [
                new TaxTableRow { Minimum = 100m, Maximum = 100m },
            ],
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenValid_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "US Sales Tax",
            EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m, Rate = 0.05m },
                new TaxTableRow { Minimum = 100m, Maximum = null, Rate = 0.07m },
            ],
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    [Fact]
    public async Task InitializedAsync_SeedsCreatedVersionAndOwner()
    {
        // Arrange
        var clock = new Mock<IClock>();
        var now = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        clock.SetupGet(c => c.UtcNow).Returns(now);

        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Name, "alice"),
            ],
            "test"));

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(a => a.HttpContext).Returns(new DefaultHttpContext { User = user });

        var handler = CreateHandler(clock: clock.Object, httpContextAccessor: httpContextAccessor.Object);
        var model = new TaxTable { Name = "T", Version = 0 };

        // Act
        await handler.InitializedAsync(new InitializedContext<TaxTable>(model), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(now, model.CreatedUtc);
        Assert.Equal(1, model.Version);
        Assert.Equal("user-1", model.OwnerId);
        Assert.Equal("alice", model.Author);
    }

    [Fact]
    public async Task UpdatingAsync_IncrementsVersionAndStampsModified()
    {
        // Arrange
        var clock = new Mock<IClock>();
        var now = new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        clock.SetupGet(c => c.UtcNow).Returns(now);

        var handler = CreateHandler(clock: clock.Object);
        var model = new TaxTable { Name = "T", Version = 2 };

        // Act
        await handler.UpdatingAsync(new UpdatingContext<TaxTable>(model, null), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(3, model.Version);
        Assert.Equal(now, model.ModifiedUtc);
    }

    [Fact]
    public async Task ValidatingAsync_WhenRowsOverlap_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "Overlapping",
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m },
                new TaxTableRow { Minimum = 50m, Maximum = 150m },
            ],
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenMultipleOpenEndedRows_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "TwoOpenEnded",
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = null },
                new TaxTableRow { Minimum = 100m, Maximum = null },
            ],
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenRowMinimumNegative_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "Negative",
            Rows =
            [
                new TaxTableRow { Minimum = -10m, Maximum = 100m },
            ],
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenOpenEndedRowBelowBoundedRange_Fails()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "OpenEndedTooLow",
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 200m },
                new TaxTableRow { Minimum = 100m, Maximum = null },
            ],
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(context.Result.Succeeded);
    }

    [Fact]
    public async Task ValidatingAsync_WhenRowsAreOrderedAndNonOverlapping_Succeeds()
    {
        // Arrange
        var handler = CreateHandler();
        var context = new ValidatingContext<TaxTable>(new TaxTable
        {
            Name = "Ordered",
            Rows =
            [
                new TaxTableRow { Minimum = 0m, Maximum = 100m },
                new TaxTableRow { Minimum = 100m, Maximum = 200m },
                new TaxTableRow { Minimum = 200m, Maximum = null },
            ],
        });

        // Act
        await handler.ValidatingAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(context.Result.Succeeded);
    }

    private static TaxTableHandler CreateHandler(
        IClock clock = null,
        IHttpContextAccessor httpContextAccessor = null)
    {
        return new TaxTableHandler(
            httpContextAccessor ?? new Mock<IHttpContextAccessor>().Object,
            clock ?? new Mock<IClock>().Object,
            new PassThroughStringLocalizer<TaxTableHandler>());
    }
}
