using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Connectors.Http;
using CrestApps.OrchardCore.DataConnectors.Drivers;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.ViewModels;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Tests.DataConnectors;

/// <summary>
/// SEC-A20 l: the HTTP connection editor drops the stored secrets when an edit would send them somewhere they were not
/// given for. These tests post the editor through <see cref="HttpConnectionDisplayDriver.UpdateAsync"/>.
/// </summary>
public sealed class HttpConnectionDriverSecretTests
{
    [Fact]
    public async Task SwitchingBasicToAPasswordGrantWithANewTokenHost_DropsTheStoredPassword()
    {
        var (connection, secrets) = Stored(new HttpAuthenticationSettings { Type = HttpAuthenticationType.Basic, UserName = "svc" }, HttpAuthenticationSecrets.Password);

        await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api.example.com/v1",
            AuthenticationType = HttpAuthenticationType.OAuth2,
            GrantType = OAuthGrantType.Password,
            TokenUrl = "https://attacker.example/token",
            ClientId = "client",
            UserName = "svc",
        });

        Assert.False(secrets.Has(connection, HttpAuthenticationSecrets.Password));
    }

    [Fact]
    public async Task SwitchingOpenIdConnectToOAuth2WithATokenUrl_DropsTheStoredClientSecret()
    {
        var (connection, secrets) = Stored(
            new HttpAuthenticationSettings { Type = HttpAuthenticationType.OpenIdConnect, Authority = "https://login.example.com", ClientId = "client" },
            HttpAuthenticationSecrets.ClientSecret);

        await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api.example.com/v1",
            AuthenticationType = HttpAuthenticationType.OAuth2,
            GrantType = OAuthGrantType.ClientCredentials,
            TokenUrl = "https://attacker.example/token",
            Authority = "https://login.example.com",
            ClientId = "client",
        });

        Assert.False(secrets.Has(connection, HttpAuthenticationSecrets.ClientSecret));
    }

    [Fact]
    public async Task AddingATokenUrlWhereThereWasNone_DropsTheStoredSecret()
    {
        var (connection, secrets) = Stored(
            new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2, GrantType = OAuthGrantType.ClientCredentials, ClientId = "client" },
            HttpAuthenticationSecrets.ClientSecret);

        await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api.example.com/v1",
            AuthenticationType = HttpAuthenticationType.OAuth2,
            GrantType = OAuthGrantType.ClientCredentials,
            TokenUrl = "https://attacker.example/token",
            ClientId = "client",
        });

        Assert.False(secrets.Has(connection, HttpAuthenticationSecrets.ClientSecret));
    }

    [Fact]
    public async Task AnEditThatKeepsTheTypeAndTheHosts_KeepsTheStoredSecret()
    {
        var (connection, secrets) = Stored(new HttpAuthenticationSettings { Type = HttpAuthenticationType.Basic, UserName = "svc" }, HttpAuthenticationSecrets.Password);

        await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api.example.com/v2",
            AuthenticationType = HttpAuthenticationType.Basic,
            UserName = "svc",
            TimeoutSeconds = 30,
        });

        Assert.True(secrets.Has(connection, HttpAuthenticationSecrets.Password));
    }

    /// <summary>
    /// When the secrets are dropped and the box is left empty, the save must say what is missing instead of saving a
    /// Basic or OAuth connection that can no longer sign in.
    /// </summary>
    [Fact]
    public async Task FixingTheHostOfABasicConnection_WithThePasswordLeftEmpty_AsksForThePassword()
    {
        var (connection, secrets) = Stored(new HttpAuthenticationSettings { Type = HttpAuthenticationType.Basic, UserName = "svc" }, HttpAuthenticationSecrets.Password);

        var modelState = await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api2.example.com/v1",
            AuthenticationType = HttpAuthenticationType.Basic,
            UserName = "svc",
            TimeoutSeconds = 30,
        });

        Assert.False(modelState.IsValid);
        Assert.Contains(modelState, entry => entry.Key.EndsWith(nameof(HttpConnectionViewModel.Password), StringComparison.Ordinal));
    }

    [Fact]
    public async Task MovingTheTokenEndpointOfAnOAuthConnection_WithTheSecretLeftEmpty_AsksForTheClientSecret()
    {
        var (connection, secrets) = Stored(
            new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2, GrantType = OAuthGrantType.ClientCredentials, TokenUrl = "https://login.example.com/token", ClientId = "client" },
            HttpAuthenticationSecrets.ClientSecret);

        var modelState = await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api.example.com/v1",
            AuthenticationType = HttpAuthenticationType.OAuth2,
            GrantType = OAuthGrantType.ClientCredentials,
            TokenUrl = "https://login2.example.com/token",
            ClientId = "client",
            TimeoutSeconds = 30,
        });

        Assert.False(modelState.IsValid);
        Assert.Contains(modelState, entry => entry.Key.EndsWith(nameof(HttpConnectionViewModel.ClientSecret), StringComparison.Ordinal));
    }

    [Fact]
    public async Task MovingTheTokenEndpointOfAPasswordGrant_WithThePasswordLeftEmpty_AsksForThePassword()
    {
        var (connection, secrets) = Stored(
            new HttpAuthenticationSettings { Type = HttpAuthenticationType.OAuth2, GrantType = OAuthGrantType.Password, TokenUrl = "https://login.example.com/token", ClientId = "client", UserName = "svc" },
            HttpAuthenticationSecrets.Password);

        var modelState = await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api.example.com/v1",
            AuthenticationType = HttpAuthenticationType.OAuth2,
            GrantType = OAuthGrantType.Password,
            TokenUrl = "https://login2.example.com/token",
            ClientId = "client",
            UserName = "svc",
            TimeoutSeconds = 30,
        });

        Assert.False(modelState.IsValid);
        Assert.Contains(modelState, entry => entry.Key.EndsWith(nameof(HttpConnectionViewModel.Password), StringComparison.Ordinal));
    }

    [Fact]
    public async Task APublicOAuthClientThatNeverHadASecret_SavesWithoutOne()
    {
        var secrets = new DataConnectionSecretManager(new EphemeralDataProtectionProvider(), NullLogger<DataConnectionSecretManager>.Instance);
        var connection = new DataConnection { Name = "api", Connector = HttpApiDataConnector.Name };

        var modelState = await PostAsync(connection, secrets, new HttpConnectionViewModel
        {
            BaseUrl = "https://api.example.com/v1",
            AuthenticationType = HttpAuthenticationType.OAuth2,
            GrantType = OAuthGrantType.ClientCredentials,
            TokenUrl = "https://login.example.com/token",
            ClientId = "client",
            TimeoutSeconds = 30,
        });

        Assert.True(modelState.IsValid);
    }

    private static (DataConnection Connection, IDataConnectionSecretManager Secrets) Stored(HttpAuthenticationSettings authentication, string secretName)
    {
        var secrets = new DataConnectionSecretManager(new EphemeralDataProtectionProvider(), NullLogger<DataConnectionSecretManager>.Instance);
        var connection = new DataConnection { Name = "api", Connector = HttpApiDataConnector.Name };

        connection.Put(new HttpConnectionSettings { BaseUrl = "https://api.example.com/v1", Authentication = authentication });
        secrets.Set(connection, secretName, "stored-secret");

        return (connection, secrets);
    }

    private static async Task<Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary> PostAsync(DataConnection connection, IDataConnectionSecretManager secrets, HttpConnectionViewModel posted)
    {
        var driver = new HttpConnectionDisplayDriver(secrets, new PassThroughStringLocalizer<HttpConnectionDisplayDriver>());
        var updater = new PostedUpdater(posted);
        var context = new UpdateEditorContext(Mock.Of<IShape>(), string.Empty, false, string.Empty, Mock.Of<IShapeFactory>(), null, updater);

        await driver.UpdateAsync(connection, context);

        return updater.ModelState;
    }

    private sealed class PostedUpdater : IUpdateModel
    {
        private readonly HttpConnectionViewModel _posted;

        public PostedUpdater(HttpConnectionViewModel posted)
        {
            _posted = posted;
        }

        public Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model) where TModel : class => TryUpdateModelAsync(model, string.Empty);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix) where TModel : class
        {
            if (model is HttpConnectionViewModel target)
            {
                foreach (var property in typeof(HttpConnectionViewModel).GetProperties().Where(property => property.CanRead && property.CanWrite))
                {
                    property.SetValue(target, property.GetValue(_posted));
                }
            }

            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params System.Linq.Expressions.Expression<Func<TModel, object>>[] includeExpressions) where TModel : class
            => TryUpdateModelAsync(model, prefix);

        public bool TryValidateModel(object model) => true;

        public bool TryValidateModel(object model, string prefix) => true;
    }
}
