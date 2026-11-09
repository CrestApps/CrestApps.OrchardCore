using System.Text.Json;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

/// <summary>
/// Covers the recipe schemas that describe the steps, settings, parts and fields added or changed in Orchard Core 4.0.
/// </summary>
public sealed class OrchardCore4RecipeSchemaTests
{
    [Fact]
    public async Task ReloadTenantRecipeStep_AcceptsTheBareStep()
    {
        // Arrange
        var schema = await new ReloadTenantRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(Evaluate(schema, """{ "name": "ReloadTenant" }"""));
        Assert.False(Evaluate(schema, """{ "name": "Reload" }"""));
    }

    [Fact]
    public async Task SecretsRecipeStep_AcceptsTheListOfTextSecrets()
    {
        // Arrange
        var schema = await new SecretsRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(Evaluate(
            schema,
            """
            {
                "name": "Secrets",
                "Secrets": [
                    { "Name": "Smtp.Password", "Store": "Database" },
                    { "Name": "Payment.ApiKey", "Value": "value-for-a-local-test-site" }
                ]
            }
            """));
        Assert.False(Evaluate(schema, """{ "name": "Secrets", "Secrets": [ { "Store": "Database" } ] }"""));
        Assert.False(Evaluate(schema, """{ "name": "Secrets" }"""));
    }

    [Fact]
    public async Task SecretsRecipeStep_AcceptsTheExportedSecrets()
    {
        // Arrange
        var schema = await new SecretsRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(Evaluate(
            schema,
            """
            {
                "name": "Secrets",
                "EncryptionKeyName": "Deployment.Key",
                "Secrets": {
                    "SmtpPassword": {
                        "SecretInfo": { "Store": "Database", "Type": "TextSecret", "Description": null, "ExpiresUtc": null },
                        "Version": 1,
                        "EncryptedKey": "a2V5",
                        "EncryptedData": "ZGF0YQ==",
                        "IV": "aXY=",
                        "Tag": "dGFn"
                    }
                }
            }
            """));
    }

    [Fact]
    public async Task DataPipelinesRecipeStep_AcceptsTheDocumentedPipeline()
    {
        // Arrange
        var schema = await new DataPipelinesRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(Evaluate(
            schema,
            """
            {
                "name": "DataPipelines",
                "Pipelines": [
                    {
                        "PipelineId": "4fa5t9rfmxagnt3ab966tzrk5r",
                        "Name": "Blog posts export",
                        "IsEnabled": true,
                        "Definition": {
                            "Steps": [
                                { "StepId": "source", "Type": "DataSource", "X": 180, "Y": 320, "Properties": { "DataSourceStepSettings": { "Source": "Contents", "DataSet": "BlogPost" } } },
                                { "StepId": "file", "Type": "CreateFile", "X": 480, "Y": 320, "Properties": { "CreateFileStepSettings": { "Format": "xlsx", "FileName": "posts" } } }
                            ],
                            "Connections": [
                                { "SourceStepId": "source", "SourcePort": "Output", "TargetStepId": "file", "TargetPort": "Input" }
                            ]
                        }
                    }
                ]
            }
            """));
        Assert.False(Evaluate(schema, """{ "name": "DataPipelines", "Pipelines": [ { "PipelineId": "abcdefghijklmnopqrstuvwxyz0" , "Name": "Too long" } ] }"""));
    }

    [Fact]
    public async Task CreateOrUpdateRateLimitPoliciesRecipeStep_AcceptsTheDefaultGlobalPolicy()
    {
        // Arrange
        var schema = await new CreateOrUpdateRateLimitPoliciesRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(Evaluate(
            schema,
            """
            {
                "name": "CreateOrUpdateRateLimitPolicies",
                "policies": [
                    {
                        "PolicyId": "[js: uuid()]",
                        "Name": "Default Global Policy",
                        "IsEnabled": true,
                        "Scope": "Global",
                        "Limiters": [
                            { "Id": "[js: uuid()]", "Source": "FixedWindow", "Properties": { "FixedWindowRateLimiterData": { "PermitLimit": 1000, "WindowSeconds": 60, "QueueLimit": 0 } } }
                        ]
                    }
                ]
            }
            """));
        Assert.False(Evaluate(schema, """{ "name": "CreateOrUpdateRateLimitPolicies", "policies": [ { "Name": "Bad", "Scope": "Tenant" } ] }"""));
        Assert.False(Evaluate(schema, """{ "name": "CreateOrUpdateRateLimitPolicies" }"""));
    }

    [Fact]
    public async Task SettingsRecipeStep_AcceptsThePageSizeSelection()
    {
        // Arrange
        var schema = await new SettingsRecipeStep([]).GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(Evaluate(schema, """{ "name": "settings", "AllowPageSizeSelection": true, "PageSizeOptions": [ 10, 25, 50 ] }"""));
        Assert.False(Evaluate(schema, """{ "name": "settings", "PageSizeOptions": [ 0 ] }"""));
    }

    [Theory]
    [InlineData("\"https://www.example.com\"", true)]
    [InlineData("\"\"", true)]
    [InlineData("null", true)]
    [InlineData("\"/relative\"", false)]
    [InlineData("\"www.example.com\"", false)]
    public async Task SettingsRecipeStep_RequiresAbsoluteBaseUrls(string value, bool isValid)
    {
        // Arrange
        var schema = await new SettingsRecipeStep([]).GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.Equal(isValid, Evaluate(schema, $$"""{ "name": "settings", "BaseUrl": {{value}}, "CdnBaseUrl": {{value}} }"""));
    }

    [Fact]
    public async Task OpenIdServerSettingsRecipeStep_DescribesTheEndSessionConfirmation()
    {
        // Arrange
        var schema = await new OpenIdServerSettingsRecipeStep().GetSchemaAsync(TestContext.Current.CancellationToken);

        // Act
        var json = JsonSerializer.Serialize(schema);

        // Assert
        Assert.Contains("\"RequireEndSessionConfirmation\"", json);
    }

    [Fact]
    public async Task AdminSettingsSchema_AcceptsTheOrchardCore4Options()
    {
        // Arrange
        var schema = await BuildAsync(new AdminSettingsSchema());

        // Act & Assert
        Assert.True(Evaluate(
            schema,
            """
            {
                "DisplayQuickNavigation": true,
                "ShowBreadcrumb": false,
                "MenuBehavior": "Persistent",
                "ListLayout": "Grid",
                "ActionsLayout": "Menu",
                "AllowUserListLayoutSelection": true
            }
            """));
        Assert.False(Evaluate(schema, """{ "ListActionsLayout": "Menu" }"""));
    }

    [Fact]
    public async Task LoginSettingsSchema_AcceptsThePersistentAuthenticationCookie()
    {
        // Arrange
        var schema = await BuildAsync(new LoginSettingsSchema());

        // Act & Assert
        Assert.True(Evaluate(schema, """{ "UsePersistentAuthenticationCookie": true }"""));
    }

    [Fact]
    public async Task HttpsSettingsSchema_UsesTheStrictTransportSecurityMode()
    {
        // Arrange
        var schema = await BuildAsync(new HttpsSettingsSchema());

        // Act & Assert
        Assert.True(Evaluate(schema, """{ "StrictTransportSecurityMode": "FromConfiguration", "RequireHttps": true }"""));
        Assert.False(Evaluate(schema, """{ "EnableStrictTransportSecurity": true }"""));
    }

    [Theory]
    [InlineData(typeof(SmtpSettingsSchema), """{ "Host": "smtp.example.com", "PasswordSecretName": "Smtp.Password" }""")]
    [InlineData(typeof(GitHubAuthenticationSettingsSchema), """{ "ClientID": "id", "ClientSecretSecretName": "GitHub.ClientSecret" }""")]
    [InlineData(typeof(GoogleAuthenticationSettingsSchema), """{ "ClientID": "id", "ClientSecretSecretName": "Google.ClientSecret" }""")]
    [InlineData(typeof(MicrosoftAccountSettingsSchema), """{ "AppId": "id", "AppSecretSecretName": "Microsoft.AppSecret" }""")]
    [InlineData(typeof(FacebookSettingsSchema), """{ "AppId": "id", "SdkJs": "sdk.js", "AppSecretSecretName": "Facebook.AppSecret" }""")]
    [InlineData(typeof(TwitterSettingsSchema), """{ "ConsumerKey": "key", "AccessToken": "token", "ConsumerSecretSecretName": "Twitter.ConsumerSecret", "AccessTokenSecretSecretName": "Twitter.AccessTokenSecret" }""")]
    [InlineData(typeof(TwilioSettingsSchema), """{ "PhoneNumber": "+15555550100", "AccountSID": "sid", "AuthTokenSecretName": "Twilio.AuthToken" }""")]
    [InlineData(typeof(AzureSmsSettingsSchema), """{ "PhoneNumber": "+15555550100", "ConnectionStringSecretName": "AzureSms.ConnectionString" }""")]
    [InlineData(typeof(AzureEmailSettingsSchema), """{ "DefaultSender": "noreply@example.com", "ConnectionStringSecretName": "AzureEmail.ConnectionString" }""")]
    [InlineData(typeof(AzureAISearchDefaultSettingsSchema), """{ "AuthenticationType": "ApiKey", "Endpoint": "https://search.example.com", "ApiKeySecretName": "AzureAISearch.ApiKey" }""")]
    [InlineData(typeof(OpenIdClientSettingsSchema), """{ "DisplayName": "Login", "Authority": "https://login.example.com", "ClientId": "id", "ClientSecretSecretName": "OpenId.ClientSecret" }""")]
    public async Task CredentialSettings_AcceptASecretReferenceInsteadOfTheProtectedValue(Type schemaType, string json)
    {
        // Arrange
        var schema = await BuildAsync((ISiteSettingsSchemaDefinition)Activator.CreateInstance(schemaType));

        // Act & Assert
        Assert.True(Evaluate(schema, json));
    }

    [Fact]
    public async Task OpenIdSecretSettingsSchema_ReferencesTheKeySecrets()
    {
        // Arrange
        var schema = await BuildAsync(new OpenIdSecretSettingsSchema());

        // Act & Assert
        Assert.True(Evaluate(schema, """{ "SigningKeySecretName": "OpenId.SigningCertificate", "EncryptionKeySecretName": "OpenId.EncryptionCertificate" }"""));
        Assert.False(Evaluate(schema, """{ "SigningCertificateThumbprint": "ABC" }"""));
    }

    [Fact]
    public async Task TextFieldSchema_AcceptsTheLengthLimits()
    {
        // Arrange
        var settings = await new TextFieldSchema().GetSettingsSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.True(Evaluate(settings, """{ "TextFieldSettings": { "MinLength": 2, "MaxLength": 50 } }"""));
        Assert.False(Evaluate(settings, """{ "TextFieldSettings": { "MinLength": -1 } }"""));
    }

    [Theory]
    [InlineData(typeof(HtmlBodyPartSchema), """{ "HtmlBodyPartSettings": { "RenderLiquid": true } }""")]
    [InlineData(typeof(MarkdownBodyPartSchema), """{ "MarkdownBodyPartSettings": { "RenderLiquid": true } }""")]
    [InlineData(typeof(HtmlFieldSchema), """{ "HtmlFieldSettings": { "RenderLiquid": true } }""")]
    [InlineData(typeof(MarkdownFieldSchema), """{ "MarkdownFieldSettings": { "RenderLiquid": true } }""")]
    public async Task BodySettings_RejectRenderLiquid(Type schemaType, string json)
    {
        // Arrange
        var definition = (IContentSchemaDefinition)Activator.CreateInstance(schemaType);
        var settings = await definition.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.False(Evaluate(settings, json));
    }

    [Fact]
    public async Task LiquidPartAndLiquidField_AreDescribed()
    {
        // Arrange
        var part = new LiquidPartSchema();
        var field = new LiquidFieldSchema();

        // Act
        var partSettings = await part.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);
        var fieldSettings = await field.GetSettingsSchemaAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("LiquidPart", part.Name);
        Assert.Equal(ContentDefinitionSchemaType.Part, part.Type);
        Assert.Equal("LiquidField", field.Name);
        Assert.Equal(ContentDefinitionSchemaType.Field, field.Type);
        Assert.NotNull(partSettings);
        Assert.NotNull(fieldSettings);
    }

    private static async Task<JsonSchemaBuilder> BuildAsync(ISiteSettingsSchemaDefinition definition)
        => await definition.GetSchemaAsync(TestContext.Current.CancellationToken);

    private static bool Evaluate(JsonSchemaBuilder builder, string json)
        => Evaluate(builder.Build(), json);

    private static bool Evaluate(JsonSchema schema, string json)
    {
        using var document = JsonDocument.Parse(json);

        return schema.Evaluate(document.RootElement).IsValid;
    }
}
