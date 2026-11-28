using System.Net;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Audiences;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Round 12 L4 and L5: who may change invitations. Inviting a user audience opens its members (the invitation list names
/// them), so a holder of Manage invitations of own surveys may invite only an audience their survey is for, as the
/// participation report decides; and an invitation collector is changed only with the right to manage invitations, never
/// with Manage alone through the generic collectors page.
/// </summary>
public sealed partial class SurveyRespondingTests
{
    [Fact]
    public async Task R12L4_AnOwnInvitationsHolder_CannotInviteAnAudienceTheirSurveyIsNotFor()
    {
        // Arrange: an audience of every administrator, and an owner who may manage the invitations of their own surveys.
        var audience = "r12-admins-" + Guid.NewGuid().ToString("n")[..6];
        var (surveyId, collectorId) = await OwnSurveyWithInvitationsAsync("r12-inviter", "R12Inviter", "ManageOwnSurveyInvitations");

        await _fixture.InTenantAsync(async services =>
        {
            var errors = await services.GetRequiredService<IUserAudienceService>().SaveDefinitionAsync(new UserAudienceDefinition
            {
                Name = audience,
                DisplayName = "Round 12 administrators",
                Rules = [new UserAudienceRule { Source = RoleUserAudienceRuleSource.SourceName, Values = ["Administrator"] }],
            });

            Assert.Empty(errors);
        });

        using var inviter = await _fixture.CreateClientAsync("r12-inviter");
        var cancellationToken = TestContext.Current.CancellationToken;
        var page = await inviter.GetStringAsync($"Admin/surveys/invitations/{collectorId}/invite", cancellationToken);

        // Act
        var posted = await inviter.PostAsync($"Admin/surveys/invitations/{collectorId}/invite", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(page),
            ["Audience"] = audience,
        }), cancellationToken);
        var invited = await _fixture.InTenantAsync(async services =>
            (await services.GetRequiredService<SurveyInvitationStore>().ListBySurveyAsync(surveyId)).Count);

        // Assert: the audience isn't offered, and posting it invites nobody.
        Assert.DoesNotContain($"value=\"{audience}\"", page, StringComparison.Ordinal);
        Assert.Equal(0, invited);
        Assert.NotEqual(HttpStatusCode.Redirect, posted.StatusCode);
    }

    [Fact]
    public async Task R12L5_ManageAlone_CannotChangeAnInvitationCollector()
    {
        // Arrange: an editor who manages their own survey but not its invitations, and an invitation collector an
        // administrator made on it.
        var (surveyId, collectorId) = await OwnSurveyWithInvitationsAsync("r12-editor", "R12Editor");

        using var editor = await _fixture.CreateClientAsync("r12-editor");
        var cancellationToken = TestContext.Current.CancellationToken;
        var collectors = await editor.GetStringAsync($"Admin/surveys/{surveyId}/collectors", cancellationToken);

        // Act
        var edit = await editor.GetAsync($"Admin/surveys/collectors/{collectorId}", cancellationToken);
        var status = await editor.PostAsync($"Admin/surveys/collectors/{collectorId}/status?status=Closed", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = SurveysSiteFixture.Token(collectors),
        }), cancellationToken);
        var collector = await _fixture.InTenantAsync(services => services.GetRequiredService<SurveyCollectorStore>().FindByIdAsync(collectorId).AsTask());

        // Assert: both are refused (the admin answers a refusal with its access denied page), and the collector stays open.
        Assert.NotEqual(HttpStatusCode.OK, edit.StatusCode);
        Assert.DoesNotContain("/collectors", status.Headers.Location?.OriginalString ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(CollectorStatus.Open, collector.Status);
    }

    // A published survey owned by a new user of a new role (Manage own surveys, plus the given permissions), with an
    // invitation collector an administrator made on it.
    private async Task<(string SurveyId, string CollectorId)> OwnSurveyWithInvitationsAsync(string userName, string role, params string[] permissions)
    {
        var (surveyId, _, _) = await PublishAsync("Invitation rights " + userName);

        var collectorId = await _fixture.InTenantAsync(async services =>
        {
            await SurveysSiteFixture.RunRecipeAsync(services, new JsonObject
            {
                ["name"] = "Round12" + role,
                ["steps"] = new JsonArray(new JsonObject
                {
                    ["name"] = "Roles",
                    ["Roles"] = new JsonArray(new JsonObject
                    {
                        ["Name"] = role,
                        ["Description"] = role + " role",
                        ["PermissionBehavior"] = "Add",
                        ["Permissions"] = new JsonArray([.. new[] { "AccessAdminPanel", "ManageOwnSurveys" }.Concat(permissions).Select(permission => (JsonNode)permission)]),
                    }),
                }),
            });

            var userId = await SurveysSiteFixture.CreateUserAsync(services, userName);
            var users = services.GetRequiredService<UserManager<IUser>>();
            await users.AddToRoleAsync(await users.FindByIdAsync(userId), role);

            var store = services.GetRequiredService<SurveyStore>();
            var survey = await store.FindByIdAsync(surveyId);

            survey.OwnerId = userId;
            await store.UpdateAsync(survey);

            var manager = services.GetRequiredService<SurveyCollectorManager>();
            var collector = manager.New(surveyId, SurveyCollectorKinds.Invitation);

            collector.Name = "Invitations";
            await manager.SaveAsync(collector, isNew: true);
            await services.GetRequiredService<global::YesSql.ISession>().SaveChangesAsync();

            return collector.ItemId;
        });

        return (surveyId, collectorId);
    }
}
