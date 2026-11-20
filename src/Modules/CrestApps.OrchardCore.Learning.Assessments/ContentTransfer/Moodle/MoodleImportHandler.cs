using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Services;
using CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Qti;
using CrestApps.OrchardCore.Learning.Assessments.Core.Indexes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Moodle;
using CrestApps.OrchardCore.Learning.Assessments.Core.Qti;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Indexes;
using CrestApps.OrchardCore.Learning.Core.Moodle;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;
using CrestApps.OrchardCore.Learning.Surveys;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Entities;
using OrchardCore.Html.Models;
using OrchardCore.Infrastructure.Html;
using OrchardCore.Lists.Models;
using OrchardCore.Media;
using OrchardCore.Modules;
using OrchardCore.Title.Models;
using OrchardCore.Users;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer.Moodle;

/// <summary>
/// Imports the rows the Moodle reader wrote: the course (its summary with its pictures, its picture, dates, category,
/// enrolment, completion and its sections as outline groups, with the items imported before placed in them) and each
/// activity as its item (a SCORM or IMS content package through the package importer, a quiz with its questions through
/// the question columns of the QTI import, a file in the media library, a link, a page, a label as text, an assignment
/// as coursework, a feedback or a choice as a survey), with its restrictions and the pictures of its text, placed in its
/// section of the course. A hidden activity is optional. A row with a reason fails with it.
/// Only rows of a file the Moodle format read are touched: the Moodle columns of any other file are ignored.
/// </summary>
internal sealed class MoodleImportHandler : IContentImportHandler
{
    /// <summary>
    /// The prefix of the outline sections the import makes, followed by the Moodle section id.
    /// </summary>
    public const string SectionPrefix = "moodle-";

    private const int SummaryLength = 500;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

    private readonly IContentManager _contentManager;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly ISession _session;
    private readonly IServiceProvider _services;
    private readonly IEnumerable<IContentTransferFileFormatProvider> _formats;
    private readonly MoodleWorkspaces _workspaces;
    private readonly CourseCategoryService _categories;
    private readonly IHtmlSanitizerService _htmlSanitizer;
    private readonly IMediaFileStore _mediaFileStore;
    private readonly MediaOptions _mediaOptions;
    private readonly QuestionTypeOptions _questionTypes;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly Dictionary<string, string> _banks = new(StringComparer.Ordinal);

    internal readonly IStringLocalizer S;

    public MoodleImportHandler(
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ISession session,
        IServiceProvider services,
        IEnumerable<IContentTransferFileFormatProvider> formats,
        MoodleWorkspaces workspaces,
        CourseCategoryService categories,
        IHtmlSanitizerService htmlSanitizer,
        IMediaFileStore mediaFileStore,
        IOptions<MediaOptions> mediaOptions,
        IOptions<QuestionTypeOptions> questionTypes,
        IClock clock,
        ILogger<MoodleImportHandler> logger,
        IStringLocalizer<MoodleImportHandler> stringLocalizer)
    {
        _contentManager = contentManager;
        _contentDefinitionManager = contentDefinitionManager;
        _session = session;
        _services = services;
        _formats = formats;
        _workspaces = workspaces;
        _categories = categories;
        _htmlSanitizer = htmlSanitizer;
        _mediaFileStore = mediaFileStore;
        _mediaOptions = mediaOptions.Value;
        _questionTypes = questionTypes.Value;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Tells whether an import's file was read by the Moodle format: only such rows carry the Moodle columns the reader
    /// wrote.
    /// </summary>
    /// <param name="formats">The file formats.</param>
    /// <param name="entry">The import.</param>
    /// <returns><see langword="true"/> for a Moodle backup.</returns>
    internal static bool ReadByMoodle(IEnumerable<IContentTransferFileFormatProvider> formats, ContentTransferEntry entry)
    {
        var fileName = string.IsNullOrWhiteSpace(entry?.StoredFileName) ? entry?.UploadedFileName : entry.StoredFileName;

        return !string.IsNullOrWhiteSpace(fileName) && (formats ?? []).OfType<MoodleFileFormatProvider>().Any(format => format.CanHandle(fileName));
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<ImportColumn>> GetColumnsAsync(ImportContentContext context)
        => Task.FromResult<IReadOnlyCollection<ImportColumn>>([]);

    /// <inheritdoc />
    public Task ExportAsync(ContentExportContext context)
        => Task.CompletedTask;

    /// <inheritdoc />
    public async Task ImportAsync(ContentImportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Columns is null || !context.Columns.Contains(MoodleColumns.Source) || !ReadByMoodle(_formats, context.Entry))
        {
            return;
        }

        if (Cell(context, MoodleColumns.Error) is { Length: > 0 } error)
        {
            context.Fail(error, Cell(context, MoodleColumns.ErrorCategory));

            return;
        }

        var source = Cell(context, MoodleColumns.Source);

        if (string.IsNullOrEmpty(source))
        {
            return;
        }

        if (_workspaces.Find(Cell(context, MoodleColumns.Workspace)) is not { } workspace)
        {
            context.Fail(S["The backup is no longer open. Import the file again."], S["Not imported from Moodle"]);

            return;
        }

        var backup = workspace.Backup;

        if (source == backup.CourseSource)
        {
            await ImportCourseAsync(context, backup);

            return;
        }

        var activity = backup.Activities.FirstOrDefault(candidate => backup.ActivitySource(candidate.ModuleId) == source);

        if (activity is null)
        {
            context.Fail(S["The backup has no activity {0}.", source], S["Not imported from Moodle"]);

            return;
        }

        var imported = activity.ModuleName switch
        {
            "scorm" => await ImportPackageAsync(context, backup, activity),
            "imscp" => await ImportImsPackageAsync(context, backup, activity),
            "resource" => await ImportFileAsync(context, backup, activity),
            "url" => await ImportLinkAsync(context, backup, activity),
            "page" => await ImportPageAsync(context, backup, activity),
            "label" => await ImportLabelAsync(context, backup, activity),
            "quiz" => await ImportQuizAsync(context, backup, activity),
            "assign" => await ImportAssignmentAsync(context, backup, activity),
            "feedback" => await ImportSurveyAsync(context, backup, activity, MoodleSurveys.FromFeedback(activity), withBody: true),
            "choice" => await ImportSurveyAsync(context, backup, activity, MoodleSurveys.FromChoice(activity), withBody: false),
            _ => Fail(context, S["The {0} activity is not imported.", activity.ModuleName]),
        };

        if (imported)
        {
            await ApplyRestrictionsAsync(context.ContentItem, activity);
            await AttachAsync(context.ContentItem, backup, activity);
        }
    }

    private async Task ImportCourseAsync(ContentImportContext context, MoodleBackup backup)
    {
        var course = context.ContentItem;
        var moodle = backup.Course;
        var summary = Html(moodle.Summary, Urls(await ImportMediaAsync(course, backup, CourseFiles(backup, "summary"))));

        course.Alter<CoursePart>(part =>
        {
            part.SourceIdentifier = backup.CourseSource;
            part.Code = !string.IsNullOrWhiteSpace(moodle.IdNumber)
                ? moodle.IdNumber.Trim()
                : !string.IsNullOrWhiteSpace(moodle.ShortName) && !string.Equals(moodle.ShortName.Trim(), moodle.FullName?.Trim(), StringComparison.OrdinalIgnoreCase) ? moodle.ShortName.Trim() : part.Code;
            part.Summary = Shorten(MoodleQuestionMapper.Plain(moodle.Summary), SummaryLength);
            part.StartsUtc = moodle.StartUtc;
            part.EndsUtc = moodle.EndUtc is { } end && (moodle.StartUtc is null || end > moodle.StartUtc) ? end : null;
            ApplyEnrolment(part, moodle);
            ApplyCompletion(part, moodle);
        });

        if (summary is not null)
        {
            course.Alter<HtmlBodyPart>(part => part.Html = summary);
        }

        // The course's picture (its overview file) is the cover the catalog and the course page show.
        var cover = (await ImportMediaAsync(course, backup, CourseFiles(backup, "overviewfiles").Where(file => ImageExtensions.Contains(Path.GetExtension(file.FileName ?? string.Empty))).Take(1))).FirstOrDefault();

        if (cover.Path is not null)
        {
            var content = (JsonObject)course.Content;

            if (content[LearningConstants.ContentTypes.Course] is not JsonObject coursePart)
            {
                coursePart = [];
                content[LearningConstants.ContentTypes.Course] = coursePart;
            }

            coursePart["Cover"] = new JsonObject
            {
                ["Paths"] = new JsonArray(cover.Path),
                ["MediaTexts"] = new JsonArray(string.Empty),
            };
        }

        if (!string.IsNullOrWhiteSpace(moodle.CategoryName) && await _categories.FindOrAddAsync([moodle.CategoryName]) is { } categoryId)
        {
            await _categories.SetCategoriesAsync(course, [categoryId]);
        }

        // The sections, in order, with the items of this backup imported before placed in them, and the items the
        // course has from elsewhere kept in their section.
        var children = await ChildrenAsync(backup);
        var existing = course.Get<CourseOutlinePart>(nameof(CourseOutlinePart))?.Sections ?? [];
        var sections = new List<CourseSection>();

        foreach (var moodleSection in backup.Sections)
        {
            var id = SectionPrefix + moodleSection.Id;
            var old = existing.FirstOrDefault(section => section.Id == id);
            var section = new CourseSection
            {
                Id = id,
                Title = SectionTitle(moodleSection),
                Summary = SectionSummary(backup, moodleSection),
                ReleaseRules = old?.ReleaseRules ?? [],
            };

            foreach (var moduleId in moodleSection.Sequence)
            {
                if (children.TryGetValue(backup.ActivitySource(moduleId), out var itemId) && backup.FindActivity(moduleId) is { } activity)
                {
                    section.Items.Add(new CourseOutlineItem { ContentItemId = itemId, Required = IsRequired(backup, activity) });
                }
            }

            foreach (var item in old?.Items.Where(item => !children.ContainsValue(item.ContentItemId)) ?? [])
            {
                section.Items.Add(item);
            }

            if (section.Items.Count > 0 || !string.IsNullOrEmpty(section.Summary) || old is not null)
            {
                sections.Add(section);
            }
        }

        // A section of the course's own that the backup does not have is kept after them.
        foreach (var section in existing.Where(section => !sections.Any(candidate => candidate.Id == section.Id) && !section.Id.StartsWith(SectionPrefix, StringComparison.Ordinal)))
        {
            sections.Add(section);
        }

        course.Alter<CourseOutlinePart>(part => part.Sections = sections);
    }

    private static void ApplyEnrolment(CoursePart part, MoodleCourse moodle)
    {
        var enabled = moodle.Enrolments.Where(enrolment => enrolment.Enabled).ToList();
        var self = enabled.FirstOrDefault(enrolment => enrolment.Method == "self" && enrolment.AllowsNewEnrolments);

        // Self enrolment with a key takes a key here too; the key itself is never imported, so the course needs one set.
        part.EnrollmentMode = self is null
            ? enabled.Any(enrolment => enrolment.Method == "apply") ? EnrollmentMode.Approval : EnrollmentMode.Closed
            : self.HasPassword ? EnrollmentMode.Key : EnrollmentMode.Open;
        part.AccessDays = self is { EnrolPeriodSeconds: > 0 } ? (int)Math.Ceiling(self.EnrolPeriodSeconds / 86400d) : part.AccessDays;
    }

    private static void ApplyCompletion(CoursePart part, MoodleCourse moodle)
    {
        var criteria = part.CompletionCriteria
            .Where(rule => rule.Name is not (LearningRuleNames.MinimumGrade or LearningRuleNames.StaffApproval))
            .ToList();

        if (moodle.CompletionCriteria.FirstOrDefault(criterion => criterion.Type == MoodleCompletionCriterion.Grade && criterion.GradePass is > 0) is { } grade)
        {
            var max = moodle.GradeMax is > 0 ? moodle.GradeMax.Value : 100;
            var rule = new LearningRuleSettings { Name = LearningRuleNames.MinimumGrade };
            rule.Values["Grade"] = Math.Clamp(Math.Round(grade.GradePass.Value / max * 100, 2), 0, 100).ToString("0.##", CultureInfo.InvariantCulture);
            criteria.Add(rule);
        }

        // Manual completion by a role becomes approval by the course staff in the matching role.
        if (moodle.CompletionCriteria.FirstOrDefault(criterion => criterion.Type == MoodleCompletionCriterion.Role) is { } role)
        {
            var approval = new LearningRuleSettings { Name = LearningRuleNames.StaffApproval };

            if (MoodleCompletion.StaffRole(role.RoleShortName) is { } staffRole)
            {
                approval.Values["Role"] = staffRole;
            }

            criteria.Add(approval);
        }

        part.CompletionCriteria = criteria;

        // A Moodle course without completion criteria never completes.
        part.NeverCompletes = moodle.CompletionCriteria.Count == 0;
    }

    private async Task<bool> ImportPackageAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var file = MoodleCourseRows.PackageFile(backup, activity);

        if (file is null)
        {
            return Fail(context, S["The backup has no package file for the SCORM package \"{0}\".", activity.Name]);
        }

        var importer = _services.GetRequiredService<LearningPackageImporter>();
        var fileName = file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? file.FileName : file.FileName + ".zip";
        PackageImportResult result;

        await using (var stream = new FileStream(backup.GetContentPath(file), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            result = await importer.ImportAsync(fileName, stream);
        }

        if (!result.Succeeded)
        {
            var reason = _services.GetRequiredService<PackageImportErrorText>().Get(result);

            return Fail(context, S["The SCORM package \"{0}\" ({1}) was not imported: {2}", activity.Name, file.FileName, reason.Value]);
        }

        var item = context.ContentItem;
        item.Alter<LearningPackagePart>(part =>
        {
            result.ApplyTo(part, file.FileName, context.Entry?.Owner, _clock.UtcNow);
            part.OpenInNewWindow = MoodleScormSettings.OpensInNewWindow(activity);
            part.MaxAttempts = MoodleScormSettings.MaxAttempts(activity);
            part.GradingMethod = MoodleScormSettings.Grading(activity);
        });

        var (mode, passMark) = MoodleCompletion.ForScorm(activity);
        ApplyItem(item, backup, activity, mode, passMark);
        await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));

        return true;
    }

    // An IMS content package: the package as it was uploaded, or a package made of the files Moodle extracted. It plays
    // without the run-time API and completes when the learner opens it.
    private async Task<bool> ImportImsPackageAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var files = MoodleCourseRows.ImsFiles(backup, activity);

        if (files.Count == 0)
        {
            return Fail(context, S["The backup has no file for the IMS content package \"{0}\".", activity.Name]);
        }

        var importer = _services.GetRequiredService<LearningPackageImporter>();
        var fileName = files.Count == 1 && files[0].FileArea == "backup" ? files[0].FileName : SafeFileName(activity.Name) + ".zip";
        PackageImportResult result;

        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".zip";
        }

        await using (var stream = await ImsPackageStreamAsync(backup, files))
        {
            result = await importer.ImportAsync(fileName, stream);
        }

        if (!result.Succeeded)
        {
            var reason = _services.GetRequiredService<PackageImportErrorText>().Get(result);

            return Fail(context, S["The IMS content package \"{0}\" was not imported: {1}", activity.Name, reason.Value]);
        }

        var item = context.ContentItem;
        item.Alter<LearningPackagePart>(part => result.ApplyTo(part, fileName, context.Entry?.Owner, _clock.UtcNow));

        ApplyItem(item, backup, activity, PackageImportResult.CompletionModeFor(result.Manifest?.Standard) ?? MoodleCompletion.ForViewed(activity), null);
        await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));

        return true;
    }

    private static async Task<Stream> ImsPackageStreamAsync(MoodleBackup backup, IReadOnlyList<MoodleFile> files)
    {
        if (files.Count == 1 && files[0].FileArea == "backup")
        {
            return new FileStream(backup.GetContentPath(files[0]), FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        var zip = new MemoryStream();

        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(MoodleFiles.PathOf(file).TrimStart('/'));

                await using var target = entry.Open();
                await using var source = new FileStream(backup.GetContentPath(file), FileMode.Open, FileAccess.Read, FileShare.Read);
                await source.CopyToAsync(target);
            }
        }

        zip.Position = 0;

        return zip;
    }

    private async Task<bool> ImportFileAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var files = MoodleCourseRows.ResourceFiles(backup, activity);

        if (files.Count == 0)
        {
            return Fail(context, S["The backup has no file for \"{0}\".", activity.Name]);
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file.FileName ?? string.Empty);

            if (!_mediaOptions.AllowedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return Fail(context, S["The file \"{0}\" is of a kind the media library does not take ({1}). Allow it in the media settings and import again.", file.FileName, string.IsNullOrEmpty(extension) ? "?" : extension]);
            }

            if (_mediaOptions.MaxFileSize > 0 && new FileInfo(backup.GetContentPath(file)).Length > _mediaOptions.MaxFileSize)
            {
                return Fail(context, S["The file \"{0}\" is larger than the media library takes ({1} MB). Raise its largest file size and import again.", file.FileName, _mediaOptions.MaxFileSize / (1024 * 1024)]);
            }
        }

        var item = context.ContentItem;
        var folder = $"mediafields/{item.ContentType}/{item.ContentItemId}";
        var paths = new List<string>();
        var names = new List<string>();

        foreach (var file in files)
        {
            var name = UniqueName(SafeFileName(file.FileName), paths);
            var path = folder + "/" + name;

            await using (var stream = new FileStream(backup.GetContentPath(file), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await _mediaFileStore.CreateFileFromStreamAsync(path, stream, overwrite: true);
            }

            paths.Add(path);
            names.Add(file.FileName);
        }

        var content = (JsonObject)item.Content;

        if (content[LearningConstants.ContentTypes.FileLesson] is not JsonObject part)
        {
            part = [];
            content[LearningConstants.ContentTypes.FileLesson] = part;
        }

        part["Files"] = new JsonObject
        {
            ["Paths"] = new JsonArray([.. paths.Select(path => (JsonNode)path)]),
            ["MediaTexts"] = new JsonArray([.. names.Select(_ => (JsonNode)string.Empty)]),
            ["AttachedFileNames"] = new JsonArray([.. names.Select(name => (JsonNode)name)]),
        };

        ApplyItem(item, backup, activity, MoodleCompletion.ForViewed(activity), null);
        await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));

        return true;
    }

    private async Task<bool> ImportLinkAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var address = activity.Get("externalurl")?.Trim();

        if (!MoodleCourseRows.IsWebAddress(address))
        {
            return Fail(context, S["\"{0}\" is not a web address.", address]);
        }

        // The link field is set by ContentTransfer's field handler, which runs after this one, from its columns.
        context.Row[MoodleCourseRows.LinkUrlColumn] = address;
        context.Row[MoodleCourseRows.LinkTextColumn] = activity.Name;
        context.Row[MoodleCourseRows.LinkTargetColumn] = "_blank";

        var item = context.ContentItem;

        ApplyItem(item, backup, activity, MoodleCompletion.ForViewed(activity), null);
        await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));

        return true;
    }

    private async Task<bool> ImportPageAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var item = context.ContentItem;

        ApplyItem(item, backup, activity, MoodleCompletion.ForViewed(activity), null);
        await ApplyBodyAsync(item, backup, activity, activity.Get("content") ?? activity.Get("intro"));

        return true;
    }

    // A label is text shown in place in the outline and the player, with its pictures in the media library.
    private async Task<bool> ImportLabelAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var item = context.ContentItem;

        ApplyItem(item, backup, activity, null, null);
        await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));

        return true;
    }

    // An assignment is coursework: its due date, points, what learners hand in (nothing for a grade-only sign-off), the
    // file limits and resubmission; it completes on hand-in when Moodle does, else when graded.
    private async Task<bool> ImportAssignmentAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var assignment = MoodleAssignments.Read(activity);
        var item = context.ContentItem;

        item.Alter<CourseworkPart>(part =>
        {
            part.DueUtc = assignment.DueUtc;
            part.MaxPoints = assignment.MaxPoints;
            part.SubmissionType = assignment switch
            {
                { AcceptsText: true, AcceptsFiles: true } => CourseworkSubmissionType.TextAndFiles,
                { AcceptsFiles: true } => CourseworkSubmissionType.Files,
                { AcceptsText: true } => CourseworkSubmissionType.Text,
                _ => CourseworkSubmissionType.SignOff,
            };
            part.MaxFiles = assignment.MaxFiles ?? part.MaxFiles;
            part.MaxFileSizeMb = assignment.MaxFileSizeMb ?? part.MaxFileSizeMb;
            part.AllowResubmission = assignment.AllowResubmission;
        });

        ApplyItem(item, backup, activity, assignment.CompletesOnSubmit ? CompletionModes.Submitted : CompletionModes.Graded, null);
        item.Alter<CourseItemPart>(part => part.Gradable = true);
        await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));

        return true;
    }

    // A feedback or a choice is a survey of the Learning Surveys module; it completes when the learner answers.
    private async Task<bool> ImportSurveyAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity, MoodleSurvey survey, bool withBody)
    {
        if (survey.Questions.Count == 0 || survey.Questions.Any(question => question.Kind != MoodleSurveyQuestionKind.Text && question.Options.Count == 0))
        {
            return Fail(context, S["\"{0}\" has no question the site can keep.", activity.Name]);
        }

        var item = context.ContentItem;

        item.Alter<SurveyPart>(part =>
        {
            part.Questions = [.. survey.Questions.Take(SurveysConstants.MaxQuestions).Select((question, index) => new SurveyQuestion
            {
                // Stable ids, so importing the backup again keeps the answers given.
                Id = "moodle-" + (index + 1).ToString(CultureInfo.InvariantCulture),
                Text = Shorten(question.Text, 500),
                Type = question.Kind switch
                {
                    MoodleSurveyQuestionKind.MultipleChoice => SurveyQuestionType.MultipleChoice,
                    MoodleSurveyQuestionKind.Text => SurveyQuestionType.Text,
                    _ => SurveyQuestionType.SingleChoice,
                },
                Options = [.. question.Options.Take(SurveysConstants.MaxOptions).Select(option => Shorten(option, 200))],
                Required = question.Required,
            })];
            part.Anonymous = survey.Anonymous;
            part.AllowChangeAnswer = survey.AllowChangeAnswer;
            part.CloseUtc = survey.CloseUtc;
            part.ResultsVisibility = survey.Results switch
            {
                MoodleSurveyResults.AfterAnswer => SurveyResultsVisibility.AfterAnswer,
                MoodleSurveyResults.AfterClose => SurveyResultsVisibility.AfterClose,
                MoodleSurveyResults.Always => SurveyResultsVisibility.Always,
                _ => SurveyResultsVisibility.Never,
            };
        });

        ApplyItem(item, backup, activity, CompletionModes.Submitted, null);

        if (withBody)
        {
            await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));
        }

        return true;
    }

    private async Task<bool> ImportQuizAsync(ContentImportContext context, MoodleBackup backup, MoodleActivity activity)
    {
        var questionIds = new List<string>();
        var publish = context.Entry?.GetOrCreate<ImportContentOptionsPart>().PublishImportedContent ?? false;

        // The fixed questions, in slot order.
        foreach (var slot in activity.Slots.Where(slot => !string.IsNullOrEmpty(slot.BankEntryId)))
        {
            if (backup.FindQuestion(slot.BankEntryId) is not { } question)
            {
                continue;
            }

            var (id, error) = await ImportQuestionAsync(context, backup, question, publish);

            if (error is not null)
            {
                return Fail(context, error);
            }

            if (id is not null && !questionIds.Contains(id, StringComparer.Ordinal))
            {
                questionIds.Add(id);
            }
        }

        // A random slot draws from its category's bank, which gets every question of the category.
        var draws = new List<QuizDrawRule>();

        foreach (var group in activity.Slots.Where(slot => !string.IsNullOrEmpty(slot.RandomCategoryId)).GroupBy(slot => slot.RandomCategoryId, StringComparer.Ordinal))
        {
            if (backup.QuestionCategories.FirstOrDefault(category => category.Id == group.Key) is not { } category)
            {
                continue;
            }

            foreach (var question in category.Questions)
            {
                var (_, error) = await ImportQuestionAsync(context, backup, question, publish);

                if (error is not null)
                {
                    return Fail(context, error);
                }
            }

            draws.Add(new QuizDrawRule
            {
                BankId = await BankAsync(context, backup, category),
                Count = group.Count(),
            });
        }

        var item = context.ContentItem;

        item.Alter<QuizPart>(part =>
        {
            part.QuestionIds = questionIds;
            part.DrawRules = draws;
            part.TimeLimitMinutes = Integer(activity.Get("timelimit")) is { } seconds && seconds > 0 ? (int)Math.Ceiling(seconds / 60d) : null;
            part.MaxAttempts = Integer(activity.Get("attempts_number")) is { } attempts && attempts > 0 ? (int)attempts : null;
            part.GradingMethod = activity.Get("grademethod") switch
            {
                "2" => GradingMethod.Average,
                "3" => GradingMethod.First,
                "4" => GradingMethod.Latest,
                _ => GradingMethod.Highest,
            };
            part.Navigation = activity.Get("navmethod") == "sequential" ? QuizNavigation.Sequential : QuizNavigation.Free;
            part.OpensUtc = Time(activity.Get("timeopen"));
            part.ClosesUtc = Time(activity.Get("timeclose"));
            part.PassMark = MoodleCompletion.QuizPassMark(activity);
            part.QuestionsPerPage = MoodleQuizSettings.QuestionsPerPage(activity);
            part.FeedbackMode = MoodleQuizSettings.FeedbackMode(activity);

            if (MoodleQuizSettings.HasReviewOptions(activity))
            {
                part.ReviewOptions = MoodleQuizSettings.ReviewOptions(activity);
            }
        });

        var mode = MoodleCompletion.ForQuiz(activity);
        ApplyItem(item, backup, activity, mode, null);
        await ApplyBodyAsync(item, backup, activity, activity.Get("intro"));

        return true;
    }

    // A question is imported through the question columns of the QTI import, so it is checked and stored as a QTI item
    // is; one imported before (by its source identifier) is updated where it is.
    private async Task<(string Id, string Error)> ImportQuestionAsync(ContentImportContext context, MoodleBackup backup, MoodleQuestion question, bool publish)
    {
        var mapping = MoodleQuestionMapper.Map(question, backup.QuestionSource(question.BankEntryId), backup.Name + "/questions.xml");

        if (mapping.Refusal is not null)
        {
            return (null, null);
        }

        var values = QtiQuestionRows.Write(new QtiReadResult { Items = [mapping.Item] }, _questionTypes, S)[0];
        var columns = QtiQuestionRows.Columns;
        var contentType = values[IndexOf(columns, ContentTransferColumns.ContentType)];

        if (string.IsNullOrEmpty(contentType) || !string.IsNullOrEmpty(values[IndexOf(columns, QtiColumns.Error)]))
        {
            return (null, null);
        }

        var identifier = QuestionSourceIndexProvider.Truncate(mapping.Item.Identifier);
        var existingId = (await _session.QueryIndex<QuestionSourceIndex>(index => index.SourceIdentifier == identifier).FirstOrDefaultAsync())?.ContentItemId;
        var item = existingId is null ? null : await _contentManager.GetAsync(existingId, VersionOptions.DraftRequired);
        var isNew = item is null || item.ContentType != contentType;

        if (isNew)
        {
            item = await _contentManager.NewAsync(contentType);

            var category = backup.QuestionCategories.FirstOrDefault(candidate => candidate.Id == question.CategoryId);
            var bankId = category is null ? null : await BankAsync(context, backup, category);

            item.Alter<ContainedPart>(part =>
            {
                part.ListContentItemId = bankId;
                part.ListContentType = AssessmentsConstants.ContentTypes.QuestionBank;
            });
        }

        using var table = new DataTable();

        foreach (var column in columns)
        {
            table.Columns.Add(column);
        }

        var row = table.NewRow();

        for (var i = 0; i < columns.Count; i++)
        {
            row[i] = (object)values[i] ?? DBNull.Value;
        }

        table.Rows.Add(row);

        var nested = new ContentImportContext
        {
            ContentItem = item,
            ContentTypeDefinition = await _contentDefinitionManager.GetTypeDefinitionAsync(contentType),
            Entry = context.Entry,
            Columns = table.Columns,
            Row = row,
        };

        await _services.GetRequiredService<IContentImportManager>().ImportAsync(nested);

        if (nested.Errors.Count > 0)
        {
            return (null, S["The question \"{0}\" was not imported: {1}", question.Name, string.Join(" ", nested.Errors)]);
        }

        var validation = await _contentManager.ValidateAsync(item);

        if (!validation.Succeeded)
        {
            return (null, S["The question \"{0}\" was not imported: {1}", question.Name, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage))]);
        }

        item.Owner = context.Entry?.Owner;
        item.Author = context.Entry?.Author;

        if (isNew)
        {
            await _contentManager.CreateAsync(item, VersionOptions.DraftRequired);
        }
        else
        {
            await _contentManager.UpdateAsync(item);
        }

        if (publish)
        {
            await _contentManager.PublishAsync(item);
        }
        else if (!isNew)
        {
            await _contentManager.SaveDraftAsync(item);
        }

        return (item.ContentItemId, null);
    }

    // The bank of a question category: the bank its questions went to before, a bank of the same title, or a new one.
    private async Task<string> BankAsync(ContentImportContext context, MoodleBackup backup, MoodleQuestionCategory category)
    {
        var key = backup.SiteKey + ":" + category.Id;

        if (_banks.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var sources = category.Questions.Select(question => QuestionSourceIndexProvider.Truncate(backup.QuestionSource(question.BankEntryId))).ToArray();

        if (sources.Length > 0 &&
            await _session.QueryIndex<QuestionSourceIndex>(index => index.SourceIdentifier.IsIn(sources) && index.QuestionBankId != null).FirstOrDefaultAsync() is { } imported)
        {
            return _banks[key] = imported.QuestionBankId;
        }

        var title = Shorten($"{backup.Course.FullName}: {category.Name}", 200);
        var existing = await _session.Query<ContentItem, ContentItemIndex>(index =>
                index.ContentType == AssessmentsConstants.ContentTypes.QuestionBank && index.Latest && index.DisplayText == title)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            return _banks[key] = existing.ContentItemId;
        }

        var bank = await _contentManager.NewAsync(AssessmentsConstants.ContentTypes.QuestionBank);
        bank.DisplayText = title;
        bank.Alter<TitlePart>(part => part.Title = title);
        bank.Owner = context.Entry?.Owner;
        bank.Author = context.Entry?.Author;
        await _contentManager.CreateAsync(bank, VersionOptions.Published);

        return _banks[key] = bank.ContentItemId;
    }

    // An item imported in a later row than its course: it joins the course and its place in the section.
    private async Task AttachAsync(ContentItem item, MoodleBackup backup, MoodleActivity activity)
    {
        var course = await FindBySourceAsync(backup.CourseSource);

        if (course is null)
        {
            return;
        }

        item.Alter<ContainedPart>(part =>
        {
            part.ListContentItemId = course.ContentItemId;
            part.ListContentType = course.ContentType;
        });

        var sections = course.Get<CourseOutlinePart>(nameof(CourseOutlinePart))?.Sections ?? [];

        if (sections.Any(section => section.Items.Any(entry => entry.ContentItemId == item.ContentItemId)))
        {
            return;
        }

        var moodleSection = backup.Sections.FirstOrDefault(section => section.Id == activity.SectionId);
        var sectionId = SectionPrefix + activity.SectionId;
        var target = sections.FirstOrDefault(section => section.Id == sectionId);

        if (target is null)
        {
            target = new CourseSection
            {
                Id = sectionId,
                Title = moodleSection is null ? S["Imported"] : SectionTitle(moodleSection),
                Summary = moodleSection is null ? null : SectionSummary(backup, moodleSection),
            };
            sections.Add(target);
        }

        // After the last item of the section that comes before it in Moodle.
        var position = 0;

        if (moodleSection is not null && target.Items.Count > 0)
        {
            var index = moodleSection.Sequence.IndexOf(activity.ModuleId);
            var earlier = moodleSection.Sequence.Take(Math.Max(index, 0)).Select(backup.ActivitySource).ToHashSet(StringComparer.Ordinal);
            var itemIds = target.Items.Select(entry => entry.ContentItemId).ToArray();
            var earlierIds = (await _session.QueryIndex<LearningSourceIndex>(index => index.ContentItemId.IsIn(itemIds)).ListAsync())
                .Where(source => earlier.Contains(source.SourceIdentifier))
                .Select(source => source.ContentItemId)
                .ToHashSet(StringComparer.Ordinal);

            for (var i = 0; i < target.Items.Count; i++)
            {
                if (earlierIds.Contains(target.Items[i].ContentItemId))
                {
                    position = i + 1;
                }
            }
        }

        target.Items.Insert(position, new CourseOutlineItem { ContentItemId = item.ContentItemId, Required = IsRequired(backup, activity) });
        course.Alter<CourseOutlinePart>(part => part.Sections = sections);
        await _contentManager.UpdateAsync(course);
    }

    private async Task<ContentItem> FindBySourceAsync(string source)
    {
        var identifier = LearningSourceIndexProvider.Truncate(source);
        var index = await _session.QueryIndex<LearningSourceIndex>(entry => entry.SourceIdentifier == identifier).FirstOrDefaultAsync();

        return index is null ? null : await _contentManager.GetAsync(index.ContentItemId, VersionOptions.Latest);
    }

    private async Task<Dictionary<string, string>> ChildrenAsync(MoodleBackup backup)
    {
        var prefix = backup.CourseSource + ":cm:";
        var children = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var index in await _session.QueryIndex<LearningSourceIndex>(entry => entry.SourceIdentifier.StartsWith(prefix)).ListAsync())
        {
            children.TryAdd(index.SourceIdentifier, index.ContentItemId);
        }

        return children;
    }

    private static void ApplyItem(ContentItem item, MoodleBackup backup, MoodleActivity activity, string mode, decimal? passMark)
    {
        var source = backup?.ActivitySource(activity.ModuleId);

        item.Alter<CourseItemPart>(part =>
        {
            part.SourceIdentifier = source ?? part.SourceIdentifier;
            part.CompletionMode = mode;
            part.PassMark = passMark;
        });
    }

    // The text of an activity, with the pictures and files of its text areas copied to the media library under the item.
    private async Task ApplyBodyAsync(ContentItem item, MoodleBackup backup, MoodleActivity activity, string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        var files = html.Contains(MoodleFiles.PluginFile, StringComparison.Ordinal)
            ? await ImportMediaAsync(item, backup, activity.Files.Where(file => file.FileArea is "intro" or "content" && file.Component == "mod_" + activity.ModuleName))
            : [];

        if (Html(html, Urls(files)) is { } body)
        {
            item.Alter<HtmlBodyPart>(part => part.Html = body);
        }
    }

    // Moodle HTML, its pictures and files pointed at the media library (those not imported left out), sanitized as
    // Orchard's HTML body is.
    private string Html(string html, IReadOnlyDictionary<string, string> urls)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var sanitized = _htmlSanitizer.Sanitize(MoodleFiles.Rewrite(html, urls));

        return string.IsNullOrWhiteSpace(MoodleQuestionMapper.Plain(sanitized)) && !sanitized.Contains("<img", StringComparison.OrdinalIgnoreCase) ? null : sanitized;
    }

    // Copies files of the backup to the media library, in the folder of the item, as the media fields keep theirs. A file
    // of a kind or size the media library does not take is left out.
    private async Task<List<(MoodleFile File, string Path, string Url)>> ImportMediaAsync(ContentItem item, MoodleBackup backup, IEnumerable<MoodleFile> files)
    {
        var imported = new List<(MoodleFile File, string Path, string Url)>();
        var paths = new List<string>();
        var folder = $"mediafields/{item.ContentType}/{item.ContentItemId}";

        foreach (var file in files)
        {
            var content = backup.GetContentPath(file);
            var extension = Path.GetExtension(file.FileName ?? string.Empty);

            if (content is null ||
                !_mediaOptions.AllowedFileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) ||
                (_mediaOptions.MaxFileSize > 0 && new FileInfo(content).Length > _mediaOptions.MaxFileSize))
            {
                continue;
            }

            var path = folder + "/" + UniqueName(SafeFileName(file.FileName), paths);

            await using (var stream = new FileStream(content, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await _mediaFileStore.CreateFileFromStreamAsync(path, stream, overwrite: true);
            }

            paths.Add(path);
            imported.Add((file, path, _mediaFileStore.MapPathToPublicUrl(path)));
        }

        return imported;
    }

    private static Dictionary<string, string> Urls(IEnumerable<(MoodleFile File, string Path, string Url)> files)
    {
        var urls = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (file, _, url) in files)
        {
            urls.TryAdd(MoodleFiles.PathOf(file), url);
        }

        return urls;
    }

    // The course's own files of an area: its summary's pictures, its picture.
    private static IEnumerable<MoodleFile> CourseFiles(MoodleBackup backup, string fileArea)
        => backup.Files.Values.Where(file =>
            file.Component == "course" &&
            file.FileArea == fileArea &&
            (string.IsNullOrEmpty(backup.Course?.ContextId) || file.ContextId == backup.Course.ContextId));

    // The restrictions of an activity the site keeps: open to the people named by e-mail address or user name (the
    // site's users with them), and an opening date. The rules the import set before are replaced.
    private async Task ApplyRestrictionsAsync(ContentItem item, MoodleActivity activity)
    {
        var restriction = MoodleRestrictions.Read(activity.Availability);
        var userIds = new List<string>();

        if (restriction.People.Count > 0 && _services.GetService<UserManager<IUser>>() is { } users)
        {
            foreach (var (field, value) in restriction.People)
            {
                var user = field == "email" ? await users.FindByEmailAsync(value) : await users.FindByNameAsync(value);

                if (user is not null && await users.GetUserIdAsync(user) is { Length: > 0 } userId && !userIds.Contains(userId, StringComparer.Ordinal))
                {
                    userIds.Add(userId);
                }
            }
        }

        item.Alter<CourseItemPart>(part =>
        {
            var rules = part.ReleaseRules.Where(rule => rule.Name is not (LearningRuleNames.People or LearningRuleNames.Date)).ToList();

            if (restriction.People.Count > 0)
            {
                var people = new LearningRuleSettings { Name = LearningRuleNames.People };
                people.Values["UserIds"] = string.Join(",", userIds);
                rules.Add(people);
            }

            if (restriction.OpensUtc is { } opens)
            {
                var date = new LearningRuleSettings { Name = LearningRuleNames.Date };
                date.Values["FromUtc"] = DateTime.SpecifyKind(opens, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);
                rules.Add(date);
            }

            part.ReleaseRules = rules;
        });
    }

    // An activity counts toward completion as Moodle says, unless it is hidden (it then holds back nothing) or a label
    // (text, which is never tracked).
    private static bool IsRequired(MoodleBackup backup, MoodleActivity activity)
        => activity.Visible && activity.ModuleName != "label" && MoodleCompletion.IsRequired(backup, activity);

    private string SectionTitle(MoodleSection section)
        => !string.IsNullOrWhiteSpace(section.Name)
            ? section.Name.Trim()
            : section.Number == 0 ? S["General"] : S["Topic {0}", section.Number];

    // The section's summary, as plain text. Its labels are Text items of their own.
    private static string SectionSummary(MoodleBackup backup, MoodleSection section)
        => MoodleQuestionMapper.Plain(section.Summary) is { Length: > 0 } summary ? summary : null;

    private bool Fail(ContentImportContext context, string error)
    {
        context.Fail(error, S["Not imported from Moodle"]);

        return false;
    }

    private static string Cell(ContentImportContext context, string column)
        => context.Columns.Contains(column) ? context.Row[column]?.ToString()?.Trim() : null;

    private static int IndexOf(IReadOnlyList<string> columns, string column)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (columns[i] == column)
            {
                return i;
            }
        }

        return -1;
    }

    private static string SafeFileName(string name)
    {
        var builder = new StringBuilder();

        foreach (var character in Path.GetFileName(name ?? string.Empty))
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '-');
        }

        var safe = builder.ToString().Trim('.', '-');

        return string.IsNullOrEmpty(safe) ? "file" : safe;
    }

    private static string UniqueName(string name, List<string> paths)
    {
        var candidate = name;
        var number = 2;

        while (paths.Any(path => path.EndsWith("/" + candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = Path.GetFileNameWithoutExtension(name) + "-" + number.ToString(CultureInfo.InvariantCulture) + Path.GetExtension(name);
            number++;
        }

        return candidate;
    }

    private static string Shorten(string text, int length)
        => string.IsNullOrEmpty(text) || text.Length <= length ? text : text[..(length - 1)].TrimEnd() + "…";

    private static long? Integer(string value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static DateTime? Time(string value)
        => Integer(value) is { } seconds && seconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime : null;
}
