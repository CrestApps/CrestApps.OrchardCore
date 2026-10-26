using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.Drivers;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// A profile template carries the same retrieval settings as a profile, with the same hint that a blank Strictness
/// or Retrieved documents uses the default.
/// </summary>
public sealed class AIProfileTemplateDataSourceDisplayDriverTests
{
    [Fact]
    public async Task UpdateAsync_WhenRetrievalFieldsAreBlank_StoresTheSiteDefaults()
    {
        // Arrange
        var template = new AIProfileTemplate { Source = AITemplateSources.Profile };
        var context = PostedFormUpdateModel.CreateContext(new EditProfileDataSourcesViewModel());

        // Act
        await CreateDriver().UpdateAsync(template, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);

        var metadata = template.GetOrCreate<AIDataSourceRagMetadata>();

        Assert.Equal(DataSourceRetrievalTestServices.SiteDefaultStrictness, metadata.Strictness);
        Assert.Equal(DataSourceRetrievalTestServices.SiteDefaultTopNDocuments, metadata.TopNDocuments);
    }

    [Theory]
    [InlineData(0, 3, nameof(EditProfileDataSourcesViewModel.Strictness))]
    [InlineData(6, 3, nameof(EditProfileDataSourcesViewModel.Strictness))]
    [InlineData(1, 2, nameof(EditProfileDataSourcesViewModel.TopNDocuments))]
    [InlineData(1, 21, nameof(EditProfileDataSourcesViewModel.TopNDocuments))]
    public async Task UpdateAsync_WhenARetrievalFieldIsOutOfRange_RejectsIt(int strictness, int topNDocuments, string invalidField)
    {
        // Arrange
        var template = new AIProfileTemplate { Source = AITemplateSources.Profile };
        var context = PostedFormUpdateModel.CreateContext(new EditProfileDataSourcesViewModel { Strictness = strictness, TopNDocuments = topNDocuments });

        // Act
        await CreateDriver().UpdateAsync(template, context);

        // Assert
        var entry = Assert.Single(context.Updater.ModelState);

        Assert.Equal(invalidField, entry.Key);
        Assert.Single(entry.Value.Errors);
    }

    private static AIProfileTemplateDataSourceDisplayDriver CreateDriver()
        => new(
            DataSourceRetrievalTestServices.SiteService(),
            DataSourceRetrievalTestServices.ODataValidator(),
            DataSourceRetrievalTestServices.DataSourceStore(),
            DataSourceRetrievalTestServices.Localizer<AIProfileTemplateDataSourceDisplayDriver>());
}
