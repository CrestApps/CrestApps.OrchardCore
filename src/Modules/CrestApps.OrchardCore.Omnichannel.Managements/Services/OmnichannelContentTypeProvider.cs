using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Events;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Maintains in-memory sets of the content types that have the <c>OmnichannelSubjectPart</c> or the
/// <c>OmnichannelContactPart</c> attached so callers can answer that question, and build subject and contact
/// content type drop downs, without scanning every content type definition on each request. The sets are warmed
/// once from the content definitions and then kept in sync through the <see cref="IContentDefinitionEventHandler"/>
/// notifications, so they always reflect the latest definitions without repeated enumeration.
/// </summary>
public sealed class OmnichannelContentTypeProvider : IContentDefinitionEventHandler
{
    private readonly Lock _lock = new();

    private volatile HashSet<string> _subjectContentTypes;
    private volatile HashSet<string> _contactContentTypes;

    /// <summary>
    /// Determines whether the specified content type has the <c>OmnichannelSubjectPart</c> attached.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    /// <returns><see langword="true"/> when the content type is a subject; otherwise, <see langword="false"/>.</returns>
    public bool IsSubjectContentType(string contentType)
        => Contains(_subjectContentTypes, contentType);

    /// <summary>
    /// Determines whether the specified content type has the <c>OmnichannelContactPart</c> attached.
    /// </summary>
    /// <param name="contentType">The technical name of the content type to test.</param>
    /// <returns><see langword="true"/> when the content type is a contact; otherwise, <see langword="false"/>.</returns>
    public bool IsContactContentType(string contentType)
        => Contains(_contactContentTypes, contentType);

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelSubjectPart</c> attached.
    /// </summary>
    /// <returns>A read-only snapshot of the subject content type names.</returns>
    public IReadOnlyCollection<string> GetSubjectContentTypes()
        => _subjectContentTypes ?? (IReadOnlyCollection<string>)[];

    /// <summary>
    /// Gets the technical names of the content types that have the <c>OmnichannelContactPart</c> attached.
    /// </summary>
    /// <returns>A read-only snapshot of the contact content type names.</returns>
    public IReadOnlyCollection<string> GetContactContentTypes()
        => _contactContentTypes ?? (IReadOnlyCollection<string>)[];

    /// <summary>
    /// Warms the cached sets from the current content definitions the first time they are requested. Subsequent
    /// calls are a no-op because the sets are afterward kept current through the content definition notifications.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager used to read the type definitions.</param>
    public async Task EnsureInitializedAsync(IContentDefinitionManager contentDefinitionManager)
    {
        if (_subjectContentTypes is not null && _contactContentTypes is not null)
        {
            return;
        }

        var definitions = await contentDefinitionManager.ListTypeDefinitionsAsync();

        var subjectContentTypes = new HashSet<string>(StringComparer.Ordinal);
        var contactContentTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in definitions)
        {
            if (OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(definition))
            {
                subjectContentTypes.Add(definition.Name);
            }

            if (OmnichannelContactDefinitionService.HasOmnichannelContactPart(definition))
            {
                contactContentTypes.Add(definition.Name);
            }
        }

        lock (_lock)
        {
            _subjectContentTypes ??= subjectContentTypes;
            _contactContentTypes ??= contactContentTypes;
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
    {
        var contentType = context.ContentTypeDefinition?.Name;

        SetSubjectMembership(contentType, isMember: false);
        SetContactMembership(contentType, isMember: false);
    }

    /// <inheritdoc/>
    public void ContentPartAttached(ContentPartAttachedContext context)
    {
        if (IsSubjectPart(context.ContentPartName))
        {
            SetSubjectMembership(context.ContentTypeName, isMember: true);
        }
        else if (IsContactPart(context.ContentPartName))
        {
            SetContactMembership(context.ContentTypeName, isMember: true);
        }
    }

    /// <inheritdoc/>
    public void ContentPartDetached(ContentPartDetachedContext context)
    {
        if (IsSubjectPart(context.ContentPartName))
        {
            SetSubjectMembership(context.ContentTypeName, isMember: false);
        }
        else if (IsContactPart(context.ContentPartName))
        {
            SetContactMembership(context.ContentTypeName, isMember: false);
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

        SetSubjectMembership(contentTypeDefinition.Name, OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentTypeDefinition));
        SetContactMembership(contentTypeDefinition.Name, OmnichannelContactDefinitionService.HasOmnichannelContactPart(contentTypeDefinition));
    }

    private void SetSubjectMembership(string contentType, bool isMember)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return;
        }

        lock (_lock)
        {
            _subjectContentTypes = WithMembership(_subjectContentTypes, contentType, isMember);
        }
    }

    private void SetContactMembership(string contentType, bool isMember)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return;
        }

        lock (_lock)
        {
            _contactContentTypes = WithMembership(_contactContentTypes, contentType, isMember);
        }
    }

    private static HashSet<string> WithMembership(HashSet<string> contentTypes, string contentType, bool isMember)
    {
        // Skip incremental updates until the set has been warmed; the initial warm reads the current
        // definitions and therefore already reflects any change that happened before it ran.
        if (contentTypes is null)
        {
            return null;
        }

        if (isMember == contentTypes.Contains(contentType))
        {
            return contentTypes;
        }

        var updated = new HashSet<string>(contentTypes, StringComparer.Ordinal);

        if (isMember)
        {
            updated.Add(contentType);
        }
        else
        {
            updated.Remove(contentType);
        }

        return updated;
    }

    private static bool Contains(HashSet<string> contentTypes, string contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        return contentTypes is not null && contentTypes.Contains(contentType);
    }

    private static bool IsSubjectPart(string partName)
        => string.Equals(partName, OmnichannelConstants.ContentParts.OmnichannelSubject, StringComparison.Ordinal);

    private static bool IsContactPart(string partName)
        => string.Equals(partName, OmnichannelConstants.ContentParts.OmnichannelContact, StringComparison.Ordinal);
}
