using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Learning;

/// <summary>
/// Learning plan Phase 7 (section 7.6): without gradebook categories the course grade is the weighted mean of the
/// gradable items, as before. With categories each category is the weighted mean of its items after its lowest scores
/// are dropped (a missing score first, never every item), the course grade is the weighted mean of the categories that
/// have an item, and items without a known category count as one more category weighted by their item weights.
/// </summary>
public sealed class CourseGradeCalculatorTests
{
    private const string Course = "course1";

    [Fact]
    public async Task WithoutCategories_TheGradeIsTheWeightedMeanOfTheGradableItems_AMissingScoreCountingAsZero()
    {
        // Arrange
        var courses = new InMemoryCourses();
        courses.AddCourse(Course);
        Item(courses, "a", weight: 1);
        Item(courses, "b", weight: 3);
        Item(courses, "c", weight: 1);
        courses.AddItem(Course, "lesson");
        var progress = Progress(("a", 80), ("b", 40), ("lesson", 100));
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var grade = CourseGradeCalculator.Compute(outline, progress);
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, progress);

        // Assert
        Assert.Equal(40m, grade);
        Assert.Equal(40m, breakdown.Grade);
        Assert.Empty(breakdown.Categories);
    }

    [Fact]
    public async Task WithoutGradableItems_ThereIsNoGrade()
    {
        // Arrange
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 1)]);
        courses.AddItem(Course, "lesson");
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, Progress(("lesson", 100)));

        // Assert
        Assert.Null(breakdown.Grade);
        Assert.Null(CourseGradeCalculator.Compute(outline, Progress(("lesson", 100))));
    }

    [Fact]
    public async Task WithCategories_TheGradeIsTheWeightedMeanOfTheCategoryScores()
    {
        // Arrange: Homework is 75 ((100 + 50) / 2), Exams is 80 ((2 x 90 + 60) / 3).
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 40), Category("ex", "Exams", 60)]);
        Item(courses, "h1", category: "hw");
        Item(courses, "h2", category: "hw");
        Item(courses, "e1", weight: 2, category: "ex");
        Item(courses, "e2", category: "ex");
        var progress = Progress(("h1", 100), ("h2", 50), ("e1", 90), ("e2", 60));
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, progress);

        // Assert
        Assert.Equal(78m, breakdown.Grade);
        Assert.Equal(78m, CourseGradeCalculator.Compute(outline, progress));
        Assert.Collection(
            breakdown.Categories,
            homework =>
            {
                Assert.Equal("hw", homework.CategoryId);
                Assert.Equal("Homework", homework.Name);
                Assert.Equal(40m, homework.Weight);
                Assert.Equal(75m, homework.Score);
                Assert.Equal(["h1", "h2"], homework.ItemIds);
                Assert.Empty(homework.DroppedItemIds);
            },
            exams =>
            {
                Assert.Equal("ex", exams.CategoryId);
                Assert.Equal(80m, exams.Score);
                Assert.Equal(["e1", "e2"], exams.ItemIds);
            });
    }

    [Fact]
    public async Task DropLowest_LeavesOutTheLowestScores_AMissingScoreBeforeARealZero()
    {
        // Arrange: h2 has no score and h3 scored zero; one drop leaves out h2, two drops leave out both.
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 1, dropLowest: 1)]);
        Item(courses, "h1", category: "hw");
        Item(courses, "h2", category: "hw");
        Item(courses, "h3", category: "hw");
        var progress = Progress(("h1", 100), ("h3", 0));

        // Act
        var oneDrop = CourseGradeCalculator.ComputeBreakdown(await courses.GetOutlineAsync(courses.Get(Course)), progress);
        courses.Get(Course).Alter<CoursePart>(part => part.GradeCategories[0].DropLowest = 2);
        var twoDrops = CourseGradeCalculator.ComputeBreakdown(await courses.GetOutlineAsync(courses.Get(Course)), progress);

        // Assert
        Assert.Equal(50m, oneDrop.Grade);
        Assert.Equal(["h2"], Assert.Single(oneDrop.Categories).DroppedItemIds);
        Assert.True(oneDrop.IsDropped("h2"));
        Assert.False(oneDrop.IsDropped("h3"));
        Assert.Equal(100m, twoDrops.Grade);
        Assert.Equal(["h2", "h3"], Assert.Single(twoDrops.Categories).DroppedItemIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task DropLowest_NeverDropsEveryItemOfACategory()
    {
        // Arrange
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 1, dropLowest: 5)]);
        Item(courses, "h1", category: "hw");
        Item(courses, "h2", category: "hw");
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, Progress(("h1", 70), ("h2", 30)));

        // Assert
        var homework = Assert.Single(breakdown.Categories);
        Assert.Equal(70m, homework.Score);
        Assert.Equal(["h2"], homework.DroppedItemIds);
        Assert.Equal(70m, breakdown.Grade);
    }

    [Fact]
    public async Task ItemsWithoutACategory_CountAsOneMoreCategory_WeightedByTheSumOfTheirItemWeights()
    {
        // Arrange: Homework (weight 2) is 50; u1 and u2 (item weights 1 and 1) make a category of weight 2 scoring 85.
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 2, dropLowest: 1)]);
        Item(courses, "h1", category: "hw");
        Item(courses, "u1");
        Item(courses, "u2");
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, Progress(("h1", 50), ("u1", 100), ("u2", 70)));

        // Assert
        Assert.Equal(67.5m, breakdown.Grade);
        Assert.Collection(
            breakdown.Categories,
            homework =>
            {
                Assert.Equal(50m, homework.Score);
                Assert.Empty(homework.DroppedItemIds);
            },
            other =>
            {
                Assert.Null(other.CategoryId);
                Assert.Null(other.Name);
                Assert.Equal(2m, other.Weight);
                Assert.Equal(85m, other.Score);
                Assert.Equal(["u1", "u2"], other.ItemIds);
                Assert.Empty(other.DroppedItemIds);
            });
    }

    [Fact]
    public async Task AnItemWithAnUnknownCategory_CountsWithTheItemsWithoutACategory()
    {
        // Arrange
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 1)]);
        Item(courses, "h1", category: "hw");
        Item(courses, "old", category: "removed-category");
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, Progress(("h1", 80), ("old", 40)));

        // Assert
        Assert.Equal(60m, breakdown.Grade);
        Assert.Equal(["old"], breakdown.Categories.Single(category => category.CategoryId is null).ItemIds);
    }

    [Fact]
    public async Task ACategoryWithoutGradableItems_IsLeftOutOfTheGrade_ButListed()
    {
        // Arrange: Exams weighs 99 but has only an item that is not gradable.
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 1), Category("ex", "Exams", 99)]);
        Item(courses, "h1", category: "hw");
        courses.AddItem(Course, "reading", configure: part => part.GradeCategoryId = "ex");
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, Progress(("h1", 60), ("reading", 100)));

        // Assert
        Assert.Equal(60m, breakdown.Grade);
        var exams = breakdown.Categories.Single(category => category.CategoryId == "ex");
        Assert.Null(exams.Score);
        Assert.Empty(exams.ItemIds);
        Assert.DoesNotContain(breakdown.Categories, category => category.CategoryId is null);
    }

    [Fact]
    public async Task R7M8_ACompletionOnlyItem_IsLeftOutOfTheGrade_TheOthersReweighted()
    {
        // Arrange: an anonymous gradable quiz (weight 3) records its completion only; the other items are 80 and 40.
        var courses = new InMemoryCourses();
        courses.AddCourse(Course);
        Item(courses, "a", weight: 1);
        Item(courses, "anonymous", weight: 3);
        Item(courses, "c", weight: 1);
        var progress = Progress(("a", 80), ("c", 40));
        progress.Items.Add(new CourseItemProgress { ContentItemId = "anonymous", State = CourseItemState.Completed, CompletionOnly = true });
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var grade = CourseGradeCalculator.Compute(outline, progress);

        // Assert: (80 + 40) / 2, not (80 + 3 x 0 + 40) / 5.
        Assert.Equal(60m, grade);
    }

    [Fact]
    public async Task R7M8_ACompletionOnlyItemInACategory_IsLeftOutOfTheCategory_AndNeverDropped()
    {
        // Arrange: Homework drops its lowest; the anonymous quiz is in it with no score.
        var courses = new InMemoryCourses();
        courses.AddCourse(Course, part => part.GradeCategories = [Category("hw", "Homework", 1, dropLowest: 1)]);
        Item(courses, "h1", category: "hw");
        Item(courses, "h2", category: "hw");
        Item(courses, "anonymous", category: "hw");
        var progress = Progress(("h1", 90), ("h2", 50));
        progress.Items.Add(new CourseItemProgress { ContentItemId = "anonymous", State = CourseItemState.Completed, CompletionOnly = true });
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act
        var breakdown = CourseGradeCalculator.ComputeBreakdown(outline, progress);

        // Assert: the lowest real score (50) is dropped, not the anonymous quiz.
        Assert.Equal(90m, breakdown.Grade);
        Assert.Equal(["h2"], breakdown.Categories.Single().DroppedItemIds);
    }

    [Fact]
    public async Task R7M8_ACourseWhoseOnlyGradableItemIsCompletionOnly_HasNoGrade()
    {
        // Arrange
        var courses = new InMemoryCourses();
        courses.AddCourse(Course);
        Item(courses, "anonymous", weight: 2);
        var progress = Progress();
        progress.Items.Add(new CourseItemProgress { ContentItemId = "anonymous", State = CourseItemState.Completed, CompletionOnly = true });
        var outline = await courses.GetOutlineAsync(courses.Get(Course));

        // Act and assert: no grade, rather than 0.
        Assert.Null(CourseGradeCalculator.Compute(outline, progress));
    }

    private static void Item(InMemoryCourses courses, string id, decimal weight = 1, string category = null)
        => courses.AddItem(Course, id, configure: part =>
        {
            part.Gradable = true;
            part.Weight = weight;
            part.GradeCategoryId = category;
        });

    private static GradeCategory Category(string id, string name, decimal weight, int dropLowest = 0)
        => new() { Id = id, Name = name, Weight = weight, DropLowest = dropLowest };

    private static EnrollmentProgress Progress(params (string Id, decimal Score)[] scores)
        => new()
        {
            EnrollmentId = "e1",
            Items = scores.Select(score => new CourseItemProgress { ContentItemId = score.Id, Score = score.Score }).ToList(),
        };
}
