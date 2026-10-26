using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.DataSources.Drivers;
using CrestApps.OrchardCore.AI.DataSources.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// The Knowledge tab of the AI completion with configuration workflow task says a blank Strictness or Retrieved
/// documents uses the default. The fields were not nullable, so model binding refused a blank before the driver saw
/// it, and the driver then refused the zero it was left with as out of range.
/// </summary>
public sealed class AICompletionWithConfigDataSourceDisplayDriverTests
{
    [Theory]
    [InlineData(nameof(AICompletionWithConfigDataSourceViewModel.Strictness))]
    [InlineData(nameof(AICompletionWithConfigDataSourceViewModel.TopNDocuments))]
    public async Task BindModelAsync_WhenARetrievalFieldIsPostedBlank_AddsNoError(string field)
    {
        // Arrange
        var metadata = new EmptyModelMetadataProvider().GetMetadataForProperty(typeof(AICompletionWithConfigDataSourceViewModel), field);
        var modelState = new ModelStateDictionary();
        var bindingContext = new DefaultModelBindingContext
        {
            ModelMetadata = metadata,
            ModelName = field,
            ModelState = modelState,
            ValueProvider = new FormValueProvider(
                BindingSource.Form,
                new FormCollection(new Dictionary<string, StringValues> { [field] = string.Empty }),
                CultureInfo.InvariantCulture),
        };

        // Act
        await new SimpleTypeModelBinder(metadata.ModelType, NullLoggerFactory.Instance).BindModelAsync(bindingContext);

        // Assert
        Assert.Equal(0, modelState.ErrorCount);
        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Null(bindingContext.Result.Model);
    }

    [Fact]
    public async Task UpdateAsync_WhenRetrievalFieldsAreBlank_StoresTheSiteDefaults()
    {
        // Arrange
        var activity = CreateActivity();
        var context = PostedFormUpdateModel.CreateContext(new AICompletionWithConfigDataSourceViewModel());

        // Act
        await CreateDriver().UpdateAsync(activity, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);

        var metadata = activity.Interaction.GetOrCreate<AIDataSourceRagMetadata>();

        Assert.Equal(DataSourceRetrievalTestServices.SiteDefaultStrictness, metadata.Strictness);
        Assert.Equal(DataSourceRetrievalTestServices.SiteDefaultTopNDocuments, metadata.TopNDocuments);
    }

    [Fact]
    public async Task UpdateAsync_WhenRetrievalFieldsAreInRange_StoresThem()
    {
        // Arrange
        var activity = CreateActivity();
        var context = PostedFormUpdateModel.CreateContext(new AICompletionWithConfigDataSourceViewModel { Strictness = 1, TopNDocuments = 3 });

        // Act
        await CreateDriver().UpdateAsync(activity, context);

        // Assert
        Assert.True(context.Updater.ModelState.IsValid);

        var metadata = activity.Interaction.GetOrCreate<AIDataSourceRagMetadata>();

        Assert.Equal(1, metadata.Strictness);
        Assert.Equal(3, metadata.TopNDocuments);
    }

    [Theory]
    [InlineData(0, 3, nameof(AICompletionWithConfigDataSourceViewModel.Strictness))]
    [InlineData(6, 3, nameof(AICompletionWithConfigDataSourceViewModel.Strictness))]
    [InlineData(1, 2, nameof(AICompletionWithConfigDataSourceViewModel.TopNDocuments))]
    [InlineData(1, 21, nameof(AICompletionWithConfigDataSourceViewModel.TopNDocuments))]
    public async Task UpdateAsync_WhenARetrievalFieldIsOutOfRange_RejectsIt(int strictness, int topNDocuments, string invalidField)
    {
        // Arrange
        var context = PostedFormUpdateModel.CreateContext(new AICompletionWithConfigDataSourceViewModel { Strictness = strictness, TopNDocuments = topNDocuments });

        // Act
        await CreateDriver().UpdateAsync(CreateActivity(), context);

        // Assert
        var entry = Assert.Single(context.Updater.ModelState);

        Assert.Equal(invalidField, entry.Key);
        Assert.Single(entry.Value.Errors);
    }

    private static AICompletionWithConfigTask CreateActivity()
        => new(
            completionContextBuilder: null,
            completionService: null,
            deploymentManager: null,
            liquidTemplateManager: null,
            NullLogger<AICompletionWithConfigTask>.Instance,
            DataSourceRetrievalTestServices.Localizer<AICompletionWithConfigTask>());

    private static AICompletionWithConfigDataSourceDisplayDriver CreateDriver()
        => new(
            DataSourceRetrievalTestServices.SiteService(),
            DataSourceRetrievalTestServices.DataSourceStore(),
            DataSourceRetrievalTestServices.ODataValidator(),
            DataSourceRetrievalTestServices.Localizer<AICompletionWithConfigDataSourceDisplayDriver>());
}
