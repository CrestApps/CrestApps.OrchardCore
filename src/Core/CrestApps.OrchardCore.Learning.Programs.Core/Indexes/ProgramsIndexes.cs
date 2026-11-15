using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Programs.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Learning.Programs.Core.Indexes;

/// <summary>
/// Indexes the progress of people on learning paths.
/// </summary>
public sealed class LearningPathProgressIndex : CatalogItemIndex
{
    public string PathId { get; set; }

    public string UserId { get; set; }

    public LearningPathState State { get; set; }

    public DateTime? CompletedUtc { get; set; }
}

/// <summary>
/// Indexes required learning rules.
/// </summary>
public sealed class LearningRequirementIndex : CatalogItemIndex
{
    public string TargetKind { get; set; }

    public string TargetId { get; set; }

    public bool Enabled { get; set; }
}

/// <summary>
/// Indexes the states of people under required learning.
/// </summary>
public sealed class RequiredLearningAssignmentIndex : CatalogItemIndex
{
    public string RequirementId { get; set; }

    public string UserId { get; set; }

    public RequiredLearningState State { get; set; }

    public DateTime? DueUtc { get; set; }
}

/// <summary>
/// Maps learning path progress.
/// </summary>
public sealed class LearningPathProgressIndexProvider : IndexProvider<LearningPathProgress>
{
    public LearningPathProgressIndexProvider()
    {
        CollectionName = ProgramsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<LearningPathProgress> context)
    {
        context.For<LearningPathProgressIndex>()
            .Map(progress => new LearningPathProgressIndex
            {
                ItemId = progress.ItemId,
                PathId = progress.PathId,
                UserId = progress.UserId,
                State = progress.State,
                CompletedUtc = progress.CompletedUtc,
            });
    }
}

/// <summary>
/// Maps required learning rules.
/// </summary>
public sealed class LearningRequirementIndexProvider : IndexProvider<LearningRequirement>
{
    public LearningRequirementIndexProvider()
    {
        CollectionName = ProgramsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<LearningRequirement> context)
    {
        context.For<LearningRequirementIndex>()
            .Map(requirement => new LearningRequirementIndex
            {
                ItemId = requirement.ItemId,
                TargetKind = requirement.TargetKind,
                TargetId = requirement.TargetId,
                Enabled = requirement.Enabled,
            });
    }
}

/// <summary>
/// Maps the states of people under required learning.
/// </summary>
public sealed class RequiredLearningAssignmentIndexProvider : IndexProvider<RequiredLearningAssignment>
{
    public RequiredLearningAssignmentIndexProvider()
    {
        CollectionName = ProgramsConstants.CollectionName;
    }

    public override void Describe(DescribeContext<RequiredLearningAssignment> context)
    {
        context.For<RequiredLearningAssignmentIndex>()
            .Map(assignment => new RequiredLearningAssignmentIndex
            {
                ItemId = assignment.ItemId,
                RequirementId = assignment.RequirementId,
                UserId = assignment.UserId,
                State = assignment.State,
                DueUtc = assignment.DueUtc,
            });
    }
}
