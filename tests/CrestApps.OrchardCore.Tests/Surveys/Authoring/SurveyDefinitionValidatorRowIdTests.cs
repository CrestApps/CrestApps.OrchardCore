using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Tests.Surveys.TestSupport;

namespace CrestApps.OrchardCore.Tests.Surveys.Authoring;

/// <summary>
/// A row id longer than the answer index keeps would merge with another row that shares its first characters, so it is
/// refused.
/// </summary>
public sealed class SurveyDefinitionValidatorRowIdTests
{
    [Theory]
    [InlineData(64, false)]
    [InlineData(65, true)]
    public async Task ValidateAsync_RowIdLongerThanTheIndexKeeps_IsAnError(int length, bool refused)
    {
        // Arrange
        var definition = AuthoringDefinitions.TwoPages();
        definition.Pages[0].Questions[0].Rows.Add(new SurveySubQuestion { Id = new string('r', length), Text = "Row" });

        // Act
        var validator = new SurveyDefinitionValidator(AuthoringSurveyHarness.CreateRegistry(), [], new NullSurveyFormulaValidator(), new AuthoringLocalizer<SurveyDefinitionValidator>());
        var context = await validator.ValidateAsync(null, definition, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(refused, context.Messages.Any(message => message.IsError && message.Code == "long-row-id"));
        Assert.Equal(refused, SurveyDefinitionValidator.HasTooLongRowId(definition));
    }
}
