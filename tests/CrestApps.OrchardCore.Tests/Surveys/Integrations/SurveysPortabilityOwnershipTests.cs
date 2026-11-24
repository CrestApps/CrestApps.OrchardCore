using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Deployments;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore;
using OrchardCore.Deployment;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Tests.Surveys.Integrations;

/// <summary>
/// Owners, collectors and invitations that travel by name between sites, and the personal-data deployment steps that
/// need the export permission.
/// </summary>
public sealed class SurveysPortabilityOwnershipTests
{
    [Fact]
    public async Task Surveys_RoundTrip_KeepTheirOwnerByUserName()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        var surveyId = IdGenerator.GenerateId();
        await source.RunAsync(async services =>
        {
            var survey = IntegrationsTestHarness.NewSurvey(surveyId, "owned");
            survey.OwnerId = "user-author1";
            await services.GetRequiredService<SurveyStore>().CreateAsync(survey);
        });

        var step = await SurveysRecipeRoundTripTests.ExportSurveysAsync(source, includeSecrets: false);
        await using var target = await IntegrationsTestHarness.CreateAsync();

        // Act
        var errors = await target.ImportAsync<SurveysRecipeStep>(step);

        // Assert
        Assert.Empty(errors);
        Assert.Equal("author1", step["Surveys"]![0]!["OwnerUserName"]!.GetValue<string>());
        var imported = await target.RunAsync(async services => await services.GetRequiredService<SurveyStore>().FindByNameAsync("owned"));
        Assert.Equal("user-author1", imported.OwnerId);
    }

    [Fact]
    public async Task QuestionBanks_RoundTrip_KeepTheirOwnerByUserName()
    {
        // Arrange
        await using var source = await IntegrationsTestHarness.CreateAsync();
        var step = await source.RunAsync(async services =>
        {
            var bank = new QuestionBank { ItemId = IdGenerator.GenerateId(), Name = "owned-bank", Title = "Owned", OwnerId = "user-author1" };
            await services.GetRequiredService<QuestionBankStore>().CreateAsync(bank);

            return new JsonObject
            {
                ["name"] = SurveysRecipeSteps.QuestionBanks,
                [SurveysRecipeSteps.QuestionBanksSection] = new JsonArray(await services.GetRequiredService<SurveysRecipeExporter>().ExportBankAsync(bank)),
            };
        });

        await using var target = await IntegrationsTestHarness.CreateAsync();

        // Act
        var errors = await target.ImportAsync<SurveyQuestionBanksRecipeStep>(step);

        // Assert
        Assert.Empty(errors);
        var imported = await target.RunAsync(async services => await services.GetRequiredService<QuestionBankStore>().FindByNameAsync("owned-bank"));
        Assert.Equal("user-author1", imported.OwnerId);
    }

    [Fact]
    public async Task Responses_RoundTrip_FindTheirCollectorByNameAndDropAnInvitationTheSiteDoesNotHave()
    {
        // Arrange: the target has the survey with its own collector ids; one response's invitation came along, the other's
        // did not.
        await using var source = await IntegrationsTestHarness.CreateAsync();
        await SurveysRecipeRoundTripTests.SeedSurveyAsync(source);
        var surveyId = await source.RunAsync(async services => (await services.GetRequiredService<SurveyStore>().FindByNameAsync("csat")).ItemId);
        var sourceCollectorId = await source.RunAsync(async services => (await services.GetRequiredService<SurveyCollectorStore>().ListBySurveyAsync(surveyId)).Single().ItemId);
        var keptInvitation = IdGenerator.GenerateId();
        var missingInvitation = IdGenerator.GenerateId();
        var withInvitation = IdGenerator.GenerateId();
        var withoutInvitation = IdGenerator.GenerateId();
        var version = await source.RunAsync(async services => (await services.GetRequiredService<SurveyVersionStore>().ListBySurveyAsync(surveyId)).Single(item => item.Number == 2));

        await source.RunAsync(async services =>
        {
            var store = services.GetRequiredService<SurveyResponseStore>();

            await store.CreateAsync(new SurveyResponse { ItemId = withInvitation, SurveyId = surveyId, SurveyVersionId = version.ItemId, VersionNumber = 2, CollectorId = sourceCollectorId, InvitationId = keptInvitation, Status = SurveyResponseStatus.Completed });
            await store.CreateAsync(new SurveyResponse { ItemId = withoutInvitation, SurveyId = surveyId, SurveyVersionId = version.ItemId, VersionNumber = 2, CollectorId = sourceCollectorId, InvitationId = missingInvitation, Status = SurveyResponseStatus.Completed });
        });

        var responses = await source.RunAsync(async services => new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Responses,
            ["Responses"] = await services.GetRequiredService<SurveysRecipeExporter>().ExportResponsesAsync((await services.GetRequiredService<SurveyResponseStore>().GetAllAsync()).ToList()),
        });

        await using var target = await IntegrationsTestHarness.CreateAsync();
        await target.ImportAsync<SurveysRecipeStep>(await SurveysRecipeRoundTripTests.ExportSurveysAsync(source, includeSecrets: false));

        // The target's collector of the same name has another id, as a collector made on each site does.
        var targetCollectorId = IdGenerator.GenerateId();
        await target.RunAsync(async services =>
        {
            var collectors = services.GetRequiredService<SurveyCollectorStore>();
            var collector = (await collectors.ListBySurveyAsync(surveyId)).Single();
            await collectors.DeleteAsync(collector);
            await collectors.CreateAsync(new SurveyCollector { ItemId = targetCollectorId, SurveyId = surveyId, Name = collector.Name, Token = "othertokenothertoken12", IsDefault = true });
            await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation { ItemId = keptInvitation, SurveyId = surveyId, CollectorId = targetCollectorId, Email = "kept@example.com" });
        });

        // Act
        var errors = await target.ImportAsync<SurveyResponsesRecipeStep>(responses);

        // Assert
        Assert.Empty(errors);
        Assert.Equal("Main link", responses["Responses"]![0]!["CollectorName"]!.GetValue<string>());
        var imported = (await target.RunAsync(async services => await services.GetRequiredService<SurveyResponseStore>().GetAllAsync())).ToDictionary(response => response.ItemId);
        Assert.Equal(targetCollectorId, imported[withInvitation].CollectorId);
        Assert.Equal(targetCollectorId, imported[withoutInvitation].CollectorId);
        Assert.Equal(keptInvitation, imported[withInvitation].InvitationId);
        Assert.Null(imported[withoutInvitation].InvitationId);
    }

    [Fact]
    public async Task Responses_OfAnAnonymousSurvey_RoundTripWithoutTheirNumber_AndGetANewOne()
    {
        // Arrange
        var surveyId = IdGenerator.GenerateId();
        var versionId = IdGenerator.GenerateId();
        var anonymousId = IdGenerator.GenerateId();
        var identifiedSurveyId = IdGenerator.GenerateId();
        var identifiedVersionId = IdGenerator.GenerateId();
        var identifiedId = IdGenerator.GenerateId();

        async Task SeedAsync(IServiceProvider services, bool withResponses)
        {
            await services.GetRequiredService<SurveyStore>().CreateAsync(IntegrationsTestHarness.NewSurvey(surveyId, "pulse", AnonymityMode.Anonymous));
            await services.GetRequiredService<SurveyVersionStore>().CreateAsync(new SurveyVersion { ItemId = versionId, SurveyId = surveyId, Number = 1, Definition = new SurveyDefinition() });
            await services.GetRequiredService<SurveyStore>().CreateAsync(IntegrationsTestHarness.NewSurvey(identifiedSurveyId, "named"));
            await services.GetRequiredService<SurveyVersionStore>().CreateAsync(new SurveyVersion { ItemId = identifiedVersionId, SurveyId = identifiedSurveyId, Number = 1, Definition = new SurveyDefinition() });

            if (withResponses)
            {
                await services.GetRequiredService<SurveyResponseStore>().CreateAsync(new SurveyResponse { ItemId = anonymousId, SurveyId = surveyId, SurveyVersionId = versionId, VersionNumber = 1, Number = "R0004212", Status = SurveyResponseStatus.Completed });
                await services.GetRequiredService<SurveyResponseStore>().CreateAsync(new SurveyResponse { ItemId = identifiedId, SurveyId = identifiedSurveyId, SurveyVersionId = identifiedVersionId, VersionNumber = 1, Number = "R0004211", Status = SurveyResponseStatus.Completed });
            }
        }

        await using var source = await IntegrationsTestHarness.CreateAsync();
        await source.RunAsync(services => SeedAsync(services, withResponses: true));
        var step = await source.RunAsync(async services => new JsonObject
        {
            ["name"] = SurveysRecipeSteps.Responses,
            ["Responses"] = await services.GetRequiredService<SurveysRecipeExporter>().ExportResponsesAsync((await services.GetRequiredService<SurveyResponseStore>().GetAllAsync()).ToList()),
        });

        var numbers = new Mock<global::CrestApps.OrchardCore.Numbering.INumberSequence>();
        numbers.Setup(sequence => sequence.NextAsync(It.IsAny<string>(), It.IsAny<global::CrestApps.OrchardCore.Numbering.NumberSequenceOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(501);
        await using var target = await IntegrationsTestHarness.CreateAsync(services => services.AddSingleton(numbers.Object));
        await target.RunAsync(services => SeedAsync(services, withResponses: false));

        // Act
        var errors = await target.ImportAsync<SurveyResponsesRecipeStep>(step);

        // Assert
        Assert.DoesNotContain("R0004212", step.ToJsonString(), StringComparison.Ordinal);
        Assert.Contains("R0004211", step.ToJsonString(), StringComparison.Ordinal);
        Assert.Empty(errors);
        var imported = (await target.RunAsync(async services => await services.GetRequiredService<SurveyResponseStore>().GetAllAsync())).ToDictionary(response => response.ItemId);
        Assert.Equal("R0000501", imported[anonymousId].Number);
        Assert.Equal("R0004211", imported[identifiedId].Number);
    }

    [Fact]
    public async Task InvitationAndAccommodationSteps_WithoutTheExportPermission_WriteNoPersonalData()
    {
        // Arrange
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        await using var harness = await DeploymentHarnessAsync(authorization.Object);

        // Act: once with no signed-in user (a scheduled or remote export), once with a user who lacks the permission.
        var anonymous = await ExportAsync(harness, user: null);
        var denied = await ExportAsync(harness, user: SignedIn());

        // Assert
        Assert.Empty(anonymous.Invitations);
        Assert.Empty(anonymous.Overrides);
        Assert.Empty(denied.Invitations);
        Assert.Empty(denied.Overrides);
    }

    [Fact]
    public async Task InvitationAndAccommodationSteps_WithTheExportPermission_WriteEveryRecord()
    {
        // Arrange
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        await using var harness = await DeploymentHarnessAsync(authorization.Object);

        // Act
        var exported = await ExportAsync(harness, SignedIn());

        // Assert
        Assert.Equal(SurveysConstants.PageSize + 1, exported.Invitations.Count);
        Assert.Single(exported.Overrides);
    }

    private static async Task<IntegrationsTestHarness> DeploymentHarnessAsync(IAuthorizationService authorization)
    {
        var harness = await IntegrationsTestHarness.CreateAsync(services =>
        {
            services.AddSingleton(authorization);
            services.AddScoped<SurveyInvitationsDeploymentSource>();
            services.AddScoped<SurveyOverridesDeploymentSource>();
        });

        var surveyId = IdGenerator.GenerateId();

        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<SurveyStore>().CreateAsync(IntegrationsTestHarness.NewSurvey(surveyId, "exported"));

            // More than one page, so the paged read is covered.
            for (var i = 0; i <= SurveysConstants.PageSize; i++)
            {
                await services.GetRequiredService<SurveyInvitationStore>().CreateAsync(new SurveyInvitation { ItemId = IdGenerator.GenerateId(), SurveyId = surveyId, Email = $"person{i}@example.com" });
            }

            await services.GetRequiredService<SurveyOverrideStore>().CreateAsync(new SurveyOverride { ItemId = IdGenerator.GenerateId(), SurveyId = surveyId, UserId = "user-learner1", Reason = "Accommodation" });
        });

        return harness;
    }

    private static Task<(JsonArray Invitations, JsonArray Overrides)> ExportAsync(IntegrationsTestHarness harness, ClaimsPrincipal user)
        => harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = user is null ? null : new DefaultHttpContext { User = user };

            try
            {
                var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

                await services.GetRequiredService<SurveyInvitationsDeploymentSource>().ProcessDeploymentStepAsync(new SurveyInvitationsDeploymentStep(), result);
                await services.GetRequiredService<SurveyOverridesDeploymentSource>().ProcessDeploymentStepAsync(new SurveyOverridesDeploymentStep(), result);

                return (
                    result.Steps.Single(step => step["name"]!.GetValue<string>() == SurveysRecipeSteps.Invitations)[SurveysRecipeSteps.InvitationsSection]!.AsArray(),
                    result.Steps.Single(step => step["name"]!.GetValue<string>() == SurveysRecipeSteps.Overrides)[SurveysRecipeSteps.OverridesSection]!.AsArray());
            }
            finally
            {
                services.GetRequiredService<IHttpContextAccessor>().HttpContext = null;
            }
        });

    private static ClaimsPrincipal SignedIn()
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-admin1"), new Claim(ClaimTypes.Name, "admin1")], "Test"));

    [Fact]
    public void AnExportedAnonymousResponse_HasDayOnlyTimes_AndNoTimePerAnswer()
    {
        // Arrange: R6-M4, a response as SurveysRecipeJson writes it.
        var node = new JsonObject
        {
            ["StartedUtc"] = "2026-11-10T10:31:05Z",
            ["LastActivityUtc"] = new DateTime(2026, 11, 10, 10, 40, 0, DateTimeKind.Utc),
            ["CompletedUtc"] = "2026-11-10T10:41:09.123Z",
            ["Answers"] = new JsonObject
            {
                ["q1"] = new JsonObject { ["Values"] = new JsonArray("red"), ["AnsweredUtc"] = "2026-11-10T10:33:00Z", ["CheckedUtc"] = "2026-11-10T10:33:01Z" },
            },
            ["PageVisits"] = new JsonArray(new JsonObject { ["EnteredUtc"] = "2026-11-10T10:32:00Z" }),
        };

        // Act
        SurveysRecipeExporter.DaysOnly(node);
        var json = node.ToJsonString();

        // Assert
        Assert.DoesNotContain("10:3", json, StringComparison.Ordinal);
        Assert.DoesNotContain("10:4", json, StringComparison.Ordinal);
        Assert.DoesNotContain("AnsweredUtc", json, StringComparison.Ordinal);
        Assert.DoesNotContain("CheckedUtc", json, StringComparison.Ordinal);
        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), node["CompletedUtc"]!.GetValue<DateTime>());
        Assert.Equal(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), node["PageVisits"]![0]!["EnteredUtc"]!.GetValue<DateTime>());
    }

    [Fact]
    public async Task InvitationAndAccommodationSteps_WithoutTheExportPermission_SayWhyTheyAreEmpty()
    {
        // Arrange: R6-L27, a refusal must be visible, not an empty step that looks like nothing to export.
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Failed());
        await using var harness = await DeploymentHarnessAsync(authorization.Object);

        // Act
        var steps = await harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = SignedIn() };

            try
            {
                var result = new DeploymentPlanResult(Mock.Of<IFileBuilder>(), new RecipeDescriptor());

                await services.GetRequiredService<SurveyInvitationsDeploymentSource>().ProcessDeploymentStepAsync(new SurveyInvitationsDeploymentStep(), result);
                await services.GetRequiredService<SurveyOverridesDeploymentSource>().ProcessDeploymentStepAsync(new SurveyOverridesDeploymentStep(), result);

                return result.Steps.ToList();
            }
            finally
            {
                services.GetRequiredService<IHttpContextAccessor>().HttpContext = null;
            }
        });

        // Assert
        Assert.Equal(2, steps.Count);
        Assert.All(steps, step => Assert.False(string.IsNullOrEmpty(step[SurveysExportAuthorizer.RefusedProperty]?.GetValue<string>())));
    }
}
