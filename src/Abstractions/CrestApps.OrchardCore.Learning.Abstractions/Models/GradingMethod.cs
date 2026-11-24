namespace CrestApps.OrchardCore.Learning.Models;

/// <summary>
/// Which attempts set the score of an item a learner may try more than once, such as a quiz or a learning package.
/// </summary>
public enum GradingMethod
{
    /// <summary>The best attempt.</summary>
    Highest,

    /// <summary>The last graded attempt.</summary>
    Latest,

    /// <summary>The mean of the graded attempts.</summary>
    Average,

    /// <summary>The first graded attempt.</summary>
    First,
}
