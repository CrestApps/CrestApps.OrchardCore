using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Core.Indexes;

/// <summary>
/// Indexes Learning secrets by kind and owner.
/// </summary>
public sealed class LearningSecretIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the kind of secret.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the content item the secret belongs to.
    /// </summary>
    public string OwnerId { get; set; }
}

/// <summary>
/// Maps Learning secrets.
/// </summary>
public sealed class LearningSecretIndexProvider : IndexProvider<LearningSecret>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LearningSecretIndexProvider"/> class.
    /// </summary>
    public LearningSecretIndexProvider()
    {
        CollectionName = LearningConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<LearningSecret> context)
        => context.For<LearningSecretIndex>()
            .Map(secret => new LearningSecretIndex
            {
                ItemId = secret.ItemId,
                Kind = secret.Kind,
                OwnerId = secret.OwnerId,
            });
}
