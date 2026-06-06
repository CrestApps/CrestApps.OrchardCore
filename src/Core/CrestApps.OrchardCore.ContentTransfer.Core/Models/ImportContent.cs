
using Microsoft.AspNetCore.Http;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.ContentTransfer.Models;

/// <summary>
/// Represents the import content model used by display drivers to collect import options.
/// </summary>
public sealed class ImportContent : Entity
{
    /// <summary>
    /// Gets or sets the content type name.
    /// </summary>
    public string ContentTypeName { get; set; }

    /// <summary>
    /// Gets or sets the content type identifier.
    /// </summary>
    public string ContentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the uploaded file.
    /// </summary>
    public IFormFile File { get; set; }

    /// <summary>
    /// Copies all collected properties from this instance to the specified target entity.
    /// </summary>
    /// <param name="target">The target entity to copy properties to.</param>
    public void CopyPropertiesTo(Entity target)
    {
        if (Properties == null)
        {
            return;
        }

        foreach (var property in Properties)
        {
            target.Properties[property.Key] = property.Value?.DeepClone();
        }
    }
}
