using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.YesSql.Core.Numbering;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Core.Numbering;

public sealed class NumberSeriesServiceTests
{
    private const string Series = "Tests.Invoice";

    private readonly InMemoryNumberSequence _sequence = new();
    private readonly NumberSeriesOptions _options = new();
    private readonly NumberSeriesSettings _settings = new();
    private DateTimeOffset _now = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NextAsync_UsesTheRegisteredFormat_AndItsFirstNumber()
    {
        // Arrange
        Register(new NumberSeriesFormat("INV-{seq}", firstNumber: 1001));

        var service = CreateService();

        // Act
        var first = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);
        var second = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new SeriesNumber(1001, "INV-1001"), first);
        Assert.Equal(new SeriesNumber(1002, "INV-1002"), second);
    }

    [Fact]
    public async Task NextAsync_UsesTheFormatTheSiteChose_OverTheRegisteredOne()
    {
        // Arrange
        Register(new NumberSeriesFormat("INV-{seq}"));
        _settings.Formats[Series] = new NumberSeriesFormat("F{yyyy}-{seq:0000}");

        var service = CreateService();

        // Act
        var number = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("F2026-0001", number.Text);
    }

    [Fact]
    public async Task NextAsync_IgnoresAChosenFormatThatCannotBeUsed()
    {
        // Arrange
        Register(new NumberSeriesFormat("INV-{seq}"));
        _settings.Formats[Series] = new NumberSeriesFormat("no number here");

        var service = CreateService();

        // Act
        var number = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("INV-1", number.Text);
    }

    [Fact]
    public async Task NextAsync_ForASeriesNobodyRegistered_WritesTheBareNumber()
    {
        // Arrange
        var service = CreateService();

        // Act
        var number = await service.NextAsync("Tests.Unknown", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(new SeriesNumber(1, "1"), number);
    }

    [Fact]
    public async Task NextAsync_WithAYearlyReset_StartsAgainEachYear()
    {
        // Arrange
        Register(new NumberSeriesFormat("{yyyy}-{seq:000}", reset: NumberSeriesReset.Yearly));

        var service = CreateService();

        // Act
        var first = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);
        var second = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);

        _now = new DateTimeOffset(2027, 1, 2, 9, 0, 0, TimeSpan.Zero);

        var nextYear = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("2026-001", first.Text);
        Assert.Equal("2026-002", second.Text);
        Assert.Equal("2027-001", nextYear.Text);
    }

    [Fact]
    public async Task NextAsync_WithADate_UsesItForTheTokensAndThePeriod()
    {
        // Arrange
        Register(new NumberSeriesFormat("{yy}{MM}-{seq}", reset: NumberSeriesReset.Monthly));

        var service = CreateService();

        // Act
        var number = await service.NextAsync(Series, new NumberSeriesRequest { Date = new DateOnly(2025, 2, 28) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("2502-1", number.Text);
    }

    [Fact]
    public async Task NextAsync_WithAScope_CountsTheScopeOnItsOwn()
    {
        // Arrange
        Register(new NumberSeriesFormat("INV-{seq}"));

        var service = CreateService();

        // Act
        var main = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);
        var branch = await service.NextAsync(Series, new NumberSeriesRequest { Scope = "North" }, TestContext.Current.CancellationToken);
        var mainAgain = await service.NextAsync(Series, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, main.Sequence);
        Assert.Equal(1, branch.Sequence);
        Assert.Equal(2, mainAgain.Sequence);
    }

    [Fact]
    public async Task NextAsync_SeedsANewSeriesFromTheNumbersAlreadyInUse()
    {
        // Arrange
        Register(new NumberSeriesFormat("INV-{seq}", firstNumber: 1001));

        var service = CreateService();

        // Act
        var number = await service.NextAsync(Series, new NumberSeriesRequest { SeedAsync = _ => Task.FromResult(1500L) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("INV-1501", number.Text);
    }

    [Fact]
    public async Task NextAsync_WithAReset_DoesNotSeedTheNewPeriod()
    {
        // Arrange
        Register(new NumberSeriesFormat("{yyyy}-{seq}", reset: NumberSeriesReset.Yearly));

        var service = CreateService();

        // Act
        var number = await service.NextAsync(Series, new NumberSeriesRequest { SeedAsync = _ => Task.FromResult(1500L) }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("2026-1", number.Text);
    }

    private void Register(NumberSeriesFormat format)
        => _options.Series[Series] = new NumberSeries
        {
            Name = Series,
            Title = LocalizationSource.Create<NumberSeriesServiceTests>("Test invoices"),
            DefaultFormat = format,
        };

    private NumberSeriesService CreateService()
    {
        var site = new SiteSettings();

        site.Put(_settings);

        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var localClock = new Mock<ILocalClock>();

        localClock.Setup(clock => clock.GetLocalNowAsync()).ReturnsAsync(() => _now);

        return new NumberSeriesService(_sequence, siteService.Object, localClock.Object, Options.Create(_options));
    }
}
