using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.AI;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using AgreementsAIToolsStartup = CrestApps.OrchardCore.Agreements.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Agreements.AI;

/// <summary>
/// <c>getAgreementBalance</c> gives how an agreement's hours stand, as its editor and the account's Agreements tab show
/// them: only for a signed-in person who may view agreements, by ID, number or account, with the period's day kept
/// within the agreement as the reports keep it, the entries counted on request and what people wrote marked as data. It
/// never asks for approval.
/// </summary>
public sealed class GetAgreementBalanceToolTests
{
    [Fact]
    public async Task ACallerWhoIsNotSignedIn_OrMayNotViewAgreements_IsRefused()
    {
        // Arrange
        await using var kit = await CreateAsync();

        // Act
        var anonymous = await InvokeAsync(kit, new ClaimsPrincipal(new ClaimsIdentity()), new() { ["accountId"] = "contoso" });
        var other = await InvokeAsync(kit, CalendarTestHarness.User("ana", "SomethingElse"), new() { ["accountId"] = "contoso" });

        // Assert
        Assert.Equal(GetAgreementBalanceTool.SignInRequired, anonymous);
        Assert.Equal(GetAgreementBalanceTool.NotAllowed, other);
    }

    [Fact]
    public async Task ExactlyOneWayToNameTheAgreement_IsNeeded()
    {
        // Arrange
        await using var kit = await CreateAsync();

        // Act
        var none = await InvokeAsync(kit, Viewer(), new());
        var two = await InvokeAsync(kit, Viewer(), new() { ["agreementId"] = "a", ["accountId"] = "contoso" });

        // Assert
        Assert.Equal("Give one of agreementId, agreementNumber or accountId.", none);
        Assert.Equal(none, two);
    }

    [Fact]
    public async Task AnAgreement_GivesItsHoursCoveredUsedAndLeft_WithTheEntriesNewestFirst()
    {
        // Arrange
        await using var kit = await CreateAsync();
        var agreement = await kit.CreateAsync(item =>
        {
            item.Name = "Gold support";
            item.AccountId = "contoso";
            item.Status = AgreementStatus.Active;
            item.StartDate = new DateOnly(2026, 1, 1);
            item.Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 10, RoundingMinutes = 15 };
            item.HoursWarnings = new AgreementHoursWarnings { Percents = [80] };
        });
        kit.Usage.Add((agreement.ItemId, new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc), 50));
        kit.Usage.Add((agreement.ItemId, new DateTime(2026, 11, 9, 15, 0, 0, DateTimeKind.Utc), 20));

        // Act
        var json = await InvokeAsync(kit, Viewer(), new() { ["agreementId"] = agreement.ItemId, ["includeEntries"] = true });
        var result = JsonElement.Parse(json);
        var described = result.GetProperty("agreement");
        var balance = described.GetProperty("balance");

        // Assert
        Assert.Equal(agreement.Number, described.GetProperty("number").GetString());
        Assert.Equal("Contoso Ltd", described.GetProperty("account").GetProperty("name").GetString());
        Assert.Equal("block", described.GetProperty("hoursKind").GetString());
        Assert.True(described.GetProperty("inForceToday").GetBoolean());
        Assert.Equal("2026-01-01", balance.GetProperty("periodStart").GetString());
        Assert.Equal(10m, balance.GetProperty("coveredHours").GetDecimal());
        Assert.Equal(1.5m, balance.GetProperty("usedHours").GetDecimal());
        Assert.Equal(8.5m, balance.GetProperty("leftHours").GetDecimal());
        Assert.Equal(0m, balance.GetProperty("overageHours").GetDecimal());
        Assert.Equal(15, balance.GetProperty("usedPercent").GetDouble());

        var entries = described.GetProperty("entries");
        Assert.Equal(["2026-11-09T15:00:00+00:00", "2026-11-02T15:00:00+00:00"], entries.EnumerateArray().Select(entry => entry.GetProperty("when").GetString()));
        Assert.Equal(30, entries[0].GetProperty("roundedMinutes").GetDouble());
        Assert.Equal(GetAgreementBalanceTool.UntrustedNote, result.GetProperty("note").GetString());

        // The bookkeeping of the notices and other features' terms are never returned.
        Assert.DoesNotContain("hoursWarnings", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("properties", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnAgreement_IsFoundByItsNumber_WhateverItsCase_OrByItsDigits()
    {
        // Arrange
        await using var kit = await CreateAsync();
        var agreement = await kit.CreateAsync(item => item.Name = "Bronze");

        // Act
        var result = JsonElement.Parse(await InvokeAsync(kit, Viewer(), new() { ["agreementNumber"] = agreement.Number.ToLowerInvariant() }));
        var byDigits = JsonElement.Parse(await InvokeAsync(kit, Viewer(), new() { ["agreementNumber"] = "1" }));
        var unknown = await InvokeAsync(kit, Viewer(), new() { ["agreementNumber"] = "SA-99999" });

        // Assert
        Assert.Equal(agreement.ItemId, result.GetProperty("agreement").GetProperty("id").GetString());
        Assert.Equal(agreement.ItemId, byDigits.GetProperty("agreement").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("agreement").GetProperty("balance").ValueKind);
        Assert.Equal("The agreement covers no hours.", result.GetProperty("agreement").GetProperty("balanceNote").GetString());
        Assert.StartsWith("No agreement has that ID or number.", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADayBeforeTheStart_IsRefused_AndADayAfterTheEnd_IsTheLastDay()
    {
        // Arrange
        await using var kit = await CreateAsync();
        var agreement = await kit.CreateAsync(item =>
        {
            item.Name = "Spring allowance";
            item.Status = AgreementStatus.Expired;
            item.StartDate = new DateOnly(2026, 1, 1);
            item.EndDate = new DateOnly(2026, 6, 30);
            item.Hours = new AgreementHours { Kind = AgreementHoursKind.MonthlyAllowance, Hours = 5 };
        });

        // Act
        var before = await InvokeAsync(kit, Viewer(), new() { ["agreementId"] = agreement.ItemId, ["date"] = "2025-12-31" });
        var after = JsonElement.Parse(await InvokeAsync(kit, Viewer(), new() { ["agreementId"] = agreement.ItemId, ["date"] = "2026-09-15" }));
        var balance = after.GetProperty("agreement").GetProperty("balance");

        // Assert
        Assert.StartsWith($"Agreement {agreement.Number} starts on 2026-01-01", before, StringComparison.Ordinal);
        Assert.Equal("2026-06-30", balance.GetProperty("clampedTo").GetString());
        Assert.Equal("2026-06-01", balance.GetProperty("periodStart").GetString());
        Assert.Equal(5m, balance.GetProperty("coveredHours").GetDecimal());
    }

    [Fact]
    public async Task AnAccount_ListsItsAgreementsNewestFirst_WithTheBalanceOfTheActiveOnesOnly()
    {
        // Arrange
        await using var kit = await CreateAsync();
        await kit.CreateAsync(item => { item.Name = "Old"; item.AccountId = "contoso"; item.Status = AgreementStatus.Expired; item.StartDate = new DateOnly(2025, 1, 1); item.EndDate = new DateOnly(2025, 12, 31); item.Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 4 }; });
        await kit.CreateAsync(item => { item.Name = "Current"; item.AccountId = "contoso"; item.Status = AgreementStatus.Active; item.StartDate = new DateOnly(2026, 1, 1); item.Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 8 }; });
        await kit.CreateAsync(item => { item.Name = "Elsewhere"; item.AccountId = "fabrikam"; item.Status = AgreementStatus.Active; item.StartDate = new DateOnly(2026, 1, 1); });

        // Act
        var result = JsonElement.Parse(await InvokeAsync(kit, Viewer(), new() { ["accountId"] = "contoso" }));
        var agreements = result.GetProperty("agreements");

        // Assert
        Assert.Equal("Contoso Ltd", result.GetProperty("account").GetProperty("name").GetString());
        Assert.Equal(["Current", "Old"], agreements.EnumerateArray().Select(agreement => agreement.GetProperty("name").GetString()));
        Assert.Equal(8m, agreements[0].GetProperty("balance").GetProperty("coveredHours").GetDecimal());
        Assert.Equal(JsonValueKind.Null, agreements[1].GetProperty("balance").ValueKind);
        Assert.False(result.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task AnAccountsName_IsGivenOnlyForAnAccountTheCallerMayView()
    {
        // Arrange
        await using var kit = await CreateAsync();

        // Act
        var notAnAccount = await InvokeAsync(kit, Viewer(), new() { ["accountId"] = "minutes" });
        var notViewable = JsonElement.Parse(await InvokeAsync(kit, CalendarTestHarness.User("ana", AgreementsPermissions.ViewAgreements.Name), new() { ["accountId"] = "contoso" }));
        var viewable = JsonElement.Parse(await InvokeAsync(kit, Viewer(), new() { ["accountId"] = "contoso" }));

        // Assert
        Assert.DoesNotContain("Board minutes", notAnAccount, StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, JsonElement.Parse(notAnAccount).GetProperty("account").GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Null, notViewable.GetProperty("account").GetProperty("name").ValueKind);
        Assert.Equal("Contoso Ltd", viewable.GetProperty("account").GetProperty("name").GetString());
    }

    [Fact]
    public void TheTool_IsRegisteredWithItsTexts_AndAsksNoApproval()
    {
        // Act
        var tools = WorkforceTimeOffAdminAIToolTests.GetTools(typeof(AgreementsAIToolsStartup));

        // Assert
        var tool = Assert.Single(tools).Value;
        Assert.Equal(GetAgreementBalanceTool.TheName, Assert.Single(tools).Key);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Agreements", tool.Category);
        Assert.False(tool.RequiresApproval);
    }

    internal static Task<AgreementsTests.Kit> CreateAsync()
        => AgreementsTests.Kit.CreateAsync(services =>
        {
            var accounts = new Mock<IContentManager>();
            var contoso = new ContentItem { ContentItemId = "contoso", ContentType = "Account", DisplayText = "Contoso Ltd" };
            var minutes = new ContentItem { ContentItemId = "minutes", ContentType = "Article", DisplayText = "Board minutes" };

            contoso.Weld(new AccountPart());
            accounts.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
                .ReturnsAsync((string id, VersionOptions _) => id switch
                {
                    "contoso" => contoso,
                    "minutes" => minutes,
                    _ => null,
                });

            services.AddSingleton(accounts.Object);
            services.AddSingleton(WorkforceTimeOffAdminAIToolTests.LocalClock(() => AgreementsTests.Noon));
            services.AddScoped<TestUserAccessor>();
            services.AddScoped<IUserAccessor>(provider => provider.GetRequiredService<TestUserAccessor>());
        });

    private static ClaimsPrincipal Viewer()
        => CalendarTestHarness.User("viewer", AgreementsPermissions.ViewAgreements.Name, "ViewContent");

    private static Task<string> InvokeAsync(AgreementsTests.Kit kit, ClaimsPrincipal user, Dictionary<string, object> arguments)
        => kit.Harness.RunAsync(async services =>
        {
            services.GetRequiredService<TestUserAccessor>().User = user;

            var result = await new GetAgreementBalanceTool().InvokeAsync(
                new AIFunctionArguments(arguments.ToDictionary(pair => pair.Key, pair => (object)JsonSerializer.SerializeToElement(pair.Value))) { Services = services },
                TestContext.Current.CancellationToken);

            return result?.ToString();
        }, commit: false);

    private sealed class TestUserAccessor : IUserAccessor
    {
        public ClaimsPrincipal User { get; set; }
    }
}
