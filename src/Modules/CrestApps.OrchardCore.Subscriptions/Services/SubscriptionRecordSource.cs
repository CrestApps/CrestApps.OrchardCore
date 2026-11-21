using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Makes subscription agreements a record kind (<see cref="SubscriptionConstants.RecordKind"/>), so any module can
/// link to a subscription, pick one and show its title without referencing Subscriptions. The id is the
/// subscription's item id. Users who manage subscriptions may search them, by title, as the agreements report does.
/// </summary>
public sealed class SubscriptionRecordSource : IRecordSource
{
    private const string DetailAction = "Detail";
    private const string DetailController = "SubscriptionAgreements";

    private readonly ISubscriptionStore _store;
    private readonly SubscriptionDisplayText _displayText;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRecordSource"/> class.
    /// </summary>
    /// <param name="store">The subscription store.</param>
    /// <param name="displayText">The words the subscription screens show for a status.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SubscriptionRecordSource(
        ISubscriptionStore store,
        SubscriptionDisplayText displayText,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _displayText = displayText;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string Kind => SubscriptionConstants.RecordKind;

    /// <inheritdoc/>
    [JsonIgnore]
    public LocalizationSource DisplayName => LocalizationSource.Create<SubscriptionRecordSource>("Subscription");

    /// <inheritdoc/>
    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, SubscriptionPermissions.ManageSubscriptions);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.Count <= 0 || !await CanSearchAsync(search.User, cancellationToken))
        {
            return [];
        }

        var text = search.Text?.Trim();
        var page = await _store.PageAsync(1, search.Count, new SubscriptionQuery { Search = string.IsNullOrEmpty(text) ? null : text }, cancellationToken);

        return [.. page.Entries.Select(ToSummary)];
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

        foreach (var subscription in await _store.GetAsync(wanted, cancellationToken))
        {
            summaries[subscription.ItemId] = ToSummary(subscription);
        }

        return summaries;
    }

    private RecordSummary ToSummary(Subscription subscription)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return new RecordSummary
        {
            Id = subscription.ItemId,
            Title = subscription.Title,
            Description = _displayText.StatusName(subscription.Status),
            AdminUrl = httpContext is null
                ? null
                : _linkGenerator.GetPathByAction(httpContext, DetailAction, DetailController, new { area = SubscriptionConstants.Features.Area, itemId = subscription.ItemId }),
        };
    }
}
