using CrestApps.OrchardCore.Omnichannel.Models;
using CrestApps.OrchardCore.Omnichannel.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Projects Orchard Core content types carrying the omnichannel subject part into subject definitions.
/// </summary>
/// <remarks>
/// Subjects stay content types and content items in Orchard Core. Nothing here changes what is
/// stored; it only describes those types in the vocabulary the Omnichannel services work in.
/// </remarks>
public sealed class ContentTypeSubjectDefinitionProvider : ISubjectDefinitionProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentTypeSubjectDefinitionProvider"/> class.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public ContentTypeSubjectDefinitionProvider(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    /// <summary>
    /// Determines whether a content type is a subject type.
    /// </summary>
    /// <param name="contentTypeDefinition">The content type definition.</param>
    /// <returns><see langword="true"/> when the type carries the omnichannel subject part.</returns>
    public static bool IsSubjectType(ContentTypeDefinition contentTypeDefinition)
        => contentTypeDefinition?.Parts.Any(part =>
            part.Name == OmnichannelConstants.ContentParts.OmnichannelSubject) == true;

    /// <summary>
    /// Projects a content type definition into a subject definition.
    /// </summary>
    /// <param name="contentTypeDefinition">The content type definition.</param>
    /// <returns>The subject definition, or <see langword="null"/> when the type is not a subject type.</returns>
    public static SubjectDefinition Project(ContentTypeDefinition contentTypeDefinition)
    {
        if (!IsSubjectType(contentTypeDefinition))
        {
            return null;
        }

        return new SubjectDefinition
        {
            Name = contentTypeDefinition.Name,
            DisplayText = contentTypeDefinition.DisplayName,
            Fields = ProjectFields(contentTypeDefinition),
        };
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<SubjectDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var contentTypes = await _contentDefinitionManager.ListTypeDefinitionsAsync();

        return contentTypes
            .Where(IsSubjectType)
            .OrderBy(contentType => contentType.DisplayName)
            .Select(Project)
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<SubjectDefinition> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Project(await _contentDefinitionManager.GetTypeDefinitionAsync(name));
    }

    /// <summary>
    /// Lists the fields an automated conversation is allowed to set on a subject of this type.
    /// </summary>
    /// <remarks>
    /// Only text fields, which is what has always been offered: the model is handed a list of fields
    /// rather than being asked to author arbitrary structure, so a mis-heard phrase cannot end up in
    /// a part the tenant never meant to expose. The name is qualified with the part, because two parts
    /// on the same type can declare a field of the same name.
    /// </remarks>
    /// <param name="contentTypeDefinition">The content type definition.</param>
    /// <returns>The fields.</returns>
    private static List<SubjectFieldDefinition> ProjectFields(ContentTypeDefinition contentTypeDefinition)
    {
        var fields = new List<SubjectFieldDefinition>();

        foreach (var part in contentTypeDefinition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                if (!string.Equals(field.FieldDefinition?.Name, nameof(TextField), StringComparison.Ordinal))
                {
                    continue;
                }

                fields.Add(new SubjectFieldDefinition
                {
                    Name = $"{part.Name}.{field.Name}",
                    DisplayText = field.DisplayName(),
                    Type = SubjectFieldType.Text,
                    AllowAIUpdate = true,
                });
            }
        }

        return fields;
    }
}
