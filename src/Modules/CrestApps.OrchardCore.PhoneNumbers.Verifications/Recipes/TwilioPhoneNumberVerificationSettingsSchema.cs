using CrestApps.OrchardCore.PhoneNumbers.Verifications.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Recipes;

/// <summary>
/// Describes the <c>TwilioPhoneNumberVerificationSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class TwilioPhoneNumberVerificationSettingsSchema : SiteSettingsSchemaBase
{
    private const string ProtectorPurpose = "PhoneNumberVerifications.Twilio";

    public override string Name => nameof(TwilioPhoneNumberVerificationSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The Twilio Lookup phone number verification provider.")
            .Properties(
                (nameof(TwilioPhoneNumberVerificationSettings.IsEnabled), PhoneNumberVerificationSchemaBuilders.IsEnabled()),
                (nameof(TwilioPhoneNumberVerificationSettings.AuthenticationType), PhoneNumberVerificationSchemaBuilders.Enum(
                    "How the provider authenticates: with an API key SID and secret, or with the account SID and auth token.",
                    nameof(TwilioPhoneNumberVerificationAuthenticationType.ApiKey),
                    nameof(TwilioPhoneNumberVerificationAuthenticationType.AccountCredentials))),
                (nameof(TwilioPhoneNumberVerificationSettings.ApiKeySid), PhoneNumberVerificationSchemaBuilders.NullableString("The API key SID, used with ApiKey authentication.")),
                (nameof(TwilioPhoneNumberVerificationSettings.ProtectedApiKeySecret), PhoneNumberVerificationSchemaBuilders.ProtectedSecret("The API key secret, used with ApiKey authentication.", ProtectorPurpose)),
                (nameof(TwilioPhoneNumberVerificationSettings.AccountSid), PhoneNumberVerificationSchemaBuilders.NullableString("The account SID, used with AccountCredentials authentication.")),
                (nameof(TwilioPhoneNumberVerificationSettings.ProtectedAuthToken), PhoneNumberVerificationSchemaBuilders.ProtectedSecret("The auth token, used with AccountCredentials authentication.", ProtectorPurpose)),
                (nameof(TwilioPhoneNumberVerificationSettings.CountryCode), PhoneNumberVerificationSchemaBuilders.NullableString("The ISO 3166-1 alpha-2 country code used when a number is given in national format, such as 'US'.")),
                (nameof(TwilioPhoneNumberVerificationSettings.Fields), PhoneNumberVerificationSchemaBuilders.NullableString("The comma-separated Lookup data packages requested with each lookup, such as 'line_type_intelligence'.")))
            .AdditionalProperties(true);
}
