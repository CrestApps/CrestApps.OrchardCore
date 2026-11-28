using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Queries;

namespace CrestApps.OrchardCore.Tests.Agent.Queries;

/// <summary>
/// The rows a query tool returns are bounded: a page of rows, long values shortened, the whole page under a size
/// limit, and values named like a credential left out.
/// </summary>
public sealed class QueryResultRowsTests
{
    [Theory]
    [InlineData("PasswordHash", true)]
    [InlineData("password", true)]
    [InlineData("SecurityStamp", true)]
    [InlineData("security_stamp", true)]
    [InlineData("ApiKey", true)]
    [InlineData("api-key", true)]
    [InlineData("ClientSecret", true)]
    [InlineData("AccessToken", true)]
    [InlineData("ConnectionString", true)]
    [InlineData("HashKey", true)]
    [InlineData("Pin", true)]
    [InlineData("Salt", true)]
    [InlineData("DisplayText", false)]
    [InlineData("ContentItemId", false)]
    [InlineData("Pinned", false)]
    [InlineData("Shipping", false)]
    [InlineData("KeyFieldName", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSecretName_TellsACredentialNameFromAnotherName(string name, bool expected)
    {
        // Act
        var result = QueryResultRows.IsSecretName(name);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CreatePage_WithALongValue_ShortensItAndSaysSo()
    {
        // Arrange
        var text = new string('x', QueryResultRows.MaxValueLength + 500);
        var rows = new object[]
        {
            new JsonObject
            {
                ["Body"] = text,
                ["Title"] = "Short",
            },
        };

        // Act
        var page = QueryResultRows.CreatePage(rows, 1, 20);

        // Assert
        var body = Assert.Single(page.Items)["Body"].GetValue<string>();

        Assert.StartsWith(new string('x', QueryResultRows.MaxValueLength), body, StringComparison.Ordinal);
        Assert.EndsWith($"... [shortened, {text.Length} characters in all]", body, StringComparison.Ordinal);
        Assert.Equal("Short", page.Items[0]["Title"].GetValue<string>());
        Assert.True(page.ValuesShortened);
        Assert.False(page.Truncated);
        Assert.Contains(page.GetNotes(), note => note.Contains("were shortened", StringComparison.Ordinal));
    }

    [Fact]
    public void CreatePage_WhenTheRowsAreLargerThanAResult_StopsBeforeTheSizeLimitAndSaysSo()
    {
        // Arrange
        var rows = Enumerable.Range(1, 50)
            .Select(number => (object)new JsonObject
            {
                ["Id"] = number,
                ["Body"] = new string('x', QueryResultRows.MaxValueLength),
            })
            .ToArray();

        // Act
        var page = QueryResultRows.CreatePage(rows, 1, 50);

        // Assert
        Assert.Equal(50, page.Total);
        Assert.True(page.Truncated);
        Assert.InRange(page.Items.Count, 1, 49);
        Assert.True(page.Items.Sum(row => row.ToJsonString().Length) <= QueryResultRows.MaxResultLength);
        Assert.Equal(1, page.Items[0]["Id"].GetValue<int>());
        Assert.Contains(page.GetNotes(), note => note.Contains("fewer rows than asked for", StringComparison.Ordinal));
    }

    [Fact]
    public void CreatePage_ForACompleteSmallResult_HasNoNotes()
    {
        // Arrange
        var rows = new object[]
        {
            new Dictionary<string, object>
            {
                ["Total"] = 12L,
                ["ContentType"] = "BlogPost",
                ["Empty"] = null,
            },
        };

        // Act
        var page = QueryResultRows.CreatePage(rows, 1, 20);

        // Assert
        var row = Assert.Single(page.Items);

        Assert.Equal(12, row["Total"].GetValue<long>());
        Assert.Equal("BlogPost", row["ContentType"].GetValue<string>());
        Assert.Null(row["Empty"]);
        Assert.Null(page.GetNotes());
    }

    [Fact]
    public void CreatePage_WithoutRows_IsEmpty()
    {
        // Act
        var page = QueryResultRows.CreatePage(null, 1, 20);

        // Assert
        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
        Assert.Null(page.GetNotes());
    }

    [Fact]
    public void Cut_NeverSplitsACharacter()
    {
        // Arrange
        var text = "ab\U0001F600cd";

        // Act
        var result = QueryResultRows.Cut(text, 3);

        // Assert
        Assert.Equal("ab", result);
    }
}
