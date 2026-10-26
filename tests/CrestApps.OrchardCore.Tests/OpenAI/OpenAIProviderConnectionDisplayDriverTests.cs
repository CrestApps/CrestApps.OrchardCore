using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Core;
using CrestApps.OrchardCore.OpenAI.Drivers;
using CrestApps.OrchardCore.OpenAI.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Modules.AI.DataSources;
using Microsoft.AspNetCore.DataProtection;

namespace CrestApps.OrchardCore.Tests.OpenAI;

/// <summary>
/// An OpenAI connection needs an API key. The editor reported a missing key under the Endpoint field, so the message
/// showed beside a field that was filled in correctly.
/// </summary>
public sealed class OpenAIProviderConnectionDisplayDriverTests
{
    [Fact]
    public async Task UpdateAsync_WhenNoApiKeyIsStoredOrPosted_ReportsItUnderTheApiKeyField()
    {
        // Arrange
        var connection = new AIProviderConnection { ClientName = OpenAIConstants.ClientName };
        var context = PostedFormUpdateModel.CreateContext(new OpenAIConnectionViewModel { Endpoint = "https://api.openai.com/v1" });

        // Act
        await CreateDriver().UpdateAsync(connection, context);

        // Assert
        var entry = Assert.Single(context.Updater.ModelState);

        Assert.Equal(nameof(OpenAIConnectionViewModel.ApiKey), entry.Key);
        Assert.Single(entry.Value.Errors);
    }

    [Fact]
    public async Task UpdateAsync_WhenAnApiKeyIsAlreadyStored_AcceptsABlankOne()
    {
        // Arrange
        var connection = new AIProviderConnection { ClientName = OpenAIConstants.ClientName };
        connection.Put(new OpenAIConnectionMetadata { ApiKey = "stored-key" });

        var context = PostedFormUpdateModel.CreateContext(new OpenAIConnectionViewModel { Endpoint = "https://api.openai.com/v1" });

        // Act
        await CreateDriver().UpdateAsync(connection, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);
        Assert.Equal("stored-key", connection.GetOrCreate<OpenAIConnectionMetadata>().ApiKey);
    }

    private static OpenAIProviderConnectionDisplayDriver CreateDriver()
        => new(
            new EphemeralDataProtectionProvider(),
            DataSourceRetrievalTestServices.Localizer<OpenAIProviderConnectionDisplayDriver>());
}
