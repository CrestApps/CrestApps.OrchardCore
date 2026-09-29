using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Events;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Maintains tenant-scoped snapshots of the content types that carry the omnichannel and CRM marker parts, so
/// callers can answer "is this a subject, a contact, a lead, an account or an opportunity?" and build content type
/// drop downs without scanning every content type definition on each request. The snapshots are warmed once from
/// the content definitions and then kept in sync through the <see cref="IContentDefinitionEventHandler"/>
/// notifications.
/// </summary>
public sealed class OmnichannelContentTypeProvider : IContentDefinitionEventHandler
{
    [Flags]
    private enum Markers
    {
        None = 0,
        Subject = 1,
        Reachable = 2,
        Lead = 4,
        Account = 8,
        Opportunity = 16,
    }

    private static readonly IReadOnlyCollection<string> _empty = Array.Empty<string>();

    private readonly object _lock = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly bool _crmEnabled;

    // Bumped under _lock whenever a relevant content definition change is observed, including before the
    // snapshot is warmed. A warm captures this value before reading the definitions and discards its snapshot
    // if the version moved while the read was in flight, so an attach or detach that races the warm is
    // never lost.
    private long _version;
    private Dictionary<string, Markers> _markers;
    private volatile Snapshot _snapshot;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContentTypeProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">The scope factory used to warm the sets on demand from the async accessors,
    /// since this provider is a singleton and cannot capture the scoped content definition manager directly.</param>
    /// <param name="crmOptions">Whether the CRM feature is enabled. While it is off there are no lead types, so a
    /// type carrying the lead part is a plain contact type.</param>
    public OmnichannelContentTypeProvider(
        IServiceScopeFactory scopeFactory,
        IOptions<OmnichannelCrmOptions> crmOptions = null)
    {
        _scopeFactory = scopeFactory;
        _crmEnabled = crmOptions?.Value?.Enabled == true;
    }

    /// <summary>
    /// Determines whether the specified content type has the <c>OmnichannelSubjectPart</c> attached.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    /// <returns><see langword="true"/> when the content type is a subject; otherwise, <see langword="false"/>.</returns>
    public bool IsSubjectContentType(string contentType)
        => Contains(_snapshot?.Subjects, contentType);

    /// <summary>
    /// Determines whether the specified content type has the <c>OmnichannelContactPart</c> attached. Lead types
    /// carry the part too, so they are reachable content types as well.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    /// <returns><see langword="true"/> when the content type is a contact; otherwise, <see langword="false"/>.</returns>
    public bool IsContactContentType(string contentType)
        => Contains(_snapshot?.Reachable, contentType);

    /// <summary>
    /// Determines whether the specified content type is a lead type.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    public bool IsLeadContentType(string contentType)
        => Contains(_snapshot?.Leads, contentType);

    /// <summary>
    /// Determines whether the specified content type is an account type.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    public bool IsAccountContentType(string contentType)
        => Contains(_snapshot?.Accounts, contentType);

    /// <summary>
    /// Determines whether the specified content type is an opportunity type.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    public bool IsOpportunityContentType(string contentType)
        => Contains(_snapshot?.Opportunities, contentType);

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelSubjectPart</c> attached.
    /// </summary>
    /// <returns>A read-only snapshot of the subject content type names.</returns>
    public IReadOnlyCollection<string> GetSubjectContentTypes()
        => _snapshot?.Subjects ?? _empty;

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelContactPart</c> attached,
    /// leads included.
    /// </summary>
    /// <returns>A read-only snapshot of the contact content type names.</returns>
    public IReadOnlyCollection<string> GetContactContentTypes()
        => _snapshot?.Reachable ?? _empty;

    /// <summary>
    /// Gets the technical names of the contact types that are not lead types: the clean contact records.
    /// </summary>
    public IReadOnlyCollection<string> GetContactKindContentTypes()
        => _snapshot?.Contacts ?? _empty;

    /// <summary>
    /// Gets the technical names of the lead types.
    /// </summary>
    public IReadOnlyCollection<string> GetLeadContentTypes()
        => _snapshot?.Leads ?? _empty;

    /// <summary>
    /// Gets the technical names of the account types.
    /// </summary>
    public IReadOnlyCollection<string> GetAccountContentTypes()
        => _snapshot?.Accounts ?? _empty;

    /// <summary>
    /// Gets the technical names of the opportunity types.
    /// </summary>
    public IReadOnlyCollection<string> GetOpportunityContentTypes()
        => _snapshot?.Opportunities ?? _empty;

    /// <summary>
    /// Gets the subject content type names, warming the cached sets on demand when they have not been
    /// initialized yet. This lets callers that do not have an <see cref="IContentDefinitionManager"/> at hand
    /// read the snapshot without an explicit warm step.
    /// </summary>
    /// <returns>A read-only snapshot of the subject content type names.</returns>
    public async ValueTask<IReadOnlyCollection<string>> GetSubjectContentTypesAsync()
    {
        await EnsureInitializedAsync();

        return GetSubjectContentTypes();
    }

    /// <summary>
    /// Gets the contact content type names, leads included, warming the cached sets on demand when they have not
    /// been initialized yet.
    /// </summary>
    /// <returns>A read-only snapshot of the contact content type names.</returns>
    public async ValueTask<IReadOnlyCollection<string>> GetContactContentTypesAsync()
    {
        await EnsureInitializedAsync();

        return GetContactContentTypes();
    }

    /// <summary>
    /// Gets the contact types that are not lead types, warming the cached sets on demand.
    /// </summary>
    public async ValueTask<IReadOnlyCollection<string>> GetContactKindContentTypesAsync()
    {
        await EnsureInitializedAsync();

        return GetContactKindContentTypes();
    }

    /// <summary>
    /// Gets the lead types, warming the cached sets on demand.
    /// </summary>
    public async ValueTask<IReadOnlyCollection<string>> GetLeadContentTypesAsync()
    {
        await EnsureInitializedAsync();

        return GetLeadContentTypes();
    }

    /// <summary>
    /// Gets the account types, warming the cached sets on demand.
    /// </summary>
    public async ValueTask<IReadOnlyCollection<string>> GetAccountContentTypesAsync()
    {
        await EnsureInitializedAsync();

        return GetAccountContentTypes();
    }

    /// <summary>
    /// Gets the opportunity types, warming the cached sets on demand.
    /// </summary>
    public async ValueTask<IReadOnlyCollection<string>> GetOpportunityContentTypesAsync()
    {
        await EnsureInitializedAsync();

        return GetOpportunityContentTypes();
    }

    /// <summary>
    /// Warms the cached sets on demand using a fresh scope, for the async accessors used by callers that do not
    /// pass an <see cref="IContentDefinitionManager"/> themselves.
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_snapshot is not null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var contentDefinitionManager = scope.ServiceProvider.GetRequiredService<IContentDefinitionManager>();

        await EnsureInitializedAsync(contentDefinitionManager);
    }

    /// <summary>
    /// Warms the cached sets from the current content definitions the first time they are requested. Subsequent
    /// calls are a no-op because the sets are afterward kept current through the content definition notifications.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read the type definitions.</param>
    public async Task EnsureInitializedAsync(IContentDefinitionManager contentDefinitionManager)
    {
        ArgumentNullException.ThrowIfNull(contentDefinitionManager);

        while (true)
        {
            long versionSnapshot;

            lock (_lock)
            {
                if (_snapshot is not null)
                {
                    return;
                }

                versionSnapshot = _version;
            }

            var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

            var markers = new Dictionary<string, Markers>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                var value = GetMarkers(definition);

                if (value != Markers.None)
                {
                    markers[definition.Name] = value;
                }
            }

            lock (_lock)
            {
                if (_snapshot is not null)
                {
                    // Another thread finished warming while this read was in flight. Its result already
                    // reflects every event applied incrementally, so keep it.
                    return;
                }

                if (_version == versionSnapshot)
                {
                    _markers = markers;
                    _snapshot = BuildSnapshot(markers);

                    return;
                }

                // A content definition change was observed while the definitions were being read, so this
                // snapshot may predate it. Discard it and read again from the now-current definitions.
            }
        }
    }

    /// <inheritdoc/>
    public void ContentTypeCreated(ContentTypeCreatedContext context)
        => Apply(context.ContentTypeDefinition);

    /// <inheritdoc/>
    public void ContentTypeUpdated(ContentTypeUpdatedContext context)
        => Apply(context.ContentTypeDefinition);

    /// <inheritdoc/>
    public void ContentTypeImported(ContentTypeImportedContext context)
        => Apply(context.ContentTypeDefinition);

    /// <inheritdoc/>
    public void ContentTypeRemoved(ContentTypeRemovedContext context)
        => SetMarkers(context.ContentTypeDefinition?.Name, _ => Markers.None);

    /// <inheritdoc/>
    public void ContentPartAttached(ContentPartAttachedContext context)
    {
        var marker = GetMarker(context.ContentPartName);

        if (marker != Markers.None)
        {
            SetMarkers(context.ContentTypeName, current => current | marker);
        }
    }

    /// <inheritdoc/>
    public void ContentPartDetached(ContentPartDetachedContext context)
    {
        var marker = GetMarker(context.ContentPartName);

        if (marker != Markers.None)
        {
            SetMarkers(context.ContentTypeName, current => current & ~marker);
        }
    }

    /// <inheritdoc/>
    public void ContentTypeImporting(ContentTypeImportingContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartCreated(ContentPartCreatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartUpdated(ContentPartUpdatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartRemoved(ContentPartRemovedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartImporting(ContentPartImportingContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartImported(ContentPartImportedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentTypePartUpdated(ContentTypePartUpdatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentFieldAttached(ContentFieldAttachedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentFieldUpdated(ContentFieldUpdatedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentFieldDetached(ContentFieldDetachedContext context)
    {
    }

    /// <inheritdoc/>
    public void ContentPartFieldUpdated(ContentPartFieldUpdatedContext context)
    {
    }

    private void Apply(ContentTypeDefinition contentTypeDefinition)
    {
        if (contentTypeDefinition is null)
        {
            return;
        }

        var markers = GetMarkers(contentTypeDefinition);

        SetMarkers(contentTypeDefinition.Name, _ => markers);
    }

    private void SetMarkers(string contentType, Func<Markers, Markers> update)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return;
        }

        lock (_lock)
        {
            // Bump the version even when the snapshot is still null so an in-flight warm re-reads. The
            // incremental update below is a no-op until the snapshot has been warmed, because the warm reads the
            // current definitions and needs no events applied on top.
            _version++;

            if (_markers is null)
            {
                return;
            }

            var current = _markers.GetValueOrDefault(contentType);
            var updated = update(current);

            if (updated == current)
            {
                return;
            }

            var markers = new Dictionary<string, Markers>(_markers, StringComparer.Ordinal);

            if (updated == Markers.None)
            {
                markers.Remove(contentType);
            }
            else
            {
                markers[contentType] = updated;
            }

            _markers = markers;
            _snapshot = BuildSnapshot(markers);
        }
    }

    private Snapshot BuildSnapshot(Dictionary<string, Markers> markers)
    {
        var subjects = new HashSet<string>(StringComparer.Ordinal);
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        var contacts = new HashSet<string>(StringComparer.Ordinal);
        var leads = new HashSet<string>(StringComparer.Ordinal);
        var accounts = new HashSet<string>(StringComparer.Ordinal);
        var opportunities = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (contentType, value) in markers)
        {
            if (value.HasFlag(Markers.Subject))
            {
                subjects.Add(contentType);
            }

            if (value.HasFlag(Markers.Reachable))
            {
                reachable.Add(contentType);

                // Until the CRM feature is enabled a lead part is inert, so the type stays a plain contact.
                if (_crmEnabled && value.HasFlag(Markers.Lead))
                {
                    leads.Add(contentType);
                }
                else
                {
                    contacts.Add(contentType);
                }
            }

            if (value.HasFlag(Markers.Account))
            {
                accounts.Add(contentType);
            }

            if (value.HasFlag(Markers.Opportunity))
            {
                opportunities.Add(contentType);
            }
        }

        return new Snapshot(subjects, reachable, contacts, leads, accounts, opportunities);
    }

    private static Markers GetMarkers(ContentTypeDefinition definition)
    {
        var markers = Markers.None;

        if (OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(definition))
        {
            markers |= Markers.Subject;
        }

        if (OmnichannelContactDefinitionService.HasOmnichannelContactPart(definition))
        {
            markers |= Markers.Reachable;
        }

        if (OmnichannelRecordKinds.HasPart(definition, OmnichannelConstants.ContentParts.Lead))
        {
            markers |= Markers.Lead;
        }

        if (OmnichannelRecordKinds.IsAccount(definition))
        {
            markers |= Markers.Account;
        }

        if (OmnichannelRecordKinds.IsOpportunity(definition))
        {
            markers |= Markers.Opportunity;
        }

        return markers;
    }

    private static Markers GetMarker(string partName)
        => partName switch
        {
            OmnichannelConstants.ContentParts.OmnichannelSubject => Markers.Subject,
            OmnichannelConstants.ContentParts.OmnichannelContact => Markers.Reachable,
            OmnichannelConstants.ContentParts.Lead => Markers.Lead,
            OmnichannelConstants.ContentParts.Account => Markers.Account,
            OmnichannelConstants.ContentParts.Opportunity => Markers.Opportunity,
            _ => Markers.None,
        };

    private static bool Contains(HashSet<string> contentTypes, string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        return contentTypes is not null && contentTypes.Contains(contentType);
    }

    private sealed record Snapshot(
        HashSet<string> Subjects,
        HashSet<string> Reachable,
        HashSet<string> Contacts,
        HashSet<string> Leads,
        HashSet<string> Accounts,
        HashSet<string> Opportunities);
}
