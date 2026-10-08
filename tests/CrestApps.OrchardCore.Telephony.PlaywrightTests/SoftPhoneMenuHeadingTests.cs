using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests;

/// <summary>
/// The presence menu in the soft phone header heads its break reasons with "Break" (or "Request break"). It was drawn
/// as a grey line the size of an item between the items, and an agent read it as a disabled Break choice. It is now a
/// section label; this test holds it to looking like one and to staying out of the way of the items it names.
/// </summary>
public sealed partial class SoftPhoneMenuHeadingTests : SoftPhoneBrowserTest
{
    [Fact]
    public async Task PresenceMenuBreakHeading_RendersAsANonInteractiveSectionLabel()
    {
        // Arrange - the heading and its group exactly as the Contact Center header view writes them.
        var view = await File.ReadAllTextAsync(
            Path.Combine(SoftPhoneAssets.ModuleDirectory, "..", "CrestApps.OrchardCore.ContactCenter", "Views", "Items", "ContactCenterSoftPhonePresence.Header.cshtml"),
            TestContext.Current.CancellationToken);
        var heading = HeadingPattern().Match(view);
        var group = GroupPattern().Match(view);

        Assert.True(heading.Success, "The presence header no longer writes its break heading as a menu section label.");
        Assert.True(group.Success, "The presence header no longer groups the break reasons under their heading.");
        Assert.DoesNotContain("dropdown-header", view, StringComparison.Ordinal);
        Assert.Contains("role=\"presentation\"", heading.Value, StringComparison.Ordinal);
        Assert.Contains($"id=\"{group.Groups["id"].Value}\"", heading.Value, StringComparison.Ordinal);

        var fixture =
            "<div class=\"dropdown-menu\" data-test-presence-menu>" +
            "<button type=\"button\" class=\"dropdown-item\">Available</button>" +
            group.Value +
            Regex.Replace(heading.Value, "@\\w+", string.Empty) + "Break</div>" +
            "<button type=\"button\" class=\"dropdown-item\">Lunch</button>" +
            "<button type=\"button\" class=\"dropdown-item\">Personal</button>" +
            "</div></div>";

        var page = await OpenAsync("?styled", DesktopAppViewport);

        // Act
        await page.EvaluateAsync(
            "([html]) => document.querySelector('#telephony-soft-phone').insertAdjacentHTML('afterbegin', html)",
            new[] { fixture });

        // Assert - a small uppercase label, not an item-sized grey line, and nothing that takes a click or focus.
        var label = page.Locator("[data-test-presence-menu] .telephony-soft-phone__menu-heading");
        var style = await label.EvaluateAsync<JsonElement>(
            """
            element => {
                const heading = getComputedStyle(element);
                const item = getComputedStyle(document.querySelector('[data-test-presence-menu] .dropdown-item'));
                return {
                    textTransform: heading.textTransform,
                    pointerEvents: heading.pointerEvents,
                    cursor: heading.cursor,
                    smaller: String(parseFloat(heading.fontSize) < parseFloat(item.fontSize)),
                    tabIndex: String(element.tabIndex)
                };
            }
            """);
        Assert.Equal("uppercase", style.GetProperty("textTransform").GetString());
        Assert.Equal("none", style.GetProperty("pointerEvents").GetString());
        Assert.Equal("default", style.GetProperty("cursor").GetString());
        Assert.Equal("true", style.GetProperty("smaller").GetString());
        Assert.Equal("-1", style.GetProperty("tabIndex").GetString());

        // Assert - it names its reasons for assistive technology instead of posing as one of them.
        var reasons = page.GetByRole(AriaRole.Group, new PageGetByRoleOptions { Name = "Break" });
        Assert.Equal(2, await reasons.GetByRole(AriaRole.Button).CountAsync());
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Break", Exact = true }).CountAsync());
        await CaptureAsync(page, "presence-menu-heading");
    }

    [Fact]
    public async Task AgentWorkspacePresenceMenuBreakHeading_NamesItsReasonsInsteadOfPosingAsOne()
    {
        // Arrange - the agent workspace draws the same menu with its own markup and stylesheet.
        var view = await File.ReadAllTextAsync(
            Path.Combine(SoftPhoneAssets.ModuleDirectory, "..", "CrestApps.OrchardCore.ContactCenter", "Views", "AgentWorkspace", "Index.cshtml"),
            TestContext.Current.CancellationToken);

        // Act
        var heading = WorkspaceHeadingPattern().Match(view);
        var group = GroupPattern().Match(view);

        // Assert
        Assert.True(heading.Success, "The agent workspace no longer writes its break heading as a menu section label.");
        Assert.True(group.Success, "The agent workspace no longer groups the break reasons under their heading.");
        Assert.Contains("role=\"presentation\"", heading.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("menuitem", heading.Value, StringComparison.Ordinal);
        Assert.Contains($"id=\"{group.Groups["id"].Value}\"", heading.Value, StringComparison.Ordinal);
    }

    [GeneratedRegex("<div class=\"cc-menu__heading\"[^>]*>")]
    private static partial Regex WorkspaceHeadingPattern();

    [GeneratedRegex("<div class=\"telephony-soft-phone__menu-heading\"[^>]*>")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex("<div role=\"group\" aria-labelledby=\"(?<id>[^\"]+)\">")]
    private static partial Regex GroupPattern();
}
