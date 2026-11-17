using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Indexes;

/// <summary>
/// The contact list queries run on the rows a tenant has, with the declarations the module's index providers make
/// (OrchardCore PR #20201): what they return and count, and the SQL YesSql sends for them.
/// </summary>
/// <remarks>
/// The contact index has one row per content item version, so a list that joins it with the content item index
/// isn't grouped by document, and its count reads the index tables alone. The phone number index has a row per
/// number: declaring a single row would make saving a contact with two numbers throw, and a contact with two
/// matching numbers would come back twice. So a list that joins it stays grouped.
/// </remarks>
public sealed partial class ContactIndexDeclarationQueryTests : IDisposable
{
    private readonly (string ConnectionString, string Path) _database = ContactPhoneTestStore.CreateDatabase("contact-index-declarations");

    [Fact]
    public async Task ContactList_JoinedToTheContactIndex_IsNotGroupedAndCountsFromTheIndexTables()
    {
        // Arrange
        var (store, logger) = await CreateStoreAsync();
        var older = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550101"));
        var newer = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550102"));
        var lead = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.LeadContentType, ("Cell", "+17025550103"));
        older.ModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        newer.ModifiedUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        lead.ModifiedUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await ContactPhoneTestStore.SaveAsync(store, older, newer, lead);
        logger.Entries.Clear();

        await using var session = store.CreateSession();

        // Act
        var query = session.Query<ContentItem, ContentItemIndex>(index => index.Latest && index.ContentType == ContactPhoneTestStore.ContactContentType)
            .With<OmnichannelContactIndex>(index => !index.IsConverted)
            .With<ContentItemIndex>()
            .OrderByDescending(index => index.ModifiedUtc);
        var count = await query.CountAsync(TestContext.Current.CancellationToken);
        var page = await query.Take(10).ListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal([newer.ContentItemId, older.ContentItemId], page.Select(item => item.ContentItemId));
        Assert.DoesNotContain("GROUP BY", PageSql(logger), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(DocumentTablePattern(), CountSql(logger));
    }

    [Fact]
    public async Task ContactList_JoinedToThePhoneNumberIndex_ReturnsAndCountsAContactWithTwoMatchingNumbersOnce()
    {
        // Arrange
        var (store, logger) = await CreateStoreAsync();
        var twoNumbers = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550101"), ("Work", "+17025550102"));
        var oneNumber = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550201"));
        var elsewhere = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+13105550301"));
        twoNumbers.ModifiedUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        oneNumber.ModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        elsewhere.ModifiedUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Saving a contact with two numbers throws when the phone number index declares a single row per document.
        await ContactPhoneTestStore.SaveAsync(store, twoNumbers, oneNumber, elsewhere);
        logger.Entries.Clear();

        await using var session = store.CreateSession();

        // Act
        var query = session.Query<ContentItem, ContentItemIndex>(index => index.Latest)
            .With<OmnichannelContactPhoneNumberIndex>(index => index.NormalizedPhoneNumber.StartsWith("+1702555"))
            .With<ContentItemIndex>()
            .OrderByDescending(index => index.ModifiedUtc);
        var count = await query.CountAsync(TestContext.Current.CancellationToken);
        var page = await query.Take(10).ListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal([twoNumbers.ContentItemId, oneNumber.ContentItemId], page.Select(item => item.ContentItemId));
        Assert.Contains("GROUP BY", PageSql(logger), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(DocumentTablePattern(), CountSql(logger));
    }

    public void Dispose()
    {
        if (File.Exists(_database.Path))
        {
            File.Delete(_database.Path);
        }
    }

    private async Task<(IStore Store, RecordingLogger<IStore> Logger)> CreateStoreAsync()
    {
        var store = await ContactPhoneTestStore.CreateAsync(_database.ConnectionString);
        var logger = new RecordingLogger<IStore>();
        store.Configuration.Logger = logger;

        return (store, logger);
    }

    private static string PageSql(RecordingLogger<IStore> logger)
        => Assert.Single(Statements(logger), sql => !sql.Contains("count(", StringComparison.OrdinalIgnoreCase));

    private static string CountSql(RecordingLogger<IStore> logger)
        => Assert.Single(Statements(logger), sql => sql.Contains("count(", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Statements(RecordingLogger<IStore> logger)
        => logger.At(LogLevel.Debug).Where(sql => sql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

    // The document table on its own, not the DocumentId column of an index table.
    [GeneratedRegex(@"\bDocument\b")]
    private static partial Regex DocumentTablePattern();
}
