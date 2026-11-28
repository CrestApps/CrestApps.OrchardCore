using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Designer.ViewModels;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// The JSON schema of a Report Builder query, as the designer saves it, and the reading of one from a tool argument.
/// </summary>
internal static class ReportQuerySchema
{
    /// <summary>
    /// The schema of a query, for a property of a tool's schema.
    /// </summary>
    public const string Query =
    """
    {
          "type": "object",
          "description": "The query, as the Report Builder saves it. Refer to a field as alias.FieldName.",
          "properties": {
            "dataSets": {
              "type": "array",
              "description": "The data sets the query reads; the first one is the main one.",
              "items": {
                "type": "object",
                "properties": {
                  "alias": { "type": "string", "description": "A short name for the data set, used in field names, such as o." },
                  "source": { "type": "string", "description": "The data source's name, from listReportDataSets." },
                  "dataSet": { "type": "string", "description": "The data set's name, from listReportDataSets." }
                },
                "required": ["alias", "source", "dataSet"],
                "additionalProperties": false
              }
            },
            "joins": {
              "type": "array",
              "description": "How each data set after the first is joined.",
              "items": {
                "type": "object",
                "properties": {
                  "alias": { "type": "string", "description": "The alias of the data set being joined." },
                  "type": { "type": "string", "enum": ["Inner", "Left", "Right", "Full"], "description": "The kind of join." },
                  "conditions": {
                    "type": "array",
                    "description": "The fields that must be equal.",
                    "items": {
                      "type": "object",
                      "properties": {
                        "leftField": { "type": "string", "description": "A field of an earlier data set, as alias.FieldName." },
                        "rightField": { "type": "string", "description": "A field of the joined data set, as alias.FieldName." }
                      },
                      "required": ["leftField", "rightField"],
                      "additionalProperties": false
                    }
                  }
                },
                "required": ["alias", "conditions"],
                "additionalProperties": false
              }
            },
            "calculatedFields": {
              "type": "array",
              "description": "Fields computed with a formula, which columns, filters and sorts can use by name.",
              "items": {
                "type": "object",
                "properties": {
                  "name": { "type": "string", "description": "The name the field is used by." },
                  "label": { "type": "string", "description": "The label shown." },
                  "expression": { "type": "string", "description": "The formula, with fields as alias.FieldName and functions from getReportDataSetSchema." }
                },
                "required": ["name", "expression"],
                "additionalProperties": false
              }
            },
            "filters": {
              "type": "array",
              "description": "Conditions on the rows.",
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string", "description": "A short identifier of the filter." },
                  "field": { "type": "string", "description": "The field, as alias.FieldName, or a calculated field's name." },
                  "operator": {
                    "type": "string",
                    "enum": ["Equals", "NotEquals", "Contains", "NotContains", "StartsWith", "EndsWith", "GreaterThan", "GreaterThanOrEqual", "LessThan", "LessThanOrEqual", "Between", "In", "NotIn", "IsEmpty", "IsNotEmpty", "InLastDays", "InNextDays"],
                    "description": "How the field is compared."
                  },
                  "values": { "type": "array", "items": { "type": "string" }, "description": "The values compared with; dates as yyyy-MM-dd; Between takes two." },
                  "stage": { "type": "string", "enum": ["Rows", "Result"], "description": "Rows filters the rows read (the default); Result filters the grouped result, such as totals." },
                  "exposed": { "type": "boolean", "description": "Whether the people who run a saved report can change the filter." },
                  "label": { "type": "string", "description": "The label of an exposed filter." },
                  "control": { "type": "string", "enum": ["Auto", "Text", "Select", "MultiSelect", "DateRange", "NumberRange", "Boolean", "RelativeDate"], "description": "The control of an exposed filter." }
                },
                "required": ["field", "operator"],
                "additionalProperties": false
              }
            },
            "columns": {
              "type": "array",
              "description": "The columns of the result. Columns without an aggregate group the rows; columns with one are measures.",
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string", "description": "A short identifier of the column, used by sorts and visuals." },
                  "field": { "type": "string", "description": "The field, as alias.FieldName, or a calculated field's name." },
                  "label": { "type": "string", "description": "The column header." },
                  "aggregate": { "type": "string", "enum": ["None", "Count", "CountDistinct", "Sum", "Average", "Min", "Max", "Median"], "description": "How the rows of a group are combined." },
                  "transform": { "type": "string", "enum": ["None", "Upper", "Lower", "Trim", "Length", "Year", "Quarter", "Month", "Week", "Day", "DayOfWeek", "Hour", "MonthOfYear", "Round"], "description": "A change applied to the value first, such as grouping a date by Month." },
                  "format": { "type": "string", "description": "An optional display format, such as N2 or C." },
                  "hidden": { "type": "boolean", "description": "Whether the column is left out of what viewers see." }
                },
                "required": ["field"],
                "additionalProperties": false
              }
            },
            "sorts": {
              "type": "array",
              "description": "The order of the result.",
              "items": {
                "type": "object",
                "properties": {
                  "columnId": { "type": "string", "description": "The id of a column." },
                  "descending": { "type": "boolean", "description": "Whether to sort from the highest." }
                },
                "required": ["columnId"],
                "additionalProperties": false
              }
            },
            "limit": { "type": "integer", "minimum": 1, "description": "The most rows of the result." }
          },
          "required": ["dataSets", "columns"],
          "additionalProperties": false
        }
    """;

    /// <summary>
    /// Reads a query from a tool argument.
    /// </summary>
    /// <param name="element">The argument.</param>
    /// <param name="query">The query, normalized as the designer normalizes it.</param>
    /// <returns>The problem to tell the model, or <see langword="null"/>.</returns>
    public static string TryRead(JsonElement? element, out ReportQueryDefinition query)
    {
        query = null;

        if (element is not { ValueKind: JsonValueKind.Object } value)
        {
            return "Give the query as an object with dataSets and columns. Call getReportDataSetSchema for the field names.";
        }

        try
        {
            query = ReportDesignNormalizer.Normalize(value.Deserialize<ReportQueryDefinition>(ReportDesignerJson.Options));
        }
        catch (JsonException exception)
        {
            return $"The query could not be read: {exception.Message}";
        }

        if (query?.DataSets is not { Count: > 0 })
        {
            return "The query reads no data set. Add at least one to dataSets.";
        }

        // The designer gives every column and filter an id; a model may leave them out.
        for (var index = 0; index < query.Columns.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(query.Columns[index].Id))
            {
                query.Columns[index].Id = "c" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        for (var index = 0; index < query.Filters.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(query.Filters[index].Id))
            {
                query.Filters[index].Id = "f" + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
    }
}
