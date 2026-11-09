using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.WorkOrders.Models;
using Json.Schema;
using static CrestApps.OrchardCore.WorkOrders.Recipes.WorkOrdersSchemaBuilder;

namespace CrestApps.OrchardCore.WorkOrders.Recipes;

/// <summary>
/// Describes the WorkOrders recipe step to CrestApps Recipes.
/// </summary>
internal sealed class WorkOrdersRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => WorkOrdersRecipeStep.RecipeName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= Step(
            WorkOrdersRecipeStep.RecipeName,
            "Creates or updates work order checklists, service levels, sub-statuses and types, and service agreements when the agreements feature is on. Records are matched by ItemId only; an existing record takes only the members given. The sections are imported in this order, so a type can point at a checklist or service level of the same step by ItemId.",
            ("Checklists", Array(Checklist(), "The checklist templates.")),
            ("ServiceLevels", Array(ServiceLevel(), "The service level policies.")),
            ("SubStatuses", Array(SubStatus(), "The sub-statuses.")),
            ("Types", Array(TypeEntry(), "The work order types.")),
            ("Agreements", Array(Agreement(), "The service agreements. Read only when the service agreements feature is on; their customers, accounts and assets must be on the site too.")));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Checklist()
    {
        var question = Object(
            "A question on the checklist.",
            ("Id", Text("The question's id within the checklist, which ShowWhen refers to.")),
            ("Section", Text("The section the question is in.")),
            ("Text", Text("The question.")),
            ("Kind", Enum<ChecklistQuestionKind>("The kind of answer.")),
            ("Required", Flag("Whether it must be answered.")),
            ("Help", Text("Help text.")),
            ("Min", OptionalNumber("The lowest number accepted.")),
            ("Max", OptionalNumber("The highest number accepted.")),
            ("Unit", Text("The unit of a number.")),
            ("Choices", Strings("The choices of a Choice or MultiChoice question.")),
            ("MinPhotos", Integer("How many photos a Photo question needs.")),
            ("Meter", Text("The asset meter a MeterReading question records.")),
            ("ShowWhen", OptionalObject(
                "Shows the question only after another was answered a certain way; null for always.",
                ("QuestionId", Text("The id of the question it depends on.")),
                ("EqualsValue", Text("The answer that shows it.")))));

        return Object(
            "A checklist to fill in on a job, such as an inspection.",
            ("ItemId", ItemId("checklist")),
            ("Name", Text("The checklist name.")),
            ("Questions", Array(question, "The questions, in order.")));
    }

    private static JsonSchemaBuilder ServiceLevel()
    {
        var target = Object(
            "The targets of one priority.",
            ("Priority", Enum<WorkOrderPriority>("The priority.")),
            ("ResponseMinutes", Integer("The minutes to the first response; 0 for none.")),
            ("ResolveMinutes", Integer("The minutes to resolution; 0 for none.")));

        return Object(
            "A service level policy: how fast work orders must be answered and resolved, by priority.",
            ("ItemId", ItemId("service level policy")),
            ("Name", Text("The policy name.")),
            ("Targets", Array(target, "The targets, by priority.")),
            ("HoursCalendarId", Text("The business hours calendar the clock runs in; null for around the clock or WeeklyHours.")),
            ("WeeklyHours", OptionalObject(
                "The same opening hours every week the clock runs in; null for none.",
                ("Days", EnumArray<DayOfWeek>("The days open.")),
                ("Opens", Time("When they open. Defaults to 09:00.")),
                ("Closes", Time("When they close. Defaults to 17:00.")))),
            ("TimeZoneId", Text("The IANA time zone of the business hours.")),
            ("PauseStatuses", EnumArray<WorkOrderStatus>("The statuses the clock stops in.")),
            ("WarningPercent", Integer("How far through a target, in percent, the warning is raised. Defaults to 80.")));
    }

    private static JsonSchemaBuilder SubStatus()
        => Object(
            "A finer status inside one, such as Waiting for parts inside On hold.",
            ("ItemId", ItemId("sub-status")),
            ("Name", Text("The sub-status name.")),
            ("ParentStatus", Enum<WorkOrderStatus>("The status it belongs to.")),
            ("Color", Text("The color, such as #2b8a3e.")),
            ("PausesSla", Flag("Whether the service level clock stops in it.")));

    private static JsonSchemaBuilder TypeEntry()
    {
        var skill = Object(
            "A skill the job needs.",
            ("SkillId", Text("The skill's id.")),
            ("MinProficiency", Integer("The lowest proficiency, from 1. Defaults to 1.")));

        var line = Object(
            "A line a new work order of the type comes with.",
            ("Id", Text("The line's id.")),
            ("Kind", Enum<WorkOrderLineKind>("What the line is.")),
            ("ProductId", Text("The product's content item id, for a product or service line.")),
            ("StockItemId", Text("The stock item's id, for a product taken from stock.")),
            ("Description", Text("The line's description.")),
            ("QuantityEstimated", Number("The quantity expected.")),
            ("QuantityUsed", Number("The quantity used.")),
            ("UnitPrice", Number("The price of one.")),
            ("LocationId", Text("The stock location the product comes from.")),
            ("Billable", Flag("Whether the line is billed. Defaults to true.")));

        return Object(
            "A kind of job, with the defaults a new work order of it takes.",
            ("ItemId", ItemId("work order type")),
            ("Name", Text("The type name.")),
            ("Color", Text("The color, such as #2b8a3e.")),
            ("DefaultDurationMinutes", Integer("How long the job usually takes, in minutes. Defaults to 60.")),
            ("RequiredSkills", Array(skill, "The skills it needs.")),
            ("RequiredCertificationIds", Strings("The ids of the certifications it needs.")),
            ("CrewSize", Integer("How many people it needs. Defaults to 1.")),
            ("RequiredEquipmentTypeIds", Strings("The ids of the asset types of equipment it needs.")),
            ("ChecklistTemplateIds", Strings("The ids of the checklists it comes with.")),
            ("DefaultLines", Array(line, "The lines it comes with.")),
            ("ServiceLevelPolicyId", Text("The id of its service level policy.")),
            ("BillingMode", Enum<WorkOrderBillingMode>("How it is billed.")),
            ("Currency", Text("The ISO currency code its prices are in. Defaults to USD.")),
            ("FixedPrice", OptionalNumber("The fixed price, when billed at one.")),
            ("DefaultPriority", Enum<WorkOrderPriority>("The priority of a new work order. Defaults to Normal.")),
            ("RequiresSignature", Flag("Whether it can't be completed without the customer's signature.")),
            ("AppointmentTypeIds", Strings("The ids of the Scheduling appointment types whose bookings make a work order of this type.")),
            ("RequiredPhotos", Integer("How many photos it can't be completed without.")));
    }

    private static JsonSchemaBuilder Agreement()
    {
        var schedule = Object(
            "A recurring visit, such as a boiler service every six months.",
            ("Id", Text("The schedule's id, part of each work order's source.")),
            ("Name", Text("The schedule name, such as Annual inspection.")),
            ("TypeId", Text("The id of the type of the work orders it makes.")),
            ("Rule", Text("When it repeats, as an RFC 5545 rule such as FREQ=MONTHLY;INTERVAL=6, from the agreement's start date.")),
            ("LeadDays", Integer("How many days ahead of a visit its work order is made. Defaults to 14.")),
            ("AssetIds", Strings("The assets it covers; empty for every asset of the agreement.")));

        return Object(
            "A service agreement: recurring visits, such as preventive maintenance, for a customer over a period.",
            ("ItemId", ItemId("service agreement")),
            ("Number", Text("The number, such as SA-00001.")),
            ("Name", Text("The agreement name.")),
            ("Status", Enum<ServiceAgreementStatus>("Where the agreement is.")),
            ("CustomerId", Text("The customer: the Orchard Core user id.")),
            ("AccountId", Text("The CRM account (the company) the agreement is with.")),
            ("Contact", OptionalObject(
                "The person to contact.",
                ("Name", Text("The contact's name.")),
                ("Phone", Text("The contact's phone number.")),
                ("Email", Text("The contact's email address.")))),
            ("ServiceAddress", Address("Where the visits are.")),
            ("AccessNotes", Text("How to get in.")),
            ("AssetIds", Strings("The ids of the assets it covers.")),
            ("StartDate", Date("The first day it is in force, where the schedules start.")),
            ("EndDate", OptionalDate("The last day it is in force; null when it doesn't end.")),
            ("TimeZoneId", Text("The IANA time zone its days are in.")),
            ("Schedules", Array(schedule, "The recurring visits.")),
            ("Pricing", Enum<AgreementPricing>("How the visits are paid for.")),
            ("PricePerVisit", Number("The price of a visit, when each is invoiced.")),
            ("ExpiringNoticeDays", Integer("How many days before the end date the expiring event starts. Defaults to 30.")),
            ("ExpiringNotifiedUtc", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Format(Formats.DateTime).Description("When the expiring event started, so it starts once.")),
            ("Notes", Text("Notes.")));
    }
}
