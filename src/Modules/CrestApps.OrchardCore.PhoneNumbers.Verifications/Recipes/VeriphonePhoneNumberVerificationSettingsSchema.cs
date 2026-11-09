using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Recipes;

/// <summary>
/// Describes the <c>VeriphonePhoneNumberVerificationSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class VeriphonePhoneNumberVerificationSettingsSchema : SiteSettingsSchemaBase
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.Veriphone";

    public override string Name => nameof(VeriphonePhoneNumberVerificationSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Veriphone phone number verification provider.")
            .Properties(
                (nameof(VeriphonePhoneNumberVerificationSettings.IsEnabled), PhoneNumberVerificationSchemaBuilders.IsEnabled()),
                (nameof(VeriphonePhoneNumberVerificationSettings.ProtectedApiKey), PhoneNumberVerificationSchemaBuilders.ProtectedSecret("The API key.", ProtectorPurpose)))
            .AdditionalProperties(true);
}
