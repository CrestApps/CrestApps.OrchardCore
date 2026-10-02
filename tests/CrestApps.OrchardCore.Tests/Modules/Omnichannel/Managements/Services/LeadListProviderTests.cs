using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Tests.Utilities;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The List filter of an inventory load was a free-text box, so a load only matched when somebody retyped a list name
/// exactly. It is now a pick list of the lists the leads carry. These tests pin what that pick list offers.
/// </summary>
public sealed class LeadListProviderTests
{
    [Fact]
    public async Task GetNamesAsync_ListsEachListTheLeadsCarryOnceInNameOrder()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("list-names");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, "Trade Show Scans");
                await LeadTestStore.SaveLeadAsync(seedSession, "spring import");
                await LeadTestStore.SaveLeadAsync(seedSession, "Trade Show Scans");
                await LeadTestStore.SaveLeadAsync(seedSession, "Autumn Webinar");

                // A lead that arrived in no list adds no blank entry to pick.
                await LeadTestStore.SaveLeadAsync(seedSession, null);
                await LeadTestStore.SaveLeadAsync(seedSession, "   ");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            var names = await GetNamesAsync(store);

            // Assert
            Assert.Equal(["Autumn Webinar", "spring import", "Trade Show Scans"], names);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_MarksTheSavedListSelected()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("list-selected");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, "Spring Import");
                await LeadTestStore.SaveLeadAsync(seedSession, "Trade Show Scans");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var options = await new LeadListProvider(session).GetOptionsAsync("Trade Show Scans");

            // Assert
            Assert.Equal(["Spring Import", "Trade Show Scans"], options.Select(option => option.Value));
            Assert.Equal("Trade Show Scans", Assert.Single(options, option => option.Selected).Value);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_KeepsASavedListThatNoLeadCarriesAnyLonger()
    {
        // Arrange
        // A load saved with a list whose leads were since moved or deleted must still show that list. Dropping it
        // would make the editor read "Any list" while the stored filter still narrows the load to the old name.
        var databasePath = LeadTestStore.DatabasePath("list-retired");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            await using (var seedSession = store.CreateSession())
            {
                await LeadTestStore.SaveLeadAsync(seedSession, "Spring Import");

                await seedSession.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // Act
            await using var session = store.CreateSession();
            var options = await new LeadListProvider(session).GetOptionsAsync("Retired List");

            // Assert
            Assert.Equal(["Retired List", "Spring Import"], options.Select(option => option.Value));
            Assert.Equal("Retired List", Assert.Single(options, option => option.Selected).Value);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task GetOptionsAsync_WithNoLeadsAndNoSavedList_OffersNothing()
    {
        // Arrange
        var databasePath = LeadTestStore.DatabasePath("list-empty");
        var store = await LeadTestStore.CreateAsync($"Data Source={databasePath};Pooling=False");

        try
        {
            // Act
            await using var session = store.CreateSession();
            var options = await new LeadListProvider(session).GetOptionsAsync(null);

            // Assert
            Assert.Empty(options);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    private static async Task<IReadOnlyList<string>> GetNamesAsync(IStore store)
    {
        await using var session = store.CreateSession();

        return await new LeadListProvider(session).GetNamesAsync();
    }
}
