using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using Json.Schema;

namespace CrestApps.OrchardCore.Calendars.Recipes;

/// <summary>
/// Provides recipe schema support for the <see cref="CalendarWidgetPart"/> part of the Calendar dashboard widget.
/// </summary>
public sealed class CalendarWidgetPartSchemaDefinition : PartSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name => CalendarWidgetPart.PartName;

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    /// <inheritdoc />
    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(CalendarWidgetPart.Source), new JsonSchemaBuilder()
                    .Description("Which calendars the widget shows: 0 = the viewer's own calendars (the default), 1 = every calendar of CalendarType, 2 = the calendars in CalendarIds. Each viewer sees only the calendars they may open.")
                    .Enum(0, 1, 2, "MyCalendars", "Type", "Calendars")),
                (nameof(CalendarWidgetPart.CalendarType), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("The technical name of the calendar type whose calendars the widget shows, such as General, Personal, Team, Room or Office. Used when Source is Type.")),
                (nameof(CalendarWidgetPart.CalendarIds), new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))
                    .Description("The identifiers of the calendars the widget shows. Used when Source is Calendars.")),
                (nameof(CalendarWidgetPart.View), new JsonSchemaBuilder()
                    .Description("The view the widget opens on: 0 = agenda (the default), 1 = day, 2 = week, 3 = month.")
                    .Enum(0, 1, 2, 3, "Agenda", "Day", "Week", "Month")))
            .AdditionalProperties(true);
}
