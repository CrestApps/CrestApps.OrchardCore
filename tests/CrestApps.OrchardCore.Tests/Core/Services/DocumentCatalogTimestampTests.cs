using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Core.Services;

/// <summary>
/// A store built on the <see cref="DocumentCatalog{T, TIndex}"/> base with a clock stamps the created and updated
/// dates of a timestamped record on every save, so stores no longer copy the same saving hook.
/// </summary>
public sealed class DocumentCatalogTimestampTests
{
    private static readonly DateTime _firstSave = new(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _secondSave = new(2026, 11, 4, 17, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Create_StampsTheCreatedAndUpdatedDates()
    {
        // Arrange
        var clock = Clock(_firstSave);
        var store = new CouponStore(Mock.Of<ISession>(), clock.Object);
        var coupon = new Coupon { Code = "WELCOME" };

        // Act
        await store.CreateAsync(coupon, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_firstSave, coupon.CreatedUtc);
        Assert.Equal(_firstSave, coupon.UpdatedUtc);
    }

    [Fact]
    public async Task Update_KeepsTheCreatedDateAndMovesTheUpdatedDate()
    {
        // Arrange
        var clock = Clock(_firstSave);
        var store = new CouponStore(Mock.Of<ISession>(), clock.Object);
        var coupon = new Coupon { Code = "WELCOME" };
        await store.CreateAsync(coupon, TestContext.Current.CancellationToken);
        clock.Setup(value => value.UtcNow).Returns(_secondSave);

        // Act
        await store.UpdateAsync(coupon, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(_firstSave, coupon.CreatedUtc);
        Assert.Equal(_secondSave, coupon.UpdatedUtc);
    }

    [Fact]
    public async Task Create_KeepsACreatedDateTheRecordAlreadyHas()
    {
        // Arrange
        var imported = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var store = new CouponStore(Mock.Of<ISession>(), Clock(_firstSave).Object);
        var coupon = new Coupon { Code = "WELCOME", CreatedUtc = imported };

        // Act
        await store.CreateAsync(coupon, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(imported, coupon.CreatedUtc);
        Assert.Equal(_firstSave, coupon.UpdatedUtc);
    }

    [Fact]
    public async Task ACatalogWithoutAClock_LeavesTheDatesAlone()
    {
        // Arrange
        var store = new UnstampedCouponCatalog(Mock.Of<ISession>());
        var coupon = new Coupon { Code = "WELCOME" };

        // Act
        await store.CreateAsync(coupon, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(default, coupon.CreatedUtc);
        Assert.Equal(default, coupon.UpdatedUtc);
    }

    private static Mock<IClock> Clock(DateTime now)
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(now);

        return clock;
    }

    private sealed class UnstampedCouponCatalog : DocumentCatalog<Coupon, CouponIndex>
    {
        public UnstampedCouponCatalog(ISession session)
            : base(session)
        {
        }
    }
}
