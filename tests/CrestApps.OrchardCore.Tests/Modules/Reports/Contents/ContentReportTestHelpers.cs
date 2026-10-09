using System.Globalization;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.Contents;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.Contents.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Security;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Reports.Contents;

/// <summary>
/// A localizer that returns the English source text with its arguments filled in.
/// </summary>
internal sealed class ContentReportTestLocalizer<T> : IStringLocalizer<T>
{
    public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments), resourceNotFound: false);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return [];
    }
}

/// <summary>
/// Builds content definitions, content items, and the content report services the tests run against.
/// </summary>
internal static class ContentReportTestHelpers
{
    public static ClaimsPrincipal User { get; } = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "designer")], "Test"));

    /// <summary>
    /// A customer type with one field of every built-in field type, two unknown field types, and common parts.
    /// </summary>
    public static ContentTypeDefinition CustomerType()
    {
        return Type(
            "Customer",
            "Customer",
            stereotype: null,
            Part(
                "Customer",
                "Customer",
                "Customer details",
                Field("Email", "TextField", "Email"),
                Field("Balance", "NumericField", "Balance"),
                Field("Vip", "BooleanField", "VIP"),
                Field("Birthday", "DateField", "Birthday"),
                Field("LastContact", "DateTimeField", "Last contact"),
                Field("PreferredTime", "TimeField", "Preferred time"),
                Field("Notes", "HtmlField", "Notes"),
                Field("Bio", "MarkdownField", "Bio"),
                Field("Website", "LinkField", "Website"),
                Field("Tags", "MultiTextField", "Tags"),
                Field("Photos", "MediaField", "Photos"),
                Field("Categories", "TaxonomyField", "Categories"),
                Field("AccountManagers", "UserPickerField", "Account managers"),
                Field("Phone", "PhoneField", "Phone"),
                Field("Rating", "StarRatingField", "Rating"),
                Field("Nickname", "NicknameField", "Nickname")),
            Part("TitlePart", "TitlePart", "Title"),
            Part("AutoroutePart", "AutoroutePart", "Autoroute"));
    }

    /// <summary>
    /// An order type that points at a customer through a content picker field.
    /// </summary>
    public static ContentTypeDefinition OrderType()
    {
        return Type(
            "Order",
            "Order",
            stereotype: null,
            Part(
                "Order",
                "Order",
                "Order",
                Field("Customer", "ContentPickerField", "Customer"),
                Field("Total", "NumericField", "Total")));
    }

    public static ContentTypeDefinition Type(string name, string displayName, string stereotype, params ContentTypePartDefinition[] parts)
    {
        var settings = new JsonObject();

        if (stereotype is not null)
        {
            settings["ContentTypeSettings"] = new JsonObject
            {
                ["Stereotype"] = stereotype,
            };
        }

        var definition = new ContentTypeDefinition(name, displayName, parts, settings);

        foreach (var part in definition.Parts)
        {
            part.ContentTypeDefinition = definition;
        }

        return definition;
    }

    public static ContentTypePartDefinition Part(string name, string partDefinitionName, string displayName, params ContentPartFieldDefinition[] fields)
    {
        var settings = new JsonObject
        {
            ["ContentTypePartSettings"] = new JsonObject
            {
                ["DisplayName"] = displayName,
            },
        };

        return new ContentTypePartDefinition(name, new ContentPartDefinition(partDefinitionName, fields, []), settings);
    }

    public static ContentPartFieldDefinition Field(string name, string fieldType, string displayName)
    {
        var settings = new JsonObject
        {
            ["ContentPartFieldSettings"] = new JsonObject
            {
                ["DisplayName"] = displayName,
            },
        };

        return new ContentPartFieldDefinition(new ContentFieldDefinition(fieldType), name, settings);
    }

    /// <summary>
    /// Creates a published content item whose parts are the properties of <paramref name="contentJson"/>.
    /// </summary>
    public static ContentItem Item(string contentType, string contentItemId, string contentJson = "{}", DateTime? createdUtc = null, string displayText = null)
    {
        var contentItem = new ContentItem
        {
            ContentItemId = contentItemId,
            ContentItemVersionId = contentItemId + "-v1",
            ContentType = contentType,
            DisplayText = displayText ?? contentItemId,
            Published = true,
            Latest = true,
            Owner = "owner-1",
            Author = "admin",
            CreatedUtc = createdUtc ?? new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            ModifiedUtc = createdUtc ?? new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
            PublishedUtc = createdUtc ?? new DateTime(2026, 1, 10, 8, 0, 0, DateTimeKind.Utc),
        };

        var content = (JsonObject)contentItem.Content;

        foreach (var (name, node) in JsonNode.Parse(contentJson).AsObject())
        {
            content[name] = node?.DeepClone();
        }

        return contentItem;
    }

    /// <summary>
    /// An authorization service that lets the principal view only the given content types.
    /// </summary>
    public static Mock<IAuthorizationService> AuthorizationFor(params string[] viewableTypes)
    {
        var authorizationService = new Mock<IAuthorizationService>();

        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            {
                var allowed = user is not null &&
                    resource is ContentItem contentItem &&
                    viewableTypes.Contains(contentItem.ContentType, StringComparer.Ordinal) &&
                    requirements.OfType<PermissionRequirement>().All(requirement => requirement.Permission.Name == "ViewContent");

                return allowed
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed();
            });

        return authorizationService;
    }

    public static Mock<IContentDefinitionManager> Definitions(params ContentTypeDefinition[] definitions)
    {
        var manager = new Mock<IContentDefinitionManager>();

        manager
            .Setup(service => service.ListTypeDefinitionsAsync())
            .ReturnsAsync(definitions);
        manager
            .Setup(service => service.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => definitions.FirstOrDefault(definition => string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase)));

        return manager;
    }

    /// <summary>
    /// Builds the services the module registers, plus the given collaborators.
    /// </summary>
    public static ServiceProvider Services(
        IContentDefinitionManager contentDefinitionManager = null,
        IAuthorizationService authorizationService = null,
        ISession session = null,
        Action<IServiceCollection> configure = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(typeof(IStringLocalizer<>), typeof(ContentReportTestLocalizer<>));
        services.AddSingleton(contentDefinitionManager ?? Definitions().Object);
        services.AddSingleton(authorizationService ?? AuthorizationFor().Object);
        services.AddSingleton(session ?? Mock.Of<ISession>());

        new Startup().ConfigureServices(services);
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Reads every report field of a content item the way the data source does, without loading related data.
    /// </summary>
    public static Dictionary<string, object> ReadValues(ContentReportSchemaBuilder builder, ContentTypeDefinition definition, ContentItem contentItem)
    {
        var context = new ContentReportQueryContext(definition.Name, User, [contentItem], session: null, _ => Task.FromResult(true));

        return builder.Build(definition).ToDictionary(
            field => field.Descriptor.Name,
            field => ContentsReportDataSource.ToReportValue(field.GetValue(contentItem, context), field.Descriptor.DataType),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Creates a SQLite store with the content item index table.
    /// </summary>
    public static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        store.RegisterIndexes([new ContentItemIndexProvider()]);

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentItemVersionId", column => column.WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest")
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<DateTime>("ModifiedUtc", column => column.Nullable())
            .Column<DateTime>("PublishedUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc", column => column.Nullable())
            .Column<string>("Owner", column => column.Nullable().WithLength(255))
            .Column<string>("Author", column => column.Nullable().WithLength(255))
            .Column<string>("DisplayText", column => column.Nullable().WithLength(255)));

        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return store;
    }

    public static string DatabasePath(string suffix)
    {
        return Path.Combine(Path.GetTempPath(), $"content-reports-{suffix}-{Guid.NewGuid():N}.db");
    }
}
