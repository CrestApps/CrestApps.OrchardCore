using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.ContentAccessControl.Indexes;
using CrestApps.OrchardCore.ContentAccessControl.Migrations;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.ContentAccessControl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Markdown.Models;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Settings;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase.AI;

/// <summary>
/// The Knowledge Base AI tools on a real store: the help center's index and Content Access Control's role index in SQLite,
/// the categories Accounts › Passwords and Billing, published articles served by the content manager, a caller published
/// through <see cref="IUserAccessor"/>, <c>ViewContent</c> refused on chosen articles, a site base URL and a local clock
/// five hours behind UTC.
/// </summary>
internal sealed class KnowledgeBaseAIToolHarness : IAsyncDisposable
{
    public const string BaseUrl = "https://example.com/";

    // The names below avoid Accounts, Staff and the like: in a test that imports them with "using static", a bare
    // Staff or Accounts would bind to the CrestApps.OrchardCore.Staff or .Accounts namespace first.
    public const string AccountsCategory = "t-accounts";

    public const string PasswordsCategory = "t-passwords";

    public const string BillingCategory = "t-billing";

    public static readonly string[] ForEveryone = [];

    public static readonly string[] ForSignedIn = ["Authenticated"];

    public static readonly string[] ForStaff = ["Editor", "Administrator"];

    private readonly Dictionary<string, ContentItem> _published = new(StringComparer.Ordinal);

    private CalendarTestHarness _store;

    private KnowledgeBaseAIToolHarness()
    {
    }

    /// <summary>
    /// Gets the content manager double, to check what the tools loaded.
    /// </summary>
    public Mock<IContentManager> ContentManager { get; } = new();

    /// <summary>
    /// Gets the articles <c>ViewContent</c> is refused on.
    /// </summary>
    public HashSet<string> Refused { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the caller the user accessor publishes.
    /// </summary>
    public ClaimsPrincipal Caller { get; set; }

    /// <summary>
    /// A signed-in person with no role that matters.
    /// </summary>
    public static ClaimsPrincipal Dana()
        => CalendarTestHarness.User("dana");

    /// <summary>
    /// A signed-in staff member, in the Editor role.
    /// </summary>
    public static ClaimsPrincipal StaffCaller()
        => CalendarTestHarness.User("staff", "role:Editor");

    public static async Task<KnowledgeBaseAIToolHarness> CreateAsync()
    {
        var harness = new KnowledgeBaseAIToolHarness();

        harness.ContentManager
            .Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((IEnumerable<string> ids, VersionOptions options) => ids.Where(harness._published.ContainsKey).Select(id => harness._published[id]).ToList());
        harness.ContentManager
            .Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions options) => id == "taxonomy" ? Taxonomy() : harness._published.GetValueOrDefault(id));

        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType)).ReturnsAsync(ArticleType());

        var site = new Mock<ISite>();
        site.SetupGet(value => value.BaseUrl).Returns(BaseUrl);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var localClock = new Mock<ILocalClock>();
        localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(-5)));

        harness._store = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddScoped<KnowledgeArticleSearch>();
                services.AddScoped<KnowledgeBaseCategories>();
                services.AddScoped<KnowledgeBaseHelpCenter>();
                services.AddSingleton(harness.ContentManager.Object);
                services.AddSingleton(definitions.Object);
                services.AddSingleton(siteService.Object);
                services.AddSingleton(localClock.Object);
                services.AddSingleton<IUserAccessor>(new CallerAccessor(harness));
                services.AddSingleton<IAuthorizationService>(new ViewContentAuthorizationService(harness.Refused));
            },
            async schema =>
            {
                await schema.CreateMapIndexTableAsync<KnowledgeArticleIndex>(table => table
                    .Column<string>("ContentItemId", column => column.WithLength(26))
                    .Column<string>("Title", column => column.WithLength(255))
                    .Column<string>("Summary", column => column.WithLength(1000))
                    .Column<string>("CategoryId", column => column.Nullable().WithLength(26))
                    .Column<DateTime>("PublishedUtc", column => column.Nullable())
                    .Column<int>("HelpfulCount", column => column.Nullable()));
                await ContentAccessControlMigrations.CreateRoleIndexTableAsync(schema);
            },
            [new KnowledgeArticleIndexProvider(), new ContentAccessRoleIndexProvider(ContentAccessRoleIndexTests.DefinitionServices(KnowledgeBaseConstants.ArticleType))]);

        return harness;
    }

    /// <summary>
    /// Stores content items, as publishing them would: the published ones are indexed and served by the content manager.
    /// </summary>
    /// <param name="items">The items.</param>
    public async Task AddAsync(params ContentItem[] items)
    {
        await _store.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            foreach (var item in items)
            {
                await session.SaveAsync(item);
            }
        });

        foreach (var item in items.Where(item => item.Published))
        {
            _published[item.ContentItemId] = item;
        }
    }

    /// <summary>
    /// Calls a tool in a scope of the store, as the chat does.
    /// </summary>
    /// <param name="tool">The tool.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <returns>What it returned.</returns>
    public Task<string> InvokeAsync(AIFunction tool, Dictionary<string, object> arguments = null)
        => _store.RunAsync(async services => (await tool.InvokeAsync(new AIFunctionArguments(arguments ?? []) { Services = services }, TestContext.Current.CancellationToken))?.ToString(), commit: false);

    /// <summary>
    /// Reads a tool's JSON result.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>Its root.</returns>
    public static JsonElement ReadJson(string result)
    {
        using var document = JsonDocument.Parse(result);

        return document.RootElement.Clone();
    }

    /// <summary>
    /// Makes an article, with its path <c>help/{id}</c>.
    /// </summary>
    public static ContentItem Article(string id, string title, string summary, string category, string[] roles, string body = null, int helpful = 0, bool published = true)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = KnowledgeBaseConstants.ArticleType,
            DisplayText = title,
            Published = published,
            Latest = true,
            PublishedUtc = published ? CalendarTestHarness.Now : null,
        };
        item.Apply(new KnowledgeArticlePart { Summary = summary, HelpfulCount = helpful, NotHelpfulCount = 1 });
        item.Weld(new RolePickerPart { RoleNames = roles });
        item.Apply(new AutoroutePart { Path = "help/" + id });

        if (body is not null)
        {
            item.Apply(new MarkdownBodyPart { Markdown = body });
        }

        if (category is not null)
        {
            ((JsonObject)item.Content)[KnowledgeBaseConstants.ArticleType] = new JsonObject
            {
                [KnowledgeBaseConstants.CategoryField] = new JsonObject { ["TaxonomyContentItemId"] = "taxonomy", ["TermContentItemIds"] = new JsonArray(category) },
            };
        }

        return item;
    }

    public async ValueTask DisposeAsync()
    {
        if (_store is not null)
        {
            await _store.DisposeAsync();
        }
    }

    // The Knowledge Article type's Category field, which names the categories' taxonomy.
    private static ContentTypeDefinition ArticleType()
    {
        var field = new ContentPartFieldDefinition(
            new ContentFieldDefinition("TaxonomyField"),
            KnowledgeBaseConstants.CategoryField,
            new JsonObject { ["TaxonomyFieldSettings"] = new JsonObject { ["TaxonomyContentItemId"] = "taxonomy" } });
        var part = new ContentPartDefinition(KnowledgeBaseConstants.ArticleType, [field], new JsonObject());

        return new ContentTypeDefinition(
            KnowledgeBaseConstants.ArticleType,
            "Knowledge Article",
            [new ContentTypePartDefinition(KnowledgeBaseConstants.ArticleType, part, new JsonObject())],
            new JsonObject());
    }

    // Accounts › Passwords, and Billing.
    private static ContentItem Taxonomy()
    {
        var taxonomy = new ContentItem { ContentItemId = "taxonomy", ContentType = "Taxonomy", Published = true, Latest = true };
        ((JsonObject)taxonomy.Content)["TaxonomyPart"] = new JsonObject
        {
            ["Terms"] = new JsonArray(
                Term(AccountsCategory, "Accounts", new JsonArray(Term(PasswordsCategory, "Passwords"))),
                Term(BillingCategory, "Billing")),
        };

        return taxonomy;
    }

    private static JsonObject Term(string id, string name, JsonArray children = null)
    {
        var term = new JsonObject { ["ContentItemId"] = id, ["ContentType"] = KnowledgeBaseConstants.CategoryTermType, ["DisplayText"] = name };

        if (children is not null)
        {
            term["Terms"] = children;
        }

        return term;
    }

    private sealed class CallerAccessor : IUserAccessor
    {
        private readonly KnowledgeBaseAIToolHarness _harness;

        public CallerAccessor(KnowledgeBaseAIToolHarness harness)
        {
            _harness = harness;
        }

        public ClaimsPrincipal User
        {
            get => _harness.Caller;
            set => _harness.Caller = value;
        }
    }

    // Grants every permission except ViewContent on the refused articles.
    private sealed class ViewContentAuthorizationService : IAuthorizationService
    {
        private readonly HashSet<string> _refused;

        public ViewContentAuthorizationService(HashSet<string> refused)
        {
            _refused = refused;
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(requirements.OfType<PermissionRequirement>().Any(requirement => requirement.Permission.Name == "ViewContent")
                && resource is ContentItem item
                && _refused.Contains(item.ContentItemId)
                    ? AuthorizationResult.Failed()
                    : AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
