using System.Security.Claims;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.WorkOrders.Core.Services;

/// <summary>
/// Makes work orders a record kind (<see cref="WorkOrdersConstants.RecordKind"/>), so any module can link to a work
/// order, pick one and show its number without referencing Work Orders. The id is the work order's item id. Users who
/// may see work orders may search them, by number or summary.
/// </summary>
public sealed class WorkOrderRecordSource : IRecordSource
{
    private const string DetailsAction = "Details";
    private const string DetailsController = "WorkOrders";

    private readonly WorkOrderStore _store;
    private readonly IAuthorizationService _authorizationService;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkOrderRecordSource"/> class.
    /// </summary>
    /// <param name="store">The work order store.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public WorkOrderRecordSource(
        WorkOrderStore store,
        IAuthorizationService authorizationService,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _authorizationService = authorizationService;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string Kind => WorkOrdersConstants.RecordKind;

    /// <inheritdoc/>
    [JsonIgnore]
    public LocalizationSource DisplayName => LocalizationSource.Create<WorkOrderRecordSource>("Work order");

    /// <inheritdoc/>
    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => user is not null && await _authorizationService.AuthorizeAsync(user, WorkOrdersPermissions.ViewWorkOrders);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.Count <= 0 || !await CanSearchAsync(search.User, cancellationToken))
        {
            return [];
        }

        var workOrders = await _store.SearchAsync(search.Text, search.Count, cancellationToken);

        return [.. workOrders.Select(ToSummary)];
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

        foreach (var workOrder in await _store.GetAsync(wanted, cancellationToken))
        {
            summaries[workOrder.ItemId] = ToSummary(workOrder);
        }

        return summaries;
    }

    private RecordSummary ToSummary(WorkOrder workOrder)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        return new RecordSummary
        {
            Id = workOrder.ItemId,
            Title = $"{workOrder.Number} {workOrder.Summary}".Trim(),
            Description = workOrder.Status.Text(),
            AdminUrl = httpContext is null
                ? null
                : _linkGenerator.GetPathByAction(httpContext, DetailsAction, DetailsController, new { area = WorkOrdersConstants.Features.WorkOrders, id = workOrder.ItemId }),
        };
    }
}
