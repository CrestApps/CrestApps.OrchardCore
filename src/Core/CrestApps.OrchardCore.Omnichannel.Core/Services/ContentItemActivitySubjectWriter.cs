using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Reads and writes an activity's subject where Orchard Core keeps it: a content item on the activity.
/// </summary>
/// <remarks>
/// The adapter behind <see cref="IActivitySubjectWriter"/>. It is the only place left that knows an
/// activity's subject is a content item, which is what lets that carrier change later without
/// touching the services that record what a conversation learned.
/// </remarks>
public sealed class ContentItemActivitySubjectWriter : IActivitySubjectWriter
{
    private readonly IContentManager _contentManager;
    private readonly ISubjectDefinitionProvider _subjectDefinitionProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentItemActivitySubjectWriter"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager, used to build a subject the activity does not have yet.</param>
    /// <param name="subjectDefinitionProvider">The subject definitions, which say which fields may be written.</param>
    public ContentItemActivitySubjectWriter(
        IContentManager contentManager,
        ISubjectDefinitionProvider subjectDefinitionProvider)
    {
        _contentManager = contentManager;
        _subjectDefinitionProvider = subjectDefinitionProvider;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> ReadAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = await GetFieldsAsync(activity, cancellationToken);

        if (activity.Subject is null || fields.Count == 0)
        {
            return values;
        }

        // The activity carries the subject as the node the content item serializes to, so the part and
        // field shape the definitions describe is read straight off it.
        var content = activity.Subject;

        foreach (var field in fields)
        {
            if (!TrySplit(field.Name, out var part, out var name))
            {
                continue;
            }

            var current = (content[part]?[name]?["Text"])?.ToString();

            if (!string.IsNullOrWhiteSpace(current))
            {
                values[field.Name] = current;
            }
        }

        return values;
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(
        OmnichannelActivity activity,
        IReadOnlyDictionary<string, string> fieldValues,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        if (fieldValues is null || fieldValues.Count == 0)
        {
            return false;
        }

        var fields = await GetFieldsAsync(activity, cancellationToken);

        if (fields.Count == 0)
        {
            return false;
        }

        // Built rather than fetched, because an activity's subject is carried on the activity and may
        // not exist until the first thing worth recording arrives. It is materialized as a content item
        // to apply the fields, because that is what this host's subjects are, and written back as the
        // node the activity stores.
        var subject = activity.Subject is null
            ? await _contentManager.NewAsync(activity.SubjectContentType)
            : activity.Subject.Deserialize<ContentItem>(JOptions.Default);

        if (!ContentItemOmnichannelSubjectAccessor.ApplyFields(subject, fieldValues, fields))
        {
            return false;
        }

        activity.Subject = JsonSerializer.SerializeToNode(subject, JOptions.Default)?.AsObject();

        return true;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<string>> GetWritableFieldNamesAsync(OmnichannelActivity activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var fields = await GetFieldsAsync(activity, cancellationToken);

        return [.. fields.Select(field => field.Name)];
    }

    /// <summary>
    /// Splits a qualified field name into its part and field.
    /// </summary>
    /// <param name="name">The qualified name.</param>
    /// <param name="part">The part.</param>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when the name is qualified.</returns>
    private static bool TrySplit(string name, out string part, out string field)
    {
        var separator = name?.IndexOf('.', StringComparison.Ordinal) ?? -1;

        if (separator <= 0)
        {
            part = null;
            field = null;

            return false;
        }

        part = name[..separator];
        field = name[(separator + 1)..];

        return true;
    }

    /// <summary>
    /// Gets the fields the activity's kind of subject declares.
    /// </summary>
    /// <param name="activity">The activity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The fields, which may be empty.</returns>
    private async Task<IList<SubjectFieldDefinition>> GetFieldsAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(activity.SubjectContentType))
        {
            return [];
        }

        var definition = await _subjectDefinitionProvider.FindAsync(activity.SubjectContentType, cancellationToken);

        return definition?.Fields ?? [];
    }
}
