using System.Text.Json.Nodes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Recipes;
using CrestApps.OrchardCore.Recipes.Core;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

/// <summary>
/// Pins the phone number verification site settings schemas to the settings they describe, so a setting added without
/// a schema entry fails here.
/// </summary>
public sealed class PhoneNumberVerificationSettingsSchemaTests
{
    private static readonly (ISiteSettingsSchemaDefinition Schema, Type SettingsType)[] _siteSettings =
    [
        (new PhoneNumberVerificationsSettingsSchema(), typeof(PhoneNumberVerificationsSettings)),
        (new AbstractApiPhoneNumberVerificationSettingsSchema(), typeof(AbstractApiPhoneNumberVerificationSettings)),
        (new VeriphonePhoneNumberVerificationSettingsSchema(), typeof(VeriphonePhoneNumberVerificationSettings)),
        (new TwilioPhoneNumberVerificationSettingsSchema(), typeof(TwilioPhoneNumberVerificationSettings)),
    ];

    public static TheoryData<string> SiteSettingsNames()
        => [.. _siteSettings.Select(entry => entry.Schema.Name)];

    [Theory]
    [MemberData(nameof(SiteSettingsNames))]
    public async Task SiteSettingsSchema_DescribesEverySetting(string settingsName)
    {
        // Arrange
        var (schema, settingsType) = _siteSettings.Single(candidate => candidate.Schema.Name == settingsName);
        var builder = await schema.GetSchemaAsync(TestContext.Current.CancellationToken);
        var root = JsonNode.Parse(builder.Build().Root.Source.GetRawText())!.AsObject();
        var problems = new List<string>();

        // Act
        // The generic Settings step stores the object under the name of its type, so the schema has to use that name.
        Assert.Equal(settingsType.Name, settingsName);

        ContactCenterRecipeSchemaCoverage.CheckObject(settingsType, root, settingsName, problems);

        // Assert
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Prepend($"{settingsName} is out of date with {settingsType.Name}:")));
    }
}
