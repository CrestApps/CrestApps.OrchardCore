using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Omnichannel.Core.Services;

/// <summary>
/// Writes back what an automated conversation learned: the fields on the subject, and an email address the
/// person gave for follow-up.
/// <para>
/// This is the same job whether the conversation happened over text or over the phone, and it was written twice —
/// verbatim, in the SMS handler and in the voice handler. Both copies carry fixes that were found the hard way
/// (writing into a field's real <c>Text</c> structure rather than merging a model-authored content item, and
/// upserting the email rather than appending a duplicate), and a fix applied to one of two copies is a fix that
/// silently does not apply to half the product.
/// </para>
/// </summary>
public static class OmnichannelSubjectWriter
{
    /// <summary>
    /// Makes an email address the person gave the contact's email address, replacing the one it had. Returns whether
    /// the contact changed.
    /// </summary>
    /// <remarks>
    /// The write goes through the contact type's writer: into the contact-methods bag, as a properly structured
    /// email item, or to the type's own fields when it keeps its contact methods there. A type that registered a
    /// provider of its own and no writer is left as it is.
    /// </remarks>
    /// <param name="contactMethodsWriter">The writer of the contact's contact methods.</param>
    /// <param name="contact">The contact content item.</param>
    /// <param name="email">The address the person gave.</param>
    public static Task<bool> TryApplyContactEmailAsync(IContactMethodsWriter contactMethodsWriter, ContentItem contact, string email)
    {
        ArgumentNullException.ThrowIfNull(contactMethodsWriter);

        if (contact is null || string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult(false);
        }

        email = email.Trim();

        // A conservative sanity check, so a mis-heard or mis-parsed phrase is never written as somebody's email.
        if (email.Length < 5 || !email.Contains('@', StringComparison.Ordinal) || email.Contains(' ', StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        return contactMethodsWriter.TrySetEmailAddressAsync(contact, email);
    }

    /// <summary>
    /// Lists the text fields declared on a content type, as (part, field) pairs. These are the only fields an
    /// automated conversation may set, so the model is never asked to author arbitrary structure.
    /// </summary>
    /// <param name="typeDefinition">The subject's content type definition.</param>
    public static List<(string Part, string Field)> GetSubjectTextFields(ContentTypeDefinition typeDefinition)
    {
        var fields = new List<(string, string)>();

        if (typeDefinition is null)
        {
            return fields;
        }

        foreach (var part in typeDefinition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                if (string.Equals(field.FieldDefinition?.Name, nameof(TextField), StringComparison.Ordinal))
                {
                    fields.Add((part.Name, field.Name));
                }
            }
        }

        return fields;
    }

    /// <summary>
    /// Writes model-provided values into the subject's text fields, using each field's real <c>Text</c>
    /// structure. Only keys matching a known field (by "Part.Field" or by bare field name) with a non-empty
    /// value are applied. Returns whether the subject changed.
    /// </summary>
    /// <param name="subject">The subject content item.</param>
    /// <param name="values">The values the model returned.</param>
    /// <param name="fields">The fields it was allowed to set.</param>
    public static bool ApplySubjectFields(ContentItem subject, IDictionary<string, string> values, List<(string Part, string Field)> fields)
    {
        if (subject is null || values is null || values.Count == 0 || fields.Count == 0)
        {
            return false;
        }

        var changed = false;

        // ContentItem.Content is a dynamic JsonDynamicObject; cast to the underlying JsonObject so type checks and
        // writes operate on the real node. Going through the dynamic hands back a wrapper that is never a
        // JsonObject, which made each field create a fresh part object that clobbered the previous ones.
        var content = (JsonObject)subject.Content;

        foreach (var (part, field) in fields)
        {
            if (!(values.TryGetValue($"{part}.{field}", out var value) || values.TryGetValue(field, out value)) ||
                string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (content[part] is not JsonObject partObject)
            {
                partObject = new JsonObject();
                content[part] = partObject;
            }

            partObject[field] = new JsonObject { ["Text"] = value.Trim() };
            changed = true;
        }

        return changed;
    }
}
