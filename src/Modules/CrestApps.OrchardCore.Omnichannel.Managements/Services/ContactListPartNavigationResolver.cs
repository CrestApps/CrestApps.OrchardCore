using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Lists.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Reads the <c>ListPart</c> settings of a contact's content type, so the contact's own admin pages (the activity
/// list, editor, create and complete pages) can show the same header and navigation bar the list part shows on the
/// contact's content pages.
/// </summary>
internal sealed class ContactListPartNavigationResolver
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactListPartNavigationResolver"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public ContactListPartNavigationResolver(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Resolves the list part navigation of a contact.
    /// </summary>
    /// <param name="contact">The contact content item.</param>
    /// <param name="contactTypeDefinition">The contact's content type definition, when the caller already has it.</param>
    /// <returns>The list part navigation, or <see langword="null"/> when the contact's type has no <c>ListPart</c>.</returns>
    public async Task<ContactListPartNavigation> ResolveAsync(ContentItem contact, ContentTypeDefinition contactTypeDefinition = null)
    {
        if (contact is null || string.IsNullOrEmpty(contact.ContentType))
        {
            return null;
        }

        contactTypeDefinition ??= await _contentDefinitionManager.GetTypeDefinitionAsync(contact.ContentType);

        var listTypePart = contactTypeDefinition?.Parts
            .FirstOrDefault(part => part.PartDefinition?.Name == ContactListPartShapeTableProvider.ListPartName);

        if (listTypePart is null)
        {
            return null;
        }

        var settings = listTypePart.GetSettings<ListPartSettings>();
        var containedTypeDefinitions = new List<ContentTypeDefinition>();

        foreach (var containedType in settings.ContainedContentTypes ?? [])
        {
            var containedTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(containedType);

            if (containedTypeDefinition is not null)
            {
                containedTypeDefinitions.Add(containedTypeDefinition);
            }
        }

        return new ContactListPartNavigation
        {
            ContactTypeDefinition = contactTypeDefinition,
            ContainedContentTypeDefinitions = containedTypeDefinitions.ToArray(),
            EnableOrdering = settings.EnableOrdering,
            ShowHeader = settings.ShowHeader,
        };
    }
}

/// <summary>
/// The list part navigation of a contact whose content type has the <c>ListPart</c> attached.
/// </summary>
internal sealed class ContactListPartNavigation
{
    /// <summary>
    /// Gets the contact's content type definition.
    /// </summary>
    public ContentTypeDefinition ContactTypeDefinition { get; init; }

    /// <summary>
    /// Gets the content types that can be created in the contact's list.
    /// </summary>
    public ContentTypeDefinition[] ContainedContentTypeDefinitions { get; init; } = [];

    /// <summary>
    /// Gets whether the list is ordered.
    /// </summary>
    public bool EnableOrdering { get; init; }

    /// <summary>
    /// Gets whether the list part shows its container's header.
    /// </summary>
    public bool ShowHeader { get; init; }
}
