using System.Security.Claims;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Tickets as records other records link to: the record picker searches the tickets the user may view, by number or
/// subject, and a link shows the ticket's number and subject. The ticket service is resolved when searching, because
/// it depends on the timeline, which reads record names through the record directory this source belongs to.
/// </summary>
public sealed class TicketRecordSource : IRecordSource
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITicketAccessService _access;
    private readonly IContentManager _contentManager;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketRecordSource"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider, for the ticket service.</param>
    /// <param name="access">The ticket access service.</param>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public TicketRecordSource(
        IServiceProvider serviceProvider,
        ITicketAccessService access,
        IContentManager contentManager,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _serviceProvider = serviceProvider;
        _access = access;
        _contentManager = contentManager;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public string Kind => TicketsConstants.RecordKind;

    /// <inheritdoc/>
    public LocalizationSource DisplayName => LocalizationSource.Create<TicketRecordSource>("Ticket");

    /// <inheritdoc/>
    public async Task<bool> CanSearchAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => (await _access.GetViewScopesAsync(user, cancellationToken)).Count > 0;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordSummary>> SearchAsync(RecordSearch search, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(search);

        if (search.Count <= 0)
        {
            return [];
        }

        var scoped = await _access.ApplyScopeAsync(search.User, new TicketQuery { Search = search.Text, Take = search.Count }, cancellationToken);

        if (scoped is null)
        {
            return [];
        }

        var (tickets, _) = await _serviceProvider.GetRequiredService<ITicketService>().ListAsync(scoped, cancellationToken);

        return [.. tickets.Select(ToSummary)];
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

        foreach (var ticket in await _contentManager.GetAsync(wanted, VersionOptions.Latest))
        {
            if (ticket.Has<TicketPart>())
            {
                summaries[ticket.ContentItemId] = ToSummary(ticket);
            }
        }

        return summaries;
    }

    private RecordSummary ToSummary(ContentItem ticket)
    {
        var part = ticket.GetOrCreate<TicketPart>();
        var httpContext = _httpContextAccessor.HttpContext;

        return new RecordSummary
        {
            Id = ticket.ContentItemId,
            Title = $"{part?.Number} {ticket.DisplayText}".Trim(),
            Description = part?.StatusCategory.ToString(),
            AdminUrl = httpContext is null
                ? null
                : _linkGenerator.GetPathByAction(httpContext, "Display", "Tickets", new { area = TicketsConstants.Features.Tickets, id = ticket.ContentItemId }),
        };
    }
}
