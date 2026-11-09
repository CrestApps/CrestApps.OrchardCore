using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core.Drivers;
using CrestApps.OrchardCore.AI.Core.ViewModels;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Drivers;

/// <summary>
/// The A2A and SSE MCP connection editors share <see cref="ConnectionAuthDisplayDriverBase{TModel}"/>: only the fields
/// of the chosen authentication type are kept, and a blank secret keeps the stored one.
/// </summary>
public sealed class ConnectionAuthDisplayDriverBaseTests
{
    private const string Purpose = "TestConnectionSecrets";

    private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();

    [Fact]
    public void ApplyAuthFields_WhenTheApiKeyIsLeftBlank_ShouldKeepTheStoredKey()
    {
        // Arrange
        var stored = _dataProtectionProvider.CreateProtector(Purpose).Protect("key");
        var metadata = new A2AConnectionMetadata { AuthenticationType = ClientAuthenticationType.ApiKey, ApiKey = stored };
        var model = new ConnectionAuthFieldsViewModel { AuthenticationType = ClientAuthenticationType.ApiKey, ApiKeyHeaderName = "X-Key" };
        var context = CreateContext();

        // Act
        new TestDriver<A2AConnection>(_dataProtectionProvider).Apply(context, model, metadata);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal(stored, metadata.ApiKey);
        Assert.Equal("X-Key", metadata.ApiKeyHeaderName);
    }

    [Fact]
    public void ApplyAuthFields_WhenTheTypeChanges_ShouldClearTheOtherSecrets()
    {
        // Arrange
        var metadata = new SseMcpConnectionMetadata
        {
            AuthenticationType = ClientAuthenticationType.ApiKey,
            ApiKey = "stored-key",
            Endpoint = new Uri("https://mcp.example.com/"),
        };
        var model = new ConnectionAuthFieldsViewModel
        {
            AuthenticationType = ClientAuthenticationType.Basic,
            BasicUsername = "admin",
            BasicPassword = "password",
        };
        var context = CreateContext();

        // Act
        new TestDriver<McpConnection>(_dataProtectionProvider).Apply(context, model, metadata);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Null(metadata.ApiKey);
        Assert.Equal("admin", metadata.BasicUsername);
        Assert.Equal("password", _dataProtectionProvider.CreateProtector(Purpose).Unprotect(metadata.BasicPassword));
        Assert.Equal(new Uri("https://mcp.example.com/"), metadata.Endpoint);
    }

    [Fact]
    public void ApplyAuthFields_WhenARequiredSecretIsMissing_ShouldReportIt()
    {
        // Arrange
        var metadata = new A2AConnectionMetadata();
        var model = new ConnectionAuthFieldsViewModel { AuthenticationType = ClientAuthenticationType.OAuth2ClientCredentials };
        var context = CreateContext();

        // Act
        new TestDriver<A2AConnection>(_dataProtectionProvider).Apply(context, model, metadata);

        // Assert
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(ConnectionAuthFieldsViewModel.OAuth2ClientSecret)));
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(ConnectionAuthFieldsViewModel.OAuth2ClientId)));
        Assert.True(context.Updater.ModelState.ContainsKey(nameof(ConnectionAuthFieldsViewModel.OAuth2TokenEndpoint)));
    }

    private static UpdateEditorContext CreateContext()
        => new(null, string.Empty, false, string.Empty, null, null, new FakeUpdateModel(null));

    private sealed class TestDriver<TModel> : ConnectionAuthDisplayDriverBase<TModel>
        where TModel : class
    {
        public TestDriver(IDataProtectionProvider dataProtectionProvider)
            : base(dataProtectionProvider, Purpose, new PassThroughLocalizer())
        {
        }

        public void Apply(UpdateEditorContext context, ConnectionAuthFieldsViewModel model, IConnectionAuthMetadata metadata)
            => ApplyAuthFields(context, model, metadata);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
