using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Migrations;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Documents;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Recipes.Services;
using OrchardCore.Settings;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// A real SQLite store with the Surveys tables, and the authoring services built over one session.
/// </summary>
internal sealed class AuthoringSurveyHarness : IAsyncDisposable
{
    public const string ChoiceControl = "TestChoice";
    public const string TextControl = "TestText";
    public const string BlockControl = "TestBlock";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"surveys-authoring-{Guid.NewGuid():N}.db");
    private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

    private AuthoringSurveyHarness()
    {
    }

    public IStore Store { get; private set; }

    public DateTime Now { get; set; } = new(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);

    public static async Task<AuthoringSurveyHarness> CreateAsync()
    {
        var harness = new AuthoringSurveyHarness();
        var cancellationToken = TestContext.Current.CancellationToken;

        harness.Store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={harness._databasePath};Pooling=False"));
        harness.Store.RegisterIndexes(
            [
                new SurveyIndexProvider(),
                new SurveyTagIndexProvider(),
                new SurveyTemplateIndexProvider(),
                new SurveyVersionIndexProvider(),
                new QuestionBankIndexProvider(),
                new BankQuestionIndexProvider(),
                new BankQuestionTagIndexProvider(),
                new SurveyCollectorIndexProvider(),
                new SurveyParticipationIndexProvider(),
                new SurveyInvitationIndexProvider(),
                new SurveyInvitationOptOutIndexProvider(),
                new SurveyOverrideIndexProvider(),
                new SurveyCounterIndexProvider(),
            ],
            SurveysConstants.CollectionName);
        harness.Store.RegisterIndexes([new SurveyResponseIndexProvider()], SurveysConstants.ResponsesCollectionName);

        await harness.Store.InitializeAsync(cancellationToken);
        await harness.Store.InitializeCollectionAsync(SurveysConstants.CollectionName, cancellationToken);
        await harness.Store.InitializeCollectionAsync(SurveysConstants.ResponsesCollectionName, cancellationToken);

        await using var session = harness.Store.CreateSession();
        var transaction = await session.BeginTransactionAsync(cancellationToken);

        Assert.Equal(13, await new SurveysMigrations(Mock.Of<IRecipeMigrator>(), new ResumableSteps(Mock.Of<IDocumentManager<ResumableStepsDocument>>()), harness.Store, Mock.Of<ISession>(), Mock.Of<IClock>()) { SchemaBuilder = new SchemaBuilder(harness.Store.Configuration, transaction) }.CreateAsync());

        await SurveyCountersIndexSchema.CreateAsync(new SchemaBuilder(harness.Store.Configuration, transaction));
        await transaction.CommitAsync(cancellationToken);

        return harness;
    }

    public static ISurveyControlRegistry CreateRegistry()
    {
        var options = new SurveyControlOptions();

        options.Controls[ChoiceControl] = new SurveyControlDescriptor
        {
            Name = ChoiceControl,
            Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.UsesChoices | SurveyControlCapabilities.Scorable | SurveyControlCapabilities.ConditionSource,
        };
        options.Controls[TextControl] = new SurveyControlDescriptor
        {
            Name = TextControl,
            Capabilities = SurveyControlCapabilities.Answerable | SurveyControlCapabilities.SupportsValidation | SurveyControlCapabilities.ConditionSource,
        };
        options.Controls[BlockControl] = new SurveyControlDescriptor
        {
            Name = BlockControl,
            Capabilities = SurveyControlCapabilities.None,
        };
        options.Presets["TestPreset"] = new SurveyControlPresetDescriptor
        {
            Name = "TestPreset",
            BaseControl = ChoiceControl,
            Initialize = question => question.Placeholder = "preset",
        };

        return new SurveyControlRegistry(
            Options.Create(options),
            [new AuthoringTestControlHandler(ChoiceControl, choices: true), new AuthoringTestControlHandler(TextControl), new AuthoringTestControlHandler(BlockControl)],
            [],
            []);
    }

    /// <summary>
    /// Gets the attachments the survey manager's purger releases files through.
    /// </summary>
    public SurveyFilesFakeAttachmentService Attachments { get; } = new();

    public AuthoringServices CreateServices(ISession session, ISurveyFormulaValidator formulaValidator = null, Func<ISurveyResponseQueries, ISurveyResponseQueries> responseQueries = null)
    {
        var clock = new Mock<IClock>();

        clock.Setup(c => c.UtcNow).Returns(() => Now);

        var site = new SiteSettings();

        site.Put(new SurveySettings { DefaultIncompleteExpiryHours = 72, DefaultMinimumGroupSize = 7 });

        var siteService = new Mock<ISiteService>();

        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site);

        var registry = CreateRegistry();
        var surveyStore = new SurveyStore(session);
        var versionStore = new SurveyVersionStore(session);
        var collectorStore = new SurveyCollectorStore(session);
        var bankQuestionStore = new BankQuestionStore(session);
        ISurveyResponseQueries queries = new SurveyResponseQueries(session);

        if (responseQueries is not null)
        {
            queries = responseQueries(queries);
        }
        var draftEditor = new SurveyDraftEditor(registry, new AuthoringLocalizer<SurveyDraftEditor>());
        var templateStore = new SurveyTemplateStore(session);
        var templates = new SurveyTemplateProvider(templateStore, new AuthoringLocalizer<SurveyTemplateProvider>());
        var manager = new SurveyManager(
            surveyStore,
            versionStore,
            collectorStore,
            queries,
            [templates],
            [],
            siteService.Object,
            new CrestApps.OrchardCore.Surveys.Core.Access.SurveyAnonymitySecrets(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), surveyStore, NullLogger<CrestApps.OrchardCore.Surveys.Core.Access.SurveyAnonymitySecrets>.Instance),
            new SurveyDataPurger(session, new CrestApps.OrchardCore.Surveys.Core.Runtime.SurveyResponseFiles([Attachments], NullLogger<CrestApps.OrchardCore.Surveys.Core.Runtime.SurveyResponseFiles>.Instance), [new SurveyCounterStore(session)]),
            clock.Object,
            [],
            NullLogger<CrestApps.Core.Services.NamedCatalogManager<Survey>>.Instance);
        var validator = new SurveyDefinitionValidator(registry, [], formulaValidator ?? new NullSurveyFormulaValidator(), new AuthoringLocalizer<SurveyDefinitionValidator>());
        var cache = new SurveyVersionCache(versionStore, _memoryCache, new ShellSettings { Name = "Default" });
        var publisher = new SurveyPublisher(
            manager,
            versionStore,
            collectorStore,
            cache,
            validator,
            new SurveyDefinitionDiffer(),
            new SurveyBankDrawResolver(bankQuestionStore, new AuthoringLocalizer<SurveyBankDrawResolver>()),
            [],
            clock.Object);

        return new AuthoringServices
        {
            Manager = manager,
            Publisher = publisher,
            Validator = validator,
            Registry = registry,
            DraftEditor = draftEditor,
            Templates = templates,
            TemplateStore = templateStore,
            SurveyStore = surveyStore,
            VersionStore = versionStore,
            CollectorStore = collectorStore,
            BankQuestionStore = bankQuestionStore,
        };
    }

    public ValueTask DisposeAsync()
    {
        _memoryCache.Dispose();
        TemporarySqliteDatabase.DisposeAndDelete(Store, _databasePath);

        return ValueTask.CompletedTask;
    }
}
