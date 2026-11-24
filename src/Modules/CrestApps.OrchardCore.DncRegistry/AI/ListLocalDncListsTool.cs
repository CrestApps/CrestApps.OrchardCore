using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry.AI;

/// <summary>
/// Lists the local do-not-call lists with their import progress, as the Local DNC Registry page does. Rejected rows are
/// counted, not returned: they hold the phone numbers of the people on the list.
/// </summary>
internal sealed class ListLocalDncListsTool : DncToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listLocalDncLists";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "countryCode": {
          "type": "string",
          "description": "Only lists of this country, by two-letter code, for example US."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "Lists per page, 20 when not given, at most 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the local do-not-call lists with country, status, number count, processed and imported totals, rejected rows and any import error.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => DncRegistryPermissions.ManageDncRegistrySettings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(Arguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (page, pageSize) = arguments.GetPaging();
        var countryCode = arguments.GetString("countryCode")?.ToUpperInvariant();
        var manager = services.GetRequiredService<ILocalDncListManager>();
        var total = await manager.GetCountAsync(countryCode, cancellationToken);
        var lists = await manager.GetListsAsync(page, pageSize, countryCode, cancellationToken);
        var localClock = services.GetService<ILocalClock>();
        var items = new List<object>();

        foreach (var list in lists)
        {
            items.Add(await DescribeAsync(list, localClock));
        }

        return Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = page * pageSize < total,
        });
    }

    /// <summary>
    /// Describes a list without its rejected rows or its stored file.
    /// </summary>
    internal static async Task<object> DescribeAsync(LocalDncList list, ILocalClock localClock)
        => new
        {
            id = list.ListId,
            name = list.Name,
            countryCode = list.CountryCode,
            status = list.Status.ToString(),
            phoneNumberCount = list.PhoneNumberCount,
            totalRecords = list.TotalRecords,
            totalProcessed = list.TotalProcessed,
            importedCount = list.ImportedCount,
            rejectedRows = list.ErrorMessages?.Count ?? 0,
            error = list.Error,
            failedAttempts = list.FailedAttempts,
            uploadedFileName = list.UploadedFileName,
            created = await ToLocalAsync(localClock, list.CreatedUtc),
            lastProgress = await ToLocalAsync(localClock, list.ProcessSaveUtc),
            completed = await ToLocalAsync(localClock, list.CompletedUtc),
        };
}
