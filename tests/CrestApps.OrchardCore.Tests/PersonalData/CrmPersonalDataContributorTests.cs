using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
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
            new PassThroughStringLocalizer<CrmContactPersonalDataContributor>(),
            []);
        var subject = new PersonalDataSubject(phoneNumber: "+1 (702) 555-0100");

        // Act
        await ((IPersonalDataContributor)contributor).IdentifyAsync(subject, cancellationToken);
        var export = new PersonalDataExport();
        await contributor.ExportAsync(new PersonalDataExportContext(subject, export), cancellationToken);
        var result = new PersonalDataAnonymizeResult();
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), cancellationToken);

        // Assert
        var record = Assert.Single(export.Records);
        Assert.Equal(CrmContactPersonalDataContributor.ContactKind, record.Kind);
        Assert.Equal(contact.ContentItemId, record.Id);
        Assert.Contains("+17025550100", record.Fields["PhoneNumbers"], StringComparison.Ordinal);

        // The contact does not add what it holds to the person: a shared inbox or number would pull in other people.
        Assert.Empty(subject.Names);
        Assert.Single(subject.PhoneNumbers);

        Assert.Equal(1, result.Records[CrmContactPersonalDataContributor.ContactKind]);
        Assert.NotNull(updated);
        Assert.Equal("Anonymized contact", updated.DisplayText);
        Assert.DoesNotContain("5550100", ((JsonObject)updated.Content).ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASharedPhone_DoesNotPullInASecondContact()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var ann = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], ["+17025550100"]);
        var bob = CrmPersonalDataTestContacts.Create("Bob Ray", ["bob@example.com"], ["+17025550100"]);

        await ContactPhoneTestStore.SaveAsync(_store, ann, bob);

        var updated = new List<string>();
        await using var session = _store.CreateSession();
        var contributor = CreateContributor(session, updated);
        var subject = new PersonalDataSubject(email: "ann@example.com");

        // Act
        await ((IPersonalDataContributor)contributor).IdentifyAsync(subject, cancellationToken);
        var export = new PersonalDataExport();
        await contributor.ExportAsync(new PersonalDataExportContext(subject, export), cancellationToken);
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, new PersonalDataAnonymizeResult()), cancellationToken);

        // Assert
        Assert.Empty(subject.PhoneNumbers);
        Assert.Equal([ann.ContentItemId], export.Records.Select(record => record.Id));
        Assert.Equal([ann.ContentItemId], updated);
    }

    [Fact]
    public async Task APhoneSharedByTwoContacts_ErasesNeitherByThatNumber_AndWarns()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var ann = CrmPersonalDataTestContacts.Create("Ann Lee", ["ann@example.com"], ["+17025550100"]);
        var bob = CrmPersonalDataTestContacts.Create("Bob Ray", ["bob@example.com"], ["+17025550100"]);

        await ContactPhoneTestStore.SaveAsync(_store, ann, bob);

        var updated = new List<string>();
        await using var session = _store.CreateSession();
        var contributor = CreateContributor(session, updated);
        var subject = new PersonalDataSubject(phoneNumber: "+1 (702) 555-0100");
        var result = new PersonalDataAnonymizeResult();

        // Act
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, result), cancellationToken);

        // Assert
        Assert.Empty(updated);
        Assert.Equal(0, result.Total);
        Assert.Contains(result.Warnings, warning => warning.Contains("+17025550100", StringComparison.Ordinal));
    }

    [Fact]
    public void AnActivityOfAnotherContact_IsNotThePersons_EvenAtTheSameNumber()
    {
        var persons = new HashSet<string>(["ann"], StringComparer.Ordinal);

        Assert.True(CrmActivityPersonalDataContributor.IsPersonsActivity(new OmnichannelActivity { ContactContentItemId = "ann" }, persons));
        Assert.True(CrmActivityPersonalDataContributor.IsPersonsActivity(new OmnichannelActivity { ContactContentItemId = null }, persons));
        Assert.False(CrmActivityPersonalDataContributor.IsPersonsActivity(new OmnichannelActivity { ContactContentItemId = "bob" }, persons));
    }

    [Fact]
    public async Task AContactNamedEd_WithReferences_KeepsItsIdsAfterAnErase()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var contact = CrmPersonalDataTestContacts.Create("Ed", ["ed@example.com"], ["+17025550100"]);
        var companyId = "4edq2v8rk3ed0mn1xyz9abcded";
        var ownerId = "4ed0000000000000000000000e";
        var statusId = "ed1x2v8rk3ed0mn1xyz9abcd00";

        contact.Content[ContactPhoneTestStore.ContactContentType]["Company"] = new JsonObject { ["ContentItemIds"] = new JsonArray(companyId) };
        contact.Content[ContactPhoneTestStore.ContactContentType]["AccountManager"] = new JsonObject { ["UserIds"] = new JsonArray(ownerId), ["UserNames"] = new JsonArray("edmund") };
        contact.Content["LeadPart"] = new JsonObject { ["StatusId"] = statusId };
        contact.Content[ContactPhoneTestStore.ContactContentType]["Notes"] = new JsonObject { ["Text"] = "Edited by the desk." };

        await ContactPhoneTestStore.SaveAsync(_store, contact);

        var memory = new AIContactMemory { ItemId = "m1", ContactId = contact.ContentItemId, Text = "Ed prefers mornings." };
        var memoryStore = new Mock<IAIContactMemoryStore>();
        memoryStore.Setup(store => store.FindByContactAsync(contact.ContentItemId, It.IsAny<CancellationToken>())).ReturnsAsync(memory);

        ContentItem updated = null;
        await using var session = _store.CreateSession();
        var contributor = CreateContributor(session, [], [memoryStore.Object], item => updated = item);
        var subject = new PersonalDataSubject(email: "ed@example.com");
        subject.AddName("Ed");

        // Act
        await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, new PersonalDataAnonymizeResult()), cancellationToken);

        // Assert
        Assert.NotNull(updated);
        var content = (JsonObject)updated.Content;
        Assert.Equal(contact.ContentItemId, updated.ContentItemId);
        Assert.Equal(companyId, (string)content[ContactPhoneTestStore.ContactContentType]["Company"]["ContentItemIds"][0]);
        Assert.Equal(ownerId, (string)content[ContactPhoneTestStore.ContactContentType]["AccountManager"]["UserIds"][0]);
        Assert.Equal("edmund", (string)content[ContactPhoneTestStore.ContactContentType]["AccountManager"]["UserNames"][0]);
        Assert.Equal(statusId, (string)content["LeadPart"]["StatusId"]);
        Assert.Equal("Edited by the desk.", (string)content[ContactPhoneTestStore.ContactContentType]["Notes"]["Text"]);

        // The known personal-data fields are cleared.
        Assert.Equal("Anonymized contact", updated.DisplayText);
        Assert.Null((string)content[ContactPhoneTestStore.ContactContentType]["FirstName"]["Text"]);
        Assert.Empty(TestContactMethods.Get(updated).EmailAddresses);
        Assert.Empty(TestContactMethods.Get(updated).PhoneNumbers);
        Assert.DoesNotContain("ed@example.com", content.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("5550100", content.ToJsonString(), StringComparison.Ordinal);
        memoryStore.Verify(store => store.DeleteAsync(memory, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static CrmContactPersonalDataContributor CreateContributor(ISession session, List<string> updated, IAIContactMemoryStore[] memoryStores = null, Action<ContentItem> onUpdate = null)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>()))
            .Callback<ContentItem>(item =>
            {
                updated.Add(item.ContentItemId);
                onUpdate?.Invoke(item);
            })
            .Returns(Task.CompletedTask);

        return new CrmContactPersonalDataContributor(
            new CrmPersonalDataContacts(session, new DefaultPhoneNumberService()),
            TestContactMethods.Accessor,
            contentManager.Object,
            new PassThroughStringLocalizer<CrmContactPersonalDataContributor>(),
            memoryStores ?? []);
    }
}
