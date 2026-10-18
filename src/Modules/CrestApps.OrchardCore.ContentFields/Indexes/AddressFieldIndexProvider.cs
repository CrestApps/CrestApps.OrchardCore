using CrestApps.OrchardCore.ContentFields.Fields;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentFields.Indexing.SQL;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.ContentFields.Indexes;

public sealed class AddressFieldIndexProvider : ContentFieldIndexProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<string> _ignoredTypes = [];

    private IContentDefinitionManager _contentDefinitionManager;

    public AddressFieldIndexProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<AddressFieldIndex>()
            .Map(async contentItem =>
            {
                if ((!contentItem.Published && !contentItem.Latest) || _ignoredTypes.Contains(contentItem.ContentType))
                {
                    return null;
                }

                // Resolved on first use, as Orchard Core's own field index providers do.
                _contentDefinitionManager ??= _serviceProvider.GetRequiredService<IContentDefinitionManager>();

                var typeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
                var fieldDefinitions = typeDefinition?.Parts
                    .SelectMany(part => part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == nameof(AddressField)))
                    .ToArray();

                if (fieldDefinitions is not { Length: > 0 })
                {
                    _ignoredTypes.Add(contentItem.ContentType);

                    return null;
                }

                return fieldDefinitions
                    .GetContentFields<AddressField>(contentItem)
                    .Select(pair => new AddressFieldIndex
                    {
                        Latest = contentItem.Latest,
                        Published = contentItem.Published,
                        ContentItemId = contentItem.ContentItemId,
                        ContentItemVersionId = contentItem.ContentItemVersionId,
                        ContentType = contentItem.ContentType,
                        ContentPart = pair.Definition.ContentTypePartDefinition.Name,
                        ContentField = pair.Definition.Name,
                        Country = Truncate(pair.Field.Address?.Country, 2),
                        Region = Truncate(pair.Field.Address?.Region, 100),
                        City = Truncate(pair.Field.Address?.City, 255),
                        PostalCode = Truncate(pair.Field.Address?.PostalCode, 50),
                    });
            });
    }

    private static string Truncate(string value, int length)
        => value is null || value.Length <= length ? value : value[..length];
}
