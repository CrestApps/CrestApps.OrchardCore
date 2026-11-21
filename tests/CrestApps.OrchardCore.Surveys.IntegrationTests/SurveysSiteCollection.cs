namespace CrestApps.OrchardCore.Surveys.IntegrationTests;

/// <summary>
/// Shares one Surveys site between the test classes.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SurveysSiteCollection : ICollectionFixture<SurveysSiteFixture>
{
    /// <summary>
    /// The name of the collection.
    /// </summary>
    public const string Name = "Surveys site";
}
