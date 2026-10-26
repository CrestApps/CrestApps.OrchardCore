using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.Drivers;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// The retrieval settings on an AI profile's Knowledge tab say a blank Strictness or Retrieved documents uses the
/// default. A blank was rejected as out of range, because the default it resolved to differed from the blank posted.
/// </summary>
public sealed class AIProfileDataSourceDisplayDriverTests
{
    [Fact]
    public async Task UpdateAsync_WhenRetrievalFieldsAreBlank_StoresTheSiteDefaults()
    {
        // Arrange
        var profile = new AIProfile();
        var context = PostedFormUpdateModel.CreateContext(new EditProfileDataSourcesViewModel());

        // Act
        await CreateDriver().UpdateAsync(profile, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);

        var metadata = profile.GetOrCreate<AIDataSourceRagMetadata>();

        Assert.Equal(DataSourceRetrievalTestServices.SiteDefaultStrictness, metadata.Strictness);
        Assert.Equal(DataSourceRetrievalTestServices.SiteDefaultTopNDocuments, metadata.TopNDocuments);
    }

    [Fact]
    public async Task UpdateAsync_WhenRetrievalFieldsAreInRange_StoresThem()
    {
        // Arrange
        var profile = new AIProfile();
        var context = PostedFormUpdateModel.CreateContext(new EditProfileDataSourcesViewModel { Strictness = 5, TopNDocuments = 20 });

        // Act
        await CreateDriver().UpdateAsync(profile, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);

        var metadata = profile.GetOrCreate<AIDataSourceRagMetadata>();

        Assert.Equal(5, metadata.Strictness);
        Assert.Equal(20, metadata.TopNDocuments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task UpdateAsync_WhenStrictnessIsOutOfRange_RejectsIt(int strictness)
    {
        // Arrange
        var context = PostedFormUpdateModel.CreateContext(new EditProfileDataSourcesViewModel { Strictness = strictness });

        // Act
        await CreateDriver().UpdateAsync(new AIProfile(), context);

        // Assert
        var entry = Assert.Single(context.Updater.ModelState);

        Assert.Equal(nameof(EditProfileDataSourcesViewModel.Strictness), entry.Key);
        Assert.Single(entry.Value.Errors);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(21)]
    public async Task UpdateAsync_WhenTopNDocumentsIsOutOfRange_RejectsIt(int topNDocuments)
    {
        // Arrange
        var context = PostedFormUpdateModel.CreateContext(new EditProfileDataSourcesViewModel { TopNDocuments = topNDocuments });

        // Act
        await CreateDriver().UpdateAsync(new AIProfile(), context);

        // Assert
        var entry = Assert.Single(context.Updater.ModelState);

        Assert.Equal(nameof(EditProfileDataSourcesViewModel.TopNDocuments), entry.Key);
        Assert.Single(entry.Value.Errors);
    }

    private static AIProfileDataSourceDisplayDriver CreateDriver()
        => new(
            DataSourceRetrievalTestServices.SiteService(),
            DataSourceRetrievalTestServices.ODataValidator(),
            DataSourceRetrievalTestServices.DataSourceStore(),
            DataSourceRetrievalTestServices.Localizer<AIProfileDataSourceDisplayDriver>());
}
