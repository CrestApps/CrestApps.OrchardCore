using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Verifies the automated AI voice conclusion writes a customer-provided email back to the contact's
/// ContactMethods bag as a correctly structured EmailAddress item, without the duplication a raw content-item
/// merge caused. Speech-to-text capturing the email over the phone is exercised separately (and unreliably) in
/// live testing; these assertions prove the persistence step deterministically.
/// </summary>
public sealed class OmnichannelSubjectWriterContactEmailTests
{
    [Fact]
    public async Task TryApplyContactEmail_AddsEmailAddressItem_WhenNoneExists()
    {
        var contact = CreateContactWithPhone("+17024993350");

        var changed = await OmnichannelSubjectWriter.TryApplyContactEmailAsync(CreateContentManager(), contact, "buyer@example.com");

        Assert.True(changed);
        Assert.Equal("buyer@example.com", OmnichannelSubjectWriter.GetContactEmail(contact));

        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;

        // The email is added and the pre-existing phone is left untouched (the raw merge used to append duplicates).
        var email = Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
        Assert.Equal("buyer@example.com", email.DisplayText);
        Assert.True(email.TryGet<EmailInfoPart>(out var emailPart));
        Assert.Equal("buyer@example.com", emailPart.Email.Text);
        Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber);
    }

    [Fact]
    public async Task TryApplyContactEmail_ReplacesExistingEmail_WithoutDuplicating()
    {
        var contact = CreateContactWithPhone("+17024993350");
        Assert.True(await OmnichannelSubjectWriter.TryApplyContactEmailAsync(CreateContentManager(), contact, "old@example.com"));

        var changed = await OmnichannelSubjectWriter.TryApplyContactEmailAsync(CreateContentManager(), contact, "new@example.com");

        Assert.True(changed);
        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;
        var email = Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
        Assert.Equal("new@example.com", email.DisplayText);
        Assert.Equal("new@example.com", OmnichannelSubjectWriter.GetContactEmail(contact));
    }

    [Fact]
    public async Task TryApplyContactEmail_ReturnsFalse_WhenSameEmailAlreadyOnFile()
    {
        var contact = CreateContactWithPhone("+17024993350");
        Assert.True(await OmnichannelSubjectWriter.TryApplyContactEmailAsync(CreateContentManager(), contact, "buyer@example.com"));

        // A second call with the same address (any casing) is a no-op.
        Assert.False(await OmnichannelSubjectWriter.TryApplyContactEmailAsync(CreateContentManager(), contact, "BUYER@example.com"));

        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;
        Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an email")]
    [InlineData("missing at sign")]
    [InlineData("has space @example.com")]
    public async Task TryApplyContactEmail_ReturnsFalse_ForInvalidInput(string email)
    {
        var contact = CreateContactWithPhone("+17024993350");

        var changed = await OmnichannelSubjectWriter.TryApplyContactEmailAsync(CreateContentManager(), contact, email);

        Assert.False(changed);
        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;
        Assert.DoesNotContain(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
    }


    [Fact]
    public async Task TryApplyContactEmail_CreatesTheItemThroughTheContentManager()
    {
        // The item has to be created the way every other content item is, so it arrives with an identifier and
        // its type's parts and defaults. Constructed by hand it had no identifier, and OrchardCore keys a bag's
        // items by identifier when it applies an edit -- so that one item silently stopped the contact's phone
        // numbers from saving at all.
        var contact = CreateContactWithPhone("+17024993350");
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.NewAsync(OmnichannelConstants.ContentTypes.EmailAddress))
            .ReturnsAsync(new ContentItem
            {
                ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
                ContentItemId = "made-by-the-content-manager",
            });

        Assert.True(await OmnichannelSubjectWriter.TryApplyContactEmailAsync(contentManager.Object, contact, "buyer@example.com"));

        contentManager.Verify(manager => manager.NewAsync(OmnichannelConstants.ContentTypes.EmailAddress), Times.Once);

        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;
        var email = Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
        Assert.Equal("made-by-the-content-manager", email.ContentItemId);
    }

    /// <summary>
    /// Stands in for the content manager, which is what gives a new item its identifier. Building the item by
    /// hand instead left it with none, and one item with no identifier stops the whole bag from saving -- the
    /// contact's phone numbers reverted on publish with nothing reported.
    /// </summary>
    private static IContentManager CreateContentManager()
    {
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.NewAsync(OmnichannelConstants.ContentTypes.EmailAddress))
            .ReturnsAsync(() => new ContentItem
            {
                ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
                ContentItemId = Guid.NewGuid().ToString("n"),
            });

        return contentManager.Object;
    }

    private static ContentItem CreateContactWithPhone(string number)
    {
        var contact = new ContentItem { ContentType = "Customer", DisplayText = "Test Contact" };

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
            part.Type = new TextField { Text = "Cell" };
        });

        bag.ContentItems.Add(phone);
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return contact;
    }
}
