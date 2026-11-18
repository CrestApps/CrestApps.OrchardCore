using System.Security.Claims;
using CrestApps.OrchardCore.BusinessDocuments.Drivers;
using CrestApps.OrchardCore.BusinessDocuments.ViewModels;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;
using LocalizerFactory = CrestApps.OrchardCore.Tests.Modules.Reports.Designer.PassThroughStringLocalizerFactory;
using PassThroughLocalizer = CrestApps.OrchardCore.Tests.Telephony.Doubles.PassThroughStringLocalizer<CrestApps.OrchardCore.BusinessDocuments.Drivers.NumberSeriesSettingsDisplayDriver>;

namespace CrestApps.OrchardCore.Tests.BusinessDocuments;

public sealed class NumberSeriesSettingsDisplayDriverTests
{
    private const string Invoice = "Test.Invoice";
    private const string Visit = "Test.Visit";

    private readonly NumberSeriesOptions _options = new();

    public NumberSeriesSettingsDisplayDriverTests()
    {
        _options.Series[Invoice] = new NumberSeries
        {
            Name = Invoice,
            Title = LocalizationSource.Create<NumberSeriesSettingsDisplayDriverTests>("Invoices"),
            DefaultFormat = new NumberSeriesFormat("INV-{seq:0000}"),
        };
        _options.Series[Visit] = new NumberSeries
        {
            Name = Visit,
            Title = LocalizationSource.Create<NumberSeriesSettingsDisplayDriverTests>("Visits"),
            DefaultFormat = new NumberSeriesFormat("V-{seq}"),
        };
    }

    [Fact]
    public async Task UpdateAsync_StoresAValidFormat_AndNotTheSeriesLeftAtItsRegisteredFormat()
    {
        // Arrange
        var settings = new NumberSeriesSettings();
        var context = PostedFormUpdateModel.CreateContext(Post(
            (Invoice, "INV-{yyyy}-{seq:0000}", 100, NumberSeriesReset.Yearly),
            (Visit, "V-{seq}", 1, NumberSeriesReset.Never)));

        // Act
        await CreateDriver().UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);

        var format = Assert.Single(settings.Formats).Value;

        Assert.True(settings.Formats.ContainsKey(Invoice));
        Assert.Equal("INV-{yyyy}-{seq:0000}", format.Pattern);
        Assert.Equal(100, format.FirstNumber);
        Assert.Equal(NumberSeriesReset.Yearly, format.Reset);
    }

    [Fact]
    public async Task UpdateAsync_RemovesASeriesSetBackToItsRegisteredFormat_AndKeepsSeriesNoFeatureRegisters()
    {
        // Arrange
        var settings = new NumberSeriesSettings();
        settings.Formats[Visit] = new NumberSeriesFormat("VISIT-{seq}");
        settings.Formats["Disabled.Feature"] = new NumberSeriesFormat("D-{seq}");

        var context = PostedFormUpdateModel.CreateContext(Post(
            (Visit, "V-{seq}", 1, NumberSeriesReset.Never),
            ("Unknown.Series", "U-{seq}", 1, NumberSeriesReset.Never)));

        // Act
        await CreateDriver().UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.False(settings.Formats.ContainsKey(Visit));
        Assert.False(settings.Formats.ContainsKey("Unknown.Series"));
        Assert.True(settings.Formats.ContainsKey("Disabled.Feature"));
    }

    [Theory]
    [InlineData("", 1, NumberSeriesReset.Never)]
    [InlineData("INV-{seq}-ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 1, NumberSeriesReset.Never)]
    [InlineData("INV-", 1, NumberSeriesReset.Never)]
    [InlineData("{seq}-{seq}", 1, NumberSeriesReset.Never)]
    [InlineData("INV-{day}-{seq}", 1, NumberSeriesReset.Never)]
    [InlineData("INV-{seq}", 1, NumberSeriesReset.Yearly)]
    [InlineData("INV-{yyyy}-{seq}", 1, NumberSeriesReset.Monthly)]
    [InlineData("INV-{seq}", 0, NumberSeriesReset.Never)]
    public async Task UpdateAsync_RefusesAFormatThatCannotBeUsed_AndChangesNothing(string pattern, long firstNumber, NumberSeriesReset reset)
    {
        // Arrange
        var settings = new NumberSeriesSettings();
        settings.Formats[Visit] = new NumberSeriesFormat("VISIT-{seq}");

        var context = PostedFormUpdateModel.CreateContext(Post(
            (Invoice, pattern, firstNumber, reset),
            (Visit, "V-{seq}", 1, NumberSeriesReset.Never)));

        // Act
        await CreateDriver().UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.NotEqual(NumberSeriesFormatError.None, new NumberSeriesFormat(pattern, firstNumber, reset).Validate());

        var error = Assert.Single(context.Updater.ModelState.Values.SelectMany(value => value.Errors));

        Assert.StartsWith("Invoices:", error.ErrorMessage, StringComparison.Ordinal);
        Assert.False(settings.Formats.ContainsKey(Invoice));
        Assert.Equal("VISIT-{seq}", settings.Formats[Visit].Pattern);
    }

    [Fact]
    public async Task UpdateAsync_ChangesNothing_WhenTheUserMayNotManageNumberSeries()
    {
        // Arrange
        var settings = new NumberSeriesSettings();
        var context = PostedFormUpdateModel.CreateContext(Post((Invoice, "X-{seq}", 1, NumberSeriesReset.Never)));

        // Act
        var result = await CreateDriver(allowed: false).UpdateAsync(Mock.Of<ISite>(), settings, context);

        // Assert
        Assert.Null(result);
        Assert.Empty(settings.Formats);
    }

    [Fact]
    public async Task Edit_ListsEverySeries_WithAnExampleWrittenWithToday()
    {
        // Arrange
        var settings = new NumberSeriesSettings();
        settings.Formats[Invoice] = new NumberSeriesFormat("INV-{yyyy}-{seq:0000}", 7, NumberSeriesReset.Yearly);

        // Act
        var model = Assert.Single(await DisplayResultModels.BuildAsync<NumberSeriesSettingsViewModel>(CreateDriver().Edit(Mock.Of<ISite>(), settings, null)));

        // Assert
        Assert.Equal("2026-11-13", model.Today);
        Assert.Equal(2, model.Series.Count);

        var invoice = Assert.Single(model.Series, series => series.Name == Invoice);
        var visit = Assert.Single(model.Series, series => series.Name == Visit);

        Assert.Equal("Invoices", invoice.Title);
        Assert.Equal("INV-2026-0007", invoice.Example);
        Assert.Equal("INV-{seq:0000}", invoice.DefaultPattern);
        Assert.True(invoice.IsCustomized);
        Assert.Equal("V-1", visit.Example);
        Assert.False(visit.IsCustomized);
    }

    private NumberSeriesSettingsDisplayDriver CreateDriver(bool allowed = true)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var localClock = new Mock<ILocalClock>();

        localClock.Setup(clock => clock.GetLocalNowAsync()).ReturnsAsync(new DateTimeOffset(2026, 11, 13, 9, 30, 0, TimeSpan.FromHours(-8)));

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity("Test")) },
        };

        return new NumberSeriesSettingsDisplayDriver(
            httpContextAccessor,
            authorizationService.Object,
            localClock.Object,
            new LocalizerFactory(),
            Options.Create(_options),
            new PassThroughLocalizer());
    }

    private static NumberSeriesSettingsViewModel Post(params (string Name, string Pattern, long FirstNumber, NumberSeriesReset Reset)[] series)
        => new()
        {
            Series = series
                .Select(entry => new NumberSeriesViewModel
                {
                    Name = entry.Name,
                    Pattern = entry.Pattern,
                    FirstNumber = entry.FirstNumber,
                    Reset = entry.Reset,
                })
                .ToList(),
        };
}
