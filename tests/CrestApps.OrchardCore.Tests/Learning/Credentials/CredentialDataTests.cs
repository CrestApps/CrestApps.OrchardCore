using System.Text.Json;
using CrestApps.OrchardCore.Learning.Credentials;
using CrestApps.OrchardCore.Learning.Credentials.Core.OpenBadges;
using CrestApps.OrchardCore.Learning.Credentials.Core.Services;
using CrestApps.OrchardCore.Learning.Credentials.Models;
using CrestApps.OrchardCore.Learning.Credentials.Recipes;
using CrestApps.OrchardCore.Learning.Credentials.Reports;
using CrestApps.OrchardCore.Learning.Credentials.Services;
using CrestApps.OrchardCore.Learning.Credentials.Workflows;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Modules.Reports.Designer;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Tests.Learning.Credentials;

/// <summary>
/// Phase 8, slice 5e: a personal data erasure revokes each award and keeps its status slot; the workflow input and the
/// recipe rows and the data set hold no e-mail address; a recipe row carries the signed credential as issued.
/// </summary>
public sealed class CredentialDataTests
{
    [Fact]
    public async Task Erase_RevokesEachAward_KeepsItsSlot_AndRemovesThePersonalData()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        h.Definitions.Add("crane");
        var active = await ManualAsync(h, "forklift", "ana");
        var revoked = await ManualAsync(h, "crane", "ana");
        var other = await ManualAsync(h, "forklift", "ben");
        await h.RunAsync(services => services.GetRequiredService<ICredentialAwardService>().RevokeAsync(revoked.ItemId, "admin", "Cheating"));

        // Act
        var count = await h.RunAsync(services => services.GetRequiredService<CredentialAwardEraser>().EraseAsync("ana"));
        var (erasedActive, erasedRevoked, untouched, encoded) = await h.RunAsync(async services =>
        {
            var store = services.GetRequiredService<CredentialAwardStore>();

            return (
                await store.FindByIdAsync(active.ItemId),
                await store.FindByIdAsync(revoked.ItemId),
                await store.FindByIdAsync(other.ItemId),
                await services.GetRequiredService<CredentialStatusLists>().EncodeAsync(active.StatusListNumber));
        });

        // Assert
        Assert.Equal(2, count);
        Assert.Empty(await h.AwardsAsync("ana"));

        foreach (var award in new[] { erasedActive, erasedRevoked })
        {
            Assert.NotNull(award.ErasedUtc);
            Assert.Null(award.RecipientUserId);
            Assert.Null(award.Recipient);
            Assert.Null(award.Credential);
            Assert.Null(award.Jwt);
            Assert.True(award.IsRevoked);
            Assert.True(BitstringStatusList.IsSet(encoded, award.StatusListIndex));
        }

        Assert.Equal(active.StatusListIndex, erasedActive.StatusListIndex);
        Assert.Equal(CredentialAwardEraser.ErasureReason, erasedActive.RevokedReason);
        Assert.Equal("Cheating", erasedRevoked.RevokedReason);
        Assert.Equal("ben", untouched.RecipientUserId);
        Assert.False(BitstringStatusList.IsSet(encoded, other.StatusListIndex));
        Assert.Equal(CredentialVerificationOutcome.Revoked, (await h.RunAsync(services => services.GetRequiredService<CredentialVerifier>().VerifyAsync(erasedActive))).Outcome);
    }

    [Fact]
    public async Task TheWorkflowInput_HoldsTheAward_AndNoEmailAddress()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var award = await ManualAsync(h, "forklift", "ana");

        // Act
        var input = CredentialWorkflowObserver.Input(award);

        // Assert
        Assert.Equal(award.ItemId, input["AwardId"]);
        Assert.Equal("forklift", input["DefinitionId"]);
        Assert.Equal("ana", input["UserId"]);
        Assert.DoesNotContain(input.Values, value => value?.ToString()?.Contains('@') == true);
    }

    [Fact]
    public async Task ARecipeRow_NamesPeopleByUserName_AndCarriesTheSignedCredentialAsIssued()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync();
        h.Definitions.Add("forklift");
        var award = await ManualAsync(h, "forklift", "ana");
        var names = new Dictionary<string, string> { ["ana"] = "ana.lee", ["admin"] = "administrator" };

        // Act
        var row = CredentialAwardsJson.ToRow(award, names);
        var back = row.Deserialize<CredentialAward>(CredentialAwardsJson.Options);

        // Assert
        Assert.Equal("ana.lee", row["RecipientUserName"].GetValue<string>());
        Assert.Equal("administrator", row["AwardedByUserName"].GetValue<string>());
        Assert.False(row.ContainsKey(nameof(CredentialAward.RecipientUserId)));
        Assert.False(row.ContainsKey(nameof(CredentialAward.IsRevoked)));
        Assert.DoesNotContain("ana@example.com", row.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(award.Credential, back.Credential);
        Assert.Equal(award.Jwt, back.Jwt);
        Assert.Equal(award.PublicId, back.PublicId);
        Assert.Equal(award.StatusListIndex, back.StatusListIndex);
        Assert.Equal(award.Recipient.IdentityHash, back.Recipient.IdentityHash);
        Assert.Equal(award.IssuedUtc, back.IssuedUtc);
    }

    [Fact]
    public async Task TheDataSource_ListsTheAwards_WithoutAnEmailAddressOrTheIdentityHash()
    {
        // Arrange
        await using var h = await CredentialTestHarness.CreateAsync(services =>
        {
            services.AddScoped<CredentialAwardsReportDataSet>();
            services.AddScoped<LearningCredentialsDataSource>();
        });
        h.Definitions.Add("forklift");
        var award = await ManualAsync(h, "forklift", "ana");
        var reporter = CalendarTestHarness.User("reporter", LearningCredentialsPermissions.ViewCredentialAwards.Name);

        // Act
        var (dataSets, anonymous, table) = await h.RunAsync(async services =>
        {
            var source = services.GetRequiredService<LearningCredentialsDataSource>();

            return (
                await source.GetDataSetsAsync(new DataSourceContext { User = reporter }, TestContext.Current.CancellationToken),
                await source.GetDataSetsAsync(new DataSourceContext { User = CalendarTestHarness.User("learner") }, TestContext.Current.CancellationToken),
                await ReportDesignerTestServices.ReadAsync(source, new DataSourceQuery
                {
                    DataSet = CredentialAwardsReportDataSet.DataSetName,
                    MaxRows = 100,
                    Context = new DataSourceContext { User = reporter },
                }, TestContext.Current.CancellationToken));
        });

        // Assert
        Assert.Equal([CredentialAwardsReportDataSet.DataSetName], dataSets.Select(dataSet => dataSet.Name));
        Assert.Empty(anonymous);

        var row = Assert.Single(table.Rows);
        var values = table.Fields.Select((field, index) => (field.Name, Value: row[index])).ToDictionary(pair => pair.Name, pair => pair.Value, StringComparer.Ordinal);

        Assert.Equal("ana", values["UserId"]);
        Assert.Equal(award.ItemId, values["AwardId"]);
        Assert.Equal("Forklift operator", values["Credential"]);
        Assert.DoesNotContain(values.Values, value => value?.ToString() is { } text && (text.Contains('@') || text.Contains("sha256$", StringComparison.Ordinal)));
    }

    private static async Task<CredentialAward> ManualAsync(CredentialTestHarness h, string definitionId, string userId)
        => (await h.AwardAsync(new CredentialAwardRequest { DefinitionId = definitionId, UserId = userId, RuleKind = CredentialsConstants.RuleKinds.Manual, Actor = "admin" })).Award;
}
