using CrestApps.OrchardCore.AI.Agent.Communications;
using CrestApps.OrchardCore.Tests.Agent.Security;

namespace CrestApps.OrchardCore.Tests.Agent.Communications;

/// <summary>
/// The test email and test SMS tools send a fixed message through the provider the caller picks, as the Email Test
/// and SMS Test admin pages do, to the recipient the caller names. They return what the provider answered and never
/// what a provider threw, which can name the host and the account of its settings.
/// </summary>
public sealed class SendTestMessageToolsTests
{
    [Fact]
    public async Task SendTestEmail_WithoutArguments_SendsTheFixedMessageToTheCallerThroughTheDefaultProvider()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestEmailTool(), services));

        // Assert
        var (message, provider) = Assert.Single(harness.Messages.Emails);

        Assert.Equal("SMTP", provider);
        Assert.Equal(TestMessageToolDoubles.CallerEmail, message.To);
        Assert.Equal(SendTestEmailTool.Subject, message.Subject);
        Assert.Equal(SendTestEmailTool.Body, message.TextBody);
        Assert.Null(message.Cc);
        Assert.Null(message.Bcc);
        Assert.Null(message.From);
        Assert.True(result.GetProperty("sent").GetBoolean());
        Assert.Equal("SMTP", result.GetProperty("provider").GetString());
        Assert.Equal(TestMessageToolDoubles.CallerEmail, result.GetProperty("to").GetString());

        // The providers the Email Test page offers: the enabled ones.
        var providers = result.GetProperty("availableProviders").EnumerateArray().ToList();

        Assert.Equal(["Azure", "SMTP"], providers.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal("SMTP provider", providers[1].GetProperty("displayName").GetString());
        Assert.True(providers[1].GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task SendTestEmail_ToAnAddressThroughAProvider_SendsToThatAddressThroughThatProvider()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestEmailTool(), services, new()
        {
            ["to"] = "ops@contoso.com",
            ["provider"] = "azure",
        }));

        // Assert
        var (message, provider) = Assert.Single(harness.Messages.Emails);

        Assert.Equal("Azure", provider);
        Assert.Equal("ops@contoso.com", message.To);
        Assert.True(result.GetProperty("sent").GetBoolean());
    }

    [Theory]
    [InlineData("ops@contoso.com, sales@contoso.com")]
    [InlineData("ops@contoso.com;sales@contoso.com")]
    [InlineData("Ops <ops@contoso.com>")]
    [InlineData("not an address")]
    public async Task SendTestEmail_ToAnythingButOneAddress_SendsNothing(string to)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendTestEmailTool(), services, new()
        {
            ["to"] = to,
        });

        // Assert
        Assert.Equal("Nothing was sent: to must be one email address, for example name@example.com.", result);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("Disabled")]
    [InlineData("Mailgun")]
    public async Task SendTestEmail_ThroughAProviderThatIsNotEnabled_SendsNothingAndListsTheProviders(string provider)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestEmailTool(), services, new()
        {
            ["provider"] = provider,
        }));

        // Assert
        Assert.False(result.GetProperty("sent").GetBoolean());
        Assert.Contains($"'{provider}' is not an enabled email provider", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(2, result.GetProperty("availableProviders").GetArrayLength());
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SendTestEmail_WithoutAProviderWhenTheSiteHasNoDefault_SendsNothingAndListsTheProviders()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.Messages.DefaultEmailProvider = null;

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestEmailTool(), services));

        // Assert
        Assert.False(result.GetProperty("sent").GetBoolean());
        Assert.Contains("the site has no default email provider", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.Equal(2, result.GetProperty("availableProviders").GetArrayLength());
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SendTestEmail_WhenTheProviderAnswersThatItCouldNotSend_ReturnsItsErrors()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.Messages.ProvidersFail = true;

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestEmailTool(), services));

        // Assert
        Assert.False(result.GetProperty("sent").GetBoolean());
        Assert.Equal(["The sender address is not allowed."], result.GetProperty("errors").EnumerateArray().Select(error => error.GetString()));
    }

    [Fact]
    public async Task SendTestEmail_WhenTheProviderThrows_DoesNotReturnWhatItThrew()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.Messages.ProvidersThrow = true;

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new SendTestEmailTool(), services);

        // Assert: the exception names the host and the password of the provider settings.
        var result = AgentToolTestContext.ReadJson(text);

        Assert.False(result.GetProperty("sent").GetBoolean());
        Assert.StartsWith("Unable to send the message using the selected provider.", result.GetProperty("error").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("smtp.contoso.com", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendTestSms_SendsTheFixedMessageToTheNumberThroughTheProvider()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestSmsTool(), services, new()
        {
            ["phoneNumber"] = " +15551234567 ",
            ["provider"] = "twilio",
        }));

        // Assert
        var (message, provider) = Assert.Single(harness.Messages.TextMessages);

        Assert.Equal("Twilio", provider);
        Assert.Equal("+15551234567", message.To);
        Assert.Equal("This is a test SMS message.", message.Body);
        Assert.True(result.GetProperty("sent").GetBoolean());
        Assert.Equal("Twilio", result.GetProperty("provider").GetString());
        Assert.Equal("+15551234567", result.GetProperty("to").GetString());
    }

    [Fact]
    public async Task SendTestSms_WithoutAProviderWhenOneIsEnabled_UsesIt()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.Messages.EnabledSmsProviders = ["Log"];

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestSmsTool(), services, new()
        {
            ["phoneNumber"] = "+15551234567",
        }));

        // Assert
        var (_, provider) = Assert.Single(harness.Messages.TextMessages);

        Assert.Equal("Log", provider);
        Assert.True(result.GetProperty("sent").GetBoolean());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Disabled")]
    [InlineData("Vonage")]
    public async Task SendTestSms_WithoutAnEnabledProviderToUse_SendsNothingAndListsTheProviders(string provider)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["phoneNumber"] = "+15551234567",
        };

        if (provider is not null)
        {
            arguments["provider"] = provider;
        }

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestSmsTool(), services, arguments));

        // Assert: with two providers enabled the tool does not pick one itself.
        Assert.False(result.GetProperty("sent").GetBoolean());
        Assert.Equal(["Log", "Twilio"], result.GetProperty("availableProviders").EnumerateArray().Select(name => name.GetString()));
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("555-1234")]
    [InlineData("call me")]
    public async Task SendTestSms_ToANumberThatIsNotValid_SendsNothing(string phoneNumber)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SendTestSmsTool(), services, new()
        {
            ["phoneNumber"] = phoneNumber,
            ["provider"] = "Twilio",
        });

        // Assert
        Assert.StartsWith("Nothing was sent: give phoneNumber as a valid phone number", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SendTestSms_WhenTheProviderAnswersThatItCouldNotSend_ReturnsItsErrors()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.Messages.ProvidersFail = true;

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SendTestSmsTool(), services, new()
        {
            ["phoneNumber"] = "+15551234567",
            ["provider"] = "Twilio",
        }));

        // Assert
        Assert.False(result.GetProperty("sent").GetBoolean());
        Assert.Equal(["The number is not reachable."], result.GetProperty("errors").EnumerateArray().Select(error => error.GetString()));
    }

    [Fact]
    public async Task SendTestSms_WhenTheProviderThrows_DoesNotReturnWhatItThrew()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.Messages.ProvidersThrow = true;

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new SendTestSmsTool(), services, new()
        {
            ["phoneNumber"] = "+15551234567",
            ["provider"] = "Twilio",
        });

        // Assert: the exception names the token of the provider settings.
        Assert.False(AgentToolTestContext.ReadJson(text).GetProperty("sent").GetBoolean());
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
    }
}
