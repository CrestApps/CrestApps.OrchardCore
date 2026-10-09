using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Contents.Models;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using YesSql;

namespace CrestApps.OrchardCore.Reports.Contents.Services;

/// <summary>
/// Exposes the content types of the tenant as report data sets. Each data set reads the published content items of
/// one content type, with their metadata, part data, and field values. A content type is listed, described, and read
/// only for a principal allowed to view its content items (<see cref="CommonPermissions.ViewContent"/>, including the
/// type-specific permission of a securable type).
/// </summary>
public sealed class ContentsReportDataSource : IReportDataSource
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ContentReportSchemaBuilder _schemaBuilder;
    private readonly ISession _session;
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentsReportDataSource"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="schemaBuilder">The builder of content type report fields.</param>
    /// <param name="session">The session content items are read from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContentsReportDataSource(
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService,
        ContentReportSchemaBuilder schemaBuilder,
        ISession session,
        IStringLocalizer<ContentsReportDataSource> stringLocalizer)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
        _schemaBuilder = schemaBuilder;
        _session = session;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Name => ReportsConstants.ContentsDataSource;

    /// <inheritdoc/>
    public LocalizedString DisplayName => S["Content items"];

    /// <inheritdoc/>
    public LocalizedString Description => S["The published content items of each content type, with their parts and fields."];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ReportDataSetDescriptor>> GetDataSetsAsync(ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        var user = context?.User;

        if (user is null)
        {
            return [];
        }

        var dataSets = new List<ReportDataSetDescriptor>();
        var definitions = (await _contentDefinitionManager.ListTypeDefinitionsAsync()).ToList();

        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (definition is not null && IsReportable(definition) && await CanViewAsync(user, definition.Name))
            {
                var descriptor = Describe(definition);

                foreach (var reference in BuildFields(definition, definitions)
                    .SelectMany(field => field.Descriptor.References)
                    .DistinctBy(reference => (reference.Source, reference.DataSet)))
                {
                    descriptor.References.Add(reference);
                }

                dataSets.Add(descriptor);
            }
        }

        return dataSets
            .OrderBy(dataSet => dataSet.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(dataSet => dataSet.Name, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<ReportDataSetSchema> GetSchemaAsync(string dataSet, ReportDataSourceContext context, CancellationToken cancellationToken = default)
    {
        var definition = await GetReadableDefinitionAsync(dataSet, context?.User, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        return new ReportDataSetSchema
        {
            DataSet = Describe(definition),
            Fields = (await BuildFieldsAsync(definition))
                .Select(field => field.Descriptor)
                .ToList(),
        };
    }

    /// <inheritdoc/>
    public async Task<ReportDataTable> QueryAsync(ReportDataSourceQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var user = query.Context?.User;
        var definition = await GetReadableDefinitionAsync(query.DataSet, user, cancellationToken);

        if (definition is null)
        {
            return new ReportDataTable();
        }

        var fields = SelectFields(await BuildFieldsAsync(definition), query.Fields);
        var maxRows = Math.Max(1, query.MaxRows);
        var take = maxRows == int.MaxValue
            ? maxRows
            : maxRows + 1;
        var contentType = definition.Name;

        var itemsQuery = _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == contentType && index.Published);

        foreach (var predicate in ContentReportConditionTranslator.Translate(query.Conditions))
        {
            itemsQuery = itemsQuery.Where(predicate);
        }

        // Newest first, so a report that hits the row limit shows the latest items.
        var contentItems = (await itemsQuery
            .OrderByDescending(index => index.DocumentId)
            .Take(take)
            .ListAsync(cancellationToken))
            .ToList();

        var truncated = contentItems.Count > maxRows;

        if (truncated)
        {
            contentItems.RemoveRange(maxRows, contentItems.Count - maxRows);
        }

        var queryContext = new ContentReportQueryContext(
            contentType,
            user,
            contentItems,
            _session,
            type => CanViewAsync(user, type));

        foreach (var field in fields)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await field.PrepareAsync(queryContext, cancellationToken);
        }

        var table = new ReportDataTable
        {
            Fields = fields.Select(field => field.Descriptor).ToList(),
            Truncated = truncated,
        };

        foreach (var contentItem in contentItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = new object[fields.Count];

            for (var index = 0; index < fields.Count; index++)
            {
                row[index] = ToReportValue(fields[index].GetValue(contentItem, queryContext), fields[index].Descriptor.DataType);
            }

            table.Rows.Add(row);
        }

        return table;
    }

    /// <summary>
    /// Converts a raw value to the CLR type of a report data type. Date-time values are returned in UTC; date values
    /// carry no time zone.
    /// </summary>
    /// <param name="value">The raw value.</param>
    /// <param name="dataType">The data type of the report field.</param>
    /// <returns>The converted value, or <see langword="null"/>.</returns>
    public static object ToReportValue(object value, ReportDataType dataType)
    {
        var converted = ReportDataValues.Coerce(value, dataType);

        if (converted is not DateTime date)
        {
            return converted;
        }

        if (dataType == ReportDataType.Date)
        {
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        }

        return date.Kind switch
        {
            DateTimeKind.Local => date.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(date, DateTimeKind.Utc),
            _ => date,
        };
    }

    private static List<ContentReportField> SelectFields(IReadOnlyList<ContentReportField> fields, ISet<string> requested)
    {
        if (requested is null || requested.Count == 0)
        {
            return [.. fields];
        }

        return fields
            .Where(field => requested.Contains(field.Descriptor.Name))
            .ToList();
    }

    private async Task<ContentTypeDefinition> GetReadableDefinitionAsync(string dataSet, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (user is null || string.IsNullOrEmpty(dataSet))
        {
            return null;
        }

        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(dataSet);

        if (definition is null ||
            !string.Equals(definition.Name, dataSet, StringComparison.Ordinal) ||
            !IsReportable(definition) ||
            !await CanViewAsync(user, definition.Name))
        {
            return null;
        }

        return definition;
    }

    /// <summary>
    /// Determines whether a content type is offered as a data set. Widgets are parts of pages, not records, so they are
    /// left out.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <returns><see langword="true"/> when the type is offered.</returns>
    public static bool IsReportable(ContentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return !string.Equals(definition.GetStereotype(), "Widget", StringComparison.OrdinalIgnoreCase);
    }

    // The fields of a content type, plus the container of a type that lists hold: a ListPart that contains the type
    // makes Orchard Core add a ContainedPart to its items at run time, so the type definition alone does not show it.
    private async Task<IReadOnlyList<ContentReportField>> BuildFieldsAsync(ContentTypeDefinition definition)
    {
        return BuildFields(definition, (await _contentDefinitionManager.ListTypeDefinitionsAsync()).ToList());
    }

    private List<ContentReportField> BuildFields(ContentTypeDefinition definition, IReadOnlyList<ContentTypeDefinition> definitions)
    {
        var fields = _schemaBuilder.Build(definition).ToList();
        var containers = new List<string>();

        foreach (var candidate in definitions)
        {
            foreach (var part in candidate?.Parts ?? [])
            {
                if (part?.PartDefinition?.Name == "ListPart" &&
                    ContentReportJson.ReadSetting(part.Settings, "ListPartSettings", "ContainedContentTypes").Contains(definition.Name, StringComparer.Ordinal))
                {
                    containers.Add(candidate.Name);
                }
            }
        }

        if (containers.Count == 0)
        {
            return fields;
        }

        var name = ContentReportFieldNames.ContainedListContentItemId;
        var field = fields.FirstOrDefault(candidate => string.Equals(candidate.Descriptor.Name, name, StringComparison.Ordinal));

        if (field is null)
        {
            field = new ContentElementReportField(
                new ReportFieldDescriptor(name, S["Container ID"], ReportDataType.Text, S["Container"])
                {
                    IsIdentifier = true,
                },
                "ContainedPart",
                null,
                ContentReportValueMode.Single,
                "ListContentItemId");
            fields.Add(field);
        }

        foreach (var container in containers.Distinct(StringComparer.Ordinal))
        {
            field.Descriptor.References.Add(new ReportFieldReference(ReportsConstants.ContentsDataSource, container, ContentReportFieldNames.ContentItemId));
        }

        return fields;
    }

    private async Task<bool> CanViewAsync(ClaimsPrincipal user, string contentType)
    {
        if (user is null || string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        var resource = new ContentItem
        {
            ContentType = contentType,
        };

        return await _authorizationService.AuthorizeAsync(user, CommonPermissions.ViewContent, resource);
    }

    private ReportDataSetDescriptor Describe(ContentTypeDefinition definition)
    {
        var stereotype = definition.GetStereotype();
        var displayName = string.IsNullOrEmpty(definition.DisplayName)
            ? definition.Name
            : definition.DisplayName;
        var group = string.IsNullOrEmpty(stereotype)
            ? S["Content"].Value
            : stereotype;

        return new ReportDataSetDescriptor(definition.Name, displayName, definition.GetDescription())
        {
            Group = group,
            DefaultDateField = ContentReportFieldNames.CreatedUtc,
        };
    }
}
