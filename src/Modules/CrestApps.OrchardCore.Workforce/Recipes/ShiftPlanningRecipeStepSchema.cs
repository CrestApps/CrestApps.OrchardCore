using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Workforce.Models;
using Json.Schema;
using static CrestApps.OrchardCore.Workforce.Recipes.WorkforceSchemaBuilder;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// Describes the WorkforceShiftPlanning recipe step to CrestApps Recipes.
/// </summary>
internal sealed class ShiftPlanningRecipeStepSchema : IRecipeStep
{
    private JsonSchema _cached;

    public string Name => ShiftPlanningRecipeStep.ShiftPlanningStepName;

    public ValueTask<JsonSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
    {
        _cached ??= Step(
            ShiftPlanningRecipeStep.ShiftPlanningStepName,
            "Creates or updates shift templates and rotations, matched by itemId then name, and coverage requirements, matched by itemId then by team and name. A template or rotation that is updated takes every member of the entry, so a member left out goes back to its default. Teams, activities, templates and skills named here must exist.",
            ("Templates", Array(Template(), "The shift templates to create or update.")),
            ("Patterns", Array(Pattern(), "The rotations to create or update. Templates are imported first, so a rotation can name a template of the same step.")),
            ("CoverageRequirements", Array(Coverage(), "The coverage requirements to create or update.")));

        return ValueTask.FromResult(_cached);
    }

    private static JsonSchemaBuilder Template()
    {
        var segment = Object(
            "A part of the shift spent on another activity, such as a break.",
            ("activityName", Text("The activity code, by name. Wins over activityCodeId.")),
            ("activityCodeId", Text("The activity code, by id.")),
            ("offsetMinutes", Integer("Minutes from the start of the shift.")),
            ("lengthMinutes", Integer("Length in minutes.")));

        return Object(
            "A shift template.",
            ("itemId", ItemId("template")),
            ("name", Text("The template name, unique.")),
            ("color", Text("The color, such as #2b8a3e.")),
            ("start", Time("When the shift starts.")),
            ("lengthMinutes", Integer("The shift length in minutes. Defaults to 480.")),
            ("segments", Array(segment, "The breaks and other activities within the shift.")),
            ("teamNames", Strings("The teams that use the template, by name; added to teamIds.")),
            ("teamIds", Strings("The teams that use the template, by id; empty for every team.")),
            ("requiredSkillIds", Strings("The ids of the skills a worker needs for the shift.")),
            ("enabled", Flag("Whether the template can be used. Defaults to true.")))
        .Required("name");
    }

    private static JsonSchemaBuilder Pattern()
    {
        var day = Object(
            "A day of the rotation that has a shift.",
            ("week", Integer("The week of the rotation, from 1.")),
            ("day", Enum<DayOfWeek>("The day of the week.")),
            ("templateName", Text("The shift template, by name. Wins over templateId.")),
            ("templateId", Text("The shift template, by id.")));

        var worker = Object(
            "A worker on the rotation.",
            ("workerId", Text("The worker's id.")),
            ("startDate", Date("The day the worker starts week 1 of the rotation.")));

        return Object(
            "A rotation: a repeating pattern of shifts.",
            ("itemId", ItemId("rotation")),
            ("name", Text("The rotation name, unique.")),
            ("teamName", Text("The team, by name. Wins over teamId.")),
            ("teamId", Text("The team, by id.")),
            ("weekCount", Integer("How many weeks the rotation lasts before it repeats. Defaults to 1.")),
            ("days", Array(day, "The days with a shift.")),
            ("workers", Array(worker, "The workers on the rotation.")),
            ("enabled", Flag("Whether the rotation is in use. Defaults to true.")))
        .Required("name");
    }

    private static JsonSchemaBuilder Coverage()
    {
        var row = Object(
            "How many workers are needed in a window.",
            ("day", OptionalEnum<DayOfWeek>("The day of the week it applies to; null when date is set.")),
            ("date", OptionalDate("The date it applies to, for a one-off; null for every week.")),
            ("start", Time("When the window starts.")),
            ("end", Time("When the window ends.")),
            ("minimum", Integer("The fewest workers needed.")),
            ("target", Integer("The workers wanted.")),
            ("maximum", OptionalInteger("The most workers wanted; null for no limit.")));

        return Object(
            "A coverage requirement: the staffing a team needs.",
            ("itemId", Text("The requirement's id. When it matches a stored requirement, that requirement is updated; otherwise it is matched by team and name.")),
            ("name", Text("The requirement name, unique within the team.")),
            ("teamName", Text("The team, by name. Wins over teamId.")),
            ("teamId", Text("The team, by id.")),
            ("skillName", Text("The skill the workers need, by name. Wins over skillId.")),
            ("skillId", Text("The skill the workers need, by id; null for any worker.")),
            ("source", Enum<CoverageSource>("Where the numbers come from. Defaults to Manual.")),
            ("rows", Array(row, "The staffing windows.")),
            ("effectiveFrom", OptionalDate("The first day it applies; null for always.")),
            ("effectiveTo", OptionalDate("The last day it applies; null for open-ended.")))
        .Required("name");
    }
}
