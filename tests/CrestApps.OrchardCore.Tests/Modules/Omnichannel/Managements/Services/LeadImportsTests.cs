using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// A lead records each file import it arrived in or was updated by, so the leads of one file can be loaded together.
/// </summary>
public sealed class LeadImportsTests
{
    [Fact]
    public void Record_AddsTheImportWithItsFileNameAndUploadTime()
    {
        // Arrange
        var part = new LeadPart();
        var entry = Entry("july", "July2020.csv");

        // Act
        var added = LeadImports.Record(part, entry);

        // Assert
        Assert.True(added);
        var import = Assert.Single(part.Imports);
        Assert.Equal("july", import.EntryId);
        Assert.Equal("July2020.csv", import.FileName);
        Assert.Equal(entry.CreatedUtc, import.ImportedUtc);
    }

    [Fact]
    public void Record_WhenTheLeadAlreadyRecordsTheImport_DoesNotRecordItTwice()
    {
        // Arrange
        // A paused import reads its last batch again when it resumes.
        var part = new LeadPart();
        LeadImports.Record(part, Entry("july", "July2020.csv"));

        // Act
        var added = LeadImports.Record(part, Entry("july", "July2020.csv"));

        // Assert
        Assert.False(added);
        Assert.Single(part.Imports);
    }

    [Fact]
    public void Record_KeepsEveryFileTheLeadArrivedIn()
    {
        // Arrange
        var part = new LeadPart();
        LeadImports.Record(part, Entry("july", "July2020.csv"));

        // Act
        LeadImports.Record(part, Entry("august", "August2020.csv"));

        // Assert
        Assert.Equal(["july", "august"], part.Imports.Select(import => import.EntryId));
    }

    [Fact]
    public void Record_IgnoresAnExportAndALeadThatIsNotBeingImported()
    {
        // Arrange
        var part = new LeadPart();
        var export = Entry("export", "Leads_Export.csv");
        export.Direction = ContentTransferDirection.Export;

        // Act
        var exported = LeadImports.Record(part, export);
        var saved = LeadImports.Record(part, null);

        // Assert
        Assert.False(exported);
        Assert.False(saved);
        Assert.Empty(part.Imports);
    }

    private static ContentTransferEntry Entry(string entryId, string fileName)
        => new()
        {
            EntryId = entryId,
            UploadedFileName = fileName,
            CreatedUtc = new DateTime(2020, 7, 3, 14, 0, 0, DateTimeKind.Utc),
            Direction = ContentTransferDirection.Import,
        };
}
