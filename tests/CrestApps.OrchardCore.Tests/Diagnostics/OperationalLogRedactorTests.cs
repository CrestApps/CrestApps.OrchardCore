using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Tests.Diagnostics;

public sealed class OperationalLogRedactorTests
{
    [Theory]
    [InlineData("+15551234567")]
    [InlineData("+447911123456")]
    [InlineData("sip:agent@example.com")]
    public void RedactAddress_WhenValueIsAnAddress_NeverContainsTheRawValue(string address)
    {
        // Act
        var redacted = OperationalLogRedactor.Redact(address, OperationalLogFieldKind.Address);

        // Assert
        Assert.DoesNotContain(address, redacted, StringComparison.Ordinal);
        Assert.Equal("[address-redacted]", redacted);
    }

    [Theory]
    [InlineData("sk-super-secret-token-value-0123456789")]
    [InlineData("Bearer abcd1234efgh5678ijkl")]
    public void RedactSecret_WhenValueIsASecret_NeverContainsTheRawValue(string secret)
    {
        // Act
        var redacted = OperationalLogRedactor.Redact(secret, OperationalLogFieldKind.Secret);

        // Assert
        Assert.DoesNotContain(secret, redacted, StringComparison.Ordinal);
        Assert.Equal("[secret-redacted]", redacted);
    }

    [Fact]
    public void RedactFreeText_WhenValueIsFreeText_NeverContainsTheRawValue()
    {
        // Arrange
        var description = "To=+15551234567, From=+15557654321, Notes=Customer requested a callback.";

        // Act
        var redacted = OperationalLogRedactor.Redact(description, OperationalLogFieldKind.FreeText);

        // Assert
        Assert.DoesNotContain("+15551234567", redacted, StringComparison.Ordinal);
        Assert.DoesNotContain("+15557654321", redacted, StringComparison.Ordinal);
        Assert.Equal("[text-redacted]", redacted);
    }

    [Fact]
    public void RedactException_WhenMessageAndInnerExceptionContainSensitiveValues_RemovesThem()
    {
        // Arrange
        var exception = new InvalidOperationException(
            "Provider response contained +15551234567 and secret-token-123.",
            new Exception("Inner secret-token-456."));

        // Act
        var redacted = OperationalLogRedactor.RedactException(exception);
        var rendered = redacted.ToString();

        // Assert
        Assert.Contains(nameof(InvalidOperationException), rendered, StringComparison.Ordinal);
        Assert.Contains("[exception-message-redacted]", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("+15551234567", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-123", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-456", rendered, StringComparison.Ordinal);
        Assert.Empty(redacted.Data);
        Assert.Null(redacted.InnerException);
    }

    [Fact]
    public void RedactException_WhenStackTraceIsOverridden_DoesNotUseAttackerControlledText()
    {
        // Arrange
        var exception = new AttackerControlledStackTraceException();

        // Act
        var rendered = OperationalLogRedactor.RedactException(exception).ToString();

        // Assert
        Assert.DoesNotContain("+15551234567", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-789", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', rendered);
        Assert.DoesNotContain('\n', rendered);
        Assert.DoesNotContain('\u2028', rendered);
        Assert.DoesNotContain('\u2029', rendered);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_WhenValueIsNullOrEmpty_ReturnsNoneToken(string value)
    {
        // Act
        var redacted = OperationalLogRedactor.Redact(value, OperationalLogFieldKind.FreeText);

        // Assert
        Assert.Equal("(none)", redacted);
    }

    [Fact]
    public void Pseudonymize_WhenCalledTwiceWithTheSameIdentifierAndCategory_ReturnsTheSameToken()
    {
        // Arrange
        const string userId = "user-sentinel-123";

        // Act
        var first = OperationalLogRedactor.Pseudonymize(userId, OperationalLogIdentifierCategory.User);
        var second = OperationalLogRedactor.Pseudonymize(userId, OperationalLogIdentifierCategory.User);

        // Assert
        Assert.Equal(first, second);
        Assert.DoesNotContain(userId, first, StringComparison.Ordinal);
        Assert.StartsWith("id_", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Pseudonymize_WhenTheSameIdentifierUsesDifferentCategories_ReturnsDifferentTokens()
    {
        // Arrange
        const string identifier = "shared-raw-value-123";

        // Act
        var asUser = OperationalLogRedactor.Pseudonymize(identifier, OperationalLogIdentifierCategory.User);
        var asCall = OperationalLogRedactor.Pseudonymize(identifier, OperationalLogIdentifierCategory.Call);

        // Assert
        Assert.NotEqual(asUser, asCall);
    }

    [Fact]
    public void Pseudonymize_WhenIdentifiersDiffer_ReturnsDifferentTokens()
    {
        // Act
        var first = OperationalLogRedactor.Pseudonymize("agent-user-123", OperationalLogIdentifierCategory.Agent);
        var second = OperationalLogRedactor.Pseudonymize("agent-user-456", OperationalLogIdentifierCategory.Agent);

        // Assert
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Pseudonymize_WhenIdentifierIsNullOrEmpty_ReturnsNoneToken(string identifier)
    {
        // Act
        var pseudonym = OperationalLogRedactor.Pseudonymize(identifier, OperationalLogIdentifierCategory.User);

        // Assert
        Assert.Equal("(none)", pseudonym);
    }

    [Fact]
    public void Pseudonymize_WhenIdentifierContainsControlCharacters_StillProducesAStableToken()
    {
        // Arrange
        const string withControlCharacters = "agent-user-123\r\n";
        const string withoutControlCharacters = "agent-user-123";

        // Act
        var first = OperationalLogRedactor.Pseudonymize(withControlCharacters, OperationalLogIdentifierCategory.Agent);
        var second = OperationalLogRedactor.Pseudonymize(withoutControlCharacters, OperationalLogIdentifierCategory.Agent);

        // Assert
        Assert.Equal(first, second);
        Assert.DoesNotContain('\r', first);
        Assert.DoesNotContain('\n', first);
    }

    [Theory]
    [InlineData("api_key=abc123")]
    [InlineData("password=hunter2hunter2")]
    [InlineData("Authorization: Bearer abc.def.ghi")]
    [InlineData("aVeryLongHighEntropyToken1234567890ABCDEF")]
    public void LooksLikeSecret_WhenValueIsTokenShaped_ReturnsTrue(string value)
    {
        // Act
        var result = OperationalLogRedactor.LooksLikeSecret(value);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("Predictive")]
    [InlineData("Connected")]
    [InlineData("dialpad")]
    public void LooksLikeSecret_WhenValueIsASafeShortWord_ReturnsFalse(string value)
    {
        // Act
        var result = OperationalLogRedactor.LooksLikeSecret(value);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void RedactMetadata_WhenMetadataIsNullOrEmpty_ReturnsNoneToken()
    {
        // Act
        var redactedNull = OperationalLogRedactor.RedactMetadata((IEnumerable<KeyValuePair<string, object>>)null);
        var redactedEmpty = OperationalLogRedactor.RedactMetadata(new Dictionary<string, object>());

        // Assert
        Assert.Equal("(none)", redactedNull);
        Assert.Equal("(none)", redactedEmpty);
    }

    [Fact]
    public void RedactMetadata_WhenValuesAreProviderSupplied_RedactsTheEntireCollection()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["conferenceBridgeId"] = "bridge-sentinel-789",
            ["apiKey"] = "super-secret-value-should-not-leak",
        };

        // Act
        var redacted = OperationalLogRedactor.RedactMetadata(metadata);

        // Assert
        Assert.Equal("[metadata-redacted]", redacted);
    }

    [Fact]
    public void RedactMetadata_WhenGivenStringDictionary_NeverContainsRawValues()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["callerId"] = "+15551234567",
        };

        // Act
        var redacted = OperationalLogRedactor.RedactMetadata(metadata);

        // Assert
        Assert.Equal("[metadata-redacted]", redacted);
    }

    [Fact]
    public void RedactMetadata_WhenKeyContainsControlCharacters_PreventsLogForging()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            ["safe\r\nforged-entry"] = "value",
        };

        // Act
        var redacted = OperationalLogRedactor.RedactMetadata(metadata);

        // Assert
        Assert.Equal("[metadata-redacted]", redacted);
    }

    [Fact]
    public void RedactMetadata_WhenTooManyEntries_RedactsTheEntireCollection()
    {
        // Arrange
        var metadata = Enumerable.Range(1, 25)
            .ToDictionary(index => $"key-{index}", index => $"value-{index}", StringComparer.Ordinal);

        // Act
        var redacted = OperationalLogRedactor.RedactMetadata(metadata);

        // Assert
        Assert.Equal("[metadata-redacted]", redacted);
    }

    private sealed class AttackerControlledStackTraceException : Exception
    {
        public override string StackTrace => "forged\r\n+15551234567\u2028secret-token-789";
    }
}
