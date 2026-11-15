using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Payroll.Recipes;

/// <summary>
/// Describes the "PaySchedules" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PaySchedulesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.PaySchedulesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.PaySchedulesRecipeStepName).Description("Recipe step discriminator. Must be 'PaySchedules'.")),
                (PaySchedulesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The schedule's 26-character id; omit it to match by name or create one.")),
                            ("Name", Text("The schedule's name.")),
                            ("Frequency", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Weekly", "Biweekly", "SemiMonthly", "Monthly", "FourWeekly", "Quarterly", "Yearly").Description("How often a period starts.")),
                            ("AnchorDate", Text("A day a period starts on (yyyy-MM-dd), for weekly, every-two-weeks and every-four-weeks schedules; its month starts quarterly and yearly ones.")),
                            ("WeekStart", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday").Description("The day the overtime week starts.")),
                            ("SecondPeriodStartDay", Integer("The day the second period of a twice-a-month schedule starts on, 2 to 28.")),
                            ("PayDateRule", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("DaysAfterPeriodEnd", "FixedDays").Description("How the pay date is found.")),
                            ("PayDateOffsetDays", Integer("Days after the period ends that pay is made.")),
                            ("FirstPayDay", Integer("Day of the month the first period is paid, for fixed days; 0 is the last day.")),
                            ("SecondPayDay", Integer("Day of the month the second period is paid, for fixed days; 0 is the last day.")),
                            ("NonBusinessDayShift", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Before", "After", "None").Description("Where a pay date on a day off moves.")),
                            ("ApprovalLeadDays", Integer("Days before the pay date a run must be approved.")),
                            ("ExtraPayPeriod", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Standard", "DivideByPayDates").Description("How a salary is paid in a year with one more pay date than usual.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the schedule is in use.")))
                        .Required("Name")
                        .AdditionalProperties(true))
                    .Description("The pay schedules to create, or update by id or name.")))
            .Required("name", PaySchedulesRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Integer(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description(description);
}

/// <summary>
/// Describes the "PayCodes" recipe step to CrestApps Recipes.
/// </summary>
internal sealed class PayCodesRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => PayrollConstants.PayCodesRecipeStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("name", new JsonSchemaBuilder().Type(SchemaValueType.String).Const(PayrollConstants.PayCodesRecipeStepName).Description("Recipe step discriminator. Must be 'PayCodes'.")),
                (PayCodesRecipeStep.EntriesName, new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("ItemId", Text("The code's 26-character id; omit it to match by code or create one.")),
                            ("Code", Text("The short unique code, such as REG.")),
                            ("Name", Text("The code's name.")),
                            ("Class", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Earning", "Deduction", "EmployerContribution", "ImputedIncome", "Reimbursement", "Memo").Description("The class of the code.")),
                            ("Kind", Text("What the line pays for: Regular, Overtime, DoubleTime, Holiday, PaidTimeOff, PredictabilityPay, CallBack, ShiftDifferential, Unpaid, Weekend, Salary, Bonus, Commission, Tips, or a kind a module registers.")),
                            ("Multiplier", new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("What the hourly rate is multiplied by, 0 to 10.")),
                            ("PremiumPerHour", new JsonSchemaBuilder().Type(SchemaValueType.Number | SchemaValueType.Null).Description("A premium paid an hour on top.")),
                            ("ExportCode", Text("The code an outside payroll system knows the line by.")),
                            ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the code is in use.")),
                            ("Properties", new JsonSchemaBuilder().Type(SchemaValueType.Object).Description("Module settings, such as WorkforcePayCodeSettings (WindowStart, WindowEnd, Weekdays) for a shift differential, and PayrollPayCodeSettings (Calculation, BaseCodeIds, TaxTreatment by tax category, Supplemental, CountsInRegularRate, CountsAsHoursWorked, PeriodLimit, YearLimits with EffectiveFrom and Amount, LifetimeLimit, Priority, Arrears, Proration, InRetroPay, FormBoxes).")))
                        .Required("Code")
                        .AdditionalProperties(true))
                    .Description("The pay codes to create, or update by id or code.")))
            .Required("name", PayCodesRecipeStep.EntriesName)
            .AdditionalProperties(true)
            .Build();

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Text(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);
}
