using System.Net;
using CrestApps.Core;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI.Models;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Drivers;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.Cartesia.Drivers;
using CrestApps.OrchardCore.Cartesia.ViewModels;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.AI;

/// <summary>
/// MD5: a user who may edit an AI connection, deployment or tool instance, but never saw its key, could point it at a host
/// of their own and leave the key box empty, which kept the key; the next call sent the key there. A change of address
/// (scheme, host or port) or of the way of signing in now drops the stored secrets, in the editors and in the recipes.
/// </summary>
public sealed class AISecretDestinationTests
{
    private readonly EphemeralDataProtectionProvider _protection = new();

    [Theory]
    [InlineData("https://api.vendor.example/v1", "https://api.vendor.example/v2", true)]
    [InlineData("https://api.vendor.example/", "https://API.VENDOR.EXAMPLE/", true)]
    [InlineData("https://api.vendor.example/", "https://attacker.example/", false)]
    [InlineData("https://api.vendor.example/", "http://api.vendor.example/", false)]
    [InlineData("https://api.vendor.example/", "https://api.vendor.example:8443/", false)]
    [InlineData("https://api.vendor.example/", "", false)]
    [InlineData("", "https://attacker.example/", false)]
    [InlineData("", "", true)]
    [InlineData("https://api.vendor.example/", "not a url", false)]
    public void SameOrigin_ComparesSchemeHostAndPort(string stored, string posted, bool expected)
        => Assert.Equal(expected, SecretDestination.SameOrigin(stored, posted));

    [Fact]
    public async Task HttpApiRequestEditor_DropsTheKey_WhenTheBaseUrlMovesToAnotherHost()
    {
        var instance = HttpApiRequestInstance("https://api.vendor.example/", HttpApiRequestAuthenticationType.ApiKey);
        var context = CreateContext(new HttpApiRequestToolInstanceViewModel
        {
            BaseUrl = "https://attacker.example/",
            HttpMethod = "GET",
            AuthenticationType = HttpApiRequestAuthenticationType.ApiKey,
        });

        await HttpApiRequestDriver().UpdateAsync(instance, context);

        Assert.Null(instance.GetOrCreate<HttpApiRequestToolSettings>().ApiKey);
        Assert.Contains(context.Updater.ModelState, entry => entry.Key.EndsWith(nameof(HttpApiRequestToolInstanceViewModel.ApiKey), StringComparison.Ordinal) && entry.Value.Errors.Count > 0);
    }

    [Fact]
    public async Task HttpApiRequestEditor_KeepsTheKey_WhenOnlyThePathChanges()
    {
        var instance = HttpApiRequestInstance("https://api.vendor.example/v1/", HttpApiRequestAuthenticationType.ApiKey);
        var stored = instance.GetOrCreate<HttpApiRequestToolSettings>().ApiKey;
        var context = CreateContext(new HttpApiRequestToolInstanceViewModel
        {
            BaseUrl = "https://api.vendor.example/v2/",
            HttpMethod = "GET",
            AuthenticationType = HttpApiRequestAuthenticationType.ApiKey,
        });

        await HttpApiRequestDriver().UpdateAsync(instance, context);

        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(stored, instance.GetOrCreate<HttpApiRequestToolSettings>().ApiKey);
    }

    [Fact]
    public async Task HttpApiRequestEditor_DropsTheSecrets_WhenTheOAuth2TokenEndpointMoves()
    {
        var instance = new AIToolInstance { Source = HttpApiRequestToolConstants.SourceName };
        instance.Put(new HttpApiRequestToolSettings
        {
            BaseUrl = "https://api.vendor.example/",
            AuthenticationType = HttpApiRequestAuthenticationType.OAuth2,
            TokenEndpoint = "https://login.vendor.example/token",
            ClientId = "client",
            ClientSecret = _protection.ProtectSetting(HttpApiRequestToolConstants.DataProtectionPurpose, "secret"),
        });
        var context = CreateContext(new HttpApiRequestToolInstanceViewModel
        {
            BaseUrl = "https://api.vendor.example/",
            HttpMethod = "GET",
            AuthenticationType = HttpApiRequestAuthenticationType.OAuth2,
            TokenEndpoint = "https://attacker.example/token",
            ClientId = "client",
        });

        await HttpApiRequestDriver().UpdateAsync(instance, context);

        Assert.Null(instance.GetOrCreate<HttpApiRequestToolSettings>().ClientSecret);
    }

    [Fact]
    public async Task OpenAIConnectionEditor_DropsTheKey_WhenTheEndpointMoves()
    {
        var connection = new AIProviderConnection { ClientName = OpenAIConstants.ClientName };
        connection.Put(new OpenAIConnectionMetadata
        {
            Endpoint = new Uri("https://api.openai.com/v1"),
            ApiKey = _protection.ProtectSetting(AIConstants.ConnectionProtectorName, "sk-provider"),
        });
        var context = CreateContext(new OpenAIConnectionViewModel { Endpoint = "https://attacker.example/v1" });

        await new OpenAIProviderConnectionDisplayDriver(_protection, Policy(), new PassThroughLocalizer<OpenAIProviderConnectionDisplayDriver>())
            .UpdateAsync(connection, context);

        Assert.Null(connection.GetOrCreate<OpenAIConnectionMetadata>().ApiKey);
        Assert.False(context.Updater.ModelState.IsValid);
    }

    [Fact]
    public async Task OpenAIConnectionEditor_KeepsTheKey_WhenTheEndpointStays()
    {
        var connection = new AIProviderConnection { ClientName = OpenAIConstants.ClientName };
        var key = _protection.ProtectSetting(AIConstants.ConnectionProtectorName, "sk-provider");
        connection.Put(new OpenAIConnectionMetadata { Endpoint = new Uri("https://api.openai.com/v1"), ApiKey = key });
        var context = CreateContext(new OpenAIConnectionViewModel { Endpoint = "https://api.openai.com/v1" });

        await new OpenAIProviderConnectionDisplayDriver(_protection, Policy(), new PassThroughLocalizer<OpenAIProviderConnectionDisplayDriver>())
            .UpdateAsync(connection, context);

        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(key, connection.GetOrCreate<OpenAIConnectionMetadata>().ApiKey);
    }

    [Fact]
    public async Task CartesiaDeploymentEditor_DropsTheKey_WhenABlankEndpointBecomesACustomOne()
    {
        // A blank endpoint is the provider's own; a custom one is another place.
        var deployment = new AIDeployment { ClientName = CrestApps.Core.AI.Cartesia.CartesiaConstants.ClientName };
        deployment.Properties["ApiKey"] = _protection.ProtectSetting(AIConstants.ConnectionProtectorName, "cartesia-key");
        var context = CreateContext(new CartesiaDeploymentViewModel { Endpoint = "https://attacker.example/" });

        await new CartesiaDeploymentDisplayDriver(_protection, Policy(), new PassThroughLocalizer<CartesiaDeploymentDisplayDriver>())
            .UpdateAsync(deployment, context);

        Assert.False(deployment.Properties.ContainsKey("ApiKey"));
        Assert.False(context.Updater.ModelState.IsValid);
    }

    [Theory]
    [InlineData("https://attacker.example/sse", ClientAuthenticationType.ApiKey, false)]
    [InlineData("https://mcp.vendor.example/other", ClientAuthenticationType.ApiKey, true)]
    [InlineData("https://mcp.vendor.example/sse", ClientAuthenticationType.Basic, false)]
    public void ConnectionAuthEditor_KeepsTheKeyOnlyForTheSamePlaceAndType(string endpoint, ClientAuthenticationType type, bool kept)
    {
        var metadata = new SseMcpConnectionMetadata
        {
            Endpoint = new Uri("https://mcp.vendor.example/sse"),
            AuthenticationType = ClientAuthenticationType.ApiKey,
            ApiKeyHeaderName = "X-Api-Key",
            ApiKey = _protection.ProtectSetting("test-purpose", "mcp-key"),
            BasicPassword = _protection.ProtectSetting("test-purpose", "mcp-password"),
        };
        var stored = new[] { metadata.ApiKey, metadata.BasicPassword };
        var context = CreateContext(new object());
        var model = new ConnectionAuthFieldsViewModel { AuthenticationType = type, ApiKeyHeaderName = "X-Api-Key", BasicUsername = "user" };

        new TestAuthDriver(_protection).Apply(context, model, metadata, SecretDestination.SameOrigin(metadata.Endpoint, new Uri(endpoint)));

        var secret = type == ClientAuthenticationType.ApiKey ? metadata.ApiKey : metadata.BasicPassword;

        Assert.Equal(kept, secret is not null && stored.Contains(secret));
        Assert.Equal(kept, context.Updater.ModelState.IsValid);
    }

    [Fact]
    public void RecipeUpdate_DropsTheKeptKey_WhenTheEndpointMoves()
    {
        var connection = new AIProviderConnection { ClientName = OpenAIConstants.ClientName };
        connection.Put(new OpenAIConnectionMetadata { Endpoint = new Uri("https://api.openai.com/v1"), ApiKey = "protected-key" });
        var before = RecipeSecretDestinations.Snapshot(connection);

        // The recipe changes the endpoint and leaves the key out, so the handler keeps the stored one.
        var changed = connection.GetOrCreate<OpenAIConnectionMetadata>();
        changed.Endpoint = new Uri("https://attacker.example/v1");
        connection.Put(changed);

        Assert.True(RecipeSecretDestinations.DropKeptSecrets(connection, before));
        Assert.Null(connection.GetOrCreate<OpenAIConnectionMetadata>().ApiKey);
        Assert.Equal("https://attacker.example/v1", connection.GetOrCreate<OpenAIConnectionMetadata>().Endpoint.ToString());
    }

    [Fact]
    public void RecipeUpdate_KeepsANewKey_AndAKeyForTheSamePlace()
    {
        var moved = new AIDeployment();
        moved.Properties["Endpoint"] = "https://api.cartesia.ai/";
        moved.Properties["ApiKey"] = "old";
        var movedBefore = RecipeSecretDestinations.Snapshot(moved);
        moved.Properties["Endpoint"] = "https://custom.example/";
        moved.Properties["ApiKey"] = "new";

        var stayed = new AIDeployment();
        stayed.Properties["Endpoint"] = "https://api.cartesia.ai/v1";
        stayed.Properties["ApiKey"] = "old";
        var stayedBefore = RecipeSecretDestinations.Snapshot(stayed);
        stayed.Properties["Endpoint"] = "https://api.cartesia.ai/v2";

        Assert.False(RecipeSecretDestinations.DropKeptSecrets(moved, movedBefore));
        Assert.Equal("new", moved.Properties["ApiKey"]);
        Assert.False(RecipeSecretDestinations.DropKeptSecrets(stayed, stayedBefore));
        Assert.Equal("old", stayed.Properties["ApiKey"]);
    }

    [Fact]
    public void RecipeUpdate_DropsTheKeptKey_WhenTheAuthenticationTypeChanges()
    {
        var deployment = new AIDeployment();
        deployment.Properties["Endpoint"] = "https://speech.example/";
        deployment.Properties["AuthenticationType"] = "ApiKey";
        deployment.Properties["ApiKey"] = "old";
        var before = RecipeSecretDestinations.Snapshot(deployment);
        deployment.Properties["AuthenticationType"] = "ManagedIdentity";

        Assert.True(RecipeSecretDestinations.DropKeptSecrets(deployment, before));
        Assert.False(deployment.Properties.ContainsKey("ApiKey"));
    }

    private AIToolInstance HttpApiRequestInstance(string baseUrl, HttpApiRequestAuthenticationType type)
    {
        var instance = new AIToolInstance { Source = HttpApiRequestToolConstants.SourceName };
        instance.Put(new HttpApiRequestToolSettings
        {
            BaseUrl = baseUrl,
            AuthenticationType = type,
            ApiKeyHeaderName = "X-Api-Key",
            ApiKey = _protection.ProtectSetting(HttpApiRequestToolConstants.DataProtectionPurpose, "vendor-key"),
        });

        return instance;
    }

    private HttpApiRequestToolInstanceDisplayDriver HttpApiRequestDriver()
        => new(_protection, Policy(), new PassThroughLocalizer<HttpApiRequestToolInstanceDisplayDriver>());

    // Every name resolves to a public address.
    private static AIEndpointPolicy Policy()
        => new(null, Options.Create(new AIEgressOptions()), _ => [IPAddress.Parse("93.184.215.14")]);

    private static UpdateEditorContext CreateContext(object posted)
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(posted));

    private sealed class TestConnection;

    private sealed class TestAuthDriver : ConnectionAuthDisplayDriverBase<TestConnection>
    {
        public TestAuthDriver(IDataProtectionProvider protection)
            : base(protection, "test-purpose", new PassThroughLocalizer<TestAuthDriver>())
        {
        }

        public void Apply(UpdateEditorContext context, ConnectionAuthFieldsViewModel model, IConnectionAuthMetadata metadata, bool samePlace)
            => ApplyAuthFields(context, model, metadata, samePlace);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
