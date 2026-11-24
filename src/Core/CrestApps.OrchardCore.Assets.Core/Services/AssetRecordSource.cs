using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Localization;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Assets.Core.Services;

/// <summary>
/// Makes assets a record kind (<see cref="AssetsConstants.RecordKind"/>), so any module can link to an asset, pick one
/// and show its name without referencing Assets. The id is the asset's item id. Users who may see assets may search
/// them, by name, number or serial number.
/// </summary>
public sealed class AssetRecordSource : IRecordSource
{
    private const string DetailsAction = "Details";
    private const string DetailsController = "Assets";

    private readonly AssetStore _store;
    private readonly AssetTypeStore _typeStore;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetRecordSource"/> class.
    /// </summary>
    /// <param name="store">The asset store.</param>
    /// <param name="typeStore">The asset type store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public AssetRecordSource(
        AssetStore store,
        AssetTypeStore typeStore,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _typeStore = typeStore;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string Kind => AssetsConstants.RecordKind;

    /// <inheritdoc/>
    [JsonIgnore]
    public LocalizationSource DisplayName => LocalizationSource.Create<AssetRecordSource>("Asset");

    /// <inheritdoc/>
    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, AssetsPermissions.ViewAssets);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.Count <= 0 || !await CanSearchAsync(search.User, cancellationToken))
        {
            return [];
        }

        return await ToSummariesAsync(await _store.SearchAsync(search.Text, search.Count, cancellationToken), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, RecordSummary>> GetSummariesAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        var summaries = new Dictionary<string, RecordSummary>(StringComparer.Ordinal);
        var wanted = ids?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];

        if (wanted.Length == 0)
        {
            return summaries;
        }

        foreach (var summary in await ToSummariesAsync(await _store.GetAsync(wanted, cancellationToken), cancellationToken))
        {
            summaries[summary.Id] = summary;
        }

        return summaries;
    }

    private async Task<IReadOnlyList<RecordSummary>> ToSummariesAsync(IReadOnlyCollection<Asset> assets, CancellationToken cancellationToken)
    {
        if (assets.Count == 0)
        {
            return [];
        }

        var typeIds = assets
            .Select(asset => asset.AssetTypeId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var typeNames = typeIds.Length == 0
            ? []
            : (await _typeStore.GetAsync(typeIds, cancellationToken)).ToDictionary(type => type.ItemId, type => type.Name, StringComparer.Ordinal);
        var httpContext = _httpContextAccessor.HttpContext;

        return [.. assets.Select(asset => new RecordSummary
        {
            Id = asset.ItemId,
            Title = asset.Name,
            Description = Describe(asset, typeNames.GetValueOrDefault(asset.AssetTypeId ?? string.Empty)),
            AdminUrl = httpContext is null
                ? null
                : _linkGenerator.GetPathByAction(httpContext, DetailsAction, DetailsController, new { area = AssetsConstants.Features.Assets, id = asset.ItemId }),
        })];
    }

    // The asset's number, its type and its serial number, whichever it has.
    private static string Describe(Asset asset, string typeName)
    {
        string[] parts = [asset.Number, typeName, asset.SerialNumber];

        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}
