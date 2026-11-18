using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Reports.Designer;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.AI;

/// <summary>
/// Lists the data sources and the data sets the caller may read, as the Report Builder's designer offers them: it needs
/// the permission that designs reports, and each data source is asked with the caller, so it lists only what the caller
/// may read.
/// </summary>
public sealed class ListReportDataSetsTool : ReportsAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listReportDataSets";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "source": {
          "type": "string",
          "description": "Only list the data sets of this data source, by its name, such as Contents, Users or ReportViews."
        },
        "query": {
          "type": "string",
          "description": "Only list the data sets whose name, display name, description or group contains this text."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of data sets per page, 20 by default and at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the data sources and the data sets the user may read (orders, customers, content types, users, saved views, connected data sets and so on), optionally for one data source or matching a text. Use it to find where the data for a question lives.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (!await IsAuthorizedAsync(services, user, ReportDesignerPermissions.ManageOwnReportDesigns))
        {
            return NotAllowedMessage;
        }

        var sourceName = ReadString(arguments, "source");
        var query = ReadString(arguments, "query");
        var (page, pageSize) = ReadPaging(arguments);
        var dataSourceManager = services.GetRequiredService<IDataSourceManager>();
        var localizerFactory = services.GetRequiredService<IStringLocalizerFactory>();
        var sources = dataSourceManager.GetDataSources();

        if (sourceName is not null)
        {
            sources = sources.Where(source => string.Equals(source.Name, sourceName, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (sources.Count == 0)
            {
                return $"There is no data source '{sourceName}'. Data sources: {string.Join(", ", dataSourceManager.GetDataSources().Select(source => source.Name))}.";
            }
        }

        var entries = new List<object>();
        var sourceList = new List<object>();
        var context = new DataSourceContext { User = user };

        foreach (var source in sources)
        {
            var dataSource = dataSourceManager.GetDataSource(source.Name);

            if (dataSource is null)
            {
                continue;
            }

            var sourceDisplayName = source.DisplayName is null ? source.Name : localizerFactory.Localize(source.DisplayName).Value;

            sourceList.Add(new
            {
                name = source.Name,
                displayName = sourceDisplayName,
                description = source.Description is null ? null : localizerFactory.Localize(source.Description).Value,
            });

            foreach (var dataSet in await dataSource.GetDataSetsAsync(context, cancellationToken) ?? [])
            {
                if (dataSet is null ||
                    (query is not null &&
                    !Contains(dataSet.Name, query) &&
                    !Contains(dataSet.DisplayName, query) &&
                    !Contains(dataSet.Description, query) &&
                    !Contains(dataSet.Group, query)))
                {
                    continue;
                }

                entries.Add(new
                {
                    source = source.Name,
                    sourceDisplayName,
                    name = dataSet.Name,
                    displayName = dataSet.DisplayName,
                    description = dataSet.Description,
                    group = dataSet.Group,
                    defaultDateField = dataSet.DefaultDateField,
                });
            }
        }

        return Json(new
        {
            sources = sourceList,
            items = entries.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
            total = entries.Count,
            page,
            pageSize,
            truncated = page * pageSize < entries.Count ? true : (bool?)null,
        });
    }

    private static bool Contains(string text, string query)
        => text?.Contains(query, StringComparison.CurrentCultureIgnoreCase) == true;
}
