using CrestApps.OrchardCore.AI.Agent.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes;

namespace CrestApps.OrchardCore.Tests.Agent.ContentTypes;

/// <summary>
/// Keeps content type and part definitions in memory for the content definition tool tests. The definition manager
/// reads and stores them; the definition service attaches parts and adds fields to them as the real one does, and
/// records every other call for the test to verify.
/// </summary>
internal sealed class ContentDefinitionTestStore
{
    private readonly Dictionary<string, ContentTypeDefinition> _types = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ContentPartDefinition> _parts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDefinitionTestStore"/> class.
    /// </summary>
    public ContentDefinitionTestStore()
    {
        Manager.Setup(manager => manager.LoadTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync((string name) => _types.GetValueOrDefault(name));
        Manager.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync((string name) => _types.GetValueOrDefault(name));
        Manager.Setup(manager => manager.LoadPartDefinitionAsync(It.IsAny<string>())).ReturnsAsync((string name) => _parts.GetValueOrDefault(name));
        Manager.Setup(manager => manager.GetPartDefinitionAsync(It.IsAny<string>())).ReturnsAsync((string name) => _parts.GetValueOrDefault(name));
        Manager.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(() => _types.Values.ToList());
        Manager.Setup(manager => manager.ListPartDefinitionsAsync()).ReturnsAsync(() => _parts.Values.ToList());
        Manager
            .Setup(manager => manager.StoreTypeDefinitionAsync(It.IsAny<ContentTypeDefinition>()))
            .Callback((ContentTypeDefinition definition) => _types[definition.Name] = definition)
            .Returns(Task.CompletedTask);
        Manager
            .Setup(manager => manager.StorePartDefinitionAsync(It.IsAny<ContentPartDefinition>()))
            .Callback((ContentPartDefinition definition) => _parts[definition.Name] = definition)
            .Returns(Task.CompletedTask);

        Service
            .Setup(service => service.AddPartToTypeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string partName, string typeName) => Manager.Object.AlterTypeDefinitionAsync(typeName, type => type.WithPart(partName, _parts[partName], _ => { })));
        Service
            .Setup(service => service.AddReusablePartToTypeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string name, string displayName, string description, string partName, string typeName) =>
                Manager.Object.AlterTypeDefinitionAsync(typeName, type => type.WithPart(name, _parts[partName], part =>
                {
                    part.WithDisplayName(displayName);
                    part.WithDescription(description);
                })));
        Service
            .Setup(service => service.AddFieldToPartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string fieldName, string displayName, string fieldTypeName, string partName) =>
                Manager.Object.AlterPartDefinitionAsync(partName, part => part.WithField(fieldName, field => field.OfType(fieldTypeName).WithDisplayName(displayName))));
        Service
            .Setup(service => service.GenerateFieldNameFromDisplayNameAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string _, string displayName) => displayName.Replace(" ", string.Empty, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the content definition manager double.
    /// </summary>
    public Mock<IContentDefinitionManager> Manager { get; } = new();

    /// <summary>
    /// Gets the content definition service double.
    /// </summary>
    public Mock<IContentDefinitionService> Service { get; } = new();

    /// <summary>
    /// Stores a content part definition.
    /// </summary>
    /// <param name="name">The name of the part.</param>
    /// <param name="configure">Configures the part.</param>
    /// <returns>The stored definition.</returns>
    public ContentPartDefinition AddPart(string name, Action<ContentPartDefinitionBuilder> configure = null)
    {
        var builder = new ContentPartDefinitionBuilder().Named(name);

        configure?.Invoke(builder);

        return _parts[name] = builder.Build();
    }

    /// <summary>
    /// Stores a content type definition.
    /// </summary>
    /// <param name="name">The name of the content type.</param>
    /// <param name="configure">Configures the content type.</param>
    /// <returns>The stored definition.</returns>
    public ContentTypeDefinition AddType(string name, Action<ContentTypeDefinitionBuilder> configure = null)
    {
        var builder = new ContentTypeDefinitionBuilder().WithName(name).WithDisplayName(name);

        configure?.Invoke(builder);

        return _types[name] = builder.Build();
    }

    /// <summary>
    /// Gets a stored content type definition.
    /// </summary>
    /// <param name="name">The name of the content type.</param>
    /// <returns>The definition.</returns>
    public ContentTypeDefinition TypeNamed(string name)
        => _types[name];

    /// <summary>
    /// Gets a stored content part definition.
    /// </summary>
    /// <param name="name">The name of the part.</param>
    /// <returns>The definition.</returns>
    public ContentPartDefinition PartNamed(string name)
        => _parts[name];

    /// <summary>
    /// Builds the services of a tool call for a caller who holds every permission.
    /// </summary>
    /// <param name="fieldTypes">The content field types the site has.</param>
    /// <returns>The services.</returns>
    public ServiceProvider BuildServices(params ContentField[] fieldTypes)
    {
        var services = AgentToolTestContext.CreateServices();

        services.AddOptions();
        services.AddSingleton(Manager.Object);
        services.AddSingleton(Service.Object);
        services.AddScoped<ContentMetadataService>();

        foreach (var fieldType in fieldTypes)
        {
            services.AddSingleton(fieldType);
        }

        return services.BuildServiceProvider();
    }
}
