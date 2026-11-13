namespace CrestApps.OrchardCore.Learning.Workflows;

/// <summary>
/// The editor of a Learning workflow task. Each value is a Liquid expression; a task shows only the fields it uses.
/// </summary>
public class LearningTaskViewModel
{
    public string UserId { get; set; }

    public string CourseId { get; set; }

    public string EnrollmentId { get; set; }

    public string ItemId { get; set; }

    public string Status { get; set; }

    public string AccessEnds { get; set; }

    public string Due { get; set; }

    public string Reason { get; set; }

    public string OutputName { get; set; }
}
