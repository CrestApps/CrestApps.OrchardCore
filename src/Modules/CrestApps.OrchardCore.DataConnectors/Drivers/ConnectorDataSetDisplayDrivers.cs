using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Connectors.Elasticsearch;
using CrestApps.OrchardCore.DataConnectors.Connectors.Files;
using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Relational;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DataSources.Files;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.DataConnectors.Drivers;

/// <summary>
/// Edits how a data set reads an HTTP API: the method, body, records path, response format and paging.
/// </summary>
internal sealed class HttpApiDataSetDisplayDriver : DisplayDriver<ConnectedDataSet>
{
    private readonly DataSetConnectionResolver _resolver;
    private readonly IDataFileFormatManager _fileFormatManager;

    internal readonly IStringLocalizer S;

    public HttpApiDataSetDisplayDriver(
        DataSetConnectionResolver resolver,
        IDataFileFormatManager fileFormatManager,
        IStringLocalizer<HttpApiDataSetDisplayDriver> stringLocalizer)
    {
        _resolver = resolver;
        _fileFormatManager = fileFormatManager;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ConnectedDataSet dataSet, BuildEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        return Initialize<HttpApiDataSetViewModel>("HttpApiDataSet_Edit", model =>
        {
            var settings = dataSet.GetOrCreate<HttpApiDataSetSettings>();
            var pagination = settings.Pagination ?? new HttpPaginationSettings();

            model.Method = settings.Method;
            model.Body = settings.Body;
            model.RecordsPath = settings.RecordsPath;
            model.Format = settings.Format;
            model.PaginationType = pagination.Type;
            model.PageParameter = pagination.PageParameter;
            model.FirstPage = pagination.FirstPage;
            model.OffsetParameter = pagination.OffsetParameter;
            model.SizeParameter = pagination.SizeParameter;
            model.PageSize = pagination.PageSize;
            model.CursorParameter = pagination.CursorParameter;
            model.NextPath = pagination.NextPath;
            model.MaxPages = pagination.MaxPages;
            model.Formats =
            [
                new SelectListItem(S["JSON"], string.Empty),
                .. _fileFormatManager.GetFormats()
                    .Where(format => !format.IsWorkbook && !string.Equals(format.Name, "json", StringComparison.OrdinalIgnoreCase))
                    .Select(format => new SelectListItem(format.DisplayName.Value, format.Name)),
            ];
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ConnectedDataSet dataSet, UpdateEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        var model = new HttpApiDataSetViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var method = string.Equals(model.Method, "POST", StringComparison.OrdinalIgnoreCase) ? "POST" : "GET";

        if (model.PageSize is < 1 or > 10_000)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.PageSize), S["The page size must be between 1 and 10000."]);
        }

        if (model.MaxPages is < 1 or > 100_000)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.MaxPages), S["The most pages must be between 1 and 100000."]);
        }

        if (model.PaginationType is HttpPaginationType.Cursor or HttpPaginationType.NextLink && string.IsNullOrWhiteSpace(model.NextPath))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.NextPath), S["Give the path of the next cursor or link in the response."]);
        }

        if (!string.IsNullOrWhiteSpace(model.Format) && _fileFormatManager.GetDescriptor(model.Format) is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Format), S["Pick a response format from the list."]);
        }

        dataSet.Put(new HttpApiDataSetSettings
        {
            Method = method,
            Body = method == "POST" && !string.IsNullOrWhiteSpace(model.Body) ? model.Body.Trim() : null,
            RecordsPath = string.IsNullOrWhiteSpace(model.RecordsPath) ? null : model.RecordsPath.Trim(),
            Format = string.IsNullOrWhiteSpace(model.Format) ? null : model.Format.Trim(),
            Pagination = new HttpPaginationSettings
            {
                Type = model.PaginationType,
                PageParameter = model.PageParameter?.Trim(),
                FirstPage = model.FirstPage,
                OffsetParameter = model.OffsetParameter?.Trim(),
                SizeParameter = string.IsNullOrWhiteSpace(model.SizeParameter) ? null : model.SizeParameter.Trim(),
                PageSize = model.PageSize,
                CursorParameter = model.CursorParameter?.Trim(),
                NextPath = string.IsNullOrWhiteSpace(model.NextPath) ? null : model.NextPath.Trim(),
                MaxPages = model.MaxPages,
            },
        });

        return await EditAsync(dataSet, context);
    }

    private async Task<bool> AppliesAsync(ConnectedDataSet dataSet)
        => (await _resolver.GetConnectorAsync(dataSet))?.Name == HttpApiDataConnector.Name;
}

/// <summary>
/// Edits how a data set reads an Elasticsearch index: its query and scroll.
/// </summary>
internal sealed class ElasticsearchDataSetDisplayDriver : DisplayDriver<ConnectedDataSet>
{
    private readonly DataSetConnectionResolver _resolver;

    internal readonly IStringLocalizer S;

    public ElasticsearchDataSetDisplayDriver(
        DataSetConnectionResolver resolver,
        IStringLocalizer<ElasticsearchDataSetDisplayDriver> stringLocalizer)
    {
        _resolver = resolver;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ConnectedDataSet dataSet, BuildEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        return Initialize<ElasticsearchDataSetViewModel>("ElasticsearchDataSet_Edit", model =>
        {
            var settings = dataSet.GetOrCreate<ElasticsearchDataSetSettings>();

            model.Query = settings.Query;
            model.ScrollMinutes = settings.ScrollMinutes;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ConnectedDataSet dataSet, UpdateEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        var model = new ElasticsearchDataSetViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrWhiteSpace(model.Query))
        {
            try
            {
                if (JsonNode.Parse(model.Query) is not JsonObject)
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Query), S["The query must be a JSON object, such as a term or bool clause."]);
                }
            }
            catch (JsonException)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Query), S["The query isn't valid JSON."]);
            }
        }

        if (model.ScrollMinutes is < 1 or > 60)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.ScrollMinutes), S["The scroll keep-alive must be between 1 and 60 minutes."]);
        }

        dataSet.Put(new ElasticsearchDataSetSettings
        {
            Query = string.IsNullOrWhiteSpace(model.Query) ? null : model.Query.Trim(),
            ScrollMinutes = model.ScrollMinutes,
        });

        return await EditAsync(dataSet, context);
    }

    private async Task<bool> AppliesAsync(ConnectedDataSet dataSet)
        => (await _resolver.GetConnectorAsync(dataSet))?.Name == ElasticsearchDataConnector.Name;
}

/// <summary>
/// Edits the custom query of a data set that reads a relational database, for every relational connector.
/// </summary>
internal sealed class RelationalDataSetDisplayDriver : DisplayDriver<ConnectedDataSet>
{
    private readonly DataSetConnectionResolver _resolver;

    internal readonly IStringLocalizer S;

    public RelationalDataSetDisplayDriver(
        DataSetConnectionResolver resolver,
        IStringLocalizer<RelationalDataSetDisplayDriver> stringLocalizer)
    {
        _resolver = resolver;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ConnectedDataSet dataSet, BuildEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        return Initialize<RelationalDataSetViewModel>("RelationalDataSet_Edit", model =>
        {
            var settings = dataSet.GetOrCreate<RelationalDataSetSettings>();

            model.Query = settings.Query;
            model.CommandTimeoutSeconds = settings.CommandTimeoutSeconds;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ConnectedDataSet dataSet, UpdateEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        var model = new RelationalDataSetViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrWhiteSpace(model.Query))
        {
            try
            {
                RelationalDataConnector.ValidateQuery(model.Query);
            }
            catch (DataConnectorException exception)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Query), exception.Message);
            }
        }
        else if (string.IsNullOrWhiteSpace(dataSet.Resource))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Query), S["Pick a table or view, or write a query."]);
        }

        if (model.CommandTimeoutSeconds is < 5 or > 3_600)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CommandTimeoutSeconds), S["The query timeout must be between 5 and 3600 seconds."]);
        }

        dataSet.Put(new RelationalDataSetSettings
        {
            Query = string.IsNullOrWhiteSpace(model.Query) ? null : model.Query.Trim(),
            CommandTimeoutSeconds = model.CommandTimeoutSeconds,
        });

        return await EditAsync(dataSet, context);
    }

    private async Task<bool> AppliesAsync(ConnectedDataSet dataSet)
        => (await _resolver.GetConnectorAsync(dataSet))?.Type.IsAssignableTo(typeof(RelationalDataConnector)) == true;
}

/// <summary>
/// Edits how a data set reads a media library file: the CSV delimiter and header, and the Excel sheet.
/// </summary>
internal sealed class MediaFileDataSetDisplayDriver : DisplayDriver<ConnectedDataSet>
{
    private readonly DataSetConnectionResolver _resolver;

    internal readonly IStringLocalizer S;

    public MediaFileDataSetDisplayDriver(
        DataSetConnectionResolver resolver,
        IStringLocalizer<MediaFileDataSetDisplayDriver> stringLocalizer)
    {
        _resolver = resolver;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> EditAsync(ConnectedDataSet dataSet, BuildEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        return Initialize<MediaFileDataSetViewModel>("MediaFileDataSet_Edit", model =>
        {
            var settings = dataSet.GetOrCreate<MediaFileDataSetSettings>();

            model.Delimiter = settings.Delimiter;
            model.HasHeaderRow = settings.HasHeaderRow;
            model.SheetName = settings.SheetName;
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(ConnectedDataSet dataSet, UpdateEditorContext context)
    {
        if (!await AppliesAsync(dataSet))
        {
            return null;
        }

        var model = new MediaFileDataSetViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(dataSet.Resource))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Delimiter), S["Pick the file the data set reads."]);
        }

        dataSet.Put(new MediaFileDataSetSettings
        {
            Delimiter = string.IsNullOrEmpty(model.Delimiter) ? "," : model.Delimiter,
            HasHeaderRow = model.HasHeaderRow,
            SheetName = string.IsNullOrWhiteSpace(model.SheetName) ? null : model.SheetName.Trim(),
        });

        return await EditAsync(dataSet, context);
    }

    private async Task<bool> AppliesAsync(ConnectedDataSet dataSet)
        => (await _resolver.GetConnectorAsync(dataSet))?.Name == MediaFilesDataConnector.Name;
}
