using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.Learning.Assessments.Core.Moodle;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using CrestApps.OrchardCore.Learning.Coursework;
using CrestApps.OrchardCore.Learning.Packages;
using CrestApps.OrchardCore.Learning.Surveys;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Moodle;

/// <summary>
/// The columns the Moodle reader writes.
/// </summary>
public static class MoodleColumns
{
    /// <summary>
    /// Where the row's course or activity came from; the import handler finds it in the backup by it.
    /// </summary>
    public const string Source = "MoodleSource";

    /// <summary>
    /// The token of the extracted backup the row belongs to.
    /// </summary>
    public const string Workspace = "MoodleWorkspace";

    /// <summary>
    /// Why the row is not imported. The import handler fails a row that has it.
    /// </summary>
    public const string Error = "MoodleError";

    /// <summary>
    /// The kind of the reason, which the import counts the failed rows by.
    /// </summary>
    public const string ErrorCategory = "MoodleErrorCategory";
}

/// <summary>
/// Writes a Moodle backup as rows: the course first, then each activity in the order of its section, as the item that
/// fits it, or as a failed row saying why it is not imported; a failed row for each question of a quiz that is not
/// imported and for each setting the site does not keep. A label is a Text item; an assignment, a feedback, a choice and
/// an IMS content package are imported when the feature of their item is on.
/// </summary>
internal static class MoodleCourseRows
{
    /// <summary>
    /// The modules that become an item, with the content type of the item.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ItemTypes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["scorm"] = PackagesConstants.ContentTypes.Package,
        ["imscp"] = PackagesConstants.ContentTypes.Package,
        ["quiz"] = AssessmentsConstants.ContentTypes.Quiz,
        ["resource"] = LearningConstants.ContentTypes.FileLesson,
        ["url"] = LearningConstants.ContentTypes.LinkLesson,
        ["page"] = LearningConstants.ContentTypes.PageLesson,
        ["label"] = LearningConstants.ContentTypes.TextBlock,
        ["assign"] = CourseworkConstants.ContentTypes.Coursework,
        ["feedback"] = LearningSurveysConstants.ContentTypes.FeedbackSurvey,
        ["choice"] = LearningSurveysConstants.ContentTypes.Choice,
    };

    /// <summary>
    /// The course item kind of each module's item, which is registered only while the feature of the item is on.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ItemKinds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["scorm"] = PackagesConstants.PackageKind,
        ["imscp"] = PackagesConstants.PackageKind,
        ["quiz"] = AssessmentsConstants.QuizKind,
        ["resource"] = LearningConstants.CourseItemKinds.File,
        ["url"] = LearningConstants.CourseItemKinds.Link,
        ["page"] = LearningConstants.CourseItemKinds.Page,
        ["label"] = LearningConstants.CourseItemKinds.Text,
        ["assign"] = CourseworkConstants.CourseworkKind,
        ["feedback"] = LearningSurveysConstants.SurveyKind,
        ["choice"] = LearningSurveysConstants.SurveyKind,
    };

    /// <summary>
    /// Gets the columns, in order.
    /// </summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        ContentTransferColumns.ContentType,
        "ContentItemId",
        "TitlePart_Title",
        MoodleColumns.Source,
        MoodleColumns.Workspace,
        MoodleColumns.Error,
        MoodleColumns.ErrorCategory,
        LinkUrlColumn,
        LinkTextColumn,
        LinkTargetColumn,
    ];

    // The columns ContentTransfer's link field handler reads: the import handler writes the link of a URL activity in
    // them, as the field handlers run after it and set the field from its columns.
    internal const string LinkUrlColumn = "LinkLesson_Url_Url";
    internal const string LinkTextColumn = "LinkLesson_Url_Text";
    internal const string LinkTargetColumn = "LinkLesson_Url_Target";

    /// <summary>
    /// Writes the rows of a backup.
    /// </summary>
    /// <param name="backup">The backup.</param>
    /// <param name="token">The token of the extracted backup.</param>
    /// <param name="S">The localizer of the reasons.</param>
    /// <param name="hasKind">Tells whether a course item kind is registered, that is whether its feature is on; every
    /// kind when <see langword="null"/>.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<string[]> Write(MoodleBackup backup, string token, IStringLocalizer S, Func<string, bool> hasKind = null)
    {
        ArgumentNullException.ThrowIfNull(backup);
        ArgumentNullException.ThrowIfNull(S);

        hasKind ??= _ => true;

        var course = backup.Course;
        var title = string.IsNullOrWhiteSpace(course.FullName) ? course.ShortName : course.FullName;
        var rows = new List<string[]>
        {
            Row(LearningConstants.ContentTypes.Course, title, backup.CourseSource, token),
        };

        void NotImported(string message)
            => rows.Add(Failed(title, message, S["Not imported from Moodle"]));

        void Attention(string message)
            => rows.Add(Failed(title, message, S["Needs attention after the Moodle import"]));

        // The course: enrolment, completion and visibility the site does not keep as Moodle does.
        foreach (var enrolment in course.Enrolments.Where(enrolment => enrolment.Enabled))
        {
            switch (enrolment.Method)
            {
                case "manual":
                    break;

                case "self" when enrolment.HasPassword && enrolment.AllowsNewEnrolments:
                    Attention(S["{0}: self enrolment needs a key in Moodle. The key is not imported: the course takes an enrollment key, and needs one set on its editor before learners can enroll.", title]);
                    break;

                case "self":
                    break;

                case "guest":
                    Attention(S["{0}: guest access is on in Moodle. Mark the items guests may open as free previews.", title]);
                    break;

                case "cohort":
                    break;

                default:
                    NotImported(S["{0}: the {1} enrolment method is not imported.", title, enrolment.Method]);
                    break;
            }
        }

        // Cohort sync becomes the course's cohort sync. Each one is a row of its own, which the row filter checks against
        // the site's cohorts: it is skipped when the site has the cohort, and reported when it does not.
        var cohortIndex = 0;

        foreach (var cohortSync in course.Enrolments.Where(enrolment => enrolment.Enabled && enrolment.Method == "cohort"))
        {
            rows.Add(Row(LearningConstants.ContentTypes.Course, title, CohortSource(backup, cohortIndex++), token));
        }

        foreach (var kind in course.CompletionCriteria.Select(criterion => criterion.Type).Distinct())
        {
            var condition = kind switch
            {
                MoodleCompletionCriterion.Self => S["learners marking it complete themselves"].Value,
                MoodleCompletionCriterion.Date => S["a date"].Value,
                MoodleCompletionCriterion.Unenrol => S["unenrolment"].Value,
                MoodleCompletionCriterion.Duration => S["a length of enrolment"].Value,
                MoodleCompletionCriterion.Course => S["the completion of other courses"].Value,
                _ => null,
            };

            if (condition is not null)
            {
                NotImported(S["{0}: the course completes in Moodle on {1}, which is not imported.", title, condition]);
            }
        }

        // Completion by a role becomes approval by the course staff in the matching role. Only the first role is imported;
        // a role no course staff role matches is kept, and nobody can approve until the course editor says who does.
        var roles = course.CompletionCriteria.Where(criterion => criterion.Type == MoodleCompletionCriterion.Role).Select(criterion => MoodleCompletion.StaffRole(criterion.RoleShortName)).Distinct(StringComparer.Ordinal).ToList();

        if (roles.Count > 1)
        {
            Attention(S["{0}: the course completes in Moodle when any of several roles marks it ({1}); only the first, {2}, is imported, so check who approves in the course editor.", title, string.Join(", ", roles.Select(role => role ?? "?")), roles[0] ?? "?"]);
        }

        if (roles.Count > 0)
        {
            var role = roles[0];

            if (role is null)
            {
                Attention(S["{0}: the course completes in Moodle when a role marks it; any member of the course staff may approve the completion here.", title]);
            }
            else if (!MoodleCompletion.IsCourseStaffRole(role))
            {
                Attention(S["{0}: the course completes in Moodle when the role {1} marks it, which no course staff role matches; nobody can approve the completion until you choose who approves in the course editor.", title, role]);
            }
        }

        if (course.CompletionAny && course.CompletionCriteria.Select(criterion => criterion.Type).Distinct().Count() > 1)
        {
            Attention(S["{0}: Moodle completes the course when any one of its conditions is met; here every condition must be met.", title]);
        }

        if (!course.Visible)
        {
            Attention(S["{0}: the course is hidden in Moodle. Unpublish it if it should stay hidden.", title]);
        }

        var reported = new HashSet<string>(StringComparer.Ordinal);

        foreach (var activity in backup.Activities)
        {
            var name = string.IsNullOrWhiteSpace(activity.Name) ? activity.ModuleId : activity.Name.Trim();
            var kind = Kind(activity.ModuleName, S);

            if (!ItemTypes.TryGetValue(activity.ModuleName, out var contentType))
            {
                NotImported(S["{0}: the {1} \"{2}\" (activity {3}) was not imported: the site has no {1} activity yet.", title, kind, name, activity.ModuleId]);

                continue;
            }

            if (!hasKind(ItemKinds[activity.ModuleName]))
            {
                NotImported(S["{0}: the {1} \"{2}\" (activity {3}) was not imported: turn on the feature of its item ({4}) and import the backup again.", title, kind, name, activity.ModuleId, FeatureName(activity.ModuleName, S)]);

                continue;
            }

            switch (activity.ModuleName)
            {
                case "scorm":
                    if (activity.Get("scormtype") is { } scormType && scormType != "local")
                    {
                        NotImported(S["{0}: the SCORM package \"{1}\" (activity {2}) was not imported: it is kept outside the backup ({3}).", title, name, activity.ModuleId, scormType]);

                        continue;
                    }

                    if (PackageFile(backup, activity) is null)
                    {
                        NotImported(S["{0}: the SCORM package \"{1}\" (activity {2}) was not imported: the backup has no package file for it.", title, name, activity.ModuleId]);

                        continue;
                    }

                    break;

                case "imscp":
                    if (ImsFiles(backup, activity).Count == 0)
                    {
                        NotImported(S["{0}: the IMS content package \"{1}\" (activity {2}) was not imported: the backup has no file for it.", title, name, activity.ModuleId]);

                        continue;
                    }

                    break;

                case "assign":
                    if (MoodleAssignments.Read(activity).UsesScale)
                    {
                        Attention(S["{0}: the assignment \"{1}\" is graded on a scale in Moodle; here it is graded out of 100 points.", title, name]);
                    }

                    break;

                case "feedback":
                    foreach (var skipped in MoodleSurveys.FromFeedback(activity).Skipped.Distinct(StringComparer.Ordinal))
                    {
                        Attention(S["{0}: the feedback \"{1}\" has a {2} item, which is not imported.", title, name, skipped]);
                    }

                    break;

                case "resource":
                    if (ResourceFiles(backup, activity).Count == 0)
                    {
                        NotImported(S["{0}: the file \"{1}\" (activity {2}) was not imported: the backup has no file for it.", title, name, activity.ModuleId]);

                        continue;
                    }

                    break;

                case "url":
                    if (!IsWebAddress(activity.Get("externalurl")))
                    {
                        NotImported(S["{0}: the link \"{1}\" (activity {2}) was not imported: \"{3}\" is not a web address.", title, name, activity.ModuleId, activity.Get("externalurl")]);

                        continue;
                    }

                    break;

                case "quiz":
                    if (!string.IsNullOrEmpty(activity.Get("password")))
                    {
                        Attention(S["{0}: the quiz \"{1}\" has a password in Moodle, which is not imported: only the learners enrolled in the course can take the quiz.", title, name]);
                    }

                    foreach (var refusal in QuestionRefusals(backup, activity))
                    {
                        if (reported.Add(refusal.Key))
                        {
                            NotImported(S["{0}: the question \"{1}\" of the quiz \"{2}\" was not imported: {3}.", title, refusal.Name, name, Reason(refusal.Mapping, S)]);
                        }
                    }

                    break;
            }

            if (!activity.Visible)
            {
                Attention(S["{0}: the {1} \"{2}\" is hidden in Moodle. It is imported as optional, so it holds back no completion; unpublish it if it should stay hidden.", title, kind, name]);
            }

            var restriction = MoodleRestrictions.Read(activity.Availability);

            if (restriction.Unsupported.Count > 0)
            {
                Attention(S["{0}: the {1} \"{2}\" is restricted in Moodle by {3}, which is not imported; restrict it here to an audience if it must stay restricted.", title, kind, name, string.Join(", ", restriction.Unsupported.Distinct(StringComparer.Ordinal))]);
            }

            rows.Add(Row(contentType, name, backup.ActivitySource(activity.ModuleId), token));
        }

        return rows;
    }

    /// <summary>
    /// Gets the source of the row of a course's cohort sync, by its place among the course's enabled cohort syncs.
    /// </summary>
    public static string CohortSource(MoodleBackup backup, int index)
        => backup.CourseSource + CohortMarker + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Finds the cohort sync of a cohort row's source, or <see langword="null"/> when the source is not one.
    /// </summary>
    public static MoodleEnrolment FindCohortSync(MoodleBackup backup, string source)
    {
        var prefix = backup.CourseSource + CohortMarker;

        if (string.IsNullOrEmpty(source) || !source.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(source.AsSpan(prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index))
        {
            return null;
        }

        return backup.Course.Enrolments.Where(enrolment => enrolment.Enabled && enrolment.Method == "cohort").ElementAtOrDefault(index);
    }

    /// <summary>
    /// Gets the name a cohort sync is reported by.
    /// </summary>
    public static string CohortName(MoodleEnrolment cohortSync)
        => cohortSync.CohortName ?? cohortSync.Name ?? cohortSync.CohortIdNumber ?? cohortSync.CohortId ?? "?";

    private const string CohortMarker = ":cohort:";

    /// <summary>
    /// Writes the one row of a file that is not a readable Moodle course backup.
    /// </summary>
    /// <param name="result">The refused read.</param>
    /// <param name="S">The localizer.</param>
    /// <returns>The row.</returns>
    public static string[] FileError(MoodleBackupReadResult result, IStringLocalizer S)
    {
        ArgumentNullException.ThrowIfNull(result);

        var message = result.Error switch
        {
            MoodleBackupErrors.NotAnArchive => S["The file is not a Moodle course backup: it is neither a zip nor a tar.gz archive."],
            MoodleBackupErrors.UnsafeArchive => S["The backup was refused by the safe extraction rules: {0}.", result.ErrorDetail],
            MoodleBackupErrors.NoBackupFile => S["The archive has no moodle_backup.xml at its root, so it is not a Moodle backup."],
            MoodleBackupErrors.InvalidXml => S["The file {0} of the backup is not well-formed XML, is too large, or has a DTD, which is refused.", result.ErrorDetail],
            MoodleBackupErrors.NotACourse => S["The backup is of a {0}, not of a whole course; back up the course in Moodle.", result.ErrorDetail ?? "?"],
            _ => S["The file could not be read as a Moodle course backup."],
        };

        return Failed(null, message, S["Not a Moodle course backup"]);
    }

    /// <summary>
    /// Gets the package file of a SCORM activity.
    /// </summary>
    public static MoodleFile PackageFile(MoodleBackup backup, MoodleActivity activity)
        => activity.Files.FirstOrDefault(file => file.Component == "mod_scorm" && file.FileArea == "package" && backup.GetContentPath(file) is not null);

    /// <summary>
    /// Gets the files of an IMS content package: the package as it was uploaded, or else the files Moodle extracted.
    /// </summary>
    public static IReadOnlyList<MoodleFile> ImsFiles(MoodleBackup backup, MoodleActivity activity)
    {
        var uploaded = activity.Files.Where(file => file.Component == "mod_imscp" && file.FileArea == "backup" && backup.GetContentPath(file) is not null).ToList();

        return uploaded.Count > 0
            ? [uploaded.OrderByDescending(file => file.ItemId, StringComparer.Ordinal).First()]
            : [.. activity.Files.Where(file => file.Component == "mod_imscp" && file.FileArea == "content" && backup.GetContentPath(file) is not null)];
    }

    /// <summary>
    /// Gets the files of a resource, the main file first.
    /// </summary>
    public static IReadOnlyList<MoodleFile> ResourceFiles(MoodleBackup backup, MoodleActivity activity)
        => [.. activity.Files
            .Where(file => file.Component == "mod_resource" && file.FileArea == "content" && backup.GetContentPath(file) is not null)
            .OrderByDescending(file => file.SortOrder)
            .ThenBy(file => file.FileName, StringComparer.Ordinal)];

    /// <summary>
    /// Gets whether a value is an absolute http or https address.
    /// </summary>
    public static bool IsWebAddress(string value)
        => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// Gets the questions of a quiz: the fixed questions of its slots, and every question of the categories its random
    /// slots draw from.
    /// </summary>
    public static IEnumerable<MoodleQuestion> QuizQuestions(MoodleBackup backup, MoodleActivity quiz)
    {
        foreach (var slot in quiz.Slots.Where(slot => !string.IsNullOrEmpty(slot.BankEntryId)))
        {
            if (backup.FindQuestion(slot.BankEntryId) is { } question)
            {
                yield return question;
            }
        }

        foreach (var categoryId in quiz.Slots.Select(slot => slot.RandomCategoryId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            foreach (var question in backup.QuestionCategories.FirstOrDefault(category => category.Id == categoryId)?.Questions ?? [])
            {
                yield return question;
            }
        }
    }

    private static IEnumerable<(string Key, string Name, MoodleQuestionMapping Mapping)> QuestionRefusals(MoodleBackup backup, MoodleActivity quiz)
    {
        foreach (var slot in quiz.Slots.Where(slot => !string.IsNullOrEmpty(slot.BankEntryId) && backup.FindQuestion(slot.BankEntryId) is null))
        {
            yield return ("missing:" + slot.BankEntryId, slot.BankEntryId, new MoodleQuestionMapping(null, Missing, null));
        }

        foreach (var question in QuizQuestions(backup, quiz))
        {
            var mapping = MoodleQuestionMapper.Map(question, backup.QuestionSource(question.BankEntryId), backup.Name + "/questions.xml");

            if (mapping.Refusal is not null)
            {
                yield return (question.BankEntryId, string.IsNullOrWhiteSpace(question.Name) ? question.BankEntryId : question.Name.Trim(), mapping);
            }
        }
    }

    private const string Missing = "Missing";

    private static string Reason(MoodleQuestionMapping mapping, IStringLocalizer S)
        => mapping.Refusal switch
        {
            Missing => S["the backup does not have it"],
            MoodleQuestionRefusals.UnsupportedType => S["it is a {0} question, which the site has no question type for", QuestionKind(mapping.Detail, S)],
            MoodleQuestionRefusals.Picture => S["its text shows a picture or media, which the import does not bring yet"],
            MoodleQuestionRefusals.MarkupInAnswer => S["an answer shows a picture or media, where the question type keeps plain text"],
            MoodleQuestionRefusals.Distractors => S["it has answers that match no question, which a matching question cannot hold"],
            MoodleQuestionRefusals.NoRightAnswer => S["it has no answer worth the full mark"],
            MoodleQuestionRefusals.UnsupportedAnswer => S["the answer \"{0}\" cannot be kept (a wildcard, a character a blank cannot hold, or not a number)", mapping.Detail],
            _ => S["it could not be read"],
        };

    private static string QuestionKind(string type, IStringLocalizer S)
        => type switch
        {
            "description" => S["description (an ungraded text block)"],
            "multianswer" => S["embedded answers (cloze)"],
            "calculated" or "calculatedsimple" or "calculatedmulti" => S["calculated"],
            "ddimageortext" => S["drag and drop onto image"],
            "ddmarker" => S["drag and drop markers"],
            "randomsamatch" => S["random short-answer matching"],
            "random" => S["random"],
            _ => type ?? "?",
        };

    private static string Kind(string module, IStringLocalizer S)
        => module switch
        {
            "assign" => S["assignment"],
            "forum" => S["forum"],
            "choice" => S["choice"],
            "feedback" => S["feedback survey"],
            "glossary" => S["glossary"],
            "data" => S["database"],
            "imscp" => S["IMS content package"],
            "mootyper" => S["MooTyper typing practice"],
            "lti" => S["external tool"],
            "book" => S["book"],
            "folder" => S["folder"],
            "lesson" => S["lesson"],
            "wiki" => S["wiki"],
            "workshop" => S["workshop"],
            "survey" => S["survey"],
            "h5pactivity" => S["H5P activity"],
            "bigbluebuttonbn" => S["BigBlueButton session"],
            "chat" => S["chat"],
            "scorm" => S["SCORM package"],
            "quiz" => S["quiz"],
            "resource" => S["file"],
            "url" => S["link"],
            "page" => S["page"],
            "label" => S["label"],
            _ => module,
        };

    private static string FeatureName(string module, IStringLocalizer S)
        => module switch
        {
            "assign" => S["Learning Coursework"],
            "feedback" or "choice" => S["Learning Surveys"],
            "scorm" or "imscp" => S["Learning SCORM Packages"],
            _ => module,
        };

    private static string[] Row(string contentType, string title, string source, string token)
    {
        var row = new string[Columns.Count];
        row[0] = contentType;
        row[2] = title;
        row[3] = source;
        row[4] = token;

        return row;
    }

    private static string[] Failed(string title, string message, string category)
    {
        var row = new string[Columns.Count];
        row[2] = title;
        row[5] = message;
        row[6] = category;

        return row;
    }
}
