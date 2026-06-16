using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

namespace CrestApps.OrchardCore.Tests.Core.Schemas;

public sealed class SiteSettingsSchemaDefinitionTests
{
    [Fact]
    public async Task LoginSettingsSchema_ContainsAllowRememberMeWithDefaultTrue()
    {
        var schema = await new LoginSettingsSchema().GetSchemaAsync(TestContext.Current.CancellationToken);
        var json = schema.Build().Root.Source.GetRawText();

        Assert.Contains("AllowRememberMe", json);
        Assert.Contains("remembered across browser sessions", json);
        Assert.Contains("\"default\":true", json);
    }

    [Fact]
    public async Task DncRegistrySettingsSchemas_ContainExpectedProperties()
    {
        var dncRegistrySchema = await new DncRegistrySettingsSchema().GetSchemaAsync(TestContext.Current.CancellationToken);
        var dncRegistryJson = dncRegistrySchema.Build().Root.Source.GetRawText();

        Assert.Contains("EnforceGlobally", dncRegistryJson);
        Assert.Contains("EnforcedRegistryKeys", dncRegistryJson);

        var usaSchema = await new UsaFtcDncRegistrySettingsSchema().GetSchemaAsync(TestContext.Current.CancellationToken);
        var usaJson = usaSchema.Build().Root.Source.GetRawText();

        Assert.Contains("ProtectedApiKey", usaJson);
        Assert.Contains("OrganizationId", usaJson);
        Assert.Contains("BaseUrl", usaJson);

        var canadaSchema = await new CanadaDnclRegistrySettingsSchema().GetSchemaAsync(TestContext.Current.CancellationToken);
        var canadaJson = canadaSchema.Build().Root.Source.GetRawText();

        Assert.Contains("ProtectedApiKey", canadaJson);
        Assert.Contains("AccountNumber", canadaJson);
        Assert.Contains("BaseUrl", canadaJson);
    }
}
