using CrestApps.OrchardCore.PhoneNumbers.Core;
using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Recipes;

/// <summary>
/// Describes the <c>PhoneNumberVerificationsSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
public sealed class PhoneNumberVerificationsSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(PhoneNumberVerificationsSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("How and how often phone numbers are verified.")
            .Properties(
                (nameof(PhoneNumberVerificationsSettings.RevalidationIntervalDays), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Minimum(1)
                    .Default(PhoneNumberVerificationsSettings.DefaultRevalidationIntervalDays)
                    .Description("The number of days after which a verified phone number is verified again.")),
                (nameof(PhoneNumberVerificationsSettings.MaxVerificationAttempts), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Minimum(1)
                    .Default(PhoneNumberVerificationsSettings.DefaultMaxVerificationAttempts)
                    .Description("The number of failed attempts in a row after which a record is no longer retried automatically and waits to be queued again by hand.")),
                (nameof(PhoneNumberVerificationsSettings.RequestDelayMilliseconds), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Minimum(0)
                    .Default(PhoneNumberVerificationsSettings.DefaultRequestDelayMilliseconds)
                    .Description("The delay, in milliseconds, between two provider requests during background verification, to stay under the provider's rate limits.")),
                (nameof(PhoneNumberVerificationsSettings.SelectedProvider), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Examples(
                        PhoneNumberVerificationsConstants.Providers.AbstractApi,
                        PhoneNumberVerificationsConstants.Providers.Veriphone,
                        PhoneNumberVerificationsConstants.Providers.Twilio)
                    .Description("The key of the provider that verifies phone numbers. The provider's feature must be enabled and the provider enabled in its own settings.")))
            .AdditionalProperties(true);
}
