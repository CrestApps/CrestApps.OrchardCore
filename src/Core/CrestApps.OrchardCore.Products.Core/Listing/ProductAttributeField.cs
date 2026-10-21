using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Products.Core.Listing;

/// <summary>
/// A field a provider can read, with the type part it belongs to.
/// </summary>
/// <param name="Part">The content type part that holds the field.</param>
/// <param name="Field">The field definition.</param>
/// <param name="Provider">The provider that reads the field.</param>
public sealed record ProductAttributeField(ContentTypePartDefinition Part, ContentPartFieldDefinition Field, IProductAttributeValueProvider Provider);
