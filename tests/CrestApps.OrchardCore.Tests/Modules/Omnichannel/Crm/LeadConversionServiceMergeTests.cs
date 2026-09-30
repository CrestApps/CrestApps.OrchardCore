using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Crm;

public sealed class LeadConversionServiceMergeTests
{
    private static readonly DateTime _earlier = new(2026, 1, 5, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _later = new(2026, 3, 9, 16, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void MergeCommunicationPreferences_KeepsAnOptOutFromEitherRecord()
    {
        // Arrange - the lead asked not to be called; the contact asked not to be texted.
        var lead = new ContentItem { ContentType = "Lead" };
        lead.Alter<OmnichannelContactPart>(part => part.SetDoNotCall(true, _earlier));

        var contact = new ContentItem { ContentType = "Customer" };
        contact.Alter<OmnichannelContactPart>(part => part.SetDoNotSms(true, _later));

        // Act
        LeadConversionService.MergeCommunicationPreferences(lead, contact);

        // Assert - converting can never undo a request not to be contacted.
        var merged = Part<OmnichannelContactPart>(contact);
        Assert.True(merged.DoNotCall);
        Assert.Equal(_earlier, merged.DoNotCallUtc);
        Assert.True(merged.DoNotSms);
        Assert.Equal(_later, merged.DoNotSmsUtc);
        Assert.False(merged.DoNotEmail);
    }

    [Fact]
    public void MergeCommunicationPreferences_KeepsTheEarliestTimeWhenBothOptedOut()
    {
        // Arrange
        var lead = new ContentItem { ContentType = "Lead" };
        lead.Alter<OmnichannelContactPart>(part => part.SetDoNotCall(true, _earlier));

        var contact = new ContentItem { ContentType = "Customer" };
        contact.Alter<OmnichannelContactPart>(part => part.SetDoNotCall(true, _later));

        // Act
        LeadConversionService.MergeCommunicationPreferences(lead, contact);

        // Assert
        Assert.Equal(_earlier, Part<OmnichannelContactPart>(contact).DoNotCallUtc);
    }

    [Fact]
    public void MergeCommunicationPreferences_CopiesTheTimeZoneOnlyWhenTheContactHasNone()
    {
        // Arrange
        var lead = new ContentItem { ContentType = "Lead" };
        lead.Alter<OmnichannelContactPart>(part => part.TimeZoneId = "America/Vancouver");

        var contactWithZone = new ContentItem { ContentType = "Customer" };
        contactWithZone.Alter<OmnichannelContactPart>(part => part.TimeZoneId = "America/Toronto");

        var contactWithoutZone = new ContentItem { ContentType = "Customer" };

        // Act
        LeadConversionService.MergeCommunicationPreferences(lead, contactWithZone);
        LeadConversionService.MergeCommunicationPreferences(lead, contactWithoutZone);

        // Assert
        Assert.Equal("America/Toronto", Part<OmnichannelContactPart>(contactWithZone).TimeZoneId);
        Assert.Equal("America/Vancouver", Part<OmnichannelContactPart>(contactWithoutZone).TimeZoneId);
    }

    [Fact]
    public void MergeContactMethods_NewContact_CopiesEveryMethodWithNewIdentifiers()
    {
        // Arrange
        var lead = LeadWithMethods(("Cell", "+15555550142"), ("Home", "+15555550143"));
        var contact = new ContentItem { ContentType = "Customer" };

        // Act
        LeadConversionService.MergeContactMethods(lead, contact);

        // Assert - two methods sharing an identifier make the contact editor drop its changes.
        var leadIds = lead.Get<BagPart>("ContactMethods").ContentItems.Select(item => item.ContentItemId).ToArray();
        var copied = contact.Get<BagPart>("ContactMethods").ContentItems;

        Assert.Equal(2, copied.Count);
        Assert.All(copied, item =>
        {
            Assert.False(string.IsNullOrEmpty(item.ContentItemId));
            Assert.DoesNotContain(item.ContentItemId, leadIds);
        });
        Assert.Equal(2, copied.Select(item => item.ContentItemId).Distinct().Count());
        Assert.Contains(copied, item => Part<PhoneNumberInfoPart>(item).Number.PhoneNumber == "+15555550142");
    }

    [Fact]
    public void MergeContactMethods_ExistingContact_AddsOnlyTheMethodsItLacks()
    {
        // Arrange - the contact already has the lead's cell number, written differently.
        var lead = LeadWithMethods(("Cell", "+15555550142"), ("Home", "+15555550143"));
        var contact = LeadWithMethods(("Cell", "+1 555-555-0142"));
        contact.ContentType = "Customer";

        // Act
        LeadConversionService.MergeContactMethods(lead, contact);

        // Assert
        var numbers = contact.Get<BagPart>("ContactMethods").ContentItems
            .Select(item => Part<PhoneNumberInfoPart>(item).Number.PhoneNumber)
            .ToArray();

        Assert.Equal(2, numbers.Length);
        Assert.Contains("+15555550143", numbers);
    }

    [Fact]
    public void CopyMatchingParts_NewContact_CopiesTheTypeOwnFieldsByNameAndKind()
    {
        // Arrange - each type keeps its fields in a part named after it, so they line up by field name and kind.
        var leadDefinition = new ContentTypeDefinitionBuilder()
            .WithName("Lead")
            .WithPart("Lead")
            .Build();
        var contactDefinition = new ContentTypeDefinitionBuilder()
            .WithName("Customer")
            .WithPart("Customer")
            .Build();

        leadDefinition = WithField(leadDefinition, "Lead", "Industry", "TextField");
        contactDefinition = WithField(contactDefinition, "Customer", "Industry", "TextField");

        var lead = new ContentItem { ContentType = "Lead" };
        ((JsonObject)lead.Content)["Lead"] = new JsonObject { ["Industry"] = new JsonObject { ["Text"] = "Retail" } };

        var contact = new ContentItem { ContentType = "Customer" };

        // Act
        LeadConversionService.CopyMatchingParts(lead, leadDefinition, contact, contactDefinition, overwrite: true);

        // Assert
        Assert.Equal("Retail", ((JsonObject)contact.Content)["Customer"]?["Industry"]?["Text"]?.GetValue<string>());
    }

    [Fact]
    public void CopyMatchingParts_ExistingContact_KeepsTheContactsOwnValues()
    {
        // Arrange
        var leadDefinition = WithField(new ContentTypeDefinitionBuilder().WithName("Lead").WithPart("Lead").Build(), "Lead", "Industry", "TextField");
        var contactDefinition = WithField(new ContentTypeDefinitionBuilder().WithName("Customer").WithPart("Customer").Build(), "Customer", "Industry", "TextField");

        var lead = new ContentItem { ContentType = "Lead" };
        ((JsonObject)lead.Content)["Lead"] = new JsonObject { ["Industry"] = new JsonObject { ["Text"] = "Retail" } };

        var contact = new ContentItem { ContentType = "Customer" };
        ((JsonObject)contact.Content)["Customer"] = new JsonObject { ["Industry"] = new JsonObject { ["Text"] = "Wholesale" } };

        // Act
        LeadConversionService.CopyMatchingParts(lead, leadDefinition, contact, contactDefinition, overwrite: false);

        // Assert - a lead's rough data never overwrites a clean contact's value.
        Assert.Equal("Wholesale", ((JsonObject)contact.Content)["Customer"]?["Industry"]?["Text"]?.GetValue<string>());
    }

    [Fact]
    public void CopyMatchingParts_NeverCopiesTheLeadState()
    {
        // Arrange
        var leadDefinition = new ContentTypeDefinitionBuilder().WithName("Lead").WithPart("LeadPart").Build();
        var contactDefinition = new ContentTypeDefinitionBuilder().WithName("Customer").WithPart("LeadPart").Build();

        var lead = new ContentItem { ContentType = "Lead" };
        lead.Alter<LeadPart>(part => part.Company = new TextField { Text = "Acme" });

        var contact = new ContentItem { ContentType = "Customer" };

        // Act
        LeadConversionService.CopyMatchingParts(lead, leadDefinition, contact, contactDefinition, overwrite: true);

        // Assert
        Assert.False(contact.Has<LeadPart>());
    }

    private static T Part<T>(ContentItem contentItem)
        where T : ContentPart, new()
        => contentItem.TryGet<T>(out var part) ? part : null;

    private static ContentItem LeadWithMethods(params (string Type, string Number)[] numbers)
    {
        var lead = new ContentItem { ContentType = "Lead" };

        lead.Alter<BagPart>("ContactMethods", bag =>
        {
            bag.ContentItems = numbers.Select(number =>
            {
                var method = new ContentItem
                {
                    ContentType = "PhoneNumber",
                    ContentItemId = Guid.NewGuid().ToString("N")[..26],
                };

                method.Alter<PhoneNumberInfoPart>(part =>
                {
                    part.Number = new PhoneField { PhoneNumber = number.Number };
                    part.Type = new TextField { Text = number.Type };
                });

                return method;
            }).ToList();
        });

        return lead;
    }

    private static global::OrchardCore.ContentManagement.Metadata.Models.ContentTypeDefinition WithField(
        global::OrchardCore.ContentManagement.Metadata.Models.ContentTypeDefinition definition,
        string partName,
        string fieldName,
        string fieldType)
    {
        // Re-adding a part by name keeps its old definition, so the type is built again around the new one.
        var partDefinition = new ContentPartDefinitionBuilder()
            .Named(partName)
            .WithField(fieldName, field => field.OfType(fieldType))
            .Build();

        return new ContentTypeDefinitionBuilder()
            .WithName(definition.Name)
            .WithPart(partName, partDefinition, _ => { })
            .Build();
    }
}
