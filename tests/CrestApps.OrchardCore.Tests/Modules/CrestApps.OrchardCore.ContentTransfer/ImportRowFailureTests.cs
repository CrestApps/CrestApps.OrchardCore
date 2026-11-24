using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.BackgroundTasks;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ImportRowFailureTests
{
    [Fact]
    public async Task ARowAHandlerFails_IsRecordedWithItsReason_AndNothingIsSaved()
    {
        // Arrange
        // A handler's exception is logged and the row goes on, so a handler that cannot import a row (an item of a
        // package that holds something unsupported) had no way to keep a broken content item from being saved.
        var importManager = new Mock<IContentImportManager>();
        importManager.Setup(manager => manager.ImportAsync(It.IsAny<ContentImportContext>()))
            .Callback<ContentImportContext>(context => context.Fail("The item uses a hotspot interaction.", "Not supported"))
            .Returns(Task.CompletedTask);
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).ReturnsAsync(new ContentValidateResult());
        var progress = new ImportFileProcessStatsPart { Errors = [], ErrorMessages = [] };
        var table = new DataTable();
        table.Columns.Add("Title");
        var row = table.NewRow();

        // Act
        await ImportFilesBackgroundTask.ProcessRowAsync(
            new ContentTransferEntry { EntryId = "entry" },
            new ContentItem { ContentType = "Question" },
            3,
            row,
            table.Columns,
            new ContentTypeDefinition("Question", "Question"),
            contentManager.Object,
            importManager.Object,
            progress,
            new EchoLocalizer(),
            new ImportRunTelemetry(NullLogger.Instance, "entry", 0),
            isNew: true);

        // Assert
        contentManager.Verify(manager => manager.CreateAsync(It.IsAny<ContentItem>(), It.IsAny<VersionOptions>()), Times.Never);
        Assert.Equal(0, progress.ImportedCount);
        Assert.Equal("The item uses a hotspot interaction.", progress.ErrorMessages[3]);
        Assert.Equal([3], progress.ErrorCategories["Not supported"]);
    }

    private sealed class EchoLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
