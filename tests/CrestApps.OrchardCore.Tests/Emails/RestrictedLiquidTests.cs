using System.Globalization;
using System.Text.Encodings.Web;
using CrestApps.OrchardCore.Core.Liquid;
using CrestApps.OrchardCore.Tests.Doubles;
using Fluid;
using Fluid.Values;

namespace CrestApps.OrchardCore.Tests.Emails;

/// <summary>
/// Restricted Liquid shows a reader's time: a stored UTC time goes through <c>date</c>, <c>format_date</c> and
/// <c>local</c> in the site's time zone; <c>t</c> localizes with arguments as Orchard's filter does; the standard filters
/// an existing template uses render as before; and a template using a filter that isn't available is refused on save
/// and not rendered, so its output never changes silently.
/// </summary>
public sealed class RestrictedLiquidTests
{
    private static readonly DateTime _createdUtc = new(2026, 11, 14, 13, 5, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AStoredUtcTime_IsShownInTheSitesTimeZone()
    {
        // Arrange: Paris is an hour ahead of UTC in November.
        var context = await RestrictedLiquidContexts.CreateAsync(LiquidTestClocks.In("Europe/Paris"));
        var values = Values(context);

        // Act
        var date = await RestrictedLiquid.RenderAsync("{{ Order.CreatedUtc | date: '%H:%M' }}", values, NullEncoder.Default, context);
        var formatted = await RestrictedLiquid.RenderAsync("{{ Order.CreatedUtc | format_date: 'HH:mm' }}", values, NullEncoder.Default, context);
        var local = await RestrictedLiquid.RenderAsync("{{ Order.CreatedUtc | local | date: '%H:%M' }}", values, NullEncoder.Default, context);

        // Assert
        Assert.Equal("14:05", date);
        Assert.Equal("14:05", formatted);
        Assert.Equal("14:05", local);
    }

    [Fact]
    public async Task T_LocalizesWithItsArguments()
    {
        // Arrange
        var context = new RestrictedLiquidContext
        {
            TimeZone = TimeZoneInfo.Utc,
            Culture = CultureInfo.InvariantCulture,
            Localize = (text, arguments) => text == "Hello {0}" ? string.Format(CultureInfo.InvariantCulture, "Bonjour {0}", arguments) : text,
        };

        // Act
        var text = await RestrictedLiquid.RenderAsync("{{ 'Hello {0}' | t: Name }}", [new("Name", StringValue.Create("Sam"))], NullEncoder.Default, context);

        // Assert
        Assert.Equal("Bonjour Sam", text);
    }

    [Fact]
    public async Task AnExistingTemplateWithTheStandardFilters_RendersAsBefore()
    {
        // Arrange
        const string Source = "{{ Subject | upcase }} at {{ SiteName | escape }} ({{ Lines | size }} lines, {{ Total | round: 2 }})";
        KeyValuePair<string, FluidValue>[] values =
        [
            new("Subject", StringValue.Create("Your order")),
            new("SiteName", StringValue.Create("Contoso & Co")),
            new("Lines", FluidValue.Create(new[] { 1, 2, 3 }, RestrictedLiquid.Options)),
            new("Total", NumberValue.Create(12.345m)),
        ];

        // Act
        var text = await RestrictedLiquid.RenderAsync(Source, values, HtmlEncoder.Default);

        // Assert
        Assert.True(RestrictedLiquid.TryValidate(Source, out _));
        Assert.Equal("YOUR ORDER at Contoso &amp; Co (3 lines, 12.35)", text);
    }

    [Fact]
    public async Task AFilterThatIsntAvailable_IsRefusedOnSave_AndNotRendered()
    {
        // Act
        var valid = RestrictedLiquid.TryValidate("{{ Body | markdownify }}", out var error);
        var rendered = await RestrictedLiquid.RenderAsync("{{ Body | markdownify }}", [new("Body", StringValue.Create("*hi*"))], NullEncoder.Default);

        // Assert
        Assert.False(valid);
        Assert.Contains("markdownify", error);
        Assert.Null(rendered);
        Assert.Contains("t", RestrictedLiquid.FilterNames);
        Assert.Contains("date", RestrictedLiquid.FilterNames);
    }

    private static KeyValuePair<string, FluidValue>[] Values(RestrictedLiquidContext context)
        => [new("Order", LiquidModelValues.Create(new { CreatedUtc = _createdUtc }, RestrictedLiquid.Options))];
}
