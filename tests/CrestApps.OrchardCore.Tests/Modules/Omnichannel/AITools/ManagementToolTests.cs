using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.AI;
using CrestApps.OrchardCore.Tests.Agent;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;

/// <summary>
/// The subject flow, not-in-service, AI call and text, address and CRM tools call the services the admin pages call,
/// with the pages' rules, and say what was refused.
/// </summary>
public sealed class ManagementToolTests
{
    private readonly ManagementToolHarness _harness = new();

    [Fact]
    public async Task ListSubjectFlows_ReturnsOneSubjectsActionsWithTheirDispositionAndFlags()
    {
        // Arrange
        _harness.AddSubjectType();
        _harness.Dispositions.Add(new OmnichannelDisposition { ItemId = "no-answer", Name = "No answer" });
        var action = new SubjectAction
        {
            ItemId = "action-1",
            Source = OmnichannelConstants.ActionTypes.TryAgain,
            SubjectContentType = "SalesCall",
            DispositionId = "no-answer",
            SetDoNotCall = true,
        };
        action.Put(new TryAgainActionMetadata { MaxAttempt = 3, DefaultScheduleHours = 4 });
        _harness.SubjectActions.Add(action);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListSubjectFlowsTool(), _harness.Build(), new() { ["subjectContentType"] = "SalesCall" }));

        // Assert
        var item = Assert.Single(json.GetProperty("actions").EnumerateArray());
        Assert.Equal("No answer", item.GetProperty("dispositionName").GetString());
        Assert.Equal("set", item.GetProperty("doNotCall").GetString());
        Assert.Equal("leave", item.GetProperty("doNotSms").GetString());
        Assert.Equal(3, item.GetProperty("settings").GetProperty("maxAttempts").GetInt32());
    }

    [Fact]
    public async Task ListSubjectFlows_SaysSoWhenTheSubjectDoesNotExist()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ListSubjectFlowsTool(), _harness.Build(), new() { ["subjectContentType"] = "Missing" });

        // Assert
        Assert.StartsWith("No subject content type is named 'Missing'", result);
    }

    [Fact]
    public async Task SaveSubjectAction_CreatesATryAgainActionThatMarksDoNotCall()
    {
        // Arrange
        _harness.AddSubjectType();
        _harness.Dispositions.Add(new OmnichannelDisposition { ItemId = "not-interested", Name = "Not interested" });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSubjectActionTool(), _harness.Build(), new()
        {
            ["subjectContentType"] = "SalesCall",
            ["actionType"] = "TryAgain",
            ["dispositionId"] = "not-interested",
            ["doNotCall"] = "set",
            ["maxAttempts"] = 2,
        });

        // Assert
        Assert.Equal("created", AgentToolTestContext.ReadJson(result).GetProperty("saved").GetString());
        var created = Assert.Single(_harness.CreatedActions);
        Assert.Equal("SalesCall", created.SubjectContentType);
        Assert.Equal("not-interested", created.DispositionId);
        Assert.True(created.SetDoNotCall);
        Assert.Null(created.SetDoNotSms);
        Assert.Equal(2, created.GetOrCreate<TryAgainActionMetadata>().MaxAttempt);
    }

    [Fact]
    public async Task SaveSubjectAction_RefusesADispositionThatDoesNotExist()
    {
        // Arrange
        _harness.AddSubjectType();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSubjectActionTool(), _harness.Build(), new()
        {
            ["subjectContentType"] = "SalesCall",
            ["actionType"] = "Finish",
            ["dispositionId"] = "missing",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: no disposition has the ID 'missing'", result);
        Assert.Empty(_harness.CreatedActions);
    }

    [Fact]
    public async Task SaveSubjectAction_RefusesConvertLeadWithoutTheCrmFeature()
    {
        // Arrange
        _harness.AddSubjectType();
        _harness.Dispositions.Add(new OmnichannelDisposition { ItemId = "qualified", Name = "Qualified" });

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSubjectActionTool(), _harness.Build(), new()
        {
            ["subjectContentType"] = "SalesCall",
            ["actionType"] = "ConvertLead",
            ["dispositionId"] = "qualified",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: 'ConvertLead' is not an action type here", result);
    }

    [Fact]
    public async Task ListNotInServiceNumbers_ReturnsWhoMarkedEachNumberAndWhen()
    {
        // Arrange
        _harness.NotInService.Setup(service => service.PageAsync(1, 20, "702", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PageResult<NotInServiceNumber>
            {
                Count = 1,
                Entries =
                [
                    new NotInServiceNumber
                    {
                        PhoneNumber = "+17025550123",
                        Source = OmnichannelConstants.NotInServiceSources.Manual,
                        MarkedByUsername = "admin",
                        FirstDetectedUtc = ManagementToolHarness.Now,
                        LastDetectedUtc = ManagementToolHarness.Now,
                        DetectionCount = 2,
                    },
                ],
            });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListNotInServiceNumbersTool(), _harness.Build(), new() { ["search"] = "702" }));

        // Assert
        var item = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("+17025550123", item.GetProperty("phoneNumber").GetString());
        Assert.Equal("admin", item.GetProperty("markedByUsername").GetString());
        Assert.Equal(2, item.GetProperty("detectionCount").GetInt32());
        Assert.NotNull(item.GetProperty("lastDetected").GetString());
    }

    [Fact]
    public async Task SetNumberNotInService_MarksANumberByHandAsTheCaller()
    {
        // Arrange
        NotInServiceMark mark = null;
        _harness.NotInService.Setup(service => service.MarkAsync(It.IsAny<NotInServiceMark>(), It.IsAny<CancellationToken>()))
            .Callback((NotInServiceMark value, CancellationToken _) => mark = value)
            .ReturnsAsync((NotInServiceMark value, CancellationToken _) => new NotInServiceNumber { PhoneNumber = value.PhoneNumber });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetNumberNotInServiceTool(), _harness.Build(), new()
        {
            ["phoneNumbers"] = new[] { "+17025550123" },
            ["notInService"] = true,
        }));

        // Assert
        Assert.Equal("+17025550123", Assert.Single(json.GetProperty("changed").EnumerateArray()).GetString());
        Assert.Equal(OmnichannelConstants.NotInServiceSources.Manual, mark.Source);
        Assert.Equal(AgentToolTestContext.UserName, mark.MarkedByUsername);
    }

    [Fact]
    public async Task SetNumberNotInService_ReportsANumberThatCannotBeRead()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SetNumberNotInServiceTool(), _harness.Build(), new()
        {
            ["phoneNumbers"] = new[] { "7025550123" },
            ["notInService"] = false,
        }));

        // Assert
        Assert.Empty(json.GetProperty("changed").EnumerateArray());
        Assert.Equal("7025550123", Assert.Single(json.GetProperty("unreadable").GetProperty("numbers").EnumerateArray()).GetString());
        _harness.NotInService.Verify(service => service.ClearAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartAIActivity_QueuesAnAutomatedCallStartedFromTheAssistant()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new StartAIActivityTool(), _harness.Build(), new()
        {
            ["channel"] = "call",
            ["to"] = "+15550102030",
            ["aiProfileId"] = "profile-1",
        }));

        // Assert
        Assert.True(json.GetProperty("queued").GetBoolean());
        var activity = Assert.Single(_harness.CreatedActivities);
        Assert.Equal(StartAIActivityTool.Source, activity.Source);
        Assert.Equal(OmnichannelConstants.Channels.Phone, activity.Channel);
        Assert.Equal(ActivityInteractionType.Automated, activity.InteractionType);
        Assert.Equal("+15550102030", activity.PreferredDestination);
    }

    [Fact]
    public async Task StartAIActivity_RefusesANumberThatOptedOut()
    {
        // Arrange
        _harness.OptedOutAddresses.Add("+15550102030");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new StartAIActivityTool(), _harness.Build(), new()
        {
            ["channel"] = "text",
            ["to"] = "+15550102030",
            ["aiProfileId"] = "profile-1",
        });

        // Assert
        Assert.StartsWith("Nothing was queued: +15550102030 has asked not to be reached", result);
        Assert.Empty(_harness.CreatedActivities);
    }

    [Fact]
    public async Task StartAIActivity_RefusesANumberNotInE164()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new StartAIActivityTool(), _harness.Build(), new()
        {
            ["channel"] = "call",
            ["to"] = "555 0102",
            ["aiProfileId"] = "profile-1",
        });

        // Assert
        Assert.StartsWith("Nothing was queued: give the phone number in E.164 form", result);
        Assert.Empty(_harness.CreatedActivities);
    }

    [Fact]
    public async Task StartAIActivity_RefusesANumberOnADoNotCallList()
    {
        // Arrange
        _harness.DoNotCallListAddresses.Add("+15550102030");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new StartAIActivityTool(), _harness.Build(), new()
        {
            ["channel"] = "call",
            ["to"] = "+15550102030",
            ["aiProfileId"] = "profile-1",
        });

        // Assert
        Assert.StartsWith("Nothing was queued: +15550102030 is on one of the site's do-not-call lists", result);
        Assert.Empty(_harness.CreatedActivities);
    }

    [Fact]
    public async Task RecordContactOptOut_RecordsAnOptOutAgainstTheAddress()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new RecordContactOptOutTool(), _harness.Build(), new()
        {
            ["channel"] = "sms",
            ["address"] = "+15550102030",
            ["reason"] = "asked on a call",
        }));

        // Assert
        Assert.True(json.GetProperty("recorded").GetBoolean());
        var (channel, address, change) = Assert.Single(_harness.RecordedConsent);
        Assert.Equal(ConsentChannels.Sms, channel);
        Assert.Equal("+15550102030", address);
        Assert.True(change.OptedOut);
        Assert.Null(change.ContactId);
        Assert.Equal("AIAssistant: asked on a call", change.Source);
    }

    [Fact]
    public async Task RecordContactOptOut_RecordsAnOptOutForAContact()
    {
        // Arrange
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Contact" };
        contact.Apply(nameof(OmnichannelContactPart), new OmnichannelContactPart());
        _harness.ContentItems["contact-1"] = contact;

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new RecordContactOptOutTool(), _harness.Build(), new()
        {
            ["channel"] = "phone",
            ["address"] = "+15550102030",
            ["contactId"] = "contact-1",
        }));

        // Assert
        Assert.True(json.GetProperty("recorded").GetBoolean());
        var (channel, _, change) = Assert.Single(_harness.RecordedConsent);
        Assert.Equal(ConsentChannels.Phone, channel);
        Assert.Equal("contact-1", change.ContactId);
        Assert.Equal(ConsentContactKinds.Contact, change.ContactKind);
    }

    [Fact]
    public async Task RecordContactOptOut_RefusesAContactTheCallerCannotEdit()
    {
        // Arrange
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Contact" };
        contact.Apply(nameof(OmnichannelContactPart), new OmnichannelContactPart());
        _harness.ContentItems["contact-1"] = contact;
        var services = _harness.Build(authorize: (permission, _) => permission.Name != CommonPermissions.EditContent.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RecordContactOptOutTool(), services, new()
        {
            ["channel"] = "phone",
            ["address"] = "+15550102030",
            ["contactId"] = "contact-1",
        });

        // Assert
        Assert.Equal(CrestApps.OrchardCore.AI.Core.AIToolCallerAuthorization.NotAllowedMessage, result);
        Assert.Empty(_harness.RecordedConsent);
    }

    [Fact]
    public async Task RecordContactOptOut_RefusesAnUnknownContact()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RecordContactOptOutTool(), _harness.Build(), new()
        {
            ["channel"] = "phone",
            ["address"] = "+15550102030",
            ["contactId"] = "missing",
        });

        // Assert
        Assert.StartsWith("Nothing was recorded: no CRM contact has the ID 'missing'", result);
        Assert.Empty(_harness.RecordedConsent);
    }

    [Fact]
    public async Task RecordContactOptOut_RefusesAnAddressThatIsNotAnEmail()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new RecordContactOptOutTool(), _harness.Build(), new()
        {
            ["channel"] = "email",
            ["address"] = "not an address",
        });

        // Assert
        Assert.StartsWith("Nothing was recorded: give the e-mail address", result);
        Assert.Empty(_harness.RecordedConsent);
    }

    [Fact]
    public async Task CheckContactConsent_SaysANumberThatOptedOutIsSkipped()
    {
        // Arrange
        _harness.OptedOutAddresses.Add("+15550102030");

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new CheckContactConsentTool(), _harness.Build(), new()
        {
            ["channel"] = "sms",
            ["address"] = "+15550102030",
        }));

        // Assert
        Assert.True(json.GetProperty("optedOut").GetBoolean());
        Assert.Contains("communication preferences", json.GetProperty("recordedBy").GetString());
        Assert.Contains("Opted-out numbers and addresses are skipped", json.GetProperty("message").GetString());
        Assert.Empty(_harness.RecordedConsent);
    }

    [Fact]
    public async Task CheckContactConsent_NamesADoNotCallListAsTheSource()
    {
        // Arrange
        _harness.DoNotCallListAddresses.Add("+15550102030");

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new CheckContactConsentTool(), _harness.Build(), new()
        {
            ["channel"] = "phone",
            ["address"] = "+15550102030",
        }));

        // Assert
        Assert.True(json.GetProperty("optedOut").GetBoolean());
        Assert.Equal("one of the site's do-not-call lists", json.GetProperty("recordedBy").GetString());
    }

    [Fact]
    public async Task CheckContactConsent_SaysANumberThatDidNotOptOutHasNotOptedOut()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new CheckContactConsentTool(), _harness.Build(), new()
        {
            ["channel"] = "phone",
            ["address"] = "+15550102030",
        }));

        // Assert: consent records only opt-outs, so the answer is "has not opted out", never "consented".
        Assert.False(json.GetProperty("optedOut").GetBoolean());
        Assert.False(json.TryGetProperty("recordedBy", out var recordedBy) && recordedBy.ValueKind != System.Text.Json.JsonValueKind.Null);
        Assert.Contains("has not opted out", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task CheckContactConsent_RefusesANumberNotInE164()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CheckContactConsentTool(), _harness.Build(), new()
        {
            ["channel"] = "phone",
            ["address"] = "555-0102",
        });

        // Assert
        Assert.Equal("Give the phone number in E.164 form, such as +15551234567.", result);
    }

    [Fact]
    public async Task CheckContactConsent_RefusesAContactTheCallerCannotView()
    {
        // Arrange
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Contact" };
        contact.Apply(nameof(OmnichannelContactPart), new OmnichannelContactPart());
        _harness.ContentItems["contact-1"] = contact;
        var services = _harness.Build(authorize: (permission, _) => permission.Name != CommonPermissions.ViewContent.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CheckContactConsentTool(), services, new()
        {
            ["channel"] = "phone",
            ["address"] = "+15550102030",
            ["contactId"] = "contact-1",
        });

        // Assert
        Assert.Equal(CrestApps.OrchardCore.AI.Core.AIToolCallerAuthorization.NotAllowedMessage, result);
    }

    [Fact]
    public async Task CancelAIActivity_CancelsAScheduledCall()
    {
        // Arrange
        _harness.Activities["activity-1"] = new OmnichannelActivity { ItemId = "activity-1", AIProfileId = "profile-1", Status = ActivityStatus.Scheduled };

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new CancelAIActivityTool(), _harness.Build(), new() { ["id"] = "activity-1" }));

        // Assert
        Assert.True(json.GetProperty("cancelled").GetBoolean());
        Assert.Equal(ActivityStatus.Cancelled, Assert.Single(_harness.UpdatedActivities).Status);
    }

    [Fact]
    public async Task CancelAIActivity_ReturnsAConflictWhenTheCallAlreadyStarted()
    {
        // Arrange
        _harness.Activities["activity-1"] = new OmnichannelActivity { ItemId = "activity-1", AIProfileId = "profile-1", Status = ActivityStatus.InProgress };

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new CancelAIActivityTool(), _harness.Build(), new() { ["id"] = "activity-1" });

        // Assert
        Assert.StartsWith("Conflict:", result);
        Assert.Empty(_harness.UpdatedActivities);
    }

    [Fact]
    public async Task SaveOmnichannelAddress_AddsAPhoneAndSmsNumber()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOmnichannelAddressTool(), _harness.Build(), new()
        {
            ["addressType"] = "PhoneNumber",
            ["displayText"] = "Support line",
            ["value"] = "+15550109000",
            ["capabilities"] = new[] { "Phone", "SMS" },
            ["providerName"] = "Telnyx",
        });

        // Assert
        Assert.Equal("created", AgentToolTestContext.ReadJson(result).GetProperty("saved").GetString());
        var created = Assert.Single(_harness.Endpoints.Created);
        Assert.Equal(OmnichannelAddressTypes.PhoneNumber, created.AddressType);
        Assert.Equal(new[] { "Phone", "SMS" }, created.Capabilities);
        Assert.Equal("Telnyx", created.ProviderName);
    }

    [Fact]
    public async Task SaveOmnichannelAddress_RefusesACredential()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOmnichannelAddressTool(), _harness.Build(), new()
        {
            ["addressType"] = "PhoneNumber",
            ["displayText"] = "Support line",
            ["value"] = "+15550109000",
            ["apiKey"] = "KEY-123",
        });

        // Assert
        Assert.StartsWith("Nothing was saved: 'apiKey' looks like a credential", result);
        Assert.Empty(_harness.Endpoints.Created);
    }

    [Fact]
    public async Task SaveOmnichannelAddress_RefusesACapabilityTheTypeDoesNotOffer()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveOmnichannelAddressTool(), _harness.Build(), new()
        {
            ["addressType"] = "PhoneNumber",
            ["displayText"] = "Support line",
            ["value"] = "+15550109000",
            ["capabilities"] = new[] { "Email" },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: Email cannot be used", result);
    }

    [Fact]
    public async Task ConvertLead_ConvertsThroughTheConversionServiceAsTheCaller()
    {
        // Arrange
        var lead = AddLead(converted: false);
        LeadConversionRequest request = null;
        var result = new LeadConversionResult
        {
            Contact = new ContentItem { ContentItemId = "contact-9", ContentType = "Contact", DisplayText = "John Park" },
            ContactCreated = true,
        };
        _harness.LeadConversion.Setup(service => service.ConvertAsync(It.IsAny<LeadConversionRequest>(), It.IsAny<CancellationToken>()))
            .Callback((LeadConversionRequest value, CancellationToken _) => request = value)
            .ReturnsAsync(result);
        object checkedResource = null;

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ConvertLeadTool(), _harness.Build(authorize: (permission, resource) =>
        {
            checkedResource = resource ?? checkedResource;

            return true;
        }), new()
        {
            ["leadId"] = lead.ContentItemId,
            ["accountMode"] = "CreateNew",
            ["accountName"] = "Acme Inc.",
            ["createOpportunity"] = true,
            ["opportunityAmount"] = 12000,
            ["opportunityCloseDate"] = "2026-12-18",
        }));

        // Assert
        Assert.True(json.GetProperty("converted").GetBoolean());
        Assert.Equal("contact-9", json.GetProperty("contact").GetProperty("id").GetString());
        Assert.Same(lead, checkedResource);
        Assert.Equal(LeadConversionAccountMode.CreateNew, request.AccountMode);
        Assert.Equal(12000m, request.OpportunityAmount);
        Assert.Equal(new DateTime(2026, 12, 18), request.OpportunityCloseDate);
        Assert.Equal(AgentToolTestContext.UserName, request.UserName);
    }

    [Fact]
    public async Task ConvertLead_RefusesALeadThatWasConverted()
    {
        // Arrange
        var lead = AddLead(converted: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new ConvertLeadTool(), _harness.Build(), new() { ["leadId"] = lead.ContentItemId });

        // Assert
        Assert.Contains("was already converted", result);
        _harness.LeadConversion.Verify(service => service.ConvertAsync(It.IsAny<LeadConversionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ContentItem AddLead(bool converted)
    {
        var lead = new ContentItem
        {
            ContentItemId = "lead-1",
            ContentType = "Lead",
            DisplayText = "John Park",
        };
        lead.Apply(nameof(LeadPart), new LeadPart { IsConverted = converted });
        _harness.ContentItems[lead.ContentItemId] = lead;

        return lead;
    }
}
