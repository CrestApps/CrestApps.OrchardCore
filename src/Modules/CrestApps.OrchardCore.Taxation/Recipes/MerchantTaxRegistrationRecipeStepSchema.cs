using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Describes the <c>MerchantTaxRegistration</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class MerchantTaxRegistrationRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => TaxationDeploymentSteps.MerchantTaxRegistration;

    protected override string EntriesName => MerchantTaxRegistrationStep.EntriesName;

    protected override string Description => "Creates or updates the store's own tax registrations, matched by ItemId, then by name.";

    protected override string EntryDescription => "A tax registration: where the store collects a tax, under which number.";

    protected override IEnumerable<string> RequiredEntryProperties => ["Name", "JurisdictionId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The registration's name.")),
            ("JurisdictionId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the tax jurisdiction the store is registered in.")),
            ("TaxType", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The tax type the registration covers, or none for every type.")),
            ("RegistrationNumber", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The number the tax authority gave the store.")),
            ("IsActive", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the store collects the tax under this registration.")),
            ("ThresholdAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("The sales above which the store must collect the tax, or none.")),
            ("ThresholdAccumulatedAmount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The sales recorded into the jurisdiction so far, measured against ThresholdAmount. It is kept up to date as sales are recorded, so it is usually left out.")),
            ("RegistrationDateUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the tax authority granted the registration.")),
            ("EffectiveFromUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the registration takes effect.")),
            ("EffectiveToUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the registration ends.")),
        ];
}
