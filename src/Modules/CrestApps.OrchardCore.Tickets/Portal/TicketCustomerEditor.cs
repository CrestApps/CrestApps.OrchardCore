using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// The customer's editor of a ticket type in the portal: the subject (the title) and the fields marked
/// <see cref="TicketFieldCustomerSettings.CustomerEditable"/>, nothing else. Other editors are hidden while it is built
/// (<see cref="TicketCustomerPlacementProvider"/>), and after the form is read every other part and field is put back
/// as a new item has it, so a value posted for them is dropped.
/// </summary>
public static class TicketCustomerEditor
{
    private const string ItemKey = "Tickets.CustomerEditor";

    /// <summary>
    /// Marks the request as building the customer's editor.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    public static void Begin(HttpContext httpContext)
        => httpContext.Items[ItemKey] = true;

    /// <summary>
    /// Gets whether the request builds the customer's editor.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>Whether it does.</returns>
    public static bool IsActive(HttpContext httpContext)
        => httpContext?.Items.ContainsKey(ItemKey) == true;

    /// <summary>
    /// The shapes customers see: the title part's and the editable fields' (by their placement differentiator,
    /// <c>{part}-{field}</c>), and every part and field of the type, to hide the rest.
    /// </summary>
    /// <param name="type">The ticket type.</param>
    /// <returns>The shapes shown, and all the type's shapes.</returns>
    public static (HashSet<string> Shown, HashSet<string> All) Shapes(ContentTypeDefinition type)
    {
        var shown = new HashSet<string>(StringComparer.Ordinal) { nameof(TitlePart) };
        var all = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in type?.Parts ?? [])
        {
            all.Add(part.Name);

            foreach (var field in part.PartDefinition.Fields)
            {
                var differentiator = $"{part.Name}-{field.Name}";
                all.Add(differentiator);

                if (field.GetSettings<TicketFieldCustomerSettings>().CustomerEditable)
                {
                    shown.Add(differentiator);
                }
            }
        }

        return (shown, all);
    }

    /// <summary>
    /// The fields of a ticket type customers don't fill in, as <c>{TypePartName}.{FieldName}</c>.
    /// </summary>
    /// <param name="type">The ticket type.</param>
    /// <returns>The fields.</returns>
    public static IEnumerable<string> HiddenFields(ContentTypeDefinition type)
    {
        foreach (var part in type?.Parts ?? [])
        {
            foreach (var field in part.PartDefinition.Fields)
            {
                if (!field.GetSettings<TicketFieldCustomerSettings>().CustomerEditable)
                {
                    yield return $"{part.Name}.{field.Name}";
                }
            }
        }
    }

    /// <summary>
    /// Puts back, from a new item, every part and field customers don't fill in.
    /// </summary>
    /// <param name="edited">The item the customer's form was read into.</param>
    /// <param name="fresh">A new item of the same type.</param>
    /// <param name="type">The ticket type.</param>
    public static void KeepOnlyCustomerValues(ContentItem edited, ContentItem fresh, ContentTypeDefinition type)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(fresh);

        foreach (var part in type?.Parts ?? [])
        {
            if (part.Name == nameof(TitlePart))
            {
                continue;
            }

            var editable = part.PartDefinition.Fields
                .Where(field => field.GetSettings<TicketFieldCustomerSettings>().CustomerEditable)
                .Select(field => field.Name)
                .ToHashSet(StringComparer.Ordinal);
            var original = ((JsonObject)fresh.Content)[part.Name] as JsonObject;

            if (editable.Count == 0)
            {
                Restore((JsonObject)edited.Content, part.Name, original);

                continue;
            }

            // A part with editable fields keeps only those; its other fields and values are put back.
            var current = ((JsonObject)edited.Content)[part.Name] as JsonObject;
            var kept = new JsonObject();

            foreach (var (key, value) in original ?? [])
            {
                kept[key] = value?.DeepClone();
            }

            foreach (var name in editable)
            {
                kept[name] = current?[name]?.DeepClone();
            }

            ((JsonObject)edited.Content)[part.Name] = kept;
        }
    }

    private static void Restore(JsonObject data, string name, JsonObject original)
    {
        if (original is null)
        {
            data.Remove(name);
        }
        else
        {
            data[name] = original.DeepClone();
        }
    }
}
