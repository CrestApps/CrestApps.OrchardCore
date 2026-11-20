namespace CrestApps.OrchardCore.Learning.Core.Moodle;

/// <summary>
/// Why a Moodle course backup could not be read.
/// </summary>
public static class MoodleBackupErrors
{
    /// <summary>
    /// The file is neither a zip archive nor a gzip-compressed tar archive.
    /// </summary>
    public const string NotAnArchive = "NotAnArchive";

    /// <summary>
    /// The archive was refused by the safe extraction rules; the detail says which rule.
    /// </summary>
    public const string UnsafeArchive = "UnsafeArchive";

    /// <summary>
    /// The archive has no <c>moodle_backup.xml</c> at its root.
    /// </summary>
    public const string NoBackupFile = "NoBackupFile";

    /// <summary>
    /// An XML file of the backup is not well-formed, has a DTD or is too large; the detail names the file.
    /// </summary>
    public const string InvalidXml = "InvalidXml";

    /// <summary>
    /// The backup is not of a whole course (an activity or a section backup).
    /// </summary>
    public const string NotACourse = "NotACourse";
}

/// <summary>
/// The limits that apply when a Moodle course backup is read.
/// </summary>
public sealed class MoodleBackupLimits
{
    /// <summary>
    /// Gets or sets the most entries the archive may hold. A backup keeps every file of its SCORM packages, extracted, so
    /// it can hold many.
    /// </summary>
    public int MaxEntries { get; set; } = 100_000;

    /// <summary>
    /// Gets or sets the most bytes the archive may expand to.
    /// </summary>
    public long MaxExpandedBytes { get; set; } = 5L * 1024 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the most characters an XML file of the backup may hold.
    /// </summary>
    public long MaxXmlCharacters { get; set; } = 100_000_000;
}

/// <summary>
/// The result of reading a Moodle course backup.
/// </summary>
public sealed class MoodleBackupReadResult
{
    /// <summary>
    /// Gets or sets why the backup was refused, one of <see cref="MoodleBackupErrors"/>, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets what the refusal is about, such as the file or the rule.
    /// </summary>
    public string ErrorDetail { get; set; }

    /// <summary>
    /// Gets or sets the backup read.
    /// </summary>
    public MoodleBackup Backup { get; set; }

    /// <summary>
    /// Gets a value indicating whether the backup was read.
    /// </summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// A Moodle course backup (<c>.mbz</c>), read from the folder it was extracted to.
/// </summary>
public sealed class MoodleBackup
{
    /// <summary>
    /// Gets or sets the folder the backup was extracted to.
    /// </summary>
    public string Folder { get; set; }

    /// <summary>
    /// Gets or sets the file name Moodle gave the backup, such as <c>backup-moodle2-course-12-safety-20240115-0800.mbz</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a short key of the Moodle site the backup was made on, from its site identifier.
    /// </summary>
    public string SiteKey { get; set; }

    /// <summary>
    /// Gets or sets the address of the Moodle site.
    /// </summary>
    public string WwwRoot { get; set; }

    /// <summary>
    /// Gets or sets the Moodle release, such as <c>4.2 (Build: 20230424)</c>.
    /// </summary>
    public string Release { get; set; }

    /// <summary>
    /// Gets or sets the course.
    /// </summary>
    public MoodleCourse Course { get; set; }

    /// <summary>
    /// Gets or sets the sections, in course order.
    /// </summary>
    public IList<MoodleSection> Sections { get; set; } = [];

    /// <summary>
    /// Gets or sets the activities, in the order of their sections and of each section's sequence.
    /// </summary>
    public IList<MoodleActivity> Activities { get; set; } = [];

    /// <summary>
    /// Gets or sets the question bank categories of the course and the questions they hold.
    /// </summary>
    public IList<MoodleQuestionCategory> QuestionCategories { get; set; } = [];

    /// <summary>
    /// Gets or sets the files of the backup, by id.
    /// </summary>
    public IDictionary<string, MoodleFile> Files { get; set; } = new Dictionary<string, MoodleFile>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the source identifier of the course, such as <c>moodle:1a2b3c4d5e6f:course:12</c>.
    /// </summary>
    public string CourseSource => $"moodle:{SiteKey}:course:{Course?.Id}";

    /// <summary>
    /// Gets the source identifier of an activity of the course.
    /// </summary>
    /// <param name="moduleId">The course module id.</param>
    /// <returns>The identifier, such as <c>moodle:1a2b3c4d5e6f:course:12:cm:345</c>.</returns>
    public string ActivitySource(string moduleId) => $"{CourseSource}:cm:{moduleId}";

    /// <summary>
    /// Gets the source identifier of a question.
    /// </summary>
    /// <param name="bankEntryId">The question bank entry id.</param>
    /// <returns>The identifier, such as <c>moodle:1a2b3c4d5e6f:question:55</c>.</returns>
    public string QuestionSource(string bankEntryId) => $"moodle:{SiteKey}:question:{bankEntryId}";

    /// <summary>
    /// Gets the path of a file's content in the extracted backup (<c>files/</c>, the first two characters of its hash,
    /// and its hash), or <see langword="null"/> when the backup does not have it.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>The full path.</returns>
    public string GetContentPath(MoodleFile file)
    {
        if (file?.ContentHash is not { Length: >= 3 } hash || !hash.All(char.IsAsciiLetterOrDigit))
        {
            return null;
        }

        var path = Path.Combine(Folder, "files", hash[..2], hash);

        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Finds an activity by its course module id.
    /// </summary>
    /// <param name="moduleId">The id.</param>
    /// <returns>The activity, or <see langword="null"/>.</returns>
    public MoodleActivity FindActivity(string moduleId)
        => Activities.FirstOrDefault(activity => activity.ModuleId == moduleId);

    /// <summary>
    /// Finds the latest version of a question by its bank entry id.
    /// </summary>
    /// <param name="bankEntryId">The id.</param>
    /// <returns>The question, or <see langword="null"/>.</returns>
    public MoodleQuestion FindQuestion(string bankEntryId)
        => QuestionCategories.SelectMany(category => category.Questions).FirstOrDefault(question => question.BankEntryId == bankEntryId);
}

/// <summary>
/// The course of a Moodle backup.
/// </summary>
public sealed class MoodleCourse
{
    /// <summary>Gets or sets the Moodle course id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the context id of the course, which its own files (its picture, its summary's pictures) belong to.</summary>
    public string ContextId { get; set; }

    /// <summary>Gets or sets the full name.</summary>
    public string FullName { get; set; }

    /// <summary>Gets or sets the short name.</summary>
    public string ShortName { get; set; }

    /// <summary>Gets or sets the id number, a code the site gave the course.</summary>
    public string IdNumber { get; set; }

    /// <summary>Gets or sets the summary, in HTML.</summary>
    public string Summary { get; set; }

    /// <summary>Gets or sets the course format, such as <c>topics</c> or <c>weeks</c>.</summary>
    public string Format { get; set; }

    /// <summary>Gets or sets the start date, in UTC.</summary>
    public DateTime? StartUtc { get; set; }

    /// <summary>Gets or sets the end date, in UTC.</summary>
    public DateTime? EndUtc { get; set; }

    /// <summary>Gets or sets whether students see the course.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Gets or sets whether completion tracking is on.</summary>
    public bool EnableCompletion { get; set; }

    /// <summary>Gets or sets the Moodle id of the course's category.</summary>
    public string CategoryId { get; set; }

    /// <summary>Gets or sets the name of the course's category. A backup keeps only the category itself, not the categories above it.</summary>
    public string CategoryName { get; set; }

    /// <summary>Gets or sets the most points of the course grade, from the course's grade item.</summary>
    public decimal? GradeMax { get; set; }

    /// <summary>Gets or sets the enrolment methods.</summary>
    public IList<MoodleEnrolment> Enrolments { get; set; } = [];

    /// <summary>Gets or sets the course completion criteria.</summary>
    public IList<MoodleCompletionCriterion> CompletionCriteria { get; set; } = [];

    /// <summary>Gets or sets whether any one of the criteria completes the course, instead of all of them.</summary>
    public bool CompletionAny { get; set; }
}

/// <summary>
/// An enrolment method of a Moodle course.
/// </summary>
public sealed class MoodleEnrolment
{
    /// <summary>Gets or sets the plugin, such as <c>manual</c>, <c>self</c>, <c>guest</c> or <c>cohort</c>.</summary>
    public string Method { get; set; }

    /// <summary>Gets or sets whether the method is on.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets whether the method has a password (a self enrolment key). The password itself is never read.</summary>
    public bool HasPassword { get; set; }

    /// <summary>Gets or sets how long an enrolment lasts, in seconds, or 0 for no end.</summary>
    public long EnrolPeriodSeconds { get; set; }

    /// <summary>Gets or sets whether the method takes new enrolments (a self enrolment's <c>customint6</c>).</summary>
    public bool AllowsNewEnrolments { get; set; } = true;
}

/// <summary>
/// A course completion criterion of a Moodle course.
/// </summary>
public sealed class MoodleCompletionCriterion
{
    /// <summary>The criterion type of self completion.</summary>
    public const int Self = 1;

    /// <summary>The criterion type of a date.</summary>
    public const int Date = 2;

    /// <summary>The criterion type of unenrolment.</summary>
    public const int Unenrol = 3;

    /// <summary>The criterion type of an activity.</summary>
    public const int Activity = 4;

    /// <summary>The criterion type of an enrolment duration.</summary>
    public const int Duration = 5;

    /// <summary>The criterion type of a course grade.</summary>
    public const int Grade = 6;

    /// <summary>The criterion type of a role's manual completion.</summary>
    public const int Role = 7;

    /// <summary>The criterion type of another course's completion.</summary>
    public const int Course = 8;

    /// <summary>Gets or sets the criterion type.</summary>
    public int Type { get; set; }

    /// <summary>Gets or sets the course module id of an activity criterion.</summary>
    public string ModuleId { get; set; }

    /// <summary>Gets or sets the grade to pass of a grade criterion, in course grade points.</summary>
    public decimal? GradePass { get; set; }

    /// <summary>Gets or sets the short name of the role of a role criterion, such as <c>editingteacher</c>.</summary>
    public string RoleShortName { get; set; }
}

/// <summary>
/// A section of a Moodle course.
/// </summary>
public sealed class MoodleSection
{
    /// <summary>Gets or sets the section id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the section number, 0 for the general section.</summary>
    public int Number { get; set; }

    /// <summary>Gets or sets the name, or <see langword="null"/> when Moodle names it by its number.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the summary, in HTML.</summary>
    public string Summary { get; set; }

    /// <summary>Gets or sets whether students see it.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Gets or sets the course module ids of its activities, in order.</summary>
    public IList<string> Sequence { get; set; } = [];
}

/// <summary>
/// An activity or resource of a Moodle course (a course module).
/// </summary>
public sealed class MoodleActivity
{
    /// <summary>Gets or sets the course module id.</summary>
    public string ModuleId { get; set; }

    /// <summary>Gets or sets the module, such as <c>scorm</c>, <c>quiz</c>, <c>resource</c>, <c>url</c>, <c>page</c> or <c>label</c>.</summary>
    public string ModuleName { get; set; }

    /// <summary>Gets or sets the id of its section.</summary>
    public string SectionId { get; set; }

    /// <summary>Gets or sets its name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets whether students see it.</summary>
    public bool Visible { get; set; } = true;

    /// <summary>Gets or sets the completion tracking: 0 none, 1 manual, 2 automatic.</summary>
    public int Completion { get; set; }

    /// <summary>Gets or sets whether viewing it completes it.</summary>
    public bool CompletionView { get; set; }

    /// <summary>Gets or sets whether receiving a grade completes it.</summary>
    public bool CompletionUseGrade { get; set; }

    /// <summary>Gets or sets whether a passing grade completes it.</summary>
    public bool CompletionPassGrade { get; set; }

    /// <summary>Gets or sets the context id of the activity, which its files belong to.</summary>
    public string ContextId { get; set; }

    /// <summary>Gets or sets the settings of the module, by element name (<c>intro</c>, <c>externalurl</c>, <c>timelimit</c>...).</summary>
    public IDictionary<string, string> Settings { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the files the activity refers to.</summary>
    public IList<MoodleFile> Files { get; set; } = [];

    /// <summary>Gets or sets the most points of its grade item.</summary>
    public decimal? GradeMax { get; set; }

    /// <summary>Gets or sets the grade to pass of its grade item.</summary>
    public decimal? GradePass { get; set; }

    /// <summary>Gets or sets the slots of a quiz, in order.</summary>
    public IList<MoodleQuizSlot> Slots { get; set; } = [];

    /// <summary>Gets or sets the restrictions of the activity, as Moodle's availability JSON, or <see langword="null"/>.</summary>
    public string Availability { get; set; }

    /// <summary>
    /// Gets or sets the repeated records of the module's settings, by element name, such as the <c>item</c> records of a
    /// feedback, the <c>option</c> records of a choice or the <c>plugin_config</c> records of an assignment.
    /// </summary>
    public IDictionary<string, IList<IDictionary<string, string>>> Records { get; set; } = new Dictionary<string, IList<IDictionary<string, string>>>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the records of an element name, in order.
    /// </summary>
    /// <param name="element">The element name, such as <c>item</c>.</param>
    /// <returns>The records, each its values by element name.</returns>
    public IList<IDictionary<string, string>> RecordsOf(string element)
        => Records.TryGetValue(element, out var records) ? records : [];

    /// <summary>
    /// Gets a setting, or <see langword="null"/> when the activity does not have it.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <returns>The value.</returns>
    public string Get(string name)
        => Settings.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// A slot of a Moodle quiz: a question of the bank, or a random question from a category.
/// </summary>
public sealed class MoodleQuizSlot
{
    /// <summary>Gets or sets the slot number.</summary>
    public int Slot { get; set; }

    /// <summary>Gets or sets the page the slot is on.</summary>
    public int Page { get; set; }

    /// <summary>Gets or sets the most points of the slot.</summary>
    public decimal MaxMark { get; set; } = 1;

    /// <summary>Gets or sets the question bank entry of a fixed question.</summary>
    public string BankEntryId { get; set; }

    /// <summary>Gets or sets the category a random question is drawn from.</summary>
    public string RandomCategoryId { get; set; }

    /// <summary>Gets or sets whether a random question may come from the category's sub-categories too.</summary>
    public bool RandomIncludesSubcategories { get; set; }
}

/// <summary>
/// A file of a Moodle backup, from <c>files.xml</c>.
/// </summary>
public sealed class MoodleFile
{
    /// <summary>Gets or sets the file id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the SHA-1 hash of its content, which names its content under <c>files/</c>.</summary>
    public string ContentHash { get; set; }

    /// <summary>Gets or sets the context it belongs to.</summary>
    public string ContextId { get; set; }

    /// <summary>Gets or sets the component, such as <c>mod_scorm</c>.</summary>
    public string Component { get; set; }

    /// <summary>Gets or sets the file area, such as <c>package</c> or <c>content</c>.</summary>
    public string FileArea { get; set; }

    /// <summary>Gets or sets its folder within the area.</summary>
    public string FilePath { get; set; }

    /// <summary>Gets or sets the item it belongs to within the area, such as a section id.</summary>
    public string ItemId { get; set; }

    /// <summary>Gets or sets its name.</summary>
    public string FileName { get; set; }

    /// <summary>Gets or sets its size, in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Gets or sets its MIME type.</summary>
    public string MimeType { get; set; }

    /// <summary>Gets or sets its sort order; the main file of a resource has the highest.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// A question bank category of a Moodle course.
/// </summary>
public sealed class MoodleQuestionCategory
{
    /// <summary>Gets or sets the category id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets its name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the id of the category above it, or 0.</summary>
    public string ParentId { get; set; }

    /// <summary>Gets or sets the latest version of each of its questions.</summary>
    public IList<MoodleQuestion> Questions { get; set; } = [];
}

/// <summary>
/// A question of a Moodle question bank, in its latest version.
/// </summary>
public sealed class MoodleQuestion
{
    /// <summary>Gets or sets the question id of the version.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the question bank entry id, the same for every version.</summary>
    public string BankEntryId { get; set; }

    /// <summary>Gets or sets the category id.</summary>
    public string CategoryId { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; }

    /// <summary>Gets or sets the question type, such as <c>multichoice</c>, <c>truefalse</c> or <c>essay</c>.</summary>
    public string Type { get; set; }

    /// <summary>Gets or sets the question text, in HTML.</summary>
    public string Text { get; set; }

    /// <summary>Gets or sets the general feedback, in HTML.</summary>
    public string GeneralFeedback { get; set; }

    /// <summary>Gets or sets the default mark.</summary>
    public decimal DefaultMark { get; set; } = 1;

    /// <summary>Gets or sets the answers, in order.</summary>
    public IList<MoodleAnswer> Answers { get; set; } = [];

    /// <summary>Gets or sets the type's options, by element name (<c>single</c>, <c>shuffleanswers</c>, <c>usecase</c>, <c>graderinfo</c>...).</summary>
    public IDictionary<string, string> Options { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets or sets the pairs of a matching question; an empty question text marks a distractor.</summary>
    public IList<(string Question, string Answer)> Matches { get; set; } = [];

    /// <summary>Gets or sets the tolerance of each numerical answer, by answer id.</summary>
    public IDictionary<string, decimal> Tolerances { get; set; } = new Dictionary<string, decimal>(StringComparer.Ordinal);

    /// <summary>
    /// Gets an option, or <see langword="null"/>.
    /// </summary>
    /// <param name="name">The element name.</param>
    /// <returns>The value.</returns>
    public string Option(string name)
        => Options.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// An answer of a Moodle question.
/// </summary>
public sealed class MoodleAnswer
{
    /// <summary>Gets or sets the answer id.</summary>
    public string Id { get; set; }

    /// <summary>Gets or sets the text, in HTML or plain text by its format.</summary>
    public string Text { get; set; }

    /// <summary>Gets or sets the share of the mark it is worth, from -1 to 1.</summary>
    public decimal Fraction { get; set; }

    /// <summary>Gets or sets the feedback, in HTML.</summary>
    public string Feedback { get; set; }
}
