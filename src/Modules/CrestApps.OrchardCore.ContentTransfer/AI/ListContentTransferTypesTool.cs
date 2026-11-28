using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.ContentTransfer.AI;

/// <summary>
/// Lists the content types the caller may bulk import or export, as the Import and Export pages offer them, and the
/// columns of one content type's import and export files, as the Import page and the export file show them.
/// </summary>
public sealed class ListContentTransferTypesTool : ContentTransferAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listContentTransferTypes";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "contentType": {
          "type": "string",
          "description": "The technical name of one content type whose import and export columns to return. Leave it out to list the content types only."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the content types the user may bulk import or bulk export, the file formats enabled, and, for one content type, the column names an import file takes and an export file contains. Use it before an import or export to know the columns.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var definitions = await services.GetRequiredService<IContentDefinitionManager>().ListTypeDefinitionsAsync();
        var types = new List<(string Name, string DisplayName, bool CanImport, bool CanExport)>();

        foreach (var definition in definitions)
        {
            var settings = definition.GetSettings<ContentTypeTransferSettings>();
            var canImport = settings.AllowBulkImport && await IsAuthorizedAsync(services, user, ContentTransferPermissions.ImportContentFromFile, definition.Name);
            var canExport = settings.AllowBulkExport && await IsAuthorizedAsync(services, user, ContentTransferPermissions.ExportContentFromFile, definition.Name);

            if (canImport || canExport)
            {
                types.Add((definition.Name, definition.DisplayName, canImport, canExport));
            }
        }

        // The Import and Export pages offer nothing to a user who may transfer no content type.
        if (types.Count == 0)
        {
            return NotAllowedMessage;
        }

        var formats = services.GetServices<IContentTransferFileFormatProvider>()
            .Select(provider => provider.FileExtension)
            .ToArray();

        var contentType = ReadString(arguments, "contentType");
        object columns = null;

        if (contentType is not null)
        {
            var match = types.FirstOrDefault(type => string.Equals(type.Name, contentType, StringComparison.OrdinalIgnoreCase));

            if (match.Name is null)
            {
                return $"The user can't bulk import or export the content type '{contentType}', or it doesn't exist or doesn't allow it. The content types the user can transfer are: {string.Join(", ", types.Select(type => type.Name))}.";
            }

            var definition = definitions.First(definition => definition.Name == match.Name);
            var all = await services.GetRequiredService<IContentImportManager>().GetColumnsAsync(new ImportContentContext
            {
                ContentItem = await services.GetRequiredService<IContentManager>().NewAsync(definition.Name),
                ContentTypeDefinition = definition,
            });

            columns = new
            {
                contentType = definition.Name,
                import = match.CanImport ? Describe(all.Where(column => column.Type != ImportColumnType.ExportOnly)) : null,
                export = match.CanExport ? Describe(all.Where(column => column.Type != ImportColumnType.ImportOnly)) : null,
            };
        }

        return Json(new
        {
            contentTypes = types
                .OrderBy(type => type.DisplayName ?? type.Name, StringComparer.OrdinalIgnoreCase)
                .Select(type => new
                {
                    name = type.Name,
                    displayName = type.DisplayName,
                    canImport = type.CanImport,
                    canExport = type.CanExport,
                })
                .ToArray(),
            formats,
            columns,
        });
    }

    private static object[] Describe(IEnumerable<ImportColumn> columns)
        => columns
            .Select(column => (object)new
            {
                name = column.Name,
                description = column.Description,
                isRequired = column.IsRequired,
                additionalNames = column.AdditionalNames is { Length: > 0 } names ? names : null,
                validValues = column.ValidValues is { Length: > 0 } values ? values : null,
            })
            .ToArray();
}
