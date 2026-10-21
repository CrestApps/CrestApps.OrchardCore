using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class ContactMethodsWriterTests
{
    [Fact]
    public async Task TryAddPhoneNumber_WhenTheTypeUsesTheBag_AddsANumberItHasNotGotInAnyFormat()
    {
        // Arrange
        using var services = CreateServices();
        var writer = services.GetRequiredService<IContactMethodsWriter>();
        var contact = CreateBagContact("+1 (702) 555-0100");

        // Act
        var addedSame = await writer.TryAddPhoneNumberAsync(contact, new ContactPhoneNumber { PhoneNumber = "+17025550100", Type = "Cell" });
        var addedNew = await writer.TryAddPhoneNumberAsync(contact, new ContactPhoneNumber { PhoneNumber = "+12125550100", Type = "home", CountryCode = "US", Extension = "12" });

        // Assert
        Assert.False(addedSame);
        Assert.True(addedNew);

        var items = contact.Get<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems;
        Assert.Equal(2, items.Count);

        var added = items[1];
        Assert.False(string.IsNullOrEmpty(added.ContentItemId));
        Assert.True(added.TryGet<PhoneNumberInfoPart>(out var phonePart));
        Assert.Equal("+12125550100", phonePart.Number.PhoneNumber);
        Assert.Equal("US", phonePart.Number.CountryCode);
        Assert.Equal(ContactPhoneTypes.Home, phonePart.Type.Text);
        Assert.Equal("12", phonePart.Extension.Text);
    }

    [Fact]
    public async Task TryAddEmailAddress_WhenTheTypeUsesTheBag_AddsAnAddressItHasNotGotInAnyCase()
    {
        // Arrange
        using var services = CreateServices();
        var writer = services.GetRequiredService<IContactMethodsWriter>();
        var contact = CreateBagContact("+17025550100");

        // Act & Assert
        Assert.True(await writer.TryAddEmailAddressAsync(contact, "lead@example.com"));
        Assert.False(await writer.TryAddEmailAddressAsync(contact, "LEAD@example.com"));
        Assert.True(await writer.TryAddEmailAddressAsync(contact, "other@example.com"));
        Assert.Equal(
            ["lead@example.com", "other@example.com"],
            TestContactMethods.Get(contact).EmailAddresses.Select(email => email.Email));
    }

    [Fact]
    public async Task Writes_WhenTheTypeHasAProviderAndNoWriter_LeaveTheContactAsItIs()
    {
        // Arrange
        using var services = CreateServices(collection => collection.AddContactMethodsProvider<FieldContactMethodsProvider>("Person"));
        var writer = services.GetRequiredService<IContactMethodsWriter>();
        var person = new ContentItem { ContentType = "Person" };
        person.Alter<PersonContactPart>(part => part.Email = new TextField { Text = "person@example.com" });

        // Act & Assert
        Assert.False(await writer.TrySetEmailAddressAsync(person, "new@example.com"));
        Assert.False(await writer.TryAddEmailAddressAsync(person, "new@example.com"));
        Assert.False(await writer.TryAddPhoneNumberAsync(person, new ContactPhoneNumber { PhoneNumber = "+17025550100" }));
        Assert.False(person.Has(OmnichannelConstants.NamedParts.ContactMethods));
        Assert.Equal("person@example.com", person.Get<PersonContactPart>(nameof(PersonContactPart)).Email.Text);
    }

    [Fact]
    public async Task Writes_WhenTheTypeHasAWriter_GoThroughIt()
    {
        // Arrange
        using var services = CreateServices(collection => collection
            .AddContactMethodsProvider<FieldContactMethodsProvider>("Person")
            .AddContactMethodsWriter<FieldContactMethodsWriter>("Person"));
        var writer = services.GetRequiredService<IContactMethodsWriter>();
        var person = new ContentItem { ContentType = "Person" };
        person.Alter<PersonContactPart>(part => part.Email = new TextField { Text = "person@example.com" });

        // Act
        var changed = await writer.TrySetEmailAddressAsync(person, "new@example.com");

        // Assert
        Assert.True(changed);
        Assert.Equal("new@example.com", person.Get<PersonContactPart>(nameof(PersonContactPart)).Email.Text);
        Assert.False(person.Has(OmnichannelConstants.NamedParts.ContactMethods));
    }

    [Fact]
    public void AddContactMethodsWriter_WithoutAContentType_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddContactMethodsWriter<FieldContactMethodsWriter>());
    }

    private static ServiceProvider CreateServices(Action<IServiceCollection> configure = null)
        => TestContactMethods.CreateServices(services =>
        {
            var contentManager = new Mock<IContentManager>();
            contentManager
                .Setup(manager => manager.NewAsync(It.IsAny<string>()))
                .ReturnsAsync((string contentType) => new ContentItem
                {
                    ContentType = contentType,
                    ContentItemId = Guid.NewGuid().ToString("n"),
                });

            services.AddSingleton(contentManager.Object);
            configure?.Invoke(services);
        });

    private static ContentItem CreateBagContact(string phoneNumber)
    {
        var contact = new ContentItem { ContentType = "Contact" };
        var phone = new ContentItem { ContentType = OmnichannelConstants.ContentTypes.PhoneNumber, ContentItemId = "phone-1" };

        phone.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField { PhoneNumber = phoneNumber };
            part.Type = new TextField { Text = "Cell" };
        });

        var bagPart = new BagPart();
        bagPart.ContentItems.Add(phone);
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);

        return contact;
    }

    /// <summary>
    /// Writes an email address into a <see cref="PersonContactPart"/>'s own field.
    /// </summary>
    private sealed class FieldContactMethodsWriter : IContactMethodsWriter
    {
        public Task<bool> TrySetEmailAddressAsync(ContentItem contact, string emailAddress)
        {
            contact.Alter<PersonContactPart>(part => part.Email = new TextField { Text = emailAddress });

            return Task.FromResult(true);
        }

        public Task<bool> TryAddEmailAddressAsync(ContentItem contact, string emailAddress)
            => Task.FromResult(false);

        public Task<bool> TryAddPhoneNumberAsync(ContentItem contact, ContactPhoneNumber phoneNumber)
            => Task.FromResult(false);
    }
}
