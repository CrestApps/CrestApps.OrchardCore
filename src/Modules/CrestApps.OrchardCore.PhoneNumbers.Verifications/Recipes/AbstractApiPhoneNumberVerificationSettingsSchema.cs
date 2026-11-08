using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Recipes;

/// <summary>
/// Describes the <c>AbstractApiPhoneNumberVerificationSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class AbstractApiPhoneNumberVerificationSettingsSchema : SiteSettingsSchemaBase
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.AbstractApi";

    public override string Name => nameof(AbstractApiPhoneNumberVerificationSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The AbstractAPI phone number verification provider.")
            .Properties(
                (nameof(AbstractApiPhoneNumberVerificationSettings.IsEnabled), PhoneNumberVerificationSchemaBuilders.IsEnabled()),
                (nameof(AbstractApiPhoneNumberVerificationSettings.AuthenticationType), PhoneNumberVerificationSchemaBuilders.Enum(
                    "How the provider authenticates: with the API key, or with a user name and password.",
                    nameof(PhoneNumberVerificationAuthenticationType.ApiKey),
                    nameof(PhoneNumberVerificationAuthenticationType.Basic))),
                (nameof(AbstractApiPhoneNumberVerificationSettings.ProtectedApiKey), PhoneNumberVerificationSchemaBuilders.ProtectedSecret("The API key, used with ApiKey authentication.", ProtectorPurpose)),
                (nameof(AbstractApiPhoneNumberVerificationSettings.Username), PhoneNumberVerificationSchemaBuilders.NullableString("The user name, used with Basic authentication.")),
                (nameof(AbstractApiPhoneNumberVerificationSettings.ProtectedPassword), PhoneNumberVerificationSchemaBuilders.ProtectedSecret("The password, used with Basic authentication.", ProtectorPurpose)))
            .AdditionalProperties(true);
}
