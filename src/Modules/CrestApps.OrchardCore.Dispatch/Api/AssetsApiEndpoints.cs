using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Dispatch.Api;

/// <summary>
/// The assets HTTP API at <c>api/assets/v1</c>: assets by text, type or owner, one asset, and its meter readings.
/// </summary>
internal static class AssetsApiEndpoints
{
    public const string RoutePrefix = "api/assets/v1";

    private const int MaxTake = 200;

    public static IEndpointRouteBuilder MapAssetsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags("Assets");

        group.MapGet("assets", (Delegate)ListAsync).WithSummary("Lists assets by text, type or owner.");
        group.MapGet("assets/{id}", (Delegate)GetAsync).WithSummary("Gets an asset.");
        group.MapGet("assets/{id}/readings", (Delegate)ReadingsAsync).WithSummary("Lists an asset's latest meter readings.");
        group.MapPost("assets/{id}/readings", (Delegate)RecordReadingAsync).WithSummary("Records a meter reading; a cumulative meter can't go back.");

        return routes;
    }

    internal static async Task<IResult> ListAsync(string q, string typeId, string ownerId, int? skip, int? take, HttpContext httpContext)
    {
        if (!await AllowedAsync(httpContext, manage: false))
        {
            return Results.Forbid();
        }

        var query = httpContext.RequestServices.GetRequiredService<ISession>().Query<Asset, AssetIndex>(collection: AssetsConstants.CollectionName);

        if (!string.IsNullOrWhiteSpace(typeId))
        {
            query = query.Where(index => index.AssetTypeId == typeId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            query = query.Where(index => index.OwnerId == ownerId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();

            query = query.Where(index => index.Number.Contains(text) || index.Name.Contains(text) || index.SerialNumber.Contains(text));
        }

        var count = await query.CountAsync();
        var items = await query.OrderBy(index => index.Number).Skip(Math.Max(0, skip ?? 0)).Take(Math.Clamp(take ?? 50, 1, MaxTake)).ListAsync();

        return Results.Json(new { count, items }, FieldServiceApiEndpoints.JsonOptions);
    }

    internal static async Task<IResult> GetAsync(string id, HttpContext httpContext)
    {
        if (!await AllowedAsync(httpContext, manage: false))
        {
            return Results.Forbid();
        }

        var asset = await httpContext.RequestServices.GetRequiredService<AssetManager>().FindByIdAsync(id);

        return asset is null ? Results.NotFound() : Results.Json(asset, FieldServiceApiEndpoints.JsonOptions);
    }

    internal static async Task<IResult> ReadingsAsync(string id, int? take, HttpContext httpContext)
    {
        if (!await AllowedAsync(httpContext, manage: false))
        {
            return Results.Forbid();
        }

        var readings = await httpContext.RequestServices.GetRequiredService<AssetService>().ReadingsAsync(id, Math.Clamp(take ?? 50, 1, MaxTake), httpContext.RequestAborted);

        return Results.Json(readings, FieldServiceApiEndpoints.JsonOptions);
    }

    internal static async Task<IResult> RecordReadingAsync(string id, AssetReadingInput input, HttpContext httpContext)
    {
        if (!await AllowedAsync(httpContext, manage: true))
        {
            return Results.Forbid();
        }

        if (input is null || string.IsNullOrWhiteSpace(input.Meter))
        {
            return Results.Problem(detail: "Give the meter and its value.", statusCode: StatusCodes.Status400BadRequest);
        }

        var services = httpContext.RequestServices;
        var reading = new AssetMeterReading
        {
            AssetId = id,
            Meter = input.Meter.Trim(),
            Value = input.Value,
            Utc = input.Utc ?? services.GetRequiredService<global::OrchardCore.Modules.IClock>().UtcNow,
            Source = MeterReadingSource.Api,
            RecordedBy = httpContext.User.Identity?.Name,
        };
        var errors = await services.GetRequiredService<AssetService>().RecordReadingAsync(reading, httpContext.RequestAborted);

        if (errors.Count > 0)
        {
            return Results.Problem(detail: string.Join(" ", errors), statusCode: StatusCodes.Status400BadRequest);
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(httpContext.RequestAborted);

        return Results.Json(reading, FieldServiceApiEndpoints.JsonOptions, statusCode: StatusCodes.Status201Created);
    }

    private static Task<bool> AllowedAsync(HttpContext httpContext, bool manage)
        => httpContext.RequestServices.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, manage ? AssetsPermissions.ManageAssets : AssetsPermissions.ViewAssets);
}

/// <summary>
/// A meter reading sent through the API.
/// </summary>
internal sealed class AssetReadingInput
{
    public string Meter { get; set; }

    public decimal Value { get; set; }

    public DateTime? Utc { get; set; }
}
