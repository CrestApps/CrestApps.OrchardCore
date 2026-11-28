using System.Text.Json;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Agreements;
using CrestApps.OrchardCore.Agreements.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using AgreementsAIToolsStartup = CrestApps.OrchardCore.Agreements.AIToolsStartup;

namespace CrestApps.OrchardCore.Tests.Agreements.AI;

/// <summary>
/// Agreements are a read-only record kind of the generic record tools, for every agreement whatever terms it carries:
/// read with the permission the account tab and the reports ask for, listed newest number first and found by number,
/// name, account or status, with an allow-listed projection that names other features' terms without their values.
/// </summary>
public sealed class AgreementRecordKindTests
{
    [Fact]
    public void Agreements_AreAReadOnlyKind_ReadWithViewAgreements_OnTheAgreementsFeature()
    {
        // Act
        var kind = Kind();
        var required = typeof(AgreementsAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();
        var feature = typeof(AgreementsAIToolsStartup).GetCustomAttributes(typeof(FeatureAttribute), inherit: false).Cast<FeatureAttribute>().Single();

        // Assert
        Assert.Equal("agreement", kind.Name);
        Assert.False(kind.CanWrite);
        Assert.Equal(AgreementsPermissions.ManageAgreements.Name, kind.Permission.Name);
        Assert.Equal(AgreementsPermissions.ViewAgreements.Name, kind.EffectiveReadPermission.Name);
        Assert.Contains("getServiceAgreement", kind.Description, StringComparison.Ordinal);
        Assert.Contains("getAgreementBalance", kind.Description, StringComparison.Ordinal);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.Equal(AgreementsConstants.Features.Agreements, feature.FeatureName);
    }

    [Fact]
    public async Task Agreements_AreListedNewestNumberFirst_AndFoundByNumberNameAccountCustomerOrStatus()
    {
        // Arrange
        var members = new Mock<IAccountMemberService>();
        members.Setup(service => service.FindAccountAsync("u-pat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContentItem { ContentItemId = "contoso" });
        await using var kit = await AgreementsTests.Kit.CreateAsync(services => services.AddSingleton(members.Object));
        var gold = await kit.CreateAsync(item => { item.Name = "Gold support"; item.AccountId = "contoso"; item.Status = AgreementStatus.Active; });
        var silver = await kit.CreateAsync(item => { item.Name = "Silver support"; item.AccountId = "contoso"; });
        var field = await kit.CreateAsync(item => { item.Name = "Gold field service"; item.AccountId = "fabrikam"; item.Status = AgreementStatus.Active; });
        var kind = Kind();

        // Act
        var all = await ListAsync(kit, kind, null);
        var byAccount = await ListAsync(kit, kind, "contoso");
        var byCustomer = await ListAsync(kit, kind, "u-pat");
        var byName = await ListAsync(kit, kind, "Gold");
        var byNumber = await ListAsync(kit, kind, silver.Number);
        var byStatus = await ListAsync(kit, kind, "active");

        // Assert
        Assert.Equal(3, all.Count);
        Assert.Equal([field.Number, silver.Number, gold.Number], all.Entries.Select(agreement => agreement.Number));
        Assert.Equal(["Silver support", "Gold support"], byAccount.Entries.Select(agreement => agreement.Name));
        Assert.Equal(["Silver support", "Gold support"], byCustomer.Entries.Select(agreement => agreement.Name));
        Assert.Equal(["Gold field service", "Gold support"], byName.Entries.Select(agreement => agreement.Name));
        Assert.Equal([silver.ItemId], byNumber.Entries.Select(agreement => agreement.ItemId));
        Assert.Equal(["Gold field service", "Gold support"], byStatus.Entries.Select(agreement => agreement.Name));
    }

    [Fact]
    public void TheProjection_GivesTheAllowListedFields_AndNamesOtherFeaturesTermsWithoutTheirValues()
    {
        // Arrange
        var kind = Kind();
        var agreement = new Agreement
        {
            ItemId = "a1",
            Number = "SA-00007",
            Name = "Gold",
            Status = AgreementStatus.Active,
            AccountId = "contoso",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            TimeZoneId = "America/New_York",
            Hours = new AgreementHours { Kind = AgreementHoursKind.Block, Hours = 40, RoundingMinutes = 15 },
            Notes = new string('n', 600),
            ExpiringNotifiedUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            HoursWarnings = new AgreementHoursWarnings { Percents = [80] },
        };
        agreement.Properties["WorkOrderAgreementTerms"] = new { Contact = new { Phone = "555-0100" } };

        // Act
        var json = JsonSerializer.Serialize(kind.Projection(agreement));
        var result = JsonElement.Parse(json);

        // Assert
        Assert.Equal("SA-00007", result.GetProperty("number").GetString());
        Assert.Equal("Active", result.GetProperty("status").GetString());
        Assert.Equal("2026-12-31", result.GetProperty("endDate").GetString());
        Assert.Equal("Block", result.GetProperty("hours").GetProperty("kind").GetString());
        Assert.Equal(40m, result.GetProperty("hours").GetProperty("hours").GetDecimal());
        Assert.Equal(500, result.GetProperty("notesExcerpt").GetString().Length);
        Assert.Equal(600, result.GetProperty("notesLength").GetInt32());
        Assert.Equal(["WorkOrderAgreementTerms"], result.GetProperty("otherTerms").EnumerateArray().Select(term => term.GetString()));
        Assert.DoesNotContain("555-0100", json, StringComparison.Ordinal);
        Assert.False(result.TryGetProperty("hoursWarnings", out _));
        Assert.False(result.TryGetProperty("expiringNotifiedUtc", out _));
        Assert.False(result.TryGetProperty("properties", out _));
    }

    private static AIRecordKind<Agreement> Kind()
    {
        var services = new ServiceCollection();

        new AgreementsAIToolsStartup(new PassThroughStringLocalizer<AgreementsAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        return Assert.IsType<AIRecordKind<Agreement>>(provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds["agreement"]);
    }

    private static Task<PageResult<Agreement>> ListAsync(AgreementsTests.Kit kit, AIRecordKind<Agreement> kind, string text)
        => kit.Harness.RunAsync(
            services => kind.ListAsync(services, new AIRecordQuery { Text = text, Page = 1, PageSize = 20 }, TestContext.Current.CancellationToken).AsTask(),
            commit: false);
}
