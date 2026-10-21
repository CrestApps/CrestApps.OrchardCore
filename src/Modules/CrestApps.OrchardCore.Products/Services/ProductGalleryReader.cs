using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.Core.Listing;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Finds the images a product shows: the first media field of its type that has images.
/// </summary>
public static class ProductGalleryReader
{
    public static ProductGalleryField Find(ContentTypeDefinition definition, ContentItem contentItem)
    {
        if (definition is null || contentItem is null)
        {
            return null;
        }

        // Content is a dynamic wrapper over the item's JSON; it converts to the JsonObject only through a cast.
        var content = (JsonObject)contentItem.Content;

        foreach (var part in definition.Parts)
        {
            foreach (var field in part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == "MediaField"))
            {
                if (content[part.Name]?[field.Name] is not JsonObject media)
                {
                    continue;
                }

                var paths = FieldValueReader.GetStrings(media, "Paths").Where(path => !string.IsNullOrEmpty(path)).ToArray();

                if (paths.Length > 0)
                {
                    return new ProductGalleryField(part.Name, field.Name, paths, FieldValueReader.GetStrings(media, "MediaTexts").ToArray());
                }
            }
        }

        return null;
    }
}

/// <summary>
/// The media field a product's images come from, with the media library paths of its images and their texts.
/// </summary>
public sealed record ProductGalleryField(string PartName, string FieldName, IReadOnlyList<string> Paths, IReadOnlyList<string> Texts);
