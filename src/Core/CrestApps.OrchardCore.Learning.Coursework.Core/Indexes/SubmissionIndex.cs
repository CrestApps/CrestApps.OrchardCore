using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Learning.Coursework.Core.Indexes;

/// <summary>
/// Indexes submissions by learner, enrollment, coursework, course, state and dates.
/// </summary>
public sealed class SubmissionIndex : CatalogItemIndex
{
    /// <summary>Gets or sets the enrollment.</summary>
    public string EnrollmentId { get; set; }

    /// <summary>Gets or sets the learner.</summary>
    public string UserId { get; set; }

    /// <summary>Gets or sets the course.</summary>
    public string CourseId { get; set; }

    /// <summary>Gets or sets the coursework item.</summary>
    public string CourseworkId { get; set; }

    /// <summary>Gets or sets the attempt number.</summary>
    public int Attempt { get; set; }

    /// <summary>Gets or sets whether it is the learner's latest submission.</summary>
    public bool IsCurrent { get; set; }

    /// <summary>Gets or sets the state.</summary>
    public SubmissionStatus Status { get; set; }

    /// <summary>Gets or sets whether it waits for a grader: current and not graded or returned.</summary>
    public bool Waiting { get; set; }

    /// <summary>Gets or sets whether it came after the due date.</summary>
    public bool Late { get; set; }

    /// <summary>Gets or sets when it was handed in, in UTC.</summary>
    public DateTime SubmittedUtc { get; set; }

    /// <summary>Gets or sets when it was graded, in UTC.</summary>
    public DateTime? GradedUtc { get; set; }

    /// <summary>Gets or sets the grade as a percentage.</summary>
    public decimal? Score { get; set; }
}

/// <summary>
/// Maps submissions.
/// </summary>
public sealed class SubmissionIndexProvider : IndexProvider<Submission>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionIndexProvider"/> class.
    /// </summary>
    public SubmissionIndexProvider()
    {
        CollectionName = CourseworkConstants.CollectionName;
    }

    /// <inheritdoc />
    public override void Describe(DescribeContext<Submission> context)
        => context.For<SubmissionIndex>()
            .Map(submission => new SubmissionIndex
            {
                ItemId = submission.ItemId,
                EnrollmentId = submission.EnrollmentId,
                UserId = submission.UserId,
                CourseId = submission.CourseId,
                CourseworkId = submission.CourseworkId,
                Attempt = submission.Attempt,
                IsCurrent = submission.Current,
                Status = submission.Status,
                Waiting = submission.Current && submission.Status == SubmissionStatus.Submitted,
                Late = submission.Late,
                SubmittedUtc = submission.SubmittedUtc,
                GradedUtc = submission.GradedUtc,
                Score = submission.Score,
            });
}

/// <summary>
/// Creates the index table of submissions.
/// </summary>
public static class CourseworkIndexSchema
{
    /// <summary>
    /// Creates the table and its database indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = CourseworkConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<SubmissionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EnrollmentId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<string>("CourseId", column => column.Nullable().WithLength(26))
            .Column<string>("CourseworkId", column => column.WithLength(26))
            .Column<int>("Attempt")
            .Column<bool>("IsCurrent")
            .Column<SubmissionStatus>("Status")
            .Column<bool>("Waiting")
            .Column<bool>("Late")
            .Column<DateTime>("SubmittedUtc")
            .Column<DateTime>("GradedUtc", column => column.Nullable())
            .Column<decimal>("Score", column => column.Nullable()),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<SubmissionIndex>(table =>
        {
            table.CreateIndex("IDX_Learning_SubmissionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Learning_SubmissionIndex_Enrollment", "EnrollmentId", "CourseworkId", "IsCurrent");
            table.CreateIndex("IDX_Learning_SubmissionIndex_Coursework", "CourseworkId", "SubmittedUtc");
            table.CreateIndex("IDX_Learning_SubmissionIndex_User", "UserId");
            table.CreateIndex("IDX_Learning_SubmissionIndex_Waiting", "Waiting", "SubmittedUtc");
            table.CreateIndex("IDX_Learning_SubmissionIndex_SubmittedUtc", "SubmittedUtc", "Id");
        }, collection: collection);
    }
}
