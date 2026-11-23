using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// Review P2-L7: a published survey's technical name names its Report Builder data set (<c>SurveyResponses_{name}</c>),
/// so it can't change once the survey is published.
/// </summary>
public sealed class SurveyDetailsRenameTests
{
    [Fact]
    public async Task APublishedSurvey_KeepsItsTechnicalName()
    {
        // Arrange
        var survey = new Survey { ItemId = "s1", Name = "staff-pulse", Title = "Staff pulse", PublishedVersionNumber = 2 };
        var driver = new SurveyDetailsDisplayDriver(new SurveyStore(Mock.Of<global::YesSql.ISession>()), RecipeRoundTrip.Localizer<SurveyDetailsDisplayDriver>());
        var updater = new DetailsUpdater(new SurveyDetailsEditViewModel { Name = "staff-pulse-2", Title = "Staff pulse" });

        // Act
        await driver.UpdateAsync(survey, new UpdateEditorContext(null, SurveyDetailsDisplayDriver.GroupId, false, string.Empty, null, null, updater));

        // Assert
        Assert.Equal("staff-pulse", survey.Name);
        Assert.False(updater.ModelState.IsValid);
    }

    // Fills the details model from the object given.
    private sealed class DetailsUpdater : IUpdateModel
    {
        private readonly SurveyDetailsEditViewModel _source;

        public DetailsUpdater(SurveyDetailsEditViewModel source)
        {
            _source = source;
        }

        public Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model)
            where TModel : class
            => TryUpdateModelAsync(model, string.Empty);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix)
            where TModel : class
        {
            if (model is SurveyDetailsEditViewModel details)
            {
                details.Name = _source.Name;
                details.Title = _source.Title;
                details.Description = _source.Description;
                details.Tags = _source.Tags;
            }

            return Task.FromResult(true);
        }

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params System.Linq.Expressions.Expression<Func<TModel, object>>[] includeExpressions)
            where TModel : class
            => TryUpdateModelAsync(model, prefix);

        public bool TryValidateModel(object model)
            => true;

        public bool TryValidateModel(object model, string prefix)
            => true;
    }
}
