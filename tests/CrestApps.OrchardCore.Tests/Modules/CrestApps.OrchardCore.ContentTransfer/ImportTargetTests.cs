using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

/// <summary>
/// Import targets: a file can be imported into a content type, as before, or into the records of another store (such
/// as skills) through a registered importer, with the same background run, progress and failed rows, and each target's
/// own permission.
/// </summary>
public sealed class ImportTargetTests
{
    private static readonly ClaimsPrincipal _user = new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")], "test"));

    [Fact]
    public async Task AContentTypeThatAllowsBulkImport_IsAContentTypeTarget_WithTheColumnsOfTheContentImport()
    {
        // Arrange (regression: content types import as they did)
        var columns = new[] { new ImportColumn { Name = "Title", IsRequired = true } };
        var importManager = new Mock<IContentImportManager>();
        importManager.Setup(manager => manager.GetColumnsAsync(It.Is<ImportContentContext>(context => context.ContentTypeDefinition.Name == "Article")))
            .ReturnsAsync(columns);
        var resolver = CreateResolver([Definition("Article")], importManager: importManager.Object);

        // Act
        var target = await resolver.FindAsync("Article");

        // Assert
        Assert.NotNull(target);
        Assert.True(target.IsContentType);
        Assert.Equal("Article", target.Name);
        Assert.Equal("Article display", target.DisplayName);
        Assert.Same(columns, await target.GetColumnsAsync(TestContext.Current.CancellationToken));
        Assert.IsNotAssignableFrom<IContentTransferRecordImporter>(target);
    }

    [Fact]
    public async Task AContentTypeWithoutBulkImport_OrAnUnknownName_IsNoTarget()
    {
        // Arrange
        var resolver = CreateResolver([Definition("Secret", allowBulkImport: false)]);

        // Act
        var secret = await resolver.FindAsync("Secret");
        var unknown = await resolver.FindAsync("Nothing.Here");

        // Assert
        Assert.Null(secret);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task ARegisteredImporter_IsFoundByName_AndAnImporterNamedLikeAContentType_IsIgnored()
    {
        // Arrange: a name without a dot could shadow a content type, so it never wins.
        var skills = new FakeImporter("Skills.Skill", allowed: true);
        var shadow = new FakeImporter("Article", allowed: true);
        var resolver = CreateResolver([Definition("Article")], [skills, shadow]);

        // Act
        var found = await resolver.FindAsync("skills.skill");
        var article = await resolver.FindAsync("Article");
        var listed = await resolver.ListRecordImportersAsync(_user);

        // Assert
        Assert.Same(skills, found);
        Assert.True(article.IsContentType);
        Assert.Equal([skills], listed);
    }

    [Fact]
    public async Task EachTarget_KeepsItsOwnPermission()
    {
        // Arrange: the authorization service grants everything, as for someone allowed to import every content type.
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var refused = new FakeImporter("Skills.Skill", allowed: false);
        var allowed = new FakeImporter("Skills.Other", allowed: true);
        var resolver = CreateResolver([Definition("Article")], [refused, allowed], authorization: authorization.Object);

        // Act
        var canImportArticle = await resolver.CanImportAsync(_user, "Article");
        var canImportRefused = await resolver.CanImportAsync(_user, "Skills.Skill");
        var canImportAllowed = await resolver.CanImportAsync(_user, "Skills.Other");
        var canImportUnknown = await resolver.CanImportAsync(_user, "Nothing.Here");
        var listed = await resolver.ListRecordImportersAsync(_user);

        // Assert: a content type is the content import permission for that type, as before.
        Assert.True(canImportArticle);
        authorization.Verify(service => service.AuthorizeAsync(
            _user,
            "Article",
            It.Is<IEnumerable<IAuthorizationRequirement>>(requirements => requirements.OfType<PermissionRequirement>().Single().Permission.Name == ContentTransferPermissions.ImportContentFromFile.Name)), Times.Once);
        Assert.False(canImportRefused);
        Assert.True(canImportAllowed);
        Assert.False(canImportUnknown);
        Assert.Equal([allowed], listed);
    }

    [Fact]
    public async Task ARecordBatch_CountsTheRowsItSaved_AndRecordsEachFailedRowWithItsReason()
    {
        // Arrange: row 4 failed an earlier run and is imported now.
        var importer = new FakeImporter("Skills.Skill", allowed: true, fail: row => row.Get("Name") == "bad" ? ("The name is taken.", "Duplicate") : default);
        var progress = new ImportFileProcessStatsPart { Errors = [4], ErrorMessages = new() { [4] = "Earlier failure" } };
        var rows = new[]
        {
            Row(3, "good"),
            Row(4, "fixed"),
            Row(5, "bad"),
        };

        // Act
        await RecordImportBatch.ImportAsync(importer, new ContentTransferEntry { EntryId = "e1" }, rows, progress, new EchoLocalizer(), new ImportRunTelemetry(NullLogger.Instance, "e1", 0), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, progress.ImportedCount);
        Assert.Equal([5], progress.Errors);
        Assert.Equal("The name is taken.", progress.ErrorMessages[5]);
        Assert.False(progress.ErrorMessages.ContainsKey(4));
        Assert.Equal([5], progress.ErrorCategories["Duplicate"]);
        Assert.Equal(["good", "fixed", "bad"], importer.Received);
    }

    [Fact]
    public async Task ARecordBatchThatThrows_FailsEveryRowOfTheBatch_WithTheMessage()
    {
        // Arrange
        var importer = new FakeImporter("Skills.Skill", allowed: true, throws: new InvalidOperationException("The store is offline."));
        var progress = new ImportFileProcessStatsPart { Errors = [], ErrorMessages = [] };

        // Act
        await RecordImportBatch.ImportAsync(importer, new ContentTransferEntry { EntryId = "e1" }, [Row(1, "a"), Row(2, "b")], progress, new EchoLocalizer(), new ImportRunTelemetry(NullLogger.Instance, "e1", 0), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, progress.ImportedCount);
        Assert.Equal([1, 2], progress.Errors.Order());
        Assert.Equal("The store is offline.", progress.ErrorMessages[2]);
    }

    private static ContentTransferImportRow Row(int index, string name)
        => new(index, [new KeyValuePair<string, string>("Name", name)]);

    private static ContentTypeDefinition Definition(string name, bool allowBulkImport = true)
        => new(name, name + " display", [], new JsonObject
        {
            [nameof(ContentTypeTransferSettings)] = new JsonObject { [nameof(ContentTypeTransferSettings.AllowBulkImport)] = allowBulkImport },
        });

    private static DefaultContentTransferImportTargetResolver CreateResolver(
        ContentTypeDefinition[] definitions,
        IContentTransferRecordImporter[] importers = null,
        IContentImportManager importManager = null,
        IAuthorizationService authorization = null)
    {
        var definitionManager = new Mock<IContentDefinitionManager>();
        definitionManager.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => definitions.FirstOrDefault(definition => definition.Name == name));
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.NewAsync(It.IsAny<string>())).ReturnsAsync((string type) => new ContentItem { ContentType = type });

        return new DefaultContentTransferImportTargetResolver(
            importers ?? [],
            definitionManager.Object,
            contentManager.Object,
            importManager ?? Mock.Of<IContentImportManager>(),
            authorization ?? Mock.Of<IAuthorizationService>());
    }

    private sealed class FakeImporter(string name, bool allowed, Func<ContentTransferImportRow, (string Error, string Category)> fail = null, Exception throws = null) : IContentTransferRecordImporter
    {
        public List<string> Received { get; } = [];

        public string Name => name;

        public string DisplayName => name;

        public bool IsContentType => false;

        public Task<bool> AuthorizeAsync(ClaimsPrincipal user, IAuthorizationService authorizationService)
            => Task.FromResult(allowed);

        public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<ImportColumn>>([new ImportColumn { Name = "Name" }]);

        public Task ImportAsync(ContentTransferImportBatch batch, CancellationToken cancellationToken = default)
        {
            if (throws is not null)
            {
                throw throws;
            }

            foreach (var row in batch.Rows)
            {
                Received.Add(row.Get("name"));

                if (fail?.Invoke(row) is { Error: not null } failure)
                {
                    row.Fail(failure.Error, failure.Category);
                }
            }

            return Task.CompletedTask;
        }
    }

    private sealed class EchoLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
