using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Secrets;
using CrestApps.OrchardCore.Tests.Agent.Security;
using OrchardCore.Secrets;

namespace CrestApps.OrchardCore.Tests.Agent.Secrets;

/// <summary>
/// The secret tools work on the secret manager of the Secrets admin page and never return the value of a secret, a
/// private key or PEM material. No tool takes one either: saveSecret creates a reference to a certificate of a
/// certificate store and changes the description and the expiry date of a secret, and for everything else it points
/// to the admin page.
/// </summary>
public sealed class SecretToolsTests
{
    [Fact]
    public async Task ListSecrets_ReturnsWhatTheStoresSayAboutTheSecretsWithoutReadingAValue()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new ListSecretsTool(), services);

        // Assert
        var result = AgentToolTestContext.ReadJson(text);
        var items = result.GetProperty("items").EnumerateArray().ToList();
        var smtp = items.Single(item => item.GetProperty("name").GetString() == "Smtp.Password");

        Assert.Equal(4, result.GetProperty("total").GetInt32());
        Assert.Equal(["OpenId.SigningKey", "Site.Certificate", "Smtp.Password", "Twilio.AuthToken"], items.Select(item => item.GetProperty("name").GetString()));
        Assert.Equal("Database", smtp.GetProperty("store").GetString());
        Assert.Equal("TextSecret", smtp.GetProperty("type").GetString());
        Assert.Equal("The password of the SMTP account.", smtp.GetProperty("description").GetString());
        Assert.Equal("2026-11-27T12:00:00+00:00", smtp.GetProperty("expiresAt").GetString());
        Assert.True(smtp.GetProperty("expiresWithin30Days").GetBoolean());
        Assert.Equal(1, result.GetProperty("expiredCount").GetInt32());
        Assert.Equal(1, result.GetProperty("expiringWithin30DaysCount").GetInt32());

        // Every field of a secret is one the store tells about it; none is its value.
        Assert.All(items, item => Assert.Subset(
            new HashSet<string> { "name", "store", "type", "description", "createdAt", "updatedAt", "expiresAt", "expired", "expiresWithin30Days" },
            item.EnumerateObject().Select(property => property.Name).ToHashSet()));

        var stores = result.GetProperty("stores").EnumerateArray().ToDictionary(store => store.GetProperty("name").GetString(), store => store.GetProperty("readOnly").GetBoolean());

        Assert.Equal(3, stores.Count);
        Assert.False(stores["Database"]);
        Assert.True(stores["Configuration"]);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
        harness.VerifyNothingChanged();
    }

    [Theory]
    [InlineData("expiry", "expired", "OpenId.SigningKey")]
    [InlineData("expiry", "expiringSoon", "Smtp.Password")]
    [InlineData("type", "X509Secret", "Site.Certificate")]
    [InlineData("search", "configuration", "Twilio.AuthToken")]
    [InlineData("search", "signs", "OpenId.SigningKey")]
    public async Task ListSecrets_WithAFilter_KeepsTheMatchingSecrets(string argument, string value, string expectedName)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListSecretsTool(), services, new()
        {
            [argument] = value,
        }));

        // Assert
        var item = Assert.Single(result.GetProperty("items").EnumerateArray());

        Assert.Equal(expectedName, item.GetProperty("name").GetString());
    }

    [Fact]
    public async Task ListSecrets_ThatNeverExpire_AreTheOnesWithoutAnExpiryDate()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListSecretsTool(), services, new()
        {
            ["expiry"] = "neverExpires",
        }));

        // Assert
        Assert.Equal(["Site.Certificate", "Twilio.AuthToken"], result.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task SaveSecret_ForAnExistingSecret_ChangesTheDescriptionAndKeepsTheValueAndTheExpiryDate()
    {
        // Arrange
        var harness = new SecurityToolHarness();
        var valueBefore = harness.Secrets.Database.Peek("Smtp.Password");
        var expiryBefore = harness.Secrets.Database.PeekInfo("Smtp.Password").ExpiresUtc;

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new SaveSecretTool(), services, new()
        {
            ["name"] = "smtp.password",
            ["description"] = "The password of the mail account.",
        });

        // Assert
        var result = AgentToolTestContext.ReadJson(text);
        var info = harness.Secrets.Database.PeekInfo("Smtp.Password");

        Assert.True(result.GetProperty("saved").GetBoolean());
        Assert.False(result.GetProperty("created").GetBoolean());
        Assert.Equal("The password of the mail account.", info.Description);
        Assert.Equal(expiryBefore, info.ExpiresUtc);

        // The stored secret went back into the store as it was, and nothing of it is in the result.
        Assert.Same(valueBefore, harness.Secrets.Database.Peek("Smtp.Password"));
        Assert.StartsWith(SecurityToolHarness.Canary, Assert.IsType<TextSecret>(valueBefore).Text, StringComparison.Ordinal);
        Assert.Equal(["Smtp.Password"], harness.Secrets.Database.Saves);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("The password of the mail account.", result.GetProperty("secret").GetProperty("description").GetString());
    }

    [Fact]
    public async Task SaveSecret_WithAnExpiryDate_StoresTheLocalDateAsUtc()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveSecretTool(), services, new()
        {
            ["name"] = "Site.Certificate",
            ["expiresOn"] = "2027-11-17",
        }));

        // Assert: the description the call did not name is kept.
        var info = harness.Secrets.Database.PeekInfo("Site.Certificate");

        Assert.Equal(new DateTime(2027, 11, 17, 0, 0, 0, DateTimeKind.Utc), info.ExpiresUtc);
        Assert.Equal("The certificate of the site.", info.Description);
        Assert.Equal("2027-11-17T00:00:00+00:00", result.GetProperty("secret").GetProperty("expiresAt").GetString());
    }

    [Fact]
    public async Task SaveSecret_WithRemoveExpiry_TakesTheExpiryDateAway()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveSecretTool(), services, new()
        {
            ["name"] = "OpenId.SigningKey",
            ["removeExpiry"] = true,
        });

        // Assert
        Assert.Null(harness.Secrets.Database.PeekInfo("OpenId.SigningKey").ExpiresUtc);
        Assert.StartsWith(SecurityToolHarness.Canary, Assert.IsType<RsaKeySecret>(harness.Secrets.Database.Peek("OpenId.SigningKey")).PrivateKey, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("TextSecret", "a text secret is created with its value")]
    [InlineData("RsaKeySecret", "this tool does not create RSA key secrets")]
    public async Task SaveSecret_ForANewSecretThatHoldsSecretMaterial_CreatesNothingAndPointsToTheAdminPage(string type, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSecretTool(), services, new()
        {
            ["name"] = "Stripe.ApiKey",
            ["create"] = true,
            ["type"] = type,
        });

        // Assert
        Assert.StartsWith("Nothing was saved:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Contains("Secrets admin page", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task SaveSecret_ForANewCertificateOfACertificateStore_SavesAReferenceToIt()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveSecretTool(), services, new()
        {
            ["name"] = "OpenId.SigningCertificate",
            ["create"] = true,
            ["type"] = "X509Secret",
            ["description"] = "Signs the tokens.",
            ["certificateStoreLocation"] = "CurrentUser",
            ["certificateStoreName"] = "My",
            ["thumbprint"] = "a1 b2 c3 d4 e5 f6 a1 b2 c3 d4 e5 f6 a1 b2 c3 d4 e5 f6 a1 b2",
        }));

        // Assert: the first writable store, and a secret that names the certificate and holds nothing of it.
        var secret = Assert.IsType<X509Secret>(harness.Secrets.Database.Peek("OpenId.SigningCertificate"));

        Assert.True(result.GetProperty("created").GetBoolean());
        Assert.Equal(X509SecretSource.Store, secret.Source);
        Assert.Equal(StoreLocation.CurrentUser, secret.StoreLocation);
        Assert.Equal(StoreName.My, secret.StoreName);
        Assert.Equal("A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2", secret.Thumbprint);
        Assert.Null(secret.CertificatePem);
        Assert.Null(secret.PrivateKeyPem);
        Assert.Equal("Signs the tokens.", harness.Secrets.Database.PeekInfo("OpenId.SigningCertificate").Description);
        Assert.Equal("X509Secret", result.GetProperty("secret").GetProperty("type").GetString());

        // No certificate with that thumbprint is on the machine that runs the test, and the tool says so.
        Assert.False(result.GetProperty("certificate").GetProperty("found").GetBoolean());
        Assert.Contains("No certificate with this thumbprint", result.GetProperty("warning").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "name": "Smtp.Password", "create": true, "type": "X509Secret", "thumbprint": "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2" }""", "a secret named 'Smtp.Password' exists")]
    [InlineData("""{ "name": "New.Certificate", "create": true, "type": "X509Secret" }""", "give the thumbprint of the certificate")]
    [InlineData("""{ "name": "New.Certificate", "create": true, "type": "X509Secret", "thumbprint": "not-a-thumbprint" }""", "the thumbprint must be the hexadecimal fingerprint")]
    [InlineData("""{ "name": "New.Certificate", "create": true, "type": "X509Secret", "thumbprint": "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2", "store": "Configuration" }""", "select a writable secret store")]
    [InlineData("""{ "name": "New.Certificate", "create": true, "type": "X509Secret", "thumbprint": "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2", "certificateStoreLocation": "Everywhere" }""", "certificateStoreLocation must be CurrentUser or LocalMachine")]
    [InlineData("""{ "name": "New.Certificate", "create": true }""", "give type for a new secret")]
    [InlineData("""{ "name": "Missing.Secret", "description": "x" }""", "there is no secret named 'Missing.Secret'")]
    [InlineData("""{ "name": "Twilio.AuthToken", "description": "x" }""", "the secret store 'Configuration' is read-only")]
    [InlineData("""{ "name": "Smtp.Password" }""", "give description, expiresOn or removeExpiry")]
    [InlineData("""{ "name": "Smtp.Password", "expiresOn": "next year" }""", "expiresOn must be a local date")]
    [InlineData("""{ "name": "Smtp.Password", "expiresOn": "2027-01-01", "removeExpiry": true }""", "give expiresOn or removeExpiry, not both")]
    [InlineData("""{ "name": "Site.Certificate", "thumbprint": "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2" }""", "changes the description and the expiry date of an existing secret only")]
    [InlineData("""{ "name": "Smtp.Password", "type": "X509Secret", "description": "x" }""", "changes the description and the expiry date of an existing secret only")]
    public async Task SaveSecret_WithWhatItCannotDo_SavesNothing(string json, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            .ToDictionary(pair => pair.Key, pair => (object)pair.Value);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveSecretTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was saved:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.OrdinalIgnoreCase);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task DeleteSecret_RemovesTheSecretFromItsStoreWithoutReadingItsValue()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new DeleteSecretTool(), services, new()
        {
            ["name"] = "Smtp.Password",
            ["store"] = "Database",
        });

        // Assert
        var result = AgentToolTestContext.ReadJson(text);

        Assert.True(result.GetProperty("deleted").GetBoolean());
        Assert.Equal("TextSecret", result.GetProperty("type").GetString());
        Assert.False(harness.Secrets.Database.Contains("Smtp.Password"));
        Assert.Empty(harness.Secrets.Database.ValueReads);
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Missing.Secret", null, "there is no secret named 'Missing.Secret'")]
    [InlineData("Smtp.Password", "AzureKeyVault", "there is no secret named 'Smtp.Password' in the store 'AzureKeyVault'")]
    [InlineData("Twilio.AuthToken", null, "the secret store 'Configuration' is read-only")]
    public async Task DeleteSecret_ThatCannotBeDeleted_DeletesNothing(string name, string store, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        var arguments = new Dictionary<string, object>
        {
            ["name"] = name,
        };

        if (store is not null)
        {
            arguments["store"] = store;
        }

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new DeleteSecretTool(), services, arguments);

        // Assert
        Assert.StartsWith("Nothing was deleted:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task MoveSecrets_WithoutAName_MovesEverySecretOfTheSourceStore()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var text = await AgentToolTestContext.InvokeAsync(new MoveSecretsTool(), services, new()
        {
            ["sourceStore"] = "database",
            ["destinationStore"] = "AzureKeyVault",
        });

        // Assert
        var result = AgentToolTestContext.ReadJson(text);
        var results = result.GetProperty("results").EnumerateArray().ToList();

        Assert.Equal(3, result.GetProperty("movedCount").GetInt32());
        Assert.Equal(0, result.GetProperty("failedCount").GetInt32());
        Assert.All(results, item => Assert.True(item.GetProperty("moved").GetBoolean()));
        Assert.All(results, item => Assert.Equal(["moved", "name", "savedInDestination"], item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
        Assert.True(harness.Secrets.KeyVault.Contains("Smtp.Password"));
        Assert.False(harness.Secrets.Database.Contains("Smtp.Password"));
        Assert.DoesNotContain(SecurityToolHarness.Canary, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveSecrets_WithAName_MovesThatSecretAndReportsWhyAnotherWasNotMoved()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var moved = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new MoveSecretsTool(), services, new()
        {
            ["sourceStore"] = "Database",
            ["destinationStore"] = "AzureKeyVault",
            ["name"] = "Smtp.Password",
        }));

        var missing = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new MoveSecretsTool(), services, new()
        {
            ["sourceStore"] = "Database",
            ["destinationStore"] = "AzureKeyVault",
            ["name"] = "Smtp.Password",
        }));

        // Assert
        Assert.Equal(1, moved.GetProperty("movedCount").GetInt32());
        Assert.True(harness.Secrets.KeyVault.Contains("Smtp.Password"));
        Assert.True(harness.Secrets.Database.Contains("Site.Certificate"));
        Assert.Equal(1, missing.GetProperty("failedCount").GetInt32());
        Assert.Equal("SourceMissing", Assert.Single(missing.GetProperty("results").EnumerateArray()).GetProperty("failure").GetString());
    }

    [Theory]
    [InlineData("Database", "Database", "destinationStore must be a different writable store")]
    [InlineData("Configuration", "Database", "sourceStore must be an available writable store")]
    [InlineData("Database", "Configuration", "destinationStore must be a different writable store")]
    [InlineData("Database", "Vault", "destinationStore must be a different writable store")]
    public async Task MoveSecrets_BetweenStoresTheAdminPageRefuses_MovesNothing(string source, string destination, string expected)
    {
        // Arrange
        var harness = new SecurityToolHarness();

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveSecretsTool(), services, new()
        {
            ["sourceStore"] = source,
            ["destinationStore"] = destination,
        });

        // Assert
        Assert.StartsWith("Nothing was moved:", result, StringComparison.Ordinal);
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Contains("Database, AzureKeyVault", result, StringComparison.Ordinal);
        harness.VerifyNothingChanged();
    }

    [Fact]
    public async Task MoveSecrets_WhenAStoreFails_DoesNotReturnWhatTheStoreAnswered()
    {
        // Arrange
        var harness = new SecurityToolHarness();

        harness.Secrets.MoveFails = true;

        using var services = harness.Build();

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new MoveSecretsTool(), services, new()
        {
            ["sourceStore"] = "Database",
            ["destinationStore"] = "AzureKeyVault",
            ["name"] = "Smtp.Password",
        });

        // Assert
        Assert.StartsWith("The store operation could not be completed.", result, StringComparison.Ordinal);
        Assert.DoesNotContain(SecurityToolHarness.Canary, result, StringComparison.OrdinalIgnoreCase);
        harness.VerifyNothingChanged();
    }
}
