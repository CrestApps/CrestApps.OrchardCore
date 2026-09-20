using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Projects Orchard Core content types carrying the omnichannel contact part into contact definitions.
/// </summary>
/// <remarks>
/// Contacts stay content types and content items in Orchard Core. Nothing here changes what is
/// stored; it only describes those types in the vocabulary the Omnichannel services work in.
/// </remarks>
public sealed class ContentTypeContactDefinitionProvider : IContactDefinitionProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTypeContactDefinitionProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public ContentTypeContactDefinitionProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Determines whether a content type is a contact type.
    /// </summary>
    /// <param name="contentTypeDefinition">The content type definition.</param>
    /// <returns><see langword="true"/> when the type carries the omnichannel contact part.</returns>
    public static bool IsContactType(ContentTypeDefinition contentTypeDefinition)
        => GetPart(contentTypeDefinition) is not null;

    /// <summary>
    /// Projects a content type definition into a contact definition.
    /// </summary>
    /// <param name="contentTypeDefinition">The content type definition.</param>
    /// <returns>The contact definition, or <see langword="null"/> when the type is not a contact type.</returns>
    public static ContactDefinition Project(ContentTypeDefinition contentTypeDefinition)
    {
        var part = GetPart(contentTypeDefinition);

        if (part is null)
        {
            return null;
        }

        var settings = part.GetSettings<OmnichannelContactPartSettings>();

        return new ContactDefinition
        {
            Name = contentTypeDefinition.Name,
            DisplayText = contentTypeDefinition.DisplayName,
            Settings = new ContactDefinitionSettings
            {
                RequireTimeZone = settings.RequireTimeZone,
                AutoDetectTimeZone = settings.AutoDetectTimeZone,
                UseDoNotCall = settings.UseDoNotCall,
                UseDoNotSms = settings.UseDoNotSms,
                UseDoNotEmail = settings.UseDoNotEmail,
            },
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<ContactDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        return contentTypes
            .Where(IsContactType)
            .OrderBy(contentType => contentType.DisplayName)
            .Select(Project)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<ContactDefinition> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Project(await _contentDefinitionManager.GetTypeDefinitionAsync(name));
    }

    /// <summary>
    /// Gets the omnichannel contact part of a content type, if it has one.
    /// </summary>
    /// <param name="contentTypeDefinition">The content type definition.</param>
    /// <returns>The part definition, or <see langword="null"/>.</returns>
    private static ContentTypePartDefinition GetPart(ContentTypeDefinition contentTypeDefinition)
        => contentTypeDefinition?.Parts.FirstOrDefault(part =>
            part.Name == OmnichannelConstants.ContentParts.OmnichannelContact);
}
