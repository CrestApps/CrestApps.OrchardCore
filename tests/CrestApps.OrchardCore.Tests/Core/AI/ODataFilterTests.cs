using CrestApps.OrchardCore.AI.Core.Search;

namespace CrestApps.OrchardCore.Tests.Core.AI;

/// <summary>
/// Bug: the Azure AI Search forks put filter values into OData filters unescaped, so a value holding a quote could end
/// the string literal and add its own clauses (filter injection). Every value now goes through one escaping helper.
/// </summary>
public sealed class ODataFilterTests
{
    [Fact]
    public void Equal_EscapesAQuoteInTheValue()
    {
        // Act
        var filter = ODataFilter.Equal("userId", "x' or userId ne 'y");

        // Assert
        Assert.Equal("userId eq 'x'' or userId ne ''y'", filter);
    }

    [Fact]
    public void Equal_WithANullValue_ComparesWithAnEmptyString()
    {
        Assert.Equal("userId eq ''", ODataFilter.Equal("userId", null));
    }

    [Fact]
    public void Quote_DoublesEveryQuote()
    {
        Assert.Equal("'O''Brien''s'", ODataFilter.Quote("O'Brien's"));
    }

    [Fact]
    public void DocumentsFilter_EscapesTheReferenceIdAndType()
    {
        // Act
        var filter = global::CrestApps.OrchardCore.AI.Documents.AzureAI.Services.AzureAISearchVectorSearchService.BuildFilter("doc' or 1 eq 1 or 'a", "type'");

        // Assert
        Assert.Equal("referenceId eq 'doc'' or 1 eq 1 or ''a' and referenceType eq 'type'''", filter);
    }

    [Fact]
    public void MemoryFilter_EscapesTheUserId()
    {
        // Act
        var filter = global::CrestApps.OrchardCore.AI.Memory.AzureAI.Services.AzureAISearchMemoryVectorSearchService.BuildFilter("user' or userId ne 'x");

        // Assert
        Assert.EndsWith("eq 'user'' or userId ne ''x'", filter);
    }

    [Fact]
    public void DataSourceFilter_EscapesTheDataSourceId()
    {
        // Act
        var filter = global::CrestApps.OrchardCore.AI.DataSources.AzureAI.Services.OrchardCoreAzureAISearchDataSourceContentManager.BuildODataFilter("source'", null);

        // Assert
        Assert.EndsWith("eq 'source'''", filter);
    }
}
