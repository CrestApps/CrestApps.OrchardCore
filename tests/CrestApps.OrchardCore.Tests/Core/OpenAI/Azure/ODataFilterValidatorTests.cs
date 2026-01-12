using CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

namespace CrestApps.OrchardCore.Tests.Core.OpenAI.Azure;

public sealed class ODataFilterValidatorTests
{
    private readonly ODataFilterValidator _validator;

    public ODataFilterValidatorTests()
    {
        _validator = new ODataFilterValidator();
    }

    [Fact]
    public void IsValid_WhenFilterIsNull_ShouldReturnTrue()
    {
        // Act
        var result = _validator.IsValid(null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_WhenFilterIsEmpty_ShouldReturnTrue()
    {
        // Act
        var result = _validator.IsValid("");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValid_WhenFilterIsWhitespace_ShouldReturnTrue()
    {
        // Act
        var result = _validator.IsValid("   ");

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("category eq 'documentation'")]
    [InlineData("status ne 'archived'")]
    [InlineData("priority gt 5")]
    [InlineData("rating ge 4.5")]
    [InlineData("age lt 30")]
    [InlineData("score le 100")]
    public void IsValid_WhenFilterHasSingleOperator_ShouldReturnTrue(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.True(result, $"Filter '{filter}' should be valid");
    }

    [Theory]
    [InlineData("category eq 'docs' and status eq 'published'")]
    [InlineData("status eq 'active' or status eq 'pending'")]
    [InlineData("not (status eq 'deleted')")]
    [InlineData("category eq 'docs' and (status eq 'published' or status eq 'draft')")]
    public void IsValid_WhenFilterHasMultipleOperators_ShouldReturnTrue(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.True(result, $"Filter '{filter}' should be valid");
    }

    [Theory]
    [InlineData("search.in(category, 'category1,category2')")]
    [InlineData("geo.distance(location, geography'POINT(-122.131577 47.678581)') le 5")]
    [InlineData("startswith(name, 'test')")]
    public void IsValid_WhenFilterHasFunctionCalls_ShouldReturnTrue(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.True(result, $"Filter '{filter}' should be valid");
    }

    [Theory]
    [InlineData("category = 'documentation'")]  // Using = instead of eq
    [InlineData("invalidoperator test")]  // No valid operator
    [InlineData("justtext")]  // No operator or function
    public void IsValid_WhenFilterHasNoValidOperatorOrFunction_ShouldReturnFalse(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.False(result, $"Filter '{filter}' should be invalid");
    }

    [Theory]
    [InlineData("status eq 'unmatched")]  // Unbalanced quotes
    [InlineData("name eq 'test")]  // Missing closing quote
    [InlineData("'incomplete")]  // Missing opening quote
    public void IsValid_WhenFilterHasUnbalancedQuotes_ShouldReturnFalse(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.False(result, $"Filter '{filter}' should be invalid due to unbalanced quotes");
    }

    [Theory]
    [InlineData("category eq 'docs' and (status eq 'published'")]  // Missing closing parenthesis
    [InlineData("(status eq 'active'")]  // Missing closing parenthesis
    [InlineData("status eq 'active')")]  // Missing opening parenthesis
    [InlineData(")(")]  // Wrong order
    [InlineData("abc)def(ghi")]  // Wrong order
    public void IsValid_WhenFilterHasUnbalancedParentheses_ShouldReturnFalse(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.False(result, $"Filter '{filter}' should be invalid due to unbalanced parentheses");
    }

    [Theory]
    [InlineData("((status eq 'active'))")]
    [InlineData("(category eq 'docs') and (status eq 'published')")]
    [InlineData("search.in(field, 'val1,val2')")]
    public void IsValid_WhenFilterHasBalancedParentheses_ShouldReturnTrue(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.True(result, $"Filter '{filter}' should be valid");
    }

    [Theory]
    [InlineData("CATEGORY EQ 'test'")]  // Uppercase operators
    [InlineData("status Ne 'active'")]  // Mixed case
    [InlineData("value GT 10 AND status EQ 'ok'")]  // All uppercase
    public void IsValid_WhenFilterHasCaseVariations_ShouldReturnTrue(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.True(result, $"Filter '{filter}' should be valid (case insensitive)");
    }

    [Fact]
    public void IsValid_WhenFilterIsComplex_ShouldReturnTrue()
    {
        // Arrange
        var filter = "(category eq 'docs' or category eq 'guides') and status ne 'archived' and priority gt 3";

        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.True(result, "Complex filter should be valid");
    }

    [Fact]
    public void IsValid_WhenFilterHasOnlyParentheses_ShouldReturnFalse()
    {
        // Arrange
        var filter = "()";

        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.False(result, "Filter with only parentheses should be invalid");
    }

    [Theory]
    [InlineData("eq")]
    [InlineData("and")]
    [InlineData("or")]
    public void IsValid_WhenFilterIsOnlyOperator_ShouldReturnFalse(string filter)
    {
        // Act
        var result = _validator.IsValid(filter);

        // Assert
        Assert.False(result, $"Filter with only operator '{filter}' should be invalid");
    }
}
