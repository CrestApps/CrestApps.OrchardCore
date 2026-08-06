using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Conditions;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Rules.Operators;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class RuleSchemaServiceTests
{
    [Fact]
    public async Task GetConditionDescriptorsAsync_IncludesEveryRegisteredCondition()
    {
        var service = CreateService();

        var descriptors = await service.GetConditionDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(descriptors, descriptor => descriptor.Name == "UrlCondition");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "AnyConditionGroup" && descriptor.IsGroup);
        Assert.Contains(descriptors, descriptor => descriptor.Name == "AllConditionGroup" && descriptor.IsGroup);
    }

    [Fact]
    public async Task GetOperatorDescriptorsAsync_IncludesEveryRegisteredOperator()
    {
        var service = CreateService();

        var descriptors = await service.GetOperatorDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(descriptors, descriptor => descriptor.Name == "StringStartsWithOperator");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "StringEqualsOperator");
        Assert.Equal(8, descriptors.Count);
    }

    [Fact]
    public async Task GetLayerRuleSchemaAsync_CachesResult()
    {
        var service = CreateService();

        var first = await service.GetLayerRuleSchemaAsync(TestContext.Current.CancellationToken);
        var second = await service.GetLayerRuleSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetLayerRuleSchemaAsync_ValidatesAFullRule()
    {
        // Arrange
        var schema = await CreateLayerRuleSchemaAsync();

        var json = """
        {
          "Name": null,
          "ConditionId": "4jm7m68aj30g0rczdne1c4b1cp",
          "Conditions": [
            {
              "$type": "OrchardCore.Rules.Models.AnyConditionGroup, OrchardCore.Rules",
              "DisplayText": null,
              "Name": "AnyConditionGroup",
              "ConditionId": "49kzd1py7h3vnx29608cmmta5a",
              "Conditions": [
                {
                  "$type": "OrchardCore.Rules.Models.UrlCondition, OrchardCore.Rules",
                  "Value": "/knowledge-base/category/",
                  "Operation": {
                    "$type": "OrchardCore.Rules.Models.StringStartsWithOperator, OrchardCore.Rules",
                    "CaseSensitive": false
                  },
                  "Name": "UrlCondition",
                  "ConditionId": "4q7ayd91z69gzv8qzta07rtpm0"
                },
                {
                  "$type": "OrchardCore.Rules.Models.ContentTypeCondition, OrchardCore.Rules",
                  "Value": "KnowledgeBaseArticle",
                  "Operation": {
                    "$type": "OrchardCore.Rules.Models.StringEqualsOperator, OrchardCore.Rules",
                    "CaseSensitive": false
                  },
                  "Name": "ContentTypeCondition",
                  "ConditionId": "4bee24643tz0nsgwn948venadz"
                }
              ]
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetLayerRuleSchemaAsync_AcceptsAnUnknownCustomCondition()
    {
        // Arrange
        var schema = await CreateLayerRuleSchemaAsync();

        var json = """
        {
          "Conditions": [
            {
              "$type": "My.Custom.WeatherCondition, My.Custom",
              "Name": "WeatherCondition",
              "Temperature": 25
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetLayerRuleSchemaAsync_RejectsAConditionWithoutADiscriminator()
    {
        // Arrange
        var schema = await CreateLayerRuleSchemaAsync();

        var json = """
        {
          "Conditions": [
            {
              "Name": "HomepageCondition",
              "Value": true
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetLayerRuleSchemaAsync_ValidatesNestedConditionGroups()
    {
        // Arrange
        var schema = await CreateLayerRuleSchemaAsync();

        var json = """
        {
          "Conditions": [
            {
              "$type": "OrchardCore.Rules.Models.AnyConditionGroup, OrchardCore.Rules",
              "Name": "AnyConditionGroup",
              "Conditions": [
                {
                  "$type": "OrchardCore.Rules.Models.AllConditionGroup, OrchardCore.Rules",
                  "Name": "AllConditionGroup",
                  "Conditions": [
                    {
                      "$type": "OrchardCore.Rules.Models.HomepageCondition, OrchardCore.Rules",
                      "Name": "HomepageCondition",
                      "Value": true
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    private static async Task<JsonSchema> CreateLayerRuleSchemaAsync()
    {
        var builder = await CreateService().GetLayerRuleSchemaAsync(TestContext.Current.CancellationToken);

        return builder.Build();
    }

    private static bool Evaluate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);

        var result = schema.Evaluate(document.RootElement, new EvaluationOptions
        {
            OutputFormat = OutputFormat.Flag,
        });

        return result.IsValid;
    }

    private static RuleSchemaService CreateService()
    {
        var conditions = new IRuleConditionSchemaDefinition[]
        {
            new AllConditionGroupSchema(),
            new AnyConditionGroupSchema(),
            new BooleanConditionSchema(),
            new ContentTypeConditionSchema(),
            new CultureConditionSchema(),
            new HomepageConditionSchema(),
            new IsAnonymousConditionSchema(),
            new IsAuthenticatedConditionSchema(),
            new JavascriptConditionSchema(),
            new RoleConditionSchema(),
            new UrlConditionSchema(),
        };

        var operators = new IRuleConditionOperatorSchemaDefinition[]
        {
            new StringContainsOperatorSchema(),
            new StringEndsWithOperatorSchema(),
            new StringEqualsOperatorSchema(),
            new StringNotContainsOperatorSchema(),
            new StringNotEndsWithOperatorSchema(),
            new StringNotEqualsOperatorSchema(),
            new StringNotStartsWithOperatorSchema(),
            new StringStartsWithOperatorSchema(),
        };

        return new RuleSchemaService(conditions, operators);
    }
}
