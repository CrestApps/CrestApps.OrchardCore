using System.Globalization;
using CrestApps.OrchardCore.KnowledgeBase;
using CrestApps.OrchardCore.KnowledgeBase.Migrations;
using CrestApps.OrchardCore.Roles.Core.Models;
using CrestApps.OrchardCore.Tests.Modules.Accounts;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Tests.KnowledgeBase;

/// <summary>
/// Orchard Core's content type editor reads each part's position as a whole number; the category's part at "2.5" made
/// the Knowledge Article type's page fail with a FormatException.
/// </summary>
public sealed class KnowledgeArticlePartPositionsTests
{
    [Fact]
    public void EveryPartOfANewType_HasAWholeDistinctPosition()
    {
        var positions = KnowledgeArticlePartPositions.Order.Select(KnowledgeArticlePartPositions.Of).ToList();

        Assert.All(positions, position => int.Parse(position, CultureInfo.InvariantCulture));
        Assert.Equal(positions.Count, positions.Distinct().Count());
        Assert.Equal("2", KnowledgeArticlePartPositions.Of(KnowledgeBaseConstants.WhoCanReadItPart));
        Assert.Equal("4", KnowledgeArticlePartPositions.Of(KnowledgeBaseConstants.ArticleType));
    }

    // Version 2 put the type's own part, with the category field, at 2.5.
    [Fact]
    public void ATypeWithTheCategoryAt2Point5_IsRenumberedInTheSameOrder()
    {
        var positions = KnowledgeArticlePartPositions.Renumber(
        [
            ("TitlePart", "1"),
            (KnowledgeBaseConstants.PartName, "2"),
            ("MarkdownBodyPart", "3"),
            ("AutoroutePart", "4"),
            (KnowledgeBaseConstants.ArticleType, "2.5"),
        ]);

        Assert.Equal("1", positions["TitlePart"]);
        Assert.Equal("2", positions[KnowledgeBaseConstants.PartName]);
        Assert.Equal("3", positions[KnowledgeBaseConstants.ArticleType]);
        Assert.Equal("4", positions["MarkdownBodyPart"]);
        Assert.Equal("5", positions["AutoroutePart"]);
    }

    // A site that went from version 1 to 2 has the category's part and the body at 3: the category comes first.
    [Fact]
    public void TwoPartsInTheSamePlace_KeepTheTypesUsualOrder_AndAnAddedPartStaysLast()
    {
        var positions = KnowledgeArticlePartPositions.Renumber(
        [
            ("TitlePart", "1"),
            (KnowledgeBaseConstants.PartName, "2"),
            ("MarkdownBodyPart", "3"),
            (KnowledgeBaseConstants.ArticleType, "3"),
            ("AutoroutePart", "4"),
            ("SeoMetaPart", "9"),
        ]);

        Assert.Equal(["TitlePart", KnowledgeBaseConstants.PartName, KnowledgeBaseConstants.ArticleType, "MarkdownBodyPart", "AutoroutePart", "SeoMetaPart"], positions.OrderBy(pair => int.Parse(pair.Value, CultureInfo.InvariantCulture)).Select(pair => pair.Key));
    }

    // Positions an administrator set, all whole and distinct, are left alone.
    [Fact]
    public void WholeDistinctPositions_AreLeftAsTheyAre()
        => Assert.Empty(KnowledgeArticlePartPositions.Renumber([("TitlePart", "5"), (KnowledgeBaseConstants.PartName, "1"), ("MarkdownBodyPart", "10")]));

    // On a site at version 1 the steps to 2 and 3 ran in one start; step 3 read the cached type, from before step 2
    // added the category's part, so the category stayed at 3 with the body and showed after it.
    [Fact]
    public async Task AVersion1Site_UpgradedInOneStart_PutsTheCategoryBeforeTheBody()
    {
        // Arrange
        var definitions = new CachingDefinitions();
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder()
            .WithName(KnowledgeBaseConstants.ArticleType)
            .WithPart("TitlePart", part => part.WithPosition("1"))
            .WithPart(KnowledgeBaseConstants.PartName, part => part.WithPosition("2"))
            .WithPart("MarkdownBodyPart", part => part.WithPosition("3"))
            .WithPart("AutoroutePart", part => part.WithPosition("4"))
            .Build());
        var migrations = Migrations(definitions);

        // Act: what steps 1 to 2, 2 to 3 and 3 to 4 change in the content definitions, in one go.
        await definitions.GetTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
        await migrations.DefineCategoriesAsync();
        await migrations.UpdateFrom2Async();
        await migrations.UpdateFrom3Async();

        // Assert
        var type = await definitions.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
        Assert.Equal(["TitlePart", KnowledgeBaseConstants.PartName, KnowledgeBaseConstants.ArticleType, "MarkdownBodyPart", "AutoroutePart"], Ordered(type));
        Assert.Equal(["1", "2", "3", "4", "5"], type.Parts.Select(Position).Order());
        var body = type.Parts.Single(part => part.Name == "MarkdownBodyPart");
        Assert.Equal("Wysiwyg", body.Editor());
        Assert.Equal("Body", body.GetSettings<ContentTypePartSettings>().DisplayName);
    }

    // A site already at version 3 with the category and the body both at 3.
    [Fact]
    public async Task AVersion3SiteWithTheCategoryAndTheBodyTogether_IsRenumbered_AndTheBodyIsNamed()
    {
        // Arrange
        var definitions = new CachingDefinitions();
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder()
            .WithName(KnowledgeBaseConstants.ArticleType)
            .WithPart("TitlePart", part => part.WithPosition("1"))
            .WithPart(KnowledgeBaseConstants.PartName, part => part.WithPosition("2"))
            .WithPart("MarkdownBodyPart", part => part.WithPosition("3").WithEditor("Wysiwyg"))
            .WithPart("AutoroutePart", part => part.WithPosition("4"))
            .WithPart(KnowledgeBaseConstants.ArticleType, part => part.WithPosition("3"))
            .Build());

        // Act
        Assert.Equal(4, await Migrations(definitions).UpdateFrom3Async());

        // Assert
        var type = await definitions.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
        Assert.Equal(["TitlePart", KnowledgeBaseConstants.PartName, KnowledgeBaseConstants.ArticleType, "MarkdownBodyPart", "AutoroutePart"], Ordered(type));
        Assert.Equal("Body", type.Parts.Single(part => part.Name == "MarkdownBodyPart").GetSettings<ContentTypePartSettings>().DisplayName);
    }

    // Version 5 gives the type Content Access Control's role picker, "Who can read it", right after the title.
    [Fact]
    public async Task AVersion4Site_GetsWhoCanReadIt_AfterTheTitle_WithWholePositions()
    {
        // Arrange
        var definitions = new CachingDefinitions();
        await definitions.StoreTypeDefinitionAsync(new ContentTypeDefinitionBuilder()
            .WithName(KnowledgeBaseConstants.ArticleType)
            .WithPart("TitlePart", part => part.WithPosition("1"))
            .WithPart(KnowledgeBaseConstants.PartName, part => part.WithPosition("2"))
            .WithPart(KnowledgeBaseConstants.ArticleType, part => part.WithPosition("3"))
            .WithPart("MarkdownBodyPart", part => part.WithPosition("4"))
            .WithPart("AutoroutePart", part => part.WithPosition("5"))
            .Build());
        var migrations = Migrations(definitions);

        // Act: twice, as a second run changes nothing.
        await migrations.AddWhoCanReadItAsync();
        await migrations.AddWhoCanReadItAsync();

        // Assert
        var type = await definitions.LoadTypeDefinitionAsync(KnowledgeBaseConstants.ArticleType);
        Assert.Equal(["TitlePart", KnowledgeBaseConstants.WhoCanReadItPart, KnowledgeBaseConstants.PartName, KnowledgeBaseConstants.ArticleType, "MarkdownBodyPart", "AutoroutePart"], Ordered(type));
        Assert.Equal(["1", "2", "3", "4", "5", "6"], type.Parts.Select(Position).Order());
        var picker = type.Parts.Single(part => part.Name == KnowledgeBaseConstants.WhoCanReadItPart);
        Assert.Equal("Who can read it", picker.GetSettings<ContentTypePartSettings>().DisplayName);
        Assert.True(picker.GetSettings<RolePickerPartContentAccessControlSettings>().IsContentRestricted);
        Assert.True(picker.GetSettings<RolePickerPartSettings>().AllowSelectMultiple);
    }

    private static KnowledgeBaseMigrations Migrations(IContentDefinitionManager definitions)
        => new(definitions, null, null, NullLogger<KnowledgeBaseMigrations>.Instance);

    private static string Position(ContentTypePartDefinition part)
        => part.GetSettings<ContentTypePartSettings>().Position;

    private static IEnumerable<string> Ordered(ContentTypeDefinition type)
        => type.Parts.OrderBy(part => int.Parse(Position(part), CultureInfo.InvariantCulture)).Select(part => part.Name);

    // As Orchard Core's: a read for display keeps returning the copy it first read in the scope; the load before a
    // change is the stored one.
    private sealed class CachingDefinitions : IContentDefinitionManager
    {
        private readonly InMemoryContentDefinitionManager _store = new();
        private readonly Dictionary<string, ContentTypeDefinition> _cached = new(StringComparer.Ordinal);

        public async Task<ContentTypeDefinition> GetTypeDefinitionAsync(string name)
        {
            if (!_cached.TryGetValue(name, out var type))
            {
                type = await _store.LoadTypeDefinitionAsync(name);
                _cached[name] = type;
            }

            return type;
        }

        public Task<ContentTypeDefinition> LoadTypeDefinitionAsync(string name) => _store.LoadTypeDefinitionAsync(name);

        public Task<IEnumerable<ContentTypeDefinition>> LoadTypeDefinitionsAsync() => _store.LoadTypeDefinitionsAsync();

        public Task<IEnumerable<ContentTypeDefinition>> ListTypeDefinitionsAsync() => _store.ListTypeDefinitionsAsync();

        public Task<IEnumerable<ContentPartDefinition>> LoadPartDefinitionsAsync() => _store.LoadPartDefinitionsAsync();

        public Task<IEnumerable<ContentPartDefinition>> ListPartDefinitionsAsync() => _store.ListPartDefinitionsAsync();

        public Task<ContentPartDefinition> LoadPartDefinitionAsync(string name) => _store.LoadPartDefinitionAsync(name);

        public Task<ContentPartDefinition> GetPartDefinitionAsync(string name) => _store.GetPartDefinitionAsync(name);

        public Task DeleteTypeDefinitionAsync(string name) => _store.DeleteTypeDefinitionAsync(name);

        public Task DeletePartDefinitionAsync(string name) => _store.DeletePartDefinitionAsync(name);

        public Task StoreTypeDefinitionAsync(ContentTypeDefinition contentTypeDefinition) => _store.StoreTypeDefinitionAsync(contentTypeDefinition);

        public Task StorePartDefinitionAsync(ContentPartDefinition contentPartDefinition) => _store.StorePartDefinitionAsync(contentPartDefinition);

        public Task<string> GetIdentifierAsync() => _store.GetIdentifierAsync();
    }
}
