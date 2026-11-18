using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentAccessControl.Indexes;
using CrestApps.OrchardCore.ContentAccessControl.Migrations;
using CrestApps.OrchardCore.Roles.Core;
using CrestApps.OrchardCore.Roles.Core.Indexes;
using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tests.ContentAccessControl;

/// <summary>
/// The roles allowed to read each published restricted item are indexed, so a list or a search can keep to what its
/// reader may open in SQL: one row per picked role, Anonymous and Authenticated for an item open to everyone, and none
/// for drafts or items of types that are not restricted.
/// </summary>
public sealed class ContentAccessRoleIndexTests
{
    private const string Restricted = "Handbook";
    private const string Open = "Page";

    [Fact]
    public async Task EachPublishedRestrictedItem_IsIndexed_WithTheRolesAllowedToReadIt()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Item("members", Restricted, ["Member", "Editor"]));
            await session.SaveAsync(Item("everyone", Restricted, []));
            await session.SaveAsync(Item("draft", Restricted, ["Member"], published: false));
            await session.SaveAsync(Item("page", Open, ["Member"]));
        });

        // Act
        var rows = await harness.RunAsync(
            async services => await services.GetRequiredService<ISession>().QueryIndex<ContentAccessRoleIndex>().ListAsync(TestContext.Current.CancellationToken),
            commit: false);

        // Assert
        Assert.Equal(
            ["everyone:Anonymous", "everyone:Authenticated", "members:Editor", "members:Member"],
            rows.Select(row => $"{row.ContentItemId}:{row.RoleName}").Order(StringComparer.Ordinal));
        Assert.All(rows, row => Assert.Equal(Restricted, row.ContentType));
    }

    [Fact]
    public async Task AReadersRoles_FindInSql_OnlyTheItemsTheyMayOpen()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            await session.SaveAsync(Item("members", Restricted, ["Member"]));
            await session.SaveAsync(Item("signed-in", Restricted, ["Authenticated"]));
            await session.SaveAsync(Item("visitors", Restricted, ["Anonymous"]));
            await session.SaveAsync(Item("everyone", Restricted, []));
        });

        // Act
        var found = await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();

            async Task<List<string>> FindAsync(System.Security.Claims.ClaimsPrincipal user)
            {
                var roles = ContentAccessRoles.GetReaderRoles(user).ToArray();
                var rows = await session.QueryIndex<ContentAccessRoleIndex>(row => row.RoleName.IsIn(roles)).ListAsync(TestContext.Current.CancellationToken);

                return [.. rows.Select(row => row.ContentItemId).Distinct().Order(StringComparer.Ordinal)];
            }

            return new
            {
                Visitor = await FindAsync(RoleBasedContentItemAuthorizationHandlerTests.Visitor()),
                SignedIn = await FindAsync(RoleBasedContentItemAuthorizationHandlerTests.SignedIn()),
                Member = await FindAsync(RoleBasedContentItemAuthorizationHandlerTests.SignedIn("Member")),
            };
        }, commit: false);

        // Assert
        Assert.Equal(["everyone", "visitors"], found.Visitor);
        Assert.Equal(["everyone", "signed-in"], found.SignedIn);
        Assert.Equal(["everyone", "members", "signed-in"], found.Member);
    }

    [Fact]
    public async Task UnpublishingOrUnpickingRoles_UpdatesTheRows()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await harness.RunAsync(async services => await services.GetRequiredService<ISession>().SaveAsync(Item("members", Restricted, ["Member"])));

        // Act
        await harness.RunAsync(async services =>
        {
            var session = services.GetRequiredService<ISession>();
            var item = await session.Query<ContentItem>().FirstOrDefaultAsync(TestContext.Current.CancellationToken);
            item.Alter<RolePickerPart>(part => part.RoleNames = ["Authenticated"]);
            await session.SaveAsync(item);
        });
        var rows = await harness.RunAsync(
            async services => await services.GetRequiredService<ISession>().QueryIndex<ContentAccessRoleIndex>().ListAsync(TestContext.Current.CancellationToken),
            commit: false);

        // Assert
        Assert.Equal(["Authenticated"], rows.Select(row => row.RoleName));
    }

    internal static IServiceProvider DefinitionServices(params string[] restrictedTypes)
    {
        var definitions = new Mock<IContentDefinitionManager>();

        definitions.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync((string name) => TypeDefinition(name, restrictedTypes.Contains(name)));

        return new ServiceCollection().AddSingleton(definitions.Object).BuildServiceProvider();
    }

    internal static ContentTypeDefinition TypeDefinition(string name, bool restricted)
    {
        var settings = new JsonObject
        {
            [nameof(RolePickerPartContentAccessControlSettings)] = new JsonObject { [nameof(RolePickerPartContentAccessControlSettings.IsContentRestricted)] = restricted },
        };

        return new ContentTypeDefinition(name, name, [new ContentTypePartDefinition(nameof(RolePickerPart), new ContentPartDefinition(nameof(RolePickerPart)), settings)], new JsonObject());
    }

    private static Task<CalendarTestHarness> CreateAsync()
        => CalendarTestHarness.CreateAsync(
            createSchema: ContentAccessControlMigrations.CreateRoleIndexTableAsync,
            indexProviders: [new ContentAccessRoleIndexProvider(DefinitionServices(Restricted))]);

    private static ContentItem Item(string id, string type, string[] roles, bool published = true)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = type,
            Published = published,
            Latest = true,
        };

        item.Weld(new RolePickerPart { RoleNames = roles });

        return item;
    }
}
