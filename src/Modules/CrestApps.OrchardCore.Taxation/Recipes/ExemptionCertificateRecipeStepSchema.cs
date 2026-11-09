using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Taxation.Deployments;
using Json.Schema;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// Describes the <c>ExemptionCertificate</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ExemptionCertificateRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => TaxationDeploymentSteps.ExemptionCertificate;

    protected override string EntriesName => ExemptionCertificateStep.EntriesName;

    protected override string Description => "Creates or updates customers' tax exemption certificates, matched by ItemId, then by customer and number. Import the customers first.";

    protected override string EntryDescription => "A customer's tax exemption certificate.";

    protected override IEnumerable<string> RequiredEntryProperties => ["CustomerId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The certificate's name.")),
            ("CertificateNumber", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The certificate's number.")),
            ("CustomerId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the customer the certificate belongs to.")),
            ("ExemptionType", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The kind of exemption, such as Resale.")),
            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Integer).Description("The certificate's status: only an active certificate exempts.")),
            ("EffectiveFromUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the certificate takes effect.")),
            ("ExpirationUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("When the certificate expires.")),
            ("TaxTypes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The tax types it exempts from, or none for every type.")),
            ("JurisdictionIds", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The jurisdictions it applies in, or none for every one.")),
            ("ClassificationCodes", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The product classification codes it covers, or none for every product.")),
        ];
}
