using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Exports the tax forms: who each reports, its fields and formulas, yearly values, checks and statement texts.
/// </summary>
public sealed class TaxFormsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxFormsDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxFormsDeploymentStep>("Tax forms");

    /// <inheritdoc />
    public override string Name => TaxationConstants.TaxFormsRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class TaxFormsDeploymentSource : CatalogDeploymentSource<TaxFormsDeploymentStep, TaxForm>
{
    public TaxFormsDeploymentSource(ITaxFormManager manager)
        : base(TaxFormsRecipeStep.EntriesName, manager)
    {
    }

    protected override IEnumerable<TaxForm> Order(IEnumerable<TaxForm> entries)
        => entries.OrderBy(entry => entry.Name, StringComparer.Ordinal);
}

/// <summary>
/// Imports tax forms, matched by id and then by name: a form that exists is replaced with the recipe's, fields included.
/// </summary>
internal sealed class TaxFormsRecipeStep : CatalogRecipeStep<TaxForm>
{
    public const string EntriesName = "Forms";

    private readonly ITaxFormManager _manager;

    public TaxFormsRecipeStep(ITaxFormManager manager)
        : base(TaxationConstants.TaxFormsRecipeStepName, EntriesName, manager)
    {
        _manager = manager;
    }

    protected override async ValueTask<TaxForm> FindExistingAsync(string id, JsonObject data)
    {
        if (await base.FindExistingAsync(id, data) is { } byId)
        {
            return byId;
        }

        var name = data["Name"]?.GetValue<string>();

        return string.IsNullOrWhiteSpace(name) ? null : await _manager.FindByNameAsync(name);
    }
}

internal sealed class TaxFormsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(TaxFormsDeploymentStep);

    protected override string DisplayText => "Tax forms";

    protected override string Description => "Exports every tax form, with its fields, formulas, yearly values, checks and statement texts.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

/// <summary>
/// Describes the "TaxForms" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TaxFormsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => TaxationConstants.TaxFormsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(TaxationConstants.TaxFormsRecipeStepName).Description("Recipe step discriminator. Must be 'TaxForms'.")),
                (TaxFormsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The form's 26-character id; omit it to match by name or create one.")),
                            ("Name", Text("The stored name filings keep, such as US-941: no spaces or colons.")),
                            ("DisplayName", Text("What people see, such as Form 941 (quarterly federal return).")),
                            ("Description", Text("What the form is for.")),
                            ("Source", Text("The tax form source that prepares the form, such as Payroll.")),
                            ("Jurisdiction", Text("The jurisdiction a filer must be registered in to file it, such as US or CA-QC.")),
                            ("Period", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Year", "Quarter").Description("The period a filing covers.")),
                            ("HasRecipientStatements", Boolean("Whether each recipient gets a statement (a W-2, a 1099, a T4 slip), kept, printed and sent to them once filed.")),
                            ("StatementPerJurisdiction", Boolean("Whether a recipient gets a statement for each jurisdiction under the form's, such as a T4 slip per province.")),
                            ("RecipientCondition", Text("A formula over the recipient fields that says whether a recipient is reported; empty reports a recipient with any field not zero.")),
                            ("MissingTaxIdNote", Text("The note added for a reported recipient with no tax id on file, {0} being their name; empty adds none.")),
                            ("Enabled", Boolean("Whether filers can prepare the form.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("Settings a source keeps on the form, such as PayrollTaxFormSettings (Workers: Employees, Contractors or Everyone; PeopleJurisdiction; ListJurisdictionAmounts; PayCodeFieldPrefix).")),
                            ("Fields", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Number", Text("The box or line number, such as 5a or 12D; formulas read it as [5a].")),
                                        ("Label", Text("The label, such as 5a Taxable social security wages.")),
                                        ("Level", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Recipient", "Filing").Description("On each recipient's statement, or on the filing.")),
                                        ("Formula", Text("The formula, such as GREATEST(0, TAXABLE('US-SS-EE') - [5b]); empty is zero plus what pay codes report.")),
                                        ("AddToFiling", Boolean("Whether a recipient field is also added up on the filing, under the same number.")),
                                        ("AcceptsReportedAmounts", Boolean("Whether the source's records can report amounts to the field, such as pay codes for Payroll.")),
                                        ("Print", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Never", "Always", "WhenNotZero").Description("When a recipient field is printed on the recipient's statement.")))
                                    .Required("Number", "Level")
                                    .AdditionalProperties(false))
                                .Description("The fields (the form's boxes and lines), in the order they are shown.")),
                            ("Values", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Name", Text("The name formulas read it by with VALUE('Name').")),
                                        ("Year", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The first year it applies to.")),
                                        ("Amount", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The value.")))
                                    .Required("Name", "Year", "Amount")
                                    .AdditionalProperties(false))
                                .Description("Yearly values: the latest year at or before the filing's applies.")),
                            ("Checks", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Array)
                                .Items(new JsonSchemaBuilder()
                                    .Type(SchemaValueType.Object)
                                    .Properties(
                                        ("Condition", Text("A formula over the filing fields that must be true.")),
                                        ("Note", Text("The note added to the filing when it is not.")))
                                    .Required("Condition", "Note")
                                    .AdditionalProperties(false))
                                .Description("Checks that add a note to a filing when they fail.")),
                            ("Statement", new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("Name", Text("What a recipient's statement is called, such as Form W-2.")),
                                    ("Title", Text("The printed title.")),
                                    ("Copy", Text("The line under the title.")),
                                    ("TaxIdLabel", Text("The label of the recipient's tax id.")),
                                    ("TaxIdFormat", Text("How the masked tax id is printed, {0} being its last characters.")),
                                    ("FilerTaxIdLabel", Text("The label of the filer's number.")),
                                    ("FilerAccountJurisdiction", Text("The jurisdiction of the filer's registration whose account number is printed; empty prints the filer's tax id.")),
                                    ("FileName", Text("The start of the PDF's file name, such as W-2.")))
                                .AdditionalProperties(false)
                                .Description("How a recipient's statement is printed.")))
                        .Required("Name", "DisplayName", "Source", "Jurisdiction", "Fields")
                        .AdditionalProperties(true))
                    .Description("The tax forms to create, or replace by id or name.")))
            .Required("name", TaxFormsRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Boolean(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);
}
