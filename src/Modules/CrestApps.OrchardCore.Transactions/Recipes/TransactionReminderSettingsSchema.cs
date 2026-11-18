using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Transactions.Core;
using Json.Schema;

namespace CrestApps.OrchardCore.Transactions.Recipes;

/// <summary>
/// Describes the <c>TransactionReminderSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class TransactionReminderSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(TransactionReminderSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The site settings that control how outstanding-payment reminders are sent for the tenant. Reminders are delivered through the notification system so each user receives them on their preferred channel.")
            .Properties(
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the scheduled reminder sweep is enabled. Defaults to true.")),
                ("FirstReminderDelayDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The number of days to wait after a transaction becomes due before the first reminder is sent. Defaults to 0 (remind as soon as it is due).")),
                ("ReminderIntervalDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(7).Description("The number of days to wait between reminders. Defaults to 7.")),
                ("MaxReminders", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(3).Description("The maximum number of reminders to send for a single transaction. Defaults to 3. A value of 0 or less means there is no limit.")),
                ("UpcomingReminderDays", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(3).Description("How many days before a payment falls due the owner is told it is coming due. One such reminder is sent per transaction. Defaults to 3. A value of 0 or less turns these reminders off.")),
                ("Steps", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("The staged reminder policy: the days, relative to the due date, a reminder is sent on, such as [-3, 0, 7, 14, 30]. When set, it replaces the first delay, interval, maximum and upcoming reminder.")),
                ("SendFromHour", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(8).Description("The first hour of the day (0 to 23, in the site's time zone) reminders are sent. Defaults to 8.")),
                ("SendUntilHour", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(20).Description("The hour of the day (1 to 24, in the site's time zone) reminders stop being sent. Defaults to 20.")),
                ("ChargeRetryDays", new JsonSchemaBuilder().Type(SchemaValueType.Array).Items(new JsonSchemaBuilder().Type(SchemaValueType.Integer)).Description("How many days after a declined charge of a saved card each retry is made, in order. Defaults to [1, 3, 5].")))
            .AdditionalProperties(true);
}
