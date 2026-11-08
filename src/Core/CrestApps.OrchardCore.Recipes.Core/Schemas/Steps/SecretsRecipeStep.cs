using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;

/// <summary>
/// Schema for the <c>Secrets</c> recipe step of the Orchard Core Secrets module.
/// </summary>
public sealed class SecretsRecipeStep : RecipeStepSchemaBase
{
    private static readonly string[] _secretTypes = ["TextSecret", "RsaKeySecret", "X509Secret"];

    private static readonly string[] _secretStores = ["Database", "AzureKeyVault"];

    /// <inheritdoc />
    public override string Name => "Secrets";

    protected override JsonSchema CreateSchema()
    {
        return RecipeStepSchemaBuilders.BuildNamedStep(
            Name,
            [
                ("Secrets", new JsonSchemaBuilder()
                    .OneOf(BuildSecretList(), BuildExportedSecrets())
                    .Description("The secrets to create. Use an array to declare text secrets whose values come from the recipe or, preferably, from the 'OrchardCore:Secrets:{Name}' configuration key (environment variable 'OrchardCore__Secrets__{Name}'). Use an object keyed by secret name to import the output of the Secrets deployment step.")),
                ("EncryptionKeyName", RecipeStepSchemaBuilders.String()
                    .Description("The name of the RsaKeySecret or X509Secret of this tenant that holds the private key used to decrypt the encrypted secrets of an exported object. Required when the secrets are encrypted.")),
            ],
            ["Secrets"]);
    }

    private static JsonSchemaBuilder BuildSecretList()
        => RecipeStepSchemaBuilders.Array(
            RecipeStepSchemaBuilders.Object(
                [
                    ("Name", RecipeStepSchemaBuilders.String()
                        .MinLength(1)
                        .Description("The name of the secret, for example 'Smtp.Password'. Settings reference the secret by this name.")),
                    ("Store", RecipeStepSchemaBuilders.SuggestedString(_secretStores)
                        .Description("The name of the secret store. When omitted, the default store is used.")),
                    ("Value", RecipeStepSchemaBuilders.String()
                        .Description("The clear value of the text secret. Avoid it in recipes that are committed: when omitted, the value is read from the 'OrchardCore:Secrets:{Name}' configuration key, and the secret is skipped when no value is found.")),
                ],
                ["Name"]))
            .Description("Text secrets to create or update.");

    private static JsonSchemaBuilder BuildExportedSecrets()
        => RecipeStepSchemaBuilders.Object(additionalProperties: true)
            .AdditionalProperties(RecipeStepSchemaBuilders.Object(
                [
                    ("SecretInfo", RecipeStepSchemaBuilders.Object(
                        [
                            ("Store", RecipeStepSchemaBuilders.SuggestedString(_secretStores)
                                .Description("The name of the secret store.")),
                            ("Type", RecipeStepSchemaBuilders.SuggestedString(_secretTypes)
                                .Description("The type of the secret. Defaults to 'TextSecret'.")),
                            ("Description", RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String())
                                .Description("The description of the secret.")),
                            ("ExpiresUtc", RecipeStepSchemaBuilders.Nullable(RecipeStepSchemaBuilders.String().Format(Formats.DateTime))
                                .Description("The UTC date and time when the secret expires.")),
                        ])
                        .Description("The metadata of the secret.")),
                    ("Version", RecipeStepSchemaBuilders.Integer().Description("The version of the encryption format of an encrypted secret.")),
                    ("EncryptedKey", RecipeStepSchemaBuilders.String().Description("The encrypted symmetric key of an encrypted secret.")),
                    ("EncryptedData", RecipeStepSchemaBuilders.String().Description("The encrypted value of an encrypted secret.")),
                    ("IV", RecipeStepSchemaBuilders.String().Description("The initialization vector of an encrypted secret.")),
                    ("Tag", RecipeStepSchemaBuilders.String().Description("The authentication tag of an encrypted secret.")),
                ]))
            .Description("Secrets keyed by name, as the Secrets deployment step exports them. An entry without encrypted data takes its value from the 'OrchardCore:Secrets:{Name}' configuration key and is skipped when no value is found.");
}
