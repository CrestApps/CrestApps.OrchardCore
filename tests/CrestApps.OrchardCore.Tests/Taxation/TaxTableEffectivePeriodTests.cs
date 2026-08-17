using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class TaxTableEffectivePeriodTests
{
    private static readonly DateTime Transaction = new(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsEffectiveOn_WhenNoWindow_IsAlwaysEffective()
    {
        var table = new TaxTable { Name = "Open" };

        Assert.True(TaxService.IsEffectiveOn(table, Transaction));
    }

    [Fact]
    public void IsEffectiveOn_WhenWithinWindow_IsEffective()
    {
        var table = new TaxTable
        {
            Name = "Windowed",
            EffectiveFromUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        };

        Assert.True(TaxService.IsEffectiveOn(table, Transaction));
    }

    [Fact]
    public void IsEffectiveOn_WhenBeforeStart_IsNotEffective()
    {
        var table = new TaxTable
        {
            Name = "Future",
            EffectiveFromUtc = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        Assert.False(TaxService.IsEffectiveOn(table, Transaction));
    }

    [Fact]
    public void IsEffectiveOn_WhenAfterEnd_IsNotEffective()
    {
        var table = new TaxTable
        {
            Name = "Expired",
            EffectiveToUtc = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        Assert.False(TaxService.IsEffectiveOn(table, Transaction));
    }

    [Fact]
    public void IsEffectiveOn_WhenExactlyAtEnd_IsNotEffective()
    {
        var end = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var table = new TaxTable
        {
            Name = "EndsToday",
            EffectiveToUtc = end,
        };

        Assert.False(TaxService.IsEffectiveOn(table, end));
    }

    [Fact]
    public void IsEffectiveOn_WhenExactlyAtStart_IsEffective()
    {
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var table = new TaxTable
        {
            Name = "StartsToday",
            EffectiveFromUtc = start,
        };

        Assert.True(TaxService.IsEffectiveOn(table, start));
    }
}
