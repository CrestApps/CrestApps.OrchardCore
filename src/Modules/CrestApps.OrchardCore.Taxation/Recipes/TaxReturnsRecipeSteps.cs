using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Core.Recipes;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Taxation.Recipes;

/// <summary>
/// The names of the tax returns and e-file submissions steps.
/// </summary>
internal static class TaxReturnsRecipeStepNames
{
    public const string TaxReturns = "TaxReturns";

    public const string TaxReturnsEntries = "TaxReturns";

    public const string EFileSubmissions = "EFileSubmissions";

    public const string EFileSubmissionsEntries = "Submissions";
}

/// <summary>
/// Imports tax returns, matched by id, such as returns moved from another site or another system.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "TaxReturns", "TaxReturns": [ { "ItemId": "...", "RegistrationId": "...", "Form": "Generic", "Frequency": "Monthly", "PeriodKey": "2026-10", "PeriodStart": "2026-10-01", "PeriodEnd": "2026-10-31", "DueDate": "2026-11-20", "Status": "Filed", "AmountDue": 120.5 } ] }
/// </code>
/// </remarks>
internal sealed class TaxReturnsRecipeStep : CatalogRecipeStep<TaxReturn>
{
    public TaxReturnsRecipeStep(ITaxReturnManager manager)
        : base(TaxReturnsRecipeStepNames.TaxReturns, TaxReturnsRecipeStepNames.TaxReturnsEntries, manager)
    {
    }
}

/// <summary>
/// Imports e-file submissions, matched by id.
/// </summary>
internal sealed class EFileSubmissionsRecipeStep : CatalogRecipeStep<EFileSubmission>
{
    public EFileSubmissionsRecipeStep(IEFileSubmissionManager manager)
        : base(TaxReturnsRecipeStepNames.EFileSubmissions, TaxReturnsRecipeStepNames.EFileSubmissionsEntries, manager)
    {
    }
}

/// <summary>
/// Describes the <c>TaxReturns</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TaxReturnsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => TaxReturnsRecipeStepNames.TaxReturns;

    protected override string EntriesName => TaxReturnsRecipeStepNames.TaxReturnsEntries;

    protected override string Description => "Creates or updates tax returns, matched by ItemId.";

    protected override string EntryDescription => "A tax return of a registration for a period, with its lines, boxes, status and payments.";

    protected override IEnumerable<string> RequiredEntryProperties => ["RegistrationId", "Form", "PeriodKey"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("RegistrationId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the tax registration the return is filed under.")),
            ("JurisdictionId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The id of the jurisdiction the return is filed with.")),
            ("Form", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the return form, such as Generic.")),
            ("IsAnnualReconciliation", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether it is the yearly return filed besides the periodic ones.")),
            ("Frequency", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Monthly", "Quarterly", "Annual").Description("The frequency of the period.")),
            ("PeriodKey", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The period, such as 2026-10, 2026-Q3 or 2026.")),
            ("PeriodStart", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The first day of the period.")),
            ("PeriodEnd", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The last day of the period.")),
            ("DueDate", new JsonSchemaBuilder().Type(SchemaValueType.String).Format(Formats.Date).Description("The day the return is due.")),
            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Draft", "Ready", "Filed", "Amended").Description("Where the return is.")),
            ("Lines", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The figures the modules gave: Kind, JurisdictionId, TaxType, FormLine, Category, Description, Amount, TaxAmount, Source.")),
            ("Boxes", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The form's boxes, by their number or code.")),
            ("AmountDue", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("The amount the return says is due.")),
            ("Payments", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The payments recorded: Id, Amount, PaidOn, Reference, Method.")),
            ("FiledOn", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.Date).Description("The day the return was filed.")),
            ("ConfirmationNumber", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The agency's confirmation number.")),
            ("AmendsId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The id of the return this one corrects.")),
            ("Amendment", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The amendment number: 0 for the original return.")),
        ];
}

/// <summary>
/// Describes the <c>EFileSubmissions</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class EFileSubmissionsRecipeStepSchema : RecordsRecipeStepSchemaBase
{
    public override string Name => TaxReturnsRecipeStepNames.EFileSubmissions;

    protected override string EntriesName => TaxReturnsRecipeStepNames.EFileSubmissionsEntries;

    protected override string Description => "Creates or updates e-file submissions, matched by ItemId.";

    protected override string EntryDescription => "One attempt to file a document electronically, with the file built and the agency's answer.";

    protected override IEnumerable<string> RequiredEntryProperties => ["TargetType", "TargetId"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetEntryProperties()
        =>
        [
            ("TargetType", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The kind of document filed, such as TaxReturn.")),
            ("TargetId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The id of the document filed.")),
            ("Format", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the file format.")),
            ("Transport", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The name of the filing channel.")),
            ("IsManual", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether a person files the file and records the answer.")),
            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Created", "Submitted", "Accepted", "Rejected", "Failed").Description("Where the submission is.")),
            ("FileName", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The name of the file built.")),
            ("ContentType", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The media type of the file built.")),
            ("Content", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The file built, as base64.")),
            ("ExternalId", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The id the agency or the channel gave the submission.")),
            ("ConfirmationNumber", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The agency's confirmation number.")),
            ("Messages", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.String)).Description("The messages of the channel and the agency.")),
        ];
}

/// <summary>
/// Exports every tax return as a <c>TaxReturns</c> recipe step.
/// </summary>
public sealed class TaxReturnsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<TaxReturnsDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<TaxReturnsDeploymentStep>("Tax returns");

    /// <inheritdoc />
    public override string Name => TaxReturnsRecipeStepNames.TaxReturns;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

/// <summary>
/// Exports every e-file submission as an <c>EFileSubmissions</c> recipe step.
/// </summary>
public sealed class EFileSubmissionsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<EFileSubmissionsDeploymentStep>("Taxation");
    private static readonly LocalizationSource _title = LocalizationSource.Create<EFileSubmissionsDeploymentStep>("E-file submissions");

    /// <inheritdoc />
    public override string Name => TaxReturnsRecipeStepNames.EFileSubmissions;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class TaxReturnsDeploymentSource : CatalogDeploymentSource<TaxReturnsDeploymentStep, TaxReturn>
{
    public TaxReturnsDeploymentSource(ITaxReturnManager manager)
        : base(TaxReturnsRecipeStepNames.TaxReturnsEntries, manager)
    {
    }

    protected override IEnumerable<TaxReturn> Order(IEnumerable<TaxReturn> entries)
        => entries.OrderBy(entry => entry.PeriodEnd).ThenBy(entry => entry.RegistrationId, StringComparer.Ordinal).ThenBy(entry => entry.Amendment);
}

internal sealed class EFileSubmissionsDeploymentSource : CatalogDeploymentSource<EFileSubmissionsDeploymentStep, EFileSubmission>
{
    public EFileSubmissionsDeploymentSource(IEFileSubmissionManager manager)
        : base(TaxReturnsRecipeStepNames.EFileSubmissionsEntries, manager)
    {
    }

    protected override IEnumerable<EFileSubmission> Order(IEnumerable<EFileSubmission> entries)
        => entries.OrderBy(entry => entry.CreatedUtc);
}

internal sealed class TaxReturnsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(TaxReturnsDeploymentStep);

    protected override string DisplayText => "Tax returns";

    protected override string Description => "Exports every tax return, with its amendments and payments.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class EFileSubmissionsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(EFileSubmissionsDeploymentStep);

    protected override string DisplayText => "E-file submissions";

    protected override string Description => "Exports every e-file submission, with the file built and the agency's answer.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
