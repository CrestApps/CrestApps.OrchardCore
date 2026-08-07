using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.UrlRewriting.Sources;
using CrestApps.OrchardCore.Recipes.Core.Services;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class RewriteRuleSchemaServiceTests
{
    [Fact]
    public async Task GetSourceDescriptorsAsync_IncludesEveryRegisteredSource()
    {
        var service = CreateService();

        var descriptors = await service.GetSourceDescriptorsAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, descriptors.Count);
        Assert.Contains(descriptors, descriptor => descriptor.Name == "Redirect");
        Assert.Contains(descriptors, descriptor => descriptor.Name == "Rewrite");
    }

    [Fact]
    public async Task GetRuleSchemaAsync_CachesResult()
    {
        var service = CreateService();

        var first = await service.GetRuleSchemaAsync(TestContext.Current.CancellationToken);
        var second = await service.GetRuleSchemaAsync(TestContext.Current.CancellationToken);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetRuleSchemaAsync_ValidatesARedirectRule()
    {
        // Arrange
        var schema = await CreateRuleSchemaAsync();

        var json = """
        {
          "Source": "Redirect",
          "Name": "Old blog",
          "Order": 1,
          "Pattern": "^old-blog/(.*)$",
          "SubstitutionPattern": "/blog/$1",
          "IsCaseInsensitive": true,
          "QueryStringPolicy": "Append",
          "RedirectType": "MovedPermanently"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetRuleSchemaAsync_ValidatesARewriteRule()
    {
        // Arrange
        var schema = await CreateRuleSchemaAsync();

        var json = """
        {
          "Source": "Rewrite",
          "Name": "Api",
          "Pattern": "^api/(.*)$",
          "SubstitutionPattern": "/services/$1",
          "SkipFurtherRules": true
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetRuleSchemaAsync_RejectsARedirectRuleWithAnInvalidRedirectType()
    {
        // Arrange
        var schema = await CreateRuleSchemaAsync();

        var json = """
        {
          "Source": "Redirect",
          "Pattern": "^old/(.*)$",
          "SubstitutionPattern": "/new/$1",
          "RedirectType": "SeeOther"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetRuleSchemaAsync_AcceptsAnUnknownCustomSource()
    {
        // Arrange
        var schema = await CreateRuleSchemaAsync();

        var json = """
        {
          "Source": "GeoRedirect",
          "Name": "Country",
          "Country": "CA"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GetRuleSchemaAsync_RejectsARuleWithoutASource()
    {
        // Arrange
        var schema = await CreateRuleSchemaAsync();

        var json = """
        {
          "Name": "Old blog",
          "Pattern": "^old/(.*)$"
        }
        """;

        // Act
        var result = Evaluate(schema, json);

        // Assert
        Assert.False(result);
    }

    private static async Task<JsonSchema> CreateRuleSchemaAsync()
    {
        var builder = await CreateService().GetRuleSchemaAsync(TestContext.Current.CancellationToken);

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

    private static RewriteRuleSchemaService CreateService()
    {
        var sources = new IRewriteRuleSourceSchemaDefinition[]
        {
            new UrlRedirectRuleSourceSchema(),
            new UrlRewriteRuleSourceSchema(),
        };

        return new RewriteRuleSchemaService(sources);
    }
}
