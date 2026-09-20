using System.Text.Json.Nodes;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Reads and writes Orchard Core subject content items.
/// </summary>
public sealed class ContentItemOmnichannelSubjectAccessor : IOmnichannelSubjectAccessor
{
    private readonly IContentManager _contentManager;
    private readonly ISubjectDefinitionProvider _subjectDefinitionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemOmnichannelSubjectAccessor"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager.</param>
    /// <param name="subjectDefinitionProvider">The subject definitions, which say which fields may be written.</param>
    public ContentItemOmnichannelSubjectAccessor(
        IContentManager contentManager,
        ISubjectDefinitionProvider subjectDefinitionProvider)
    {
        _contentManager = contentManager;
        _subjectDefinitionProvider = subjectDefinitionProvider;
    }

    /// <summary>
    /// Writes values into a subject content item's fields.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only keys matching a declared field are applied, by its qualified "Part.Field" name or by the
    /// bare field name, so a model is never able to author structure the tenant did not declare.
    /// </para>
    /// <para>
    /// The content is cast to the underlying <see cref="JsonObject"/> rather than going through the
    /// dynamic wrapper. Through the dynamic, each write handed back a wrapper that is never a
    /// <see cref="JsonObject"/>, so every field created a fresh part object that clobbered the one
    /// before it and only the last value survived.
    /// </para>
    /// </remarks>
    /// <param name="contentItem">The subject content item.</param>
    /// <param name="fieldValues">The values to write.</param>
    /// <param name="fields">The fields that may be written.</param>
    /// <returns><see langword="true"/> when the subject changed.</returns>
    public static bool ApplyFields(
        ContentItem contentItem,
        IReadOnlyDictionary<string, string> fieldValues,
        IEnumerable<SubjectFieldDefinition> fields)
    {
        if (contentItem is null || fieldValues is null || fieldValues.Count == 0 || fields is null)
        {
            return false;
        }

        var changed = false;
        var content = (JsonObject)contentItem.Content;

        foreach (var field in fields)
        {
            var separator = field.Name?.IndexOf('.', StringComparison.Ordinal) ?? -1;

            if (separator <= 0)
            {
                continue;
            }

            var part = field.Name[..separator];
            var name = field.Name[(separator + 1)..];

            if (!(fieldValues.TryGetValue(field.Name, out var value) || fieldValues.TryGetValue(name, out value)) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (content[part] is not JsonObject partObject)
            {
                partObject = new JsonObject();
                content[part] = partObject;
            }

            partObject[name] = new JsonObject { ["Text"] = value.Trim() };
            changed = true;
        }

        return changed;
    }

    /// <inheritdoc/>
    public async Task<OmnichannelSubject> GetAsync(string subjectId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(subjectId))
        {
            return null;
        }

        var contentItem = await _contentManager.GetAsync(subjectId, VersionOptions.Latest);

        return Project(contentItem);
    }

    /// <inheritdoc/>
    public async Task<OmnichannelSubject> CreateAsync(
        string definitionName,
        string contactId,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definitionName);

        var definition = await _subjectDefinitionProvider.FindAsync(definitionName, cancellationToken)
            ?? throw new InvalidOperationException($"There is no subject definition named '{definitionName}'.");

        var contentItem = await _contentManager.NewAsync(definitionName);

        ApplyFields(contentItem, fieldValues, definition.Fields);

        await _contentManager.CreateAsync(contentItem);

        var subject = Project(contentItem);
        subject.ContactId = contactId;

        return subject;
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(
        string subjectId,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(subjectId) || fieldValues is null || fieldValues.Count == 0)
        {
            return false;
        }

        var contentItem = await _contentManager.GetAsync(subjectId, VersionOptions.Latest);

        if (contentItem is null)
        {
            return false;
        }

        var definition = await _subjectDefinitionProvider.FindAsync(contentItem.ContentType, cancellationToken);

        if (definition is null || !ApplyFields(contentItem, fieldValues, definition.Fields))
        {
            return false;
        }

        await _contentManager.UpdateAsync(contentItem);

        return true;
    }

    /// <summary>
    /// Projects a subject content item.
    /// </summary>
    /// <param name="contentItem">The content item, or <see langword="null"/>.</param>
    /// <returns>The subject, or <see langword="null"/>.</returns>
    private static OmnichannelSubject Project(ContentItem contentItem)
    {
        if (contentItem is null)
        {
            return null;
        }

        return new OmnichannelSubject
        {
            Id = contentItem.ContentItemId,
            DefinitionName = contentItem.ContentType,
            DisplayText = contentItem.DisplayText,
            Fields = (JsonObject)contentItem.Content,
        };
    }
}
