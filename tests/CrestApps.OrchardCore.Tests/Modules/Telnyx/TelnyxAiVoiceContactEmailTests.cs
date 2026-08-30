using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Telnyx;

/// <summary>
/// Verifies the automated AI voice conclusion writes a customer-provided email back to the contact's
/// ContactMethods bag as a correctly structured EmailAddress item, without the duplication a raw content-item
/// merge caused. Speech-to-text capturing the email over the phone is exercised separately (and unreliably) in
/// live testing; these assertions prove the persistence step deterministically.
/// </summary>
public sealed class TelnyxAiVoiceContactEmailTests
{
    [Fact]
    public void TryApplyContactEmail_AddsEmailAddressItem_WhenNoneExists()
    {
        var contact = CreateContactWithPhone("+17024993350");

        var changed = TelnyxAiVoiceConversationHandler.TryApplyContactEmail(contact, "buyer@example.com");

        Assert.True(changed);
        Assert.Equal("buyer@example.com", TelnyxAiVoiceConversationHandler.GetContactEmail(contact));

        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;

        // The email is added and the pre-existing phone is left untouched (the raw merge used to append duplicates).
        var email = Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
        Assert.Equal("buyer@example.com", email.DisplayText);
        Assert.True(email.TryGet<EmailInfoPart>(out var emailPart));
        Assert.Equal("buyer@example.com", emailPart.Email.Text);
        Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.PhoneNumber);
    }

    [Fact]
    public void TryApplyContactEmail_ReplacesExistingEmail_WithoutDuplicating()
    {
        var contact = CreateContactWithPhone("+17024993350");
        Assert.True(TelnyxAiVoiceConversationHandler.TryApplyContactEmail(contact, "old@example.com"));

        var changed = TelnyxAiVoiceConversationHandler.TryApplyContactEmail(contact, "new@example.com");

        Assert.True(changed);
        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;
        var email = Assert.Single(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
        Assert.Equal("new@example.com", email.DisplayText);
        Assert.Equal("new@example.com", TelnyxAiVoiceConversationHandler.GetContactEmail(contact));
    }

    [Fact]
    public void TryApplyContactEmail_ReturnsFalse_WhenSameEmailAlreadyOnFile()
    {
        var contact = CreateContactWithPhone("+17024993350");
        Assert.True(TelnyxAiVoiceConversationHandler.TryApplyContactEmail(contact, "buyer@example.com"));

        // A second call with the same address (any casing) is a no-op.
        Assert.False(TelnyxAiVoiceConversationHandler.TryApplyContactEmail(contact, "BUYER@example.com"));

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
    public void TryApplyContactEmail_ReturnsFalse_ForInvalidInput(string email)
    {
        var contact = CreateContactWithPhone("+17024993350");

        var changed = TelnyxAiVoiceConversationHandler.TryApplyContactEmail(contact, email);

        Assert.False(changed);
        var methods = contact.GetOrCreate<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;
        Assert.DoesNotContain(methods, m => m.ContentType == OmnichannelConstants.ContentTypes.EmailAddress);
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
