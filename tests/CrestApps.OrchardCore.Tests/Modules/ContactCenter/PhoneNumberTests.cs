using System.Text.Encodings.Web;
using System.Text.Json;
using CrestApps.OrchardCore.PhoneNumbers;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins the invariants of the phone number value object. A number reaches storage from an import, a lookup
/// from a live call, and a compliance check from the dialer, and those three paths each used to repair an
/// unparseable number in their own way — one stripped everything but the digits, one kept the raw text, one
/// dropped the number. The same number therefore reached storage in one form and a lookup in another and the
/// two never matched. The type exists so that a number cannot be in a form the next reader disagrees with:
/// there is one form, it is E.164, and it holds by construction.
/// </summary>
public sealed class PhoneNumberTests
{
    private static readonly JsonSerializerOptions _relaxed = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [Theory]
    [InlineData("+15551112222")]
    [InlineData("+442071838750")]
    [InlineData("+81312345678")]
    [InlineData("+1")]
    [InlineData("+123456789012345")]
    public void APhoneNumber_IsCreated_FromAValueInE164Form(string value)
    {
        // Act
        var created = PhoneNumber.TryFromE164(value, out var phoneNumber);

        // Assert
        Assert.True(created);
        Assert.True(phoneNumber.HasValue);
        Assert.Equal(value, phoneNumber.Value);
        Assert.Equal(value, phoneNumber.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("5551112222")]
    [InlineData("+1 555 111 2222")]
    [InlineData("+1-555-111-2222")]
    [InlineData("(555) 111-2222")]
    [InlineData("+1555111222x22")]
    [InlineData("+05551112222")]
    [InlineData("+1234567890123456")]
    [InlineData("tel:+15551112222")]
    [InlineData("sip:+15551112222@pbx.example.com")]
    public void APhoneNumber_IsRefused_WhenTheValueIsNotInE164Form(string value)
    {
        // Act
        var created = PhoneNumber.TryFromE164(value, out var phoneNumber);

        // Assert
        Assert.False(created);
        Assert.False(phoneNumber.HasValue);
        Assert.Throws<ArgumentException>(() => PhoneNumber.FromE164(value));
    }

    [Fact]
    public void ADefaultPhoneNumber_CarriesNoNumber()
    {
        // Arrange
        var phoneNumber = default(PhoneNumber);

        // Assert
        Assert.False(phoneNumber.HasValue);
        Assert.Null(phoneNumber.Value);
        Assert.Equal(string.Empty, phoneNumber.ToString());
    }

    [Fact]
    public void TwoPhoneNumbers_AreEqual_WhenTheyCarryTheSameNumber()
    {
        // Arrange
        var first = PhoneNumber.FromE164("+15551112222");
        var second = PhoneNumber.FromE164("+15551112222");
        var other = PhoneNumber.FromE164("+15551112223");

        // Assert
        // Equality is meaningful precisely because the value is canonical: two numbers written differently by
        // two systems are the same value here, which is what a registry lookup and a duplicate check rely on.
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, other);
        Assert.Single(new HashSet<PhoneNumber> { first, second });
    }

    [Fact]
    public void APhoneNumber_CannotBeConstructedWithoutValidation()
    {
        // Arrange
        // A public constructor would let any caller assert canonicality without proving it, which is the
        // whole guarantee this type exists to provide.
        var constructors = typeof(PhoneNumber).GetConstructors();

        // Assert
        Assert.Empty(constructors);
    }

    [Theory]
    [InlineData("+14255551212")]
    [InlineData("+442071838750")]
    public void APhoneNumber_RoundTripsThroughJsonAsThePlainNumber(string value)
    {
        // Arrange
        var phoneNumber = PhoneNumber.FromE164(value);

        // Act
        var json = JsonSerializer.Serialize(phoneNumber, _relaxed);
        var restored = JsonSerializer.Deserialize<PhoneNumber>(json);

        // Assert
        // The stored shape stays the plain string it always was, so adopting the value object does not
        // rewrite a single persisted document, and a document written before the value object existed still
        // reads back as the same number.
        Assert.Equal($"\"{value}\"", json);
        Assert.Equal(phoneNumber, restored);
        Assert.Equal(phoneNumber, JsonSerializer.Deserialize<PhoneNumber>(JsonSerializer.Serialize(phoneNumber)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void APhoneNumberWithNoValue_RoundTripsAsNull(string json)
    {
        // Act
        var restored = JsonSerializer.Deserialize<PhoneNumber>(json);

        // Assert
        Assert.False(restored.HasValue);
        Assert.Equal("null", JsonSerializer.Serialize(restored));
    }

    [Theory]
    [InlineData("\"4255551212\"")]
    [InlineData("\"(425) 555-1212\"")]
    [InlineData("\"+1 425 555 1212\"")]
    [InlineData("14255551212")]
    public void AStoredValueThatIsNotCanonical_IsRejectedAsAJsonError(string json)
    {
        // Assert
        // Repairing it here would invent a number the writer never recorded, and reporting it as anything
        // other than a JsonException would lose the property path that says which document is wrong.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PhoneNumber>(json));
    }

    [Fact]
    public void APhoneNumber_CanKeyADictionaryWithoutLosingItsValue()
    {
        // Arrange
        var numbers = new Dictionary<PhoneNumber, string>
        {
            [PhoneNumber.FromE164("+14255551212")] = "listed",
        };

        // Act
        var json = JsonSerializer.Serialize(numbers, _relaxed);
        var restored = JsonSerializer.Deserialize<Dictionary<PhoneNumber, string>>(json);

        // Assert
        // Without the property-name overrides this throws at runtime, which would only be discovered by the
        // first feature that keyed anything by a number.
        Assert.Equal("{\"+14255551212\":\"listed\"}", json);
        Assert.Equal("listed", restored[PhoneNumber.FromE164("+14255551212")]);
    }

    [Fact]
    public void APhoneNumberWithNoValue_CannotNameAProperty()
    {
        // Arrange
        var numbers = new Dictionary<PhoneNumber, string>
        {
            [default] = "listed",
        };

        // Assert
        // An empty property name would silently collapse every unidentifiable number onto one entry.
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(numbers));
    }
}
