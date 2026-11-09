using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using Json.Schema;

namespace CrestApps.OrchardCore.Reports.Widgets.Recipes;

/// <summary>
/// Builds the members the report dashboard widget parts share.
/// </summary>
internal static class ReportWidgetPartSchemas
{
    public static JsonSchemaBuilder Period(string description, bool allowNull)
        => EnumValue<ReportWidgetPeriod>($"{description} Custom uses From and To.", allowNull);

    public static JsonSchemaBuilder Show(string description, bool allowNull)
        => EnumValue<ReportWidgetContent>(description, allowNull);

    public static JsonSchemaBuilder Day(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    public static JsonSchemaBuilder CacheMinutes()
        => new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Default(30).Description("How many minutes the widget keeps what it shows before it runs the report again; 0 runs it on every visit.");

    // A content item can carry an enumeration by name or by number, so both are accepted.
    private static JsonSchemaBuilder EnumValue<TEnum>(string description, bool allowNull)
        where TEnum : struct, Enum
    {
        var names = Enum.GetNames<TEnum>();
        List<JsonNode> values = [.. names.Select(name => (JsonNode)name), .. Enumerable.Range(0, names.Length).Select(value => (JsonNode)value)];

        if (allowNull)
        {
            values.Add(null);
        }

        return new JsonSchemaBuilder()
            .Enum(values)
            .Description($"{description} Accepted values: {string.Join(", ", names)} (or their position from 0).");
    }
}

/// <summary>
/// Describes the <see cref="ReportWidgetPart"/> of the admin dashboard report widget to CrestApps Recipes.
/// </summary>
internal sealed class ReportWidgetPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => nameof(ReportWidgetPart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(ReportWidgetPart.ReportName), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The technical name of the coded report to show, when the widget type doesn't fix one.")),
                (nameof(ReportWidgetPart.Period), ReportWidgetPartSchemas.Period("The period the widget covers; null uses the widget type's default.", allowNull: true)),
                (nameof(ReportWidgetPart.From), ReportWidgetPartSchemas.Day("The first day of a custom period, in the site's time zone.")),
                (nameof(ReportWidgetPart.To), ReportWidgetPartSchemas.Day("The last day of a custom period, in the site's time zone.")),
                (nameof(ReportWidgetPart.Show), ReportWidgetPartSchemas.Show("What of the report the widget shows; null uses the widget type's default.", allowNull: true)),
                (nameof(ReportWidgetPart.MaxRows), new JsonSchemaBuilder().Type(SchemaValueType.Integer | SchemaValueType.Null).Minimum(0).Description("How many rows of each table the widget shows; null uses the widget type's default.")),
                (nameof(ReportWidgetPart.CacheMinutes), ReportWidgetPartSchemas.CacheMinutes()))
            .AdditionalProperties(true);
}

/// <summary>
/// Describes the <see cref="ReportDesignWidgetPart"/> of the admin dashboard designed report widget to CrestApps Recipes.
/// </summary>
internal sealed class ReportDesignWidgetPartSchemaDefinition : PartSchemaDefinitionBase
{
    public override string Name => nameof(ReportDesignWidgetPart);

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                (nameof(ReportDesignWidgetPart.ReportId), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The itemId of the published designed report to show. The widget shows a report or a view, never both.")),
                (nameof(ReportDesignWidgetPart.ViewId), new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("The itemId of the reusable view to show as a table, when the widget shows no report.")),
                (nameof(ReportDesignWidgetPart.Period), ReportWidgetPartSchemas.Period("The period the widget covers, set on the first date range or relative date filter the report exposes. Defaults to Last30Days.", allowNull: false)),
                (nameof(ReportDesignWidgetPart.From), ReportWidgetPartSchemas.Day("The first day of a custom period, in the site's time zone.")),
                (nameof(ReportDesignWidgetPart.To), ReportWidgetPartSchemas.Day("The last day of a custom period, in the site's time zone.")),
                (nameof(ReportDesignWidgetPart.Show), ReportWidgetPartSchemas.Show("What of the report the widget shows; a view is always a table. Defaults to Everything.", allowNull: false)),
                (nameof(ReportDesignWidgetPart.MaxRows), new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Default(10).Description("How many rows of each table the widget shows, or 0 for all of them.")),
                (nameof(ReportDesignWidgetPart.CacheMinutes), ReportWidgetPartSchemas.CacheMinutes()))
            .AdditionalProperties(true);
}
