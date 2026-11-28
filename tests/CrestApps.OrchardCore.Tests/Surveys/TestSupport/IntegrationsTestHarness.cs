using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Deployments;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Recipes.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A SQLite-backed site with the Surveys stores and the portability and integration services of the module.
/// </summary>
internal sealed class IntegrationsTestHarness : IAsyncDisposable
{
    private IntegrationsTestHarness(CalendarTestHarness harness)
    {
        Harness = harness;
    }

    public CalendarTestHarness Harness { get; }

    public static async Task<IntegrationsTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        var harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddOptions();
                services.AddSingleton(new ShellSettings { Name = "Default" });
                services.AddSurveysStorage();
                services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
                services.AddSingleton<ISurveyUserLookup, IntegrationsUsers>();
                services.AddScoped<ISurveyAuditTrail, NullSurveyAuditTrail>();
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.AddScoped<SurveysRecipeExporter>();
                services.AddScoped<SurveysExportAuthorizer>();
                services.AddScoped<SurveysRecipeStep>();
                services.AddScoped<SurveyQuestionBanksRecipeStep>();
                services.AddScoped<SurveyResponsesRecipeStep>();
                services.AddScoped<SurveyOverridesRecipeStep>();
                services.AddScoped<SurveyInvitationsRecipeStep>();
                configure?.Invoke(services);
            },
            SurveysIndexSchema.CreateAsync,
            new IIndexProvider[]
            {
                new SurveyIndexProvider(),
                new SurveyVersionIndexProvider(),
                new QuestionBankIndexProvider(),
                new BankQuestionIndexProvider(),
                new BankQuestionTagIndexProvider(),
                new SurveyCollectorIndexProvider(),
                new SurveyResponseIndexProvider(),
                new SurveyParticipationIndexProvider(),
                new SurveyInvitationIndexProvider(),
                new SurveyInvitationOptOutIndexProvider(),
                new SurveyOverrideIndexProvider(),
            },
            [SurveysConstants.CollectionName, SurveysConstants.ResponsesCollectionName]);

        return new IntegrationsTestHarness(harness);
    }

    public Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
        => Harness.RunAsync(work);

    public Task RunAsync(Func<IServiceProvider, Task> work)
        => Harness.RunAsync(work);

    public Task<IList<string>> ImportAsync<TStep>(System.Text.Json.Nodes.JsonObject step)
        where TStep : global::OrchardCore.Recipes.Services.IRecipeStepHandler
        => Harness.RunAsync(async services =>
        {
            var context = new RecipeExecutionContext { Name = step["name"]!.GetValue<string>(), Step = step };

            await services.GetRequiredService<TStep>().ExecuteAsync(context);

            return context.Errors;
        });

    public static Survey NewSurvey(string id, string name, AnonymityMode anonymity = AnonymityMode.Identified)
        => new()
        {
            ItemId = id,
            Name = name,
            Title = "Title of " + name,
            Kind = SurveyKinds.Survey,
            Status = SurveyStatus.Open,
            AnonymitySecret = "secret-" + name,
            Access = new SurveyAccessSettings { Anonymity = anonymity },
            Draft = Definition("Draft of " + name),
        };

    public static SurveyDefinition Definition(string title)
        => new()
        {
            Title = title,
            Pages =
            [
                new SurveyPage
                {
                    Id = "p1",
                    Name = "page_1",
                    Questions =
                    [
                        new SurveyQuestion
                        {
                            Id = "q1",
                            Name = "q_satisfaction",
                            ControlType = SurveyControlNames.SingleChoice,
                            Title = "How satisfied are you?",
                            Choices =
                            [
                                new SurveyChoice { Id = "c1", Value = "1", Text = "Not at all" },
                                new SurveyChoice { Id = "c2", Value = "2", Text = "Very" },
                            ],
                        },
                    ],
                },
            ],
            Endings = [new SurveyEnding { Id = "e1", Name = "thanks", IsDefault = true }],
        };

    public ValueTask DisposeAsync()
        => Harness.DisposeAsync();

    // Every user name is on the site as "user-{name}", except "ghost".
    private sealed class IntegrationsUsers : ISurveyUserLookup
    {
        public Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
            => Task.FromResult<IDictionary<string, string>>((userIds ?? [])
                .Where(id => id is not null && id.StartsWith("user-", StringComparison.Ordinal))
                .Distinct()
                .ToDictionary(id => id, id => id.Substring(5), StringComparer.Ordinal));

        public Task<string> FindUserIdAsync(string userName)
            => Task.FromResult(string.IsNullOrWhiteSpace(userName) || userName == "ghost" ? null : "user-" + userName);
    }
}
