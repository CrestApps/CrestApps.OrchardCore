using CrestApps.OrchardCore.AI.Memory.Services;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Memory.Services;

public sealed class DefaultAIMemorySafetyServiceTests
{
    private readonly DefaultAIMemorySafetyService _service = new();

    [Fact]
    public void TryValidate_WhenMemoryIsSafe_ShouldReturnTrue()
    {
        var result = _service.TryValidate(
            "format_preference",
            "The user's response format preference.",
            "The user prefers concise bullet points.",
            out var errorMessage);

        Assert.True(result);
        Assert.Null(errorMessage);
    }

    [Theory]
    [InlineData("secret", "The user's API key.", "The user's API key is sk-1234567890abcdef")]
    [InlineData("payment", "The user's payment card.", "Credit card: 4111 1111 1111 1111")]
    [InlineData("identity", "The user's SSN.", "SSN 123-45-6789")]
    [InlineData("preferred_name", "The user's API key is sk-1234567890abcdef", "Mike")]
    public void TryValidate_WhenMemoryLooksSensitive_ShouldReturnFalse(string name, string description, string content)
    {
        var result = _service.TryValidate(name, description, content, out var errorMessage);

        Assert.False(result);
        Assert.Equal("Sensitive information must not be stored in user memory.", errorMessage);
    }
}
