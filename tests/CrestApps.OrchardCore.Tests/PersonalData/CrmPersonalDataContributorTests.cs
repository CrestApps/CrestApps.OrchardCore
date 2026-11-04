using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tests.PersonalData;

/// <summary>
/// The CRM finds a person's contacts and leads by any of their phone numbers, exports every field, and erasing keeps the
/// contact for its activities while removing the person's numbers and name from it.
/// </summary>
public sealed class CrmPersonalDataContributorTests : IAsyncLifetime
{
    private string _path;
    private IStore _store;

    public async ValueTask InitializeAsync()
    {
        var (connectionString, path) = ContactPhoneTestStore.CreateDatabase("crestapps-pd-crm");

        _path = path;
        _store = await ContactPhoneTestStore.CreateAsync(connectionString);
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _path);

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AContactFoundByPhone_IsExported_AndAnonymizedWithoutTheNumber()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var contact = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550100"));
        contact.DisplayText = "Ann Lee";
        var other = ContactPhoneTestStore.CreateRecord(ContactPhoneTestStore.ContactContentType, ("Cell", "+17025550199"));

        await ContactPhoneTestStore.SaveAsync(_store, contact, other);

        var contentManager = new Mock<IContentManager>();
        ContentItem updated = null;
        contentManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>()))
            .Callback<ContentItem>(item => updated = item)
            .Returns(Task.CompletedTask);

        await using var session = _store.CreateSession();
        var contributor = new CrmContactPersonalDataContributor(
            new CrmPersonalDataContacts(session, new DefaultPhoneNumberService()),
            TestContactMethods.Accessor,
            contentManager.Object,
            new PassThroughStringLocalizer<CrmContactPersonalDataContributor>());
        var subject = new PersonalDataSubject(phoneNumber: "+1 (702) 555-0100");

        // Act
        await contributor.IdentifyAsync(subject, cancellationToken);
        var export = new PersonalDataExport();
        await contributor.ExportAsync(new PersonalDataExportContext(subject, export), cancellationToken);
        var result = new PersonalDataAnonymizeResult();
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), cancellationToken);

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal(CrmContactPersonalDataContributor.ContactKind, record.Kind);
        Assert.Equal(contact.ContentItemId, record.Id);
        Assert.Contains("+17025550100", record.Fields["PhoneNumbers"], StringComparison.Ordinal);
        Assert.Contains("Ann Lee", subject.Names);

        Assert.Equal(1, result.Records[CrmContactPersonalDataContributor.ContactKind]);
        Assert.NotNull(updated);
        Assert.Equal("Anonymized contact", updated.DisplayText);
        Assert.DoesNotContain("5550100", ((JsonObject)updated.Content).ToJsonString(), StringComparison.Ordinal);
    }
}
