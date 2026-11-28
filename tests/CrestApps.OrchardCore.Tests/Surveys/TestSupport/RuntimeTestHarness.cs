using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.YesSql.Core.WorkQueues;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Builders;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A SQLite store with the Surveys indexes, the runtime services and the runtime test controls, with a clock the test
/// moves.
/// </summary>
internal sealed class RuntimeTestHarness : IAsyncDisposable
{
    private readonly string _databasePath;
    private readonly ServiceProvider _provider;
    private readonly ShellContext _shellContext;

    private RuntimeTestHarness(string databasePath, ServiceProvider provider, ShellContext shellContext, RuntimeTestClock clock)
    {
        _databasePath = databasePath;
        _provider = provider;
        _shellContext = shellContext;
        Clock = clock;
    }

    public static DateTime Now { get; } = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    public RuntimeTestClock Clock { get; }

    public static async Task<RuntimeTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"surveys-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration =>
        {
            configuration.UseSqLite($"Data Source={databasePath};Pooling=False");
            configuration.ContentSerializer = new global::YesSql.Serialization.DefaultContentJsonSerializer(JOptions.Default);
        });

        store.RegisterIndexes(new IIndexProvider[]
        {
            new SurveyIndexProvider(),
            new SurveyVersionIndexProvider(),
            new SurveyCollectorIndexProvider(),
            new SurveyResponseIndexProvider(),
            new SurveyParticipationIndexProvider(),
            new SurveyInvitationIndexProvider(),
            new SurveyInvitationOptOutIndexProvider(),
            new SurveyOverrideIndexProvider(),
            new QuestionBankIndexProvider(),
            new BankQuestionIndexProvider(),
            new BankQuestionTagIndexProvider(),
            new SurveyCounterIndexProvider(),
            new LeasedWorkItemIndexProvider<SurveyDeadlineWork>(SurveyDeadlineWork.CollectionName),
        });

        var cancellationToken = TestContext.Current.CancellationToken;

        await store.InitializeAsync(cancellationToken);
        await store.InitializeCollectionAsync(SurveysConstants.CollectionName, cancellationToken);
        await store.InitializeCollectionAsync(SurveysConstants.ResponsesCollectionName, cancellationToken);
        await store.InitializeCollectionAsync(SurveyDeadlineWork.CollectionName, cancellationToken);

        await using (var session = store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(cancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

            await SurveysIndexSchema.CreateAsync(schemaBuilder);
            await SurveyCountersIndexSchema.CreateAsync(schemaBuilder);
            await LeasedWorkItemIndexSchema.CreateAsync(schemaBuilder, SurveyDeadlineWork.CollectionName);
            await transaction.CommitAsync(cancellationToken);
        }

        var clock = new RuntimeTestClock { UtcNow = Now };
        var services = new ServiceCollection();

        services.AddSingleton(store);
        services.AddScoped(_ => store.CreateSession());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(RuntimeLocalizer<>));
        services.AddSingleton<IDistributedLock, KeyedDistributedLock>();
        services.AddSingleton<IClock>(clock);
        services.AddSingleton<ILocalClock>(new FixedOffsetLocalClock(() => clock.UtcNow, TimeSpan.Zero));
        var shellSettings = new ShellSettings { Name = "Default" };
        var shellHost = new Mock<IShellHost>();

        services.AddSingleton(shellSettings);
        services.AddSingleton(shellHost.Object);

        var site = new Mock<ISite>();
        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        services.AddSurveysStorage();
        services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        services.AddDistributedMemoryCache();
        services.AddSurveysRuntime();
        services.RemoveAll<INumberSequence>();
        services.AddSingleton<INumberSequence, InMemoryNumberSequence>();

        services.AddSurveyControl<RuntimeTextControl>(RuntimeTextControl.Name, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource);
        services.AddSurveyControl<RuntimeNumberControl>(RuntimeNumberControl.Name, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.ConditionSource);
        services.AddSurveyControl<RuntimeChoiceControl>(RuntimeChoiceControl.Name, descriptor => descriptor.Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.ConditionSource);
        services.AddSurveyControl<RuntimeBlockControl>(RuntimeBlockControl.Name, descriptor => descriptor.Capabilities = SurveyControlCapabilities.None);

        configure?.Invoke(services);

        var provider = services.BuildServiceProvider();

        // A shell over the test's services, whose host makes the child scopes ShellScope.UsingChildScopeAsync runs in.
        var shellContext = new ShellContext
        {
            Settings = shellSettings,
            ServiceProvider = provider,
            IsActivated = true,
        };

        shellHost.Setup(host => host.GetScopeAsync(It.IsAny<ShellSettings>())).ReturnsAsync(() => new ShellScope(shellContext));

        return new RuntimeTestHarness(databasePath, provider, shellContext, clock);
    }

    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        await using var scope = _provider.CreateAsyncScope();
        var result = await work(scope.ServiceProvider);

        await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);

        return result;
    }

    /// <summary>
    /// Runs work in a shell scope, as a background task does, so it can run parts of itself in child scopes.
    /// </summary>
    public async Task<T> RunInShellAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        var result = default(T);

        await new ShellScope(_shellContext).UsingAsync(async scope =>
        {
            result = await work(scope.ServiceProvider);

            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        return result;
    }

    /// <summary>
    /// Runs work in a shell scope and leaves the save to the scope, as Orchard Core does for a request: with
    /// <see cref="UseOrchardSession"/>, the session is saved in the scope's before-dispose callbacks, ahead of the ones
    /// registered last (such as a lock release).
    /// </summary>
    public async Task<T> RunInShellScopeAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        var result = default(T);

        await new ShellScope(_shellContext).UsingAsync(async scope => result = await work(scope.ServiceProvider));

        return result;
    }

    /// <summary>
    /// Registers the session as Orchard Core's YesSql feature does: a session made for a shell scope's own services is
    /// saved when the scope ends, in a before-dispose callback that isn't registered last, and cancelled when the scope
    /// fails. Pass it to <see cref="CreateAsync"/> (inside the configure callback).
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="beforeSave">Work to run just before the save, such as a delay that makes a slow save.</param>
    public static void UseOrchardSession(IServiceCollection services, Func<Task> beforeSave = null)
    {
        services.RemoveAll<ISession>();
        services.AddScoped(provider =>
        {
            var session = provider.GetRequiredService<IStore>().CreateSession();
            var shellScope = ShellScope.Current;

            if (shellScope is not null && provider == shellScope.ServiceProvider)
            {
                ShellScope.RegisterBeforeDispose(async _ =>
                {
                    if (beforeSave is not null)
                    {
                        await beforeSave();
                    }

                    await session.SaveChangesAsync();
                });
                ShellScope.AddExceptionHandler((_, _) => session.CancelAsync());
            }

            return session;
        });
    }

    public Task<T> ResponsesAsync<T>(Func<ISurveyResponseService, Task<T>> work)
        => RunAsync(services => work(services.GetRequiredService<ISurveyResponseService>()));

    /// <summary>
    /// Saves an open survey with a published version 1 and a default collector, and returns it.
    /// </summary>
    public Task<Survey> PublishAsync(SurveyDefinition definition, Action<Survey> configure = null)
        => RunAsync(async services =>
        {
            var survey = new Survey
            {
                Name = "s-" + Guid.NewGuid().ToString("N"),
                Title = "Survey",
                Status = SurveyStatus.Open,
                Draft = definition,
                AnonymitySecret = "secret",
            };

            configure?.Invoke(survey);
            await services.GetRequiredService<SurveyStore>().CreateAsync(survey);

            var version = new SurveyVersion
            {
                SurveyId = survey.ItemId,
                Number = 1,
                Definition = definition,
                PublishedUtc = Now,
            };

            await services.GetRequiredService<SurveyVersionStore>().CreateAsync(version);

            survey.PublishedVersionId = version.ItemId;
            survey.PublishedVersionNumber = 1;
            await services.GetRequiredService<SurveyStore>().UpdateAsync(survey);

            await services.GetRequiredService<SurveyCollectorStore>().CreateAsync(new SurveyCollector
            {
                SurveyId = survey.ItemId,
                Name = "Default",
                Token = "tok" + Guid.NewGuid().ToString("N"),
                IsDefault = true,
            });

            return survey;
        });

    public Task<SurveyStartResult> StartAsync(Survey survey, string userId = null, Action<StartResponseRequest> configure = null)
        => ResponsesAsync(responses =>
        {
            var request = new StartResponseRequest
            {
                SurveyId = survey.ItemId,
                Identity = new SurveyRespondentIdentity { UserId = userId },
            };

            configure?.Invoke(request);

            return responses.StartAsync(request);
        });

    public Task<SurveyPageResult> PostAsync(SurveyResponse response, SurveyRespondentIdentity identity, SurveyPageDirection direction, params (string Key, string Value)[] fields)
        => ResponsesAsync(responses => responses.SubmitPageAsync(new SubmitPageRequest
        {
            ResponseId = response.ItemId,
            Identity = identity,
            PageId = response.CurrentPageId,
            Direction = direction,
            Form = Form(fields),
        }));

    public Task<SurveyResponse> FindAsync(string responseId)
        => RunAsync(services => services.GetRequiredService<SurveyResponseStore>().FindByIdAsync(responseId).AsTask());

    public static IFormCollection Form(params (string Key, string Value)[] fields)
        => new FormCollection(fields
            .GroupBy(field => "q." + field.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => new StringValues(group.Select(field => field.Value).ToArray()), StringComparer.Ordinal));

    public static ClaimsPrincipal User(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();

        try
        {
            File.Delete(_databasePath);
        }
        catch (IOException)
        {
        }
    }
}
