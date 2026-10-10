using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The List filter of an inventory load offered the free-text list names the leads carried, which a file's own
/// "List" column could fill with anything, down to a person's name on every row. It now offers the files the leads
/// were imported from. These tests pin what that pick list offers.
/// </summary>
public sealed class LeadImportProviderTests
{
    private static readonly DateTime _july = new(2020, 7, 3, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _august = new(2020, 8, 10, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetImportsAsync_ListsEachImportOnceNewestFirstWithItsLeadCount()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("import-summaries");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));

                // A lead a later file updated belongs to both files.
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july), Import("august", "August2020.csv", _august));

                // A lead that was not imported adds no entry to pick.
                await LeadTestStore.SaveLeadAsync(seedSession);

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var imports = await CreateProvider(session).GetImportsAsync();

            // Assert
            Assert.Collection(
                imports,
                august =>
                {
                    Assert.Equal("august", august.EntryId);
                    Assert.Equal("August2020.csv", august.FileName);
                    Assert.Equal(_august, august.ImportedUtc);
                    Assert.Equal(1L, august.LeadCount);
                },
                july =>
                {
                    Assert.Equal("july", july.EntryId);
                    Assert.Equal("July2020.csv", july.FileName);
                    Assert.Equal(_july, july.ImportedUtc);
                    Assert.Equal(3L, july.LeadCount);
                });
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_NamesEachImportByItsFileAndMarksTheSavedOneSelected()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("import-selected");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("july", "July2020.csv", _july));
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var options = await CreateProvider(session).GetOptionsAsync("july");

            // Assert
            Assert.Equal(["august", "july"], options.Select(option => option.Value));
            Assert.StartsWith("July2020.csv", Assert.Single(options, option => option.Selected).Text);
            Assert.Equal("july", Assert.Single(options, option => option.Selected).Value);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_KeepsASavedImportThatNoLeadRecordsAnyLonger()
    {
        // Arrange
        // A load saved with an import whose leads were since deleted must still show that it loads that import.
        // Dropping it would make the editor read "Any imported file" while the stored filter still loads nothing.
        var databasePath = LeadTestStore.DatabasePath("import-retired");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, Import("august", "August2020.csv", _august));

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var options = await CreateProvider(session).GetOptionsAsync("deleted-import");

            // Assert
            Assert.Equal(["deleted-import", "august"], options.Select(option => option.Value));
            Assert.Equal("deleted-import", Assert.Single(options, option => option.Selected).Value);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static LeadImport Import(string entryId, string fileName, DateTime importedUtc)
        => new()
        {
            EntryId = entryId,
            FileName = fileName,
            ImportedUtc = importedUtc,
        };

    private static LeadImportProvider CreateProvider(ISession session)
    {
        var localClock = new Mock<ILocalClock>();
        localClock
            .Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>()))
            .ReturnsAsync((DateTimeOffset value) => value);

        return new LeadImportProvider(session, localClock.Object, new PassThroughStringLocalizer<LeadImportProvider>());
    }
}
