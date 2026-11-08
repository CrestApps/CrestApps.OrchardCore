using Json.Schema;

namespace CrestApps.OrchardCore.PhoneNumbers.Verifications.Recipes;

/// <summary>
/// Builds the schema fragments the phone number verification settings schemas share.
/// </summary>
internal static class PhoneNumberVerificationSchemaBuilders
{
    /// <summary>
    /// Describes a secret the settings screen encrypts before it stores it.
    /// </summary>
    /// <param name="description">What the secret is.</param>
    /// <param name="purpose">The data protection purpose the value is encrypted with.</param>
    public static JsonSchemaBuilder ProtectedSecret(string description, string purpose)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description($"{description} The Settings step stores it exactly as given, so it must already be encrypted by the destination tenant with the '{purpose}' data protection purpose; a clear-text value cannot be read. Prefer entering it on the settings screen.");

    /// <summary>
    /// Describes a string that may be null.
    /// </summary>
    /// <param name="description">The description of the value.</param>
    public static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description(description);

    /// <summary>
    /// Describes whether a provider is enabled.
    /// </summary>
    public static JsonSchemaBuilder IsEnabled()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Default(false)
            .Description("Whether the provider is enabled and can be selected to verify phone numbers.");

    /// <summary>
    /// Describes an enum, which site settings store by member name.
    /// </summary>
    /// <param name="description">The description of the value.</param>
    /// <param name="names">The enum member names.</param>
    public static JsonSchemaBuilder Enum(string description, params string[] names)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String)
            .Enum(names)
            .Default(names[0])
            .Description(description);
}
