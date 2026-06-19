using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.Recipes.Core;
using Json.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Agent.Contents;

public sealed class GetContentItemSchemaToolTests
{
    [Fact]
    public void Description_ShouldDirectImmediateUseBeforeCreateOrUpdate()
    {
        var tool = new GetContentItemSchemaTool();

        Assert.Contains("immediately before", tool.Description, StringComparison.Ordinal);
        Assert.Contains("createOrUpdateContentItem", tool.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokeAsync_WithMultipleContentTypes_ShouldReturnSchemaForEachType()
    {
        // Arrange
        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(x => x.GetTypeDefinitionAsync("LandingPage"))
            .ReturnsAsync(new ContentTypeDefinition("LandingPage", "LandingPage"));
        contentDefinitionManager
            .Setup(x => x.GetTypeDefinitionAsync("HeroWidget"))
            .ReturnsAsync(new ContentTypeDefinition("HeroWidget", "HeroWidget"));

        var contentItemSchemaService = new Mock<IContentItemSchemaService>();
        contentItemSchemaService
            .Setup(x => x.GetSchemaAsync("LandingPage", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Const("LandingPage"))));
        contentItemSchemaService
            .Setup(x => x.GetSchemaAsync("HeroWidget", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(("ContentType", new JsonSchemaBuilder().Const("HeroWidget"))));

        var services = new ServiceCollection()
            .AddSingleton<ILogger<GetContentItemSchemaTool>>(NullLogger<GetContentItemSchemaTool>.Instance)
            .AddSingleton(contentDefinitionManager.Object)
            .AddSingleton(contentItemSchemaService.Object)
            .BuildServiceProvider();
        var arguments = new AIFunctionArguments(new Dictionary<string, object>
        {
            ["contentTypes"] = JsonSerializer.Deserialize<JsonElement>("""["LandingPage","HeroWidget"]"""),
        })
        {
            Services = services,
        };
        var tool = new GetContentItemSchemaTool();

        // Act
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert
        var text = Assert.IsType<string>(result);
        Assert.Contains("\"LandingPage\"", text, StringComparison.Ordinal);
        Assert.Contains("\"HeroWidget\"", text, StringComparison.Ordinal);
        Assert.Contains("\"const\":\"LandingPage\"", text, StringComparison.Ordinal);
        Assert.Contains("\"const\":\"HeroWidget\"", text, StringComparison.Ordinal);
    }
}
