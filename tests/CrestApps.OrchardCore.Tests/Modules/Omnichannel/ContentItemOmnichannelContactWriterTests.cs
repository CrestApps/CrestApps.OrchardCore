using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Models;
using CrestApps.Core.PhoneNumbers;
using Microsoft.Extensions.Time.Testing;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Pins what recording something on a contact does to the content item behind it.
/// </summary>
/// <remarks>
/// The callers are automated conversations, so every assertion here is about not doing damage: an
/// address is replaced rather than appended, a mis-heard phrase is refused, an opt-out keeps the time
/// it was first recorded, and nothing is committed when the contact already said this. The
/// duplicate-identifier case has its own test because it fails silently - one bag item with no
/// identifier stops the whole bag from saving, and the contact's phone numbers simply revert.
/// </remarks>
public sealed class ContentItemOmnichannelContactWriterTests
{
    private static readonly DateTime _now = new(2026, 4, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApplyAsync_WithAnEmail_AddsIt()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out _);

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { Email = "buyer@example.com" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(changed);
        Assert.Equal("buyer@example.com", Project(contact).GetPrimaryEmail());

        var methods = Methods(contact);
        var email = Assert.Single(methods, method => method.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);

        Assert.Equal("buyer@example.com", email.DisplayText);
        Assert.True(email.TryGet<EmailInfoPart>(out var emailPart));
        Assert.Equal("buyer@example.com", emailPart.Email.Text);

        // The phone that was already there is untouched.
        Assert.Single(methods, method => method.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber);
    }

    [Fact]
    public async Task ApplyAsync_WithANewEmail_ReplacesTheOneOnFile()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out _);

        await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { Email = "old@example.com" },
            TestContext.Current.CancellationToken);

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { Email = "new@example.com" },
            TestContext.Current.CancellationToken);

        // Assert: replaced, not appended. Appending is how a contact ends up with three addresses.
        Assert.True(changed);

        var email = Assert.Single(Methods(contact), method => method.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);

        Assert.Equal("new@example.com", email.DisplayText);
        Assert.Equal("new@example.com", Project(contact).GetPrimaryEmail());
    }

    [Fact]
    public async Task ApplyAsync_WithTheSameEmail_ChangesNothing()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out var contentManager);

        await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { Email = "buyer@example.com" },
            TestContext.Current.CancellationToken);

        // Act: a second call with the same address, in any casing.
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { Email = "BUYER@example.com" },
            TestContext.Current.CancellationToken);

        // Assert: no new version of the contact for a conversation that learned nothing.
        Assert.False(changed);
        Assert.Single(Methods(contact), method => method.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
        contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an email")]
    [InlineData("missing at sign")]
    [InlineData("has space @example.com")]
    public async Task ApplyAsync_WithSomethingThatIsNotAnEmail_RecordsNothing(string email)
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out _);

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { Email = email },
            TestContext.Current.CancellationToken);

        // Assert: a mis-heard phrase must never be written as somebody's address.
        Assert.False(changed);
        Assert.DoesNotContain(Methods(contact), method => method.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
    }

    [Fact]
    public async Task ApplyAsync_CreatesTheEmailItemThroughTheContentManager()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out var contentManager, newItemId: "made-by-the-content-manager");

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { Email = "buyer@example.com" },
            TestContext.Current.CancellationToken);

        // Assert: the item has to arrive with an identifier and its type's parts and defaults. Built by
        // hand it had neither, and a bag's items are keyed by identifier when an edit is applied - so
        // that one item silently stopped the contact's phone numbers from saving at all.
        Assert.True(changed);
        contentManager.Verify(manager => manager.NewAsync(OmnichannelConstants.ContentTypes.EmailAddress), Times.Once);

        var email = Assert.Single(Methods(contact), method => method.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);

        Assert.Equal("made-by-the-content-manager", email.ContentItemId);
    }

    [Fact]
    public async Task ApplyAsync_RecordingAnOptOut_StampsWhenItWasAsked()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out _);

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { DoNotSms = true },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(changed);

        var record = Project(contact);

        Assert.True(record.DoNotSms);
        Assert.Equal(_now, record.DoNotSmsUtc);
    }

    [Fact]
    public async Task ApplyAsync_RepeatingAnOptOut_KeepsTheOriginalTime()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out _, timeProvider: out var timeProvider);

        await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { DoNotSms = true },
            TestContext.Current.CancellationToken);

        timeProvider.Advance(TimeSpan.FromDays(30));

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { DoNotSms = true },
            TestContext.Current.CancellationToken);

        // Assert: the stamp records when the person asked, and a compliance audit reads it. Re-stamping
        // it on every save would erase that.
        Assert.False(changed);
        Assert.Equal(_now, Project(contact).DoNotSmsUtc);
    }

    [Fact]
    public async Task ApplyAsync_ClearingAnOptOut_ClearsItsTime()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out _);

        await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { DoNotSms = true },
            TestContext.Current.CancellationToken);

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges { DoNotSms = false },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(changed);

        var record = Project(contact);

        Assert.False(record.DoNotSms);
        Assert.Null(record.DoNotSmsUtc);
    }

    [Fact]
    public async Task ApplyAsync_WithNothingToChange_DoesNotTouchTheContact()
    {
        // Arrange
        var contact = CreateContactWithPhone("+17024993350");
        var writer = CreateWriter(contact, out var contentManager);

        // Act
        var changed = await writer.ApplyAsync(
            contact.ContentItemId,
            new OmnichannelContactChanges(),
            TestContext.Current.CancellationToken);

        // Assert: writing a contact publishes a new version of it, so an empty change set must not.
        Assert.False(changed);
        contentManager.Verify(manager => manager.UpdateAsync(It.IsAny<ContentItem>()), Times.Never);
    }

    [Fact]
    public async Task ApplyAsync_ForAContactThatDoesNotExist_ReportsNoChange()
    {
        // Arrange
        var writer = CreateWriter(contact: null, out _);

        // Act
        var changed = await writer.ApplyAsync(
            "missing",
            new OmnichannelContactChanges { Email = "buyer@example.com" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(changed);
    }

    private static OmnichannelContact Project(ContentItem contact)
        => ContentItemOmnichannelContactProjection.Project(contact);

    private static List<ContentItem> Methods(ContentItem contact)
        => contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;

    private static ContentItemOmnichannelContactWriter CreateWriter(
        ContentItem contact,
        out Mock<IContentManager> contentManager,
        string newItemId = null)
        => CreateWriter(contact, out contentManager, out _, newItemId);

    private static ContentItemOmnichannelContactWriter CreateWriter(
        ContentItem contact,
        out Mock<IContentManager> contentManager,
        out FakeTimeProvider timeProvider,
        string newItemId = null)
    {
        contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string contentItemId, VersionOptions _) =>
                contact is not null && contentItemId == contact.ContentItemId ? contact : null);
        contentManager
            .Setup(manager => manager.NewAsync(OmnichannelConstants.ContentTypes.EmailAddress))
            .ReturnsAsync(() => new ContentItem
            {
                ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
                ContentItemId = newItemId ?? Guid.NewGuid().ToString("n"),
            });

        timeProvider = new FakeTimeProvider(_now);

        return new ContentItemOmnichannelContactWriter(
            contentManager.Object,
            Mock.Of<IPhoneNumberService>(),
            timeProvider);
    }

    private static ContentItem CreateContactWithPhone(string number)
    {
        var contact = new ContentItem
        {
            ContentType = "Customer",
            ContentItemId = "contact-1",
            DisplayText = "Test Contact",
        };

        var bag = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods);
        bag.ContentItems ??= [];

        var phone = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
            DisplayText = $"Cell: {number}",
        };

        phone.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField { PhoneNumber = number };
            part.Type = new TextField { Text = ContactPhoneNumberType.Cell };
        });

        bag.ContentItems.Add(phone);
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return contact;
    }
}
