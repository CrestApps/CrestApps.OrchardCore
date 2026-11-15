using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Describes the "PayrollPayProfiles" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PayProfilesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.PayProfilesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        var election = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Jurisdiction", Text("The jurisdiction, such as US.")),
                ("EffectiveFrom", Text("The first day, yyyy-MM-dd.")),
                ("Values", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("The values of the jurisdiction's election fields, by name.")))
            .Required("Jurisdiction", "EffectiveFrom");
        var row = new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Method", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("BankAccount", "Check")),
                ("BankAccountId", Text("The person's bank account, for a deposit.")),
                ("Split", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Amount", "Percent", "Remainder")),
                ("Amount", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null)),
                ("Percent", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null)));

        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.PayProfilesRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollPayProfiles'.")),
                (PayProfilesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The profile's id; omit it to match by staff member and employer.")),
                            ("StaffMemberId", Text("The staff member paid.")),
                            ("EmployeeNumber", Text("The staff member's employee number, instead of the id.")),
                            ("EmployerId", Text("The employer.")),
                            ("EmployerName", Text("The employer's name, instead of the id.")),
                            ("PayScheduleId", Text("The pay schedule.")),
                            ("PayScheduleName", Text("The pay schedule's name, instead of the id.")),
                            ("WorkerType", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Employee", "Contractor")),
                            ("OvertimeStatus", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("NonExempt", "Exempt")),
                            ("Tipped", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the person is paid a tipped wage, made up to the minimum wage when tips fall short.")),
                            ("Status", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Active", "OnLeave", "Terminated")),
                            ("TaxId", Text("The person's tax id in clear, protected with this site's keys on import. Only in a file exported with sensitive data.")),
                            ("LegalName", Text("The legal name, when it differs.")),
                            ("HomeAddress", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("The home address.")),
                            ("WorkSiteTeamId", Text("The work site team.")),
                            ("Elections", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(election).Description("The tax elections, kept as history.")),
                            ("Distribution", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(row).Description("How net pay is split; the last row takes the remainder.")),
                            ("DefaultAllocation", new JsonSchemaBuilder().Type(SchemaValueType.Object | SchemaValueType.Null).Description("DepartmentTeamId, SiteTeamId, ActivityCode, Job.")),
                            ("RetroPayFrom", Text("The first day retro pay may reach, yyyy-MM-dd.")))
                        .AdditionalProperties(true))
                    .Description("The pay profiles to create or update.")))
            .Required("name", PayProfilesRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}

/// <summary>
/// Describes the "PayrollPayItems" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PayItemsRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.PayItemsRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.PayItemsRecipeStepName).Description("Recipe step discriminator. Must be 'PayrollPayItems'.")),
                (PayItemsRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The item's id.")),
                            ("ProfileId", Text("The pay profile.")),
                            ("PayCodeId", Text("The pay code.")),
                            ("PayCode", Text("The pay code's code, instead of the id.")),
                            ("Amount", Number("The amount each period, or null for a percent.")),
                            ("Percent", Number("The percent of gross pay, or null for an amount.")),
                            ("StartDate", Text("The first day, yyyy-MM-dd.")),
                            ("EndDate", Text("The last day, yyyy-MM-dd, or null.")),
                            ("EmployerMatchPercent", Number("The percent the employer matches.")),
                            ("EmployerMatchLimitPercent", Number("The most the employer matches, as a percent of gross pay.")),
                            ("Goal", Number("The total to reach.")),
                            ("PaidTowardGoal", Number("How much was paid toward the goal.")),
                            ("PeriodMaximum", Number("The most taken in a period.")),
                            ("YearMaximum", Number("The most taken in a year.")),
                            ("ArrearsBalance", Number("What is owed.")),
                            ("Note", Text("A note.")))
                        .Required("ProfileId", "StartDate")
                        .AdditionalProperties(true))
                    .Description("The pay items to create or update.")))
            .Required("name", PayItemsRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Number(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description(description);
}
