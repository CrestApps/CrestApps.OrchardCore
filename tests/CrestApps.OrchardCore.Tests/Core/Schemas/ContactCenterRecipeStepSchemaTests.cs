using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

/// <summary>
/// Pins every recipe step schema that describes Contact Center, Omnichannel and phone system configuration to the record
/// it imports, so a property added to a record without a matching schema entry fails here instead of being invisible to
/// every recipe author.
/// </summary>
public sealed class ContactCenterRecipeStepSchemaTests
{
    /// <summary>
    /// Describes one recipe step schema and the record each entry of its collection carries.
    /// </summary>
    /// <param name="Schema">The schema under test.</param>
    /// <param name="CollectionName">The property of the step that holds the entries.</param>
    /// <param name="EntryType">The record each entry describes.</param>
    /// <param name="Only">When supplied, the only record members the step carries.</param>
    private sealed record StepCase(IRecipeStep Schema, string CollectionName, Type EntryType, string[] Only = null);

    private static readonly StepCase[] _steps =
    [
        new(new ContactCenterSkillRecipeStep(), "Skills", typeof(ContactCenterSkill)),
        new(new ContactCenterQueueGroupRecipeStep(), "QueueGroups", typeof(ActivityQueueGroup)),
        new(new ContactCenterBusinessHoursCalendarRecipeStep(), "Calendars", typeof(BusinessHoursCalendar)),
        new(new ContactCenterQueueRecipeStep(), "Queues", typeof(ActivityQueue)),
        new(new ContactCenterEntryPointRecipeStep(), "EntryPoints", typeof(ContactCenterEntryPoint)),
        new(new ContactCenterDialerProfileRecipeStep(), "DialerProfiles", typeof(DialerProfile)),
        new(new AgentStateReasonCodeRecipeStep(), "ReasonCodes", typeof(AgentStateReasonCode)),
        new(
            new ContactCenterAgentEntitlementRecipeStep(),
            "Agents",
            typeof(AgentProfile),
            [
                nameof(AgentProfile.UserName),
                nameof(AgentProfile.DisplayName),
                nameof(AgentProfile.MaxConcurrentInteractions),
                nameof(AgentProfile.AllowedQueueIds),
                nameof(AgentProfile.AllowedCampaignIds),
                nameof(AgentProfile.Skills),
                nameof(AgentProfile.SkillProficiencies),
                nameof(AgentProfile.QueueMemberships),
            ]),
        new(new OmnichannelDispositionRecipeStep(), "Dispositions", typeof(OmnichannelDisposition)),
        new(new OmnichannelChannelEndpointRecipeStep(), "ChannelEndpoints", typeof(OmnichannelChannelEndpoint)),
        new(new OmnichannelCampaignGroupRecipeStep(), "CampaignGroups", typeof(OmnichannelCampaignGroup)),
        new(new OmnichannelCampaignRecipeStep(), "Campaigns", typeof(OmnichannelCampaign)),
        new(new OmnichannelSubjectActionRecipeStep(), "SubjectActions", typeof(SubjectAction)),
        new(new OmnichannelCadenceRecipeStep(), "Cadences", typeof(Cadence)),
        new(new ContactCenterVoiceMediaRecipeStep(), "VoiceMedia", typeof(VoiceMediaItem)),
        new(new OmnichannelMessageTemplateRecipeStep(), "Templates", typeof(MessageTemplate)),
        new(new TelephonyExtensionRecipeStep(), "Extensions", typeof(TelephonyExtension)),
    ];

    /// <summary>
    /// The provider settings steps, each carrying one settings object under its <c>Settings</c> property.
    /// </summary>
    private static readonly (IRecipeStep Schema, Type SettingsType)[] _settingsSteps =
    [
        (new TelnyxSettingsRecipeStep(), typeof(TelnyxSettings)),
        (new TelnyxSmsSettingsRecipeStep(), typeof(TelnyxSmsSettings)),
        (new AsteriskSettingsRecipeStep(), typeof(AsteriskSettings)),
    ];

    /// <summary>
    /// The site settings groups these modules contribute to the generic <c>Settings</c> step.
    /// </summary>
    private static readonly (ISiteSettingsSchemaDefinition Schema, Type SettingsType)[] _siteSettings =
    [
        (new TelephonySettingsSchema(), typeof(TelephonySettings)),
        (new SoftPhoneWidgetSettingsSchema(), typeof(SoftPhoneWidgetSettings)),
        (new ContactCenterExternalTransferSettingsSchema(), typeof(ContactCenterExternalTransferSettings)),
        (new ContactCenterRecordingSettingsSchema(), typeof(ContactCenterRecordingSettings)),
        (new SecureCaptureSettingsSchema(), typeof(SecureCaptureSettings)),
        (new TelnyxSettingsSchema(), typeof(TelnyxSettings)),
        (new TelnyxSmsSettingsSchema(), typeof(TelnyxSmsSettings)),
        (new AsteriskSettingsSchema(), typeof(AsteriskSettings)),
    ];

    public static TheoryData<string> StepNames()
        => [.. _steps.Select(step => step.Schema.Name)];

    public static TheoryData<string> SettingsStepNames()
        => [.. _settingsSteps.Select(step => step.Schema.Name)];

    public static TheoryData<string> SiteSettingsNames()
        => [.. _siteSettings.Select(step => step.Schema.Name)];

    [Theory]
    [MemberData(nameof(StepNames))]
    public async Task Schema_DescribesEveryPropertyTheStepCarries(string stepName)
    {
        // Arrange
        var step = _steps.Single(candidate => candidate.Schema.Name == stepName);
        var schema = await step.Schema.GetSchemaAsync(TestContext.Current.CancellationToken);
        var root = ToNode(schema);
        var problems = new List<string>();

        // Act
        Assert.Equal(stepName, root["properties"]?["name"]?["const"]?.GetValue<string>());

        var collection = root["properties"]?[step.CollectionName]?.AsObject();

        Assert.True(collection is not null, $"{stepName} does not declare its '{step.CollectionName}' collection.");
        Assert.Contains(step.CollectionName, root["required"]!.AsArray().Select(node => node.GetValue<string>()));

        var items = collection["items"]?.AsObject();

        Assert.True(items is not null, $"{stepName}.{step.CollectionName} does not describe its entries.");

        ContactCenterRecipeSchemaCoverage.CheckObject(step.EntryType, items, $"{stepName}.{step.CollectionName}[]", problems, only: step.Only);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Prepend($"{stepName} is out of date with {step.EntryType.Name}:")));
    }

    [Theory]
    [MemberData(nameof(SettingsStepNames))]
    public async Task ProviderSettingsSchema_DescribesEverySetting(string stepName)
    {
        // Arrange
        var (schema, settingsType) = _settingsSteps.Single(candidate => candidate.Schema.Name == stepName);
        var root = ToNode(await schema.GetSchemaAsync(TestContext.Current.CancellationToken));
        var problems = new List<string>();

        // Act
        Assert.Equal(stepName, root["properties"]?["name"]?["const"]?.GetValue<string>());
        Assert.Contains("Settings", root["required"]!.AsArray().Select(node => node.GetValue<string>()));

        ContactCenterRecipeSchemaCoverage.CheckObject(settingsType, root["properties"]!["Settings"]!.AsObject(), $"{stepName}.Settings", problems);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Prepend($"{stepName} is out of date with {settingsType.Name}:")));
    }

    [Theory]
    [MemberData(nameof(SiteSettingsNames))]
    public async Task SiteSettingsSchema_DescribesEverySetting(string settingsName)
    {
        // Arrange
        var (schema, settingsType) = _siteSettings.Single(candidate => candidate.Schema.Name == settingsName);
        var builder = await schema.GetSchemaAsync(TestContext.Current.CancellationToken);
        var root = ToNode(builder.Build());
        var problems = new List<string>();

        // Act
        // The generic Settings step stores the object under the name of its type, so the schema has to use that name.
        Assert.Equal(settingsType.Name, settingsName);

        ContactCenterRecipeSchemaCoverage.CheckObject(settingsType, root, settingsName, problems);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Prepend($"{settingsName} is out of date with {settingsType.Name}:")));
    }

    [Fact]
    public void EveryCatalogStep_HasADistinctName()
    {
        var names = _steps.Select(step => step.Schema.Name).ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static JsonObject ToNode(JsonSchema schema)
        => JsonNode.Parse(schema.Root.Source.GetRawText())!.AsObject();
}
