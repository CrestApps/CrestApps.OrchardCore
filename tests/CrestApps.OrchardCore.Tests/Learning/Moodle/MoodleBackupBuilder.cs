using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security;
using System.Text;

namespace CrestApps.OrchardCore.Tests.Learning.Moodle;

/// <summary>
/// Builds a synthetic Moodle course backup (<c>.mbz</c>) in the layout Moodle 4.x writes: <c>moodle_backup.xml</c>,
/// <c>course/</c>, <c>sections/</c>, <c>activities/</c>, <c>questions.xml</c>, <c>files.xml</c> and the files under
/// <c>files/</c> named by their SHA-1 content hash. Every value is made up for the tests; no real backup is used.
/// </summary>
public sealed class MoodleBackupBuilder
{
    private const string Null = "$@NULL@$";

    private readonly List<SectionEntry> _sections = [];
    private readonly List<Activity> _activities = [];
    private readonly List<StoredFile> _files = [];
    private readonly List<Enrol> _enrols = [];
    private readonly List<Criterion> _criteria = [];
    private readonly List<QuestionCategory> _categories = [];
    private readonly Dictionary<string, byte[]> _extra = new(StringComparer.Ordinal);
    private int _nextFileId = 100;
    private int _nextContextId = 500;

    public MoodleBackupBuilder(int courseId, string fullName, string shortName = null)
    {
        CourseId = courseId;
        FullName = fullName;
        ShortName = shortName ?? fullName;
    }

    public int CourseId { get; }

    public string FullName { get; }

    public string ShortName { get; }

    public string SiteIdentifier { get; set; } = "0123456789abcdef0123456789abcdef";

    public string IdNumber { get; set; }

    public string Summary { get; set; } = "<p>A course made up for the tests.</p>";

    public int CategoryId { get; set; } = 7;

    public string CategoryName { get; set; } = "Synthetic category";

    public DateTime? StartUtc { get; set; } = new DateTime(2024, 1, 15, 8, 0, 0, DateTimeKind.Utc);

    public DateTime? EndUtc { get; set; }

    public bool Visible { get; set; } = true;

    public bool EnableCompletion { get; set; } = true;

    public int CourseGradeMax { get; set; } = 100;

    /// <summary>
    /// Gets or sets the text of <c>moodle_backup.xml</c> to write instead of the one the builder makes.
    /// </summary>
    public string BackupXmlOverride { get; set; }

    /// <summary>
    /// Gets or sets the backup type <c>moodle_backup.xml</c> names: <c>course</c>, <c>section</c> or <c>activity</c>.
    /// </summary>
    public string BackupType { get; set; } = "course";

    /// <summary>
    /// Gets a minimal SCORM 1.2 package: a manifest and its launch page.
    /// </summary>
    public static byte[] ScormPackage(string title = "Synthetic package")
    {
        using var output = new MemoryStream();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "imsmanifest.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <manifest identifier="synthetic" version="1.0" xmlns="http://www.imsproject.org/xsd/imscp_rootv1p1p2" xmlns:adlcp="http://www.adlnet.org/xsd/adlcp_rootv1p2">
                  <metadata><schema>ADL SCORM</schema><schemaversion>1.2</schemaversion></metadata>
                  <organizations default="org">
                    <organization identifier="org">
                      <title>{SecurityElement.Escape(title)}</title>
                      <item identifier="item1" identifierref="res1"><title>{SecurityElement.Escape(title)}</title></item>
                    </organization>
                  </organizations>
                  <resources>
                    <resource identifier="res1" type="webcontent" adlcp:scormtype="sco" href="index.html"><file href="index.html" /></resource>
                  </resources>
                </manifest>
                """);
            Write(zip, "index.html", "<!doctype html><html><head><title>Package</title></head><body><p>Synthetic package.</p></body></html>");
        }

        return output.ToArray();

        static void Write(ZipArchive zip, string name, string text)
        {
            var entry = zip.CreateEntry(name);
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes(text));
        }
    }

    public MoodleBackupBuilder Section(int id, int number, string name, string summary = null, bool visible = true)
    {
        _sections.Add(new SectionEntry(id, number, name, summary, visible));

        return this;
    }

    public MoodleBackupBuilder Enrolment(string method, bool enabled = true, string password = null, int enrolPeriodDays = 0)
    {
        _enrols.Add(new Enrol(method, enabled, password, enrolPeriodDays));

        return this;
    }

    public MoodleBackupBuilder ActivityCriterion(int cmid, string moduleName)
    {
        _criteria.Add(new Criterion(4, cmid, moduleName, null, null, null));

        return this;
    }

    public MoodleBackupBuilder GradeCriterion(decimal gradePass)
    {
        _criteria.Add(new Criterion(6, null, null, gradePass, null, null));

        return this;
    }

    public MoodleBackupBuilder RoleCriterion(int roleId, string roleShortName = "manager")
    {
        _criteria.Add(new Criterion(7, null, null, null, roleId, roleShortName));

        return this;
    }

    public MoodleBackupBuilder DateCriterion()
    {
        _criteria.Add(new Criterion(2, null, null, null, null, null));

        return this;
    }

    /// <summary>
    /// Adds a file the backup holds under <c>files/</c>, with its row in <c>files.xml</c>.
    /// </summary>
    public int File(int contextId, string component, string fileArea, string fileName, byte[] content, string mimeType = "application/octet-stream", int itemId = 0, string filePath = "/")
    {
        var id = _nextFileId++;
#pragma warning disable CA5350 // Moodle names the files of a backup by the SHA-1 hash of their content.
        var hash = Convert.ToHexStringLower(SHA1.HashData(content));
#pragma warning restore CA5350
        _files.Add(new StoredFile(id, hash, contextId, component, fileArea, fileName, content, mimeType, itemId, filePath));

        return id;
    }

    public MoodleBackupBuilder Label(int sectionId, int cmid, string html, bool visible = true, string pictureName = null, byte[] picture = null)
    {
        var activity = new Activity(cmid, "label", sectionId, StripTags(html), visible)
        {
            Fields = { ["intro"] = html, ["introformat"] = "1" },
        };

        activity.ContextId = _nextContextId++;

        if (picture is not null)
        {
            activity.FileIds.Add(File(activity.ContextId, "mod_label", "intro", pictureName, picture, "image/png"));
        }

        return Add(activity);
    }

    /// <summary>
    /// Adds the course's picture (its overview file) and a picture its summary shows.
    /// </summary>
    public MoodleBackupBuilder CourseImage(string fileName, byte[] content)
    {
        File(25, "course", "overviewfiles", fileName, content, "image/png");

        return this;
    }

    public MoodleBackupBuilder SummaryImage(string fileName, byte[] content)
    {
        File(25, "course", "summary", fileName, content, "image/png");

        return this;
    }

    /// <summary>
    /// Restricts an activity with Moodle's availability JSON.
    /// </summary>
    public MoodleBackupBuilder Restrict(int cmid, string availability)
    {
        _activities.First(activity => activity.Cmid == cmid).Availability = availability;

        return this;
    }

    public MoodleBackupBuilder Assign(int sectionId, int cmid, string name, decimal grade = 10, DateTime? dueUtc = null, bool text = true, bool files = true, int maxFiles = 3, bool completionSubmit = true)
    {
        var activity = new Activity(cmid, "assign", sectionId, name)
        {
            Completion = 2,
            GradeMax = grade > 0 ? grade : null,
            Fields =
            {
                ["intro"] = "<p>Hand in your plan.</p>",
                ["duedate"] = Unix(dueUtc),
                ["grade"] = grade.ToString(CultureInfo.InvariantCulture),
                ["attemptreopenmethod"] = "none",
                ["completionsubmit"] = completionSubmit ? "1" : "0",
            },
        };

        activity.Records.Add(("plugin_configs", "plugin_config", new() { ["plugin"] = "onlinetext", ["subtype"] = "assignsubmission", ["name"] = "enabled", ["value"] = text ? "1" : "0" }));
        activity.Records.Add(("plugin_configs", "plugin_config", new() { ["plugin"] = "file", ["subtype"] = "assignsubmission", ["name"] = "enabled", ["value"] = files ? "1" : "0" }));
        activity.Records.Add(("plugin_configs", "plugin_config", new() { ["plugin"] = "file", ["subtype"] = "assignsubmission", ["name"] = "maxfilesubmissions", ["value"] = maxFiles.ToString(CultureInfo.InvariantCulture) }));

        return Add(activity);
    }

    public MoodleBackupBuilder Feedback(int sectionId, int cmid, string name, params (string Type, string Name, string Presentation, bool Required)[] items)
    {
        var activity = new Activity(cmid, "feedback", sectionId, name)
        {
            Completion = 2,
            Fields = { ["intro"] = "<p>Tell us what you think.</p>", ["anonymous"] = "2", ["multiple_submit"] = "0", ["timeclose"] = "0" },
        };

        foreach (var item in items)
        {
            activity.Records.Add(("items", "item", new() { ["typ"] = item.Type, ["name"] = item.Name, ["presentation"] = item.Presentation, ["required"] = item.Required ? "1" : "0" }));
        }

        return Add(activity);
    }

    public MoodleBackupBuilder Choice(int sectionId, int cmid, string name, string question, params string[] options)
    {
        var activity = new Activity(cmid, "choice", sectionId, name)
        {
            Completion = 2,
            Fields = { ["intro"] = question, ["allowmultiple"] = "0", ["allowupdate"] = "1", ["showresults"] = "1", ["publish"] = "1", ["timeclose"] = "0" },
        };

        foreach (var option in options)
        {
            activity.Records.Add(("options", "option", new() { ["text"] = option, ["maxanswers"] = "0" }));
        }

        return Add(activity);
    }

    public MoodleBackupBuilder Imscp(int sectionId, int cmid, string name, byte[] package)
    {
        var activity = new Activity(cmid, "imscp", sectionId, name)
        {
            Completion = 2,
            CompletionView = true,
            Fields = { ["intro"] = "<p>Read the handbook.</p>", ["revision"] = "1" },
        };

        activity.ContextId = _nextContextId++;
        activity.FileIds.Add(File(activity.ContextId, "mod_imscp", "backup", "handbook.zip", package, "application/zip", itemId: 1));

        return Add(activity);
    }

    /// <summary>
    /// Gets a minimal IMS content package: a manifest without SCORM resources and two pages.
    /// </summary>
    public static byte[] ImsPackage(string title = "Synthetic handbook")
    {
        using var output = new MemoryStream();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "imsmanifest.xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <manifest identifier="moodle.imscp" version="1.1" xmlns="http://www.imsglobal.org/xsd/imscp_v1p1">
                  <metadata><schema>IMS Content</schema><schemaversion>1.1.4</schemaversion></metadata>
                  <organizations default="TOC1">
                    <organization identifier="TOC1">
                      <title>{SecurityElement.Escape(title)}</title>
                      <item identifier="I0" identifierref="R0"><title>Start here</title></item>
                    </organization>
                  </organizations>
                  <resources>
                    <resource identifier="R0" type="webcontent" href="index.html"><file href="index.html"/></resource>
                  </resources>
                </manifest>
                """);
            Write(zip, "index.html", "<!doctype html><html><head><title>Handbook</title></head><body><p>Synthetic handbook.</p></body></html>");
        }

        return output.ToArray();

        static void Write(ZipArchive zip, string name, string text)
        {
            var entry = zip.CreateEntry(name);
            using var stream = entry.Open();
            stream.Write(Encoding.UTF8.GetBytes(text));
        }
    }

    /// <summary>
    /// Gets a tiny PNG picture.
    /// </summary>
    public static byte[] Png()
        => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    public MoodleBackupBuilder Page(int sectionId, int cmid, string name, string content, int completion = 2, bool completionView = true)
        => Add(new Activity(cmid, "page", sectionId, name)
        {
            Completion = completion,
            CompletionView = completionView,
            Fields = { ["intro"] = string.Empty, ["content"] = content, ["contentformat"] = "1" },
        });

    public MoodleBackupBuilder Url(int sectionId, int cmid, string name, string address, int completion = 2)
        => Add(new Activity(cmid, "url", sectionId, name)
        {
            Completion = completion,
            CompletionView = completion == 2,
            Fields = { ["intro"] = "<p>Read the page.</p>", ["externalurl"] = address, ["display"] = "0" },
        });

    public MoodleBackupBuilder Resource(int sectionId, int cmid, string name, string fileName, byte[] content, int completion = 2)
    {
        var activity = new Activity(cmid, "resource", sectionId, name)
        {
            Completion = completion,
            CompletionView = completion == 2,
            Fields = { ["intro"] = string.Empty, ["display"] = "0" },
        };

        activity.FileIds.Add(File(activity.ContextId = _nextContextId++, "mod_resource", "content", fileName, content));

        return Add(activity);
    }

    public MoodleBackupBuilder Scorm(int sectionId, int cmid, string name, string fileName, byte[] package, decimal? scoreRequired = null, int statusRequired = 4, bool useGrade = false, decimal? gradePass = null, int completion = 2, bool popup = false, int maxAttempt = 0, string scormType = "local")
    {
        var activity = new Activity(cmid, "scorm", sectionId, name)
        {
            Completion = completion,
            CompletionView = true,
            CompletionUseGrade = useGrade,
            CompletionPassGrade = gradePass is not null,
            GradeMax = 100,
            GradePass = gradePass,
            Fields =
            {
                ["scormtype"] = scormType,
                ["reference"] = fileName,
                ["intro"] = "<p>Run the package.</p>",
                ["version"] = "SCORM_1.2",
                ["maxgrade"] = "100",
                ["grademethod"] = "1",
                ["maxattempt"] = maxAttempt.ToString(CultureInfo.InvariantCulture),
                ["whatgrade"] = "0",
                ["popup"] = popup ? "1" : "0",
                ["completionstatusrequired"] = statusRequired.ToString(CultureInfo.InvariantCulture),
                ["completionscorerequired"] = scoreRequired?.ToString(CultureInfo.InvariantCulture) ?? Null,
            },
        };

        activity.ContextId = _nextContextId++;

        if (package is not null)
        {
            activity.FileIds.Add(File(activity.ContextId, "mod_scorm", "package", fileName, package, "application/zip"));
        }

        return Add(activity);
    }

    public MoodleBackupBuilder Quiz(int sectionId, int cmid, string name, IEnumerable<QuizSlot> slots, decimal gradeMax = 10, decimal? gradePass = 7, int attempts = 0, int timeLimitSeconds = 0, string navigation = "free", string password = "", int completion = 2, bool passGrade = true, int gradeMethod = 1)
    {
        var activity = new Activity(cmid, "quiz", sectionId, name)
        {
            Completion = completion,
            CompletionView = false,
            CompletionUseGrade = passGrade,
            CompletionPassGrade = passGrade,
            GradeMax = gradeMax,
            GradePass = gradePass,
            Fields =
            {
                ["intro"] = "<p>Check what you learned.</p>",
                ["timeopen"] = "0",
                ["timeclose"] = "0",
                ["timelimit"] = timeLimitSeconds.ToString(CultureInfo.InvariantCulture),
                ["attempts_number"] = attempts.ToString(CultureInfo.InvariantCulture),
                ["grademethod"] = gradeMethod.ToString(CultureInfo.InvariantCulture),
                ["questionsperpage"] = "1",
                ["navmethod"] = navigation,
                ["shuffleanswers"] = "1",
                ["sumgrades"] = "5.00000",
                ["grade"] = gradeMax.ToString(CultureInfo.InvariantCulture),
                ["password"] = password,
            },
        };

        activity.Slots.AddRange(slots);
        activity.ContextId = _nextContextId++;

        return Add(activity);
    }

    public MoodleBackupBuilder Hide(int cmid)
    {
        var index = _activities.FindIndex(activity => activity.Cmid == cmid);
        var old = _activities[index];
        var hidden = new Activity(old.Cmid, old.ModuleName, old.SectionId, old.Name, visible: false)
        {
            ContextId = old.ContextId,
            Completion = old.Completion,
            CompletionView = old.CompletionView,
            CompletionUseGrade = old.CompletionUseGrade,
            CompletionPassGrade = old.CompletionPassGrade,
            GradeMax = old.GradeMax,
            GradePass = old.GradePass,
            Availability = old.Availability,
        };

        foreach (var (key, value) in old.Fields)
        {
            hidden.Fields[key] = value;
        }

        hidden.FileIds.AddRange(old.FileIds);
        hidden.Slots.AddRange(old.Slots);
        hidden.Records.AddRange(old.Records);
        _activities[index] = hidden;

        return this;
    }

    public MoodleBackupBuilder Setting(int cmid, string name, string value)
    {
        _activities.First(activity => activity.Cmid == cmid).Fields[name] = value;

        return this;
    }

    public MoodleBackupBuilder Other(int sectionId, int cmid, string moduleName, string name)
        => Add(new Activity(cmid, moduleName, sectionId, name) { Fields = { ["intro"] = string.Empty } });

    public MoodleBackupBuilder Questions(int categoryId, string name, params Question[] questions)
    {
        _categories.Add(new QuestionCategory(categoryId, name, questions));

        return this;
    }

    /// <summary>
    /// Adds a file to the archive as it is, to test how the reader treats unexpected entries.
    /// </summary>
    public MoodleBackupBuilder RawEntry(string path, byte[] content)
    {
        _extra[path] = content;

        return this;
    }

    public byte[] BuildZip()
    {
        using var output = new MemoryStream();

        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in Entries())
            {
                var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
                using var stream = entry.Open();
                stream.Write(content);
            }
        }

        return output.ToArray();
    }

    public byte[] BuildTarGz()
    {
        using var output = new MemoryStream();

        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: true))
        {
            foreach (var (path, content) in Entries())
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, path)
                {
                    DataStream = new MemoryStream(content),
                };
                tar.WriteEntry(entry);
            }
        }

        return output.ToArray();
    }

    private IEnumerable<(string Path, byte[] Content)> Entries()
    {
        yield return ("moodle_backup.xml", Utf8(BackupXmlOverride ?? MoodleBackupXml()));
        yield return ("course/course.xml", Utf8(CourseXml()));
        yield return ("course/enrolments.xml", Utf8(EnrolmentsXml()));
        yield return ("course/completion.xml", Utf8(CompletionXml()));
        yield return ("course/inforef.xml", Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<inforef></inforef>"));
        yield return ("gradebook.xml", Utf8(GradebookXml()));
        yield return ("questions.xml", Utf8(QuestionsXml()));
        yield return ("files.xml", Utf8(FilesXml()));

        foreach (var section in _sections)
        {
            yield return ($"sections/section_{section.Id}/section.xml", Utf8(SectionXml(section)));
            yield return ($"sections/section_{section.Id}/inforef.xml", Utf8("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<inforef></inforef>"));
        }

        foreach (var activity in _activities)
        {
            var folder = $"activities/{activity.ModuleName}_{activity.Cmid}";
            yield return ($"{folder}/module.xml", Utf8(ModuleXml(activity)));
            yield return ($"{folder}/{activity.ModuleName}.xml", Utf8(ActivityXml(activity)));
            yield return ($"{folder}/inforef.xml", Utf8(InforefXml(activity)));
            yield return ($"{folder}/grades.xml", Utf8(GradesXml(activity)));
        }

        foreach (var file in _files.DistinctBy(file => file.Hash))
        {
            yield return ($"files/{file.Hash[..2]}/{file.Hash}", file.Content);
        }

        foreach (var (path, content) in _extra)
        {
            yield return (path, content);
        }
    }

    private MoodleBackupBuilder Add(Activity activity)
    {
        if (activity.ContextId == 0)
        {
            activity.ContextId = _nextContextId++;
        }

        _activities.Add(activity);

        return this;
    }

    private string MoodleBackupXml()
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<moodle_backup>\n  <information>\n");
        builder.Append($"    <name>backup-moodle2-course-{CourseId}-synthetic.mbz</name>\n");
        builder.Append("    <moodle_version>2023042400</moodle_version>\n    <moodle_release>4.2 (Build: 20230424)</moodle_release>\n");
        builder.Append("    <backup_version>2023042400</backup_version>\n    <backup_release>4.2</backup_release>\n");
        builder.Append("    <backup_date>1700000000</backup_date>\n    <include_files>1</include_files>\n");
        builder.Append("    <original_wwwroot>https://moodle.example.test</original_wwwroot>\n");
        builder.Append($"    <original_site_identifier_hash>{SiteIdentifier}</original_site_identifier_hash>\n");
        builder.Append($"    <original_course_id>{CourseId}</original_course_id>\n");
        builder.Append($"    <original_course_fullname>{X(FullName)}</original_course_fullname>\n");
        builder.Append($"    <original_course_shortname>{X(ShortName)}</original_course_shortname>\n");
        builder.Append("    <original_course_contextid>25</original_course_contextid>\n");
        builder.Append($"    <details><detail backup_id=\"synthetic\"><type>{BackupType}</type><format>moodle2</format><interactive>1</interactive><mode>10</mode><execution>1</execution><executiontime>0</executiontime></detail></details>\n");
        builder.Append("    <contents>\n      <activities>\n");

        foreach (var activity in _activities)
        {
            builder.Append($"        <activity><moduleid>{activity.Cmid}</moduleid><sectionid>{activity.SectionId}</sectionid><modulename>{activity.ModuleName}</modulename><title>{X(activity.Name)}</title><directory>activities/{activity.ModuleName}_{activity.Cmid}</directory></activity>\n");
        }

        builder.Append("      </activities>\n      <sections>\n");

        foreach (var section in _sections)
        {
            builder.Append($"        <section><sectionid>{section.Id}</sectionid><title>{section.Number}</title><directory>sections/section_{section.Id}</directory></section>\n");
        }

        builder.Append($"      </sections>\n      <course><courseid>{CourseId}</courseid><title>{X(ShortName)}</title><directory>course</directory></course>\n    </contents>\n");
        builder.Append("    <settings></settings>\n  </information>\n</moodle_backup>\n");

        return builder.ToString();
    }

    private string CourseXml()
        => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <course id="{CourseId}" contextid="25">
              <shortname>{X(ShortName)}</shortname>
              <fullname>{X(FullName)}</fullname>
              <idnumber>{X(IdNumber ?? string.Empty)}</idnumber>
              <summary>{X(Summary)}</summary>
              <summaryformat>1</summaryformat>
              <format>topics</format>
              <startdate>{Unix(StartUtc)}</startdate>
              <enddate>{Unix(EndUtc)}</enddate>
              <visible>{(Visible ? 1 : 0)}</visible>
              <enablecompletion>{(EnableCompletion ? 1 : 0)}</enablecompletion>
              <category id="{CategoryId}">
                <name>{X(CategoryName)}</name>
                <description>{Null}</description>
              </category>
              <tags></tags>
            </course>
            """;

    private string EnrolmentsXml()
    {
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<enrolments>\n  <enrols>\n");
        var id = 10;

        foreach (var enrol in _enrols)
        {
            builder.Append($"    <enrol id=\"{id++}\"><enrol>{enrol.Method}</enrol><status>{(enrol.Enabled ? 0 : 1)}</status><name>{Null}</name>");
            builder.Append($"<enrolperiod>{enrol.EnrolPeriodDays * 86400}</enrolperiod><enrolstartdate>0</enrolstartdate><enrolenddate>0</enrolenddate>");
            builder.Append($"<password>{(enrol.Password is null ? Null : X(enrol.Password))}</password><roleid>5</roleid><customint6>1</customint6><user_enrolments></user_enrolments></enrol>\n");
        }

        builder.Append("  </enrols>\n</enrolments>\n");

        return builder.ToString();
    }

    private string CompletionXml()
    {
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<course_completion>\n");
        var id = 1;

        foreach (var criterion in _criteria)
        {
            builder.Append($"  <course_completion_criteria id=\"{id++}\"><course>{CourseId}</course><criteriatype>{criterion.Type}</criteriatype>");
            builder.Append($"<module>{criterion.Module ?? Null}</module><moduleinstance>{criterion.Cmid?.ToString(CultureInfo.InvariantCulture) ?? Null}</moduleinstance>");
            builder.Append($"<courseinstanceshortname>{Null}</courseinstanceshortname><enrolperiod>0</enrolperiod><timeend>0</timeend>");
            builder.Append($"<gradepass>{criterion.GradePass?.ToString("0.00000", CultureInfo.InvariantCulture) ?? Null}</gradepass><role>{criterion.Role?.ToString(CultureInfo.InvariantCulture) ?? Null}</role><roleshortname>{(criterion.Role is null ? Null : X(criterion.RoleShortName))}</roleshortname>");
            builder.Append("<course_completion_crit_completions></course_completion_crit_completions></course_completion_criteria>\n");
        }

        builder.Append($"  <course_completion_aggr_methd id=\"1\"><course>{CourseId}</course><criteriatype>{Null}</criteriatype><method>1</method><value>{Null}</value></course_completion_aggr_methd>\n");
        builder.Append("</course_completion>\n");

        return builder.ToString();
    }

    private string GradebookXml()
        => $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <gradebook>
              <grade_categories></grade_categories>
              <grade_items>
                <grade_item id="1">
                  <categoryid>{Null}</categoryid>
                  <itemname>{Null}</itemname>
                  <itemtype>course</itemtype>
                  <itemmodule>{Null}</itemmodule>
                  <iteminstance>1</iteminstance>
                  <grademax>{CourseGradeMax}.00000</grademax>
                  <grademin>0.00000</grademin>
                  <gradepass>0.00000</gradepass>
                </grade_item>
              </grade_items>
            </gradebook>
            """;

    private string SectionXml(SectionEntry section)
    {
        var sequence = string.Join(",", _activities.Where(activity => activity.SectionId == section.Id).Select(activity => activity.Cmid));

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <section id="{section.Id}">
              <number>{section.Number}</number>
              <name>{(section.Name is null ? Null : X(section.Name))}</name>
              <summary>{X(section.Summary ?? string.Empty)}</summary>
              <summaryformat>1</summaryformat>
              <sequence>{sequence}</sequence>
              <visible>{(section.Visible ? 1 : 0)}</visible>
              <availabilityjson>{Null}</availabilityjson>
            </section>
            """;
    }

    private string ModuleXml(Activity activity)
    {
        var section = _sections.First(candidate => candidate.Id == activity.SectionId);

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <module id="{activity.Cmid}" version="2023042400">
              <modulename>{activity.ModuleName}</modulename>
              <sectionid>{activity.SectionId}</sectionid>
              <sectionnumber>{section.Number}</sectionnumber>
              <idnumber></idnumber>
              <visible>{(activity.Visible ? 1 : 0)}</visible>
              <visibleoncoursepage>1</visibleoncoursepage>
              <completion>{activity.Completion}</completion>
              <completiongradeitemnumber>{(activity.CompletionUseGrade ? "0" : Null)}</completiongradeitemnumber>
              <completionpassgrade>{(activity.CompletionPassGrade ? 1 : 0)}</completionpassgrade>
              <completionview>{(activity.CompletionView ? 1 : 0)}</completionview>
              <completionexpected>0</completionexpected>
              <availability>{(activity.Availability is null ? Null : X(activity.Availability))}</availability>
            </module>
            """;
    }

    private static string ActivityXml(Activity activity)
    {
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        builder.Append($"<activity id=\"{activity.Cmid + 1000}\" moduleid=\"{activity.Cmid}\" modulename=\"{activity.ModuleName}\" contextid=\"{activity.ContextId}\">\n");
        builder.Append($"  <{activity.ModuleName} id=\"{activity.Cmid + 1000}\">\n    <name>{X(activity.Name)}</name>\n");

        foreach (var (key, value) in activity.Fields)
        {
            builder.Append($"    <{key}>{X(value)}</{key}>\n");
        }

        foreach (var container in activity.Records.GroupBy(record => record.Container))
        {
            builder.Append($"    <{container.Key}>\n");
            var recordId = 1;

            foreach (var (_, element, values) in container)
            {
                builder.Append($"      <{element} id=\"{recordId++}\">");

                foreach (var (key, value) in values)
                {
                    builder.Append($"<{key}>{X(value)}</{key}>");
                }

                builder.Append($"</{element}>\n");
            }

            builder.Append($"    </{container.Key}>\n");
        }

        if (activity.ModuleName == "quiz")
        {
            builder.Append("    <question_instances>\n");
            var slot = 1;

            foreach (var instance in activity.Slots)
            {
                builder.Append($"      <question_instance id=\"{slot}\"><quizid>{activity.Cmid + 1000}</quizid><slot>{slot}</slot><page>{instance.Page}</page><requireprevious>0</requireprevious><maxmark>{instance.MaxMark.ToString("0.0000000", CultureInfo.InvariantCulture)}</maxmark>");

                if (instance.RandomCategoryId is { } category)
                {
                    builder.Append($"<question_set_reference id=\"{slot}\"><usingcontextid>{activity.ContextId}</usingcontextid><component>mod_quiz</component><questionarea>slot</questionarea><questionscontextid>25</questionscontextid>");
                    builder.Append($"<filtercondition>{X("{\"questioncategoryid\":\"" + category + "\",\"includingsubcategories\":\"0\"}")}</filtercondition></question_set_reference>");
                }
                else
                {
                    builder.Append($"<question_reference id=\"{slot}\"><usingcontextid>{activity.ContextId}</usingcontextid><component>mod_quiz</component><questionarea>slot</questionarea><questionbankentryid>{instance.BankEntryId}</questionbankentryid><version>{Null}</version></question_reference>");
                }

                builder.Append("</question_instance>\n");
                slot++;
            }

            builder.Append("    </question_instances>\n    <sections><section id=\"1\"><firstslot>1</firstslot><heading></heading><shufflequestions>0</shufflequestions></section></sections>\n");
        }

        builder.Append($"  </{activity.ModuleName}>\n</activity>\n");

        return builder.ToString();
    }

    private static string InforefXml(Activity activity)
    {
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<inforef>\n  <fileref>\n");

        foreach (var id in activity.FileIds)
        {
            builder.Append($"    <file><id>{id}</id></file>\n");
        }

        builder.Append("  </fileref>\n</inforef>\n");

        return builder.ToString();
    }

    private static string GradesXml(Activity activity)
    {
        if (activity.GradeMax is null)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<activity_gradebook><grade_items></grade_items></activity_gradebook>";
        }

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <activity_gradebook>
              <grade_items>
                <grade_item id="{activity.Cmid + 2000}">
                  <itemtype>mod</itemtype>
                  <itemmodule>{activity.ModuleName}</itemmodule>
                  <iteminstance>{activity.Cmid + 1000}</iteminstance>
                  <itemnumber>0</itemnumber>
                  <grademax>{activity.GradeMax.Value.ToString("0.00000", CultureInfo.InvariantCulture)}</grademax>
                  <grademin>0.00000</grademin>
                  <gradepass>{(activity.GradePass ?? 0).ToString("0.00000", CultureInfo.InvariantCulture)}</gradepass>
                </grade_item>
              </grade_items>
            </activity_gradebook>
            """;
    }

    private string QuestionsXml()
    {
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<question_categories>\n");

        foreach (var category in _categories)
        {
            builder.Append($"  <question_category id=\"{category.Id}\">\n    <name>{X(category.Name)}</name>\n    <contextid>25</contextid>\n    <contextlevel>50</contextlevel>\n    <contextinstanceid>{CourseId}</contextinstanceid>\n    <info></info>\n    <parent>0</parent>\n    <question_bank_entries>\n");

            foreach (var question in category.Questions)
            {
                builder.Append($"      <question_bank_entry id=\"{question.BankEntryId}\">\n        <questioncategoryid>{category.Id}</questioncategoryid>\n        <question_version>\n");

                foreach (var version in question.OlderVersions.Append(question))
                {
                    builder.Append($"          <question_versions id=\"{version.Id + 50000}\"><version>{version.Version}</version><status>ready</status>\n            <questions>\n");
                    builder.Append(version.Xml());
                    builder.Append("            </questions>\n          </question_versions>\n");
                }

                builder.Append("        </question_version>\n      </question_bank_entry>\n");
            }

            builder.Append("    </question_bank_entries>\n  </question_category>\n");
        }

        builder.Append("</question_categories>\n");

        return builder.ToString();
    }

    private string FilesXml()
    {
        var builder = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<files>\n");

        foreach (var file in _files)
        {
            builder.Append($"  <file id=\"{file.Id}\"><contenthash>{file.Hash}</contenthash><contextid>{file.ContextId}</contextid><component>{file.Component}</component><filearea>{file.FileArea}</filearea><itemid>{file.ItemId}</itemid><filepath>{X(file.FilePath)}</filepath><filename>{X(file.FileName)}</filename><userid>2</userid><filesize>{file.Content.Length}</filesize><mimetype>{file.MimeType}</mimetype><status>0</status><source>{X(file.FileName)}</source><sortorder>0</sortorder></file>\n");
        }

        builder.Append("</files>\n");

        return builder.ToString();
    }

    private static string Unix(DateTime? value)
        => value is { } time ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) : "0";

    private static string X(string value)
        => SecurityElement.Escape(value ?? string.Empty);

    private static byte[] Utf8(string value)
        => Encoding.UTF8.GetBytes(value);

    private static string StripTags(string html)
        => System.Text.RegularExpressions.Regex.Replace(html ?? string.Empty, "<[^>]+>", string.Empty).Trim();

    private sealed record SectionEntry(int Id, int Number, string Name, string Summary, bool Visible);

    private sealed record Enrol(string Method, bool Enabled, string Password, int EnrolPeriodDays);

    private sealed record Criterion(int Type, int? Cmid, string Module, decimal? GradePass, int? Role, string RoleShortName);

    private sealed record QuestionCategory(int Id, string Name, IReadOnlyList<Question> Questions);

    private sealed record StoredFile(int Id, string Hash, int ContextId, string Component, string FileArea, string FileName, byte[] Content, string MimeType, int ItemId, string FilePath);

    private sealed class Activity
    {
        public Activity(int cmid, string moduleName, int sectionId, string name, bool visible = true)
        {
            Cmid = cmid;
            ModuleName = moduleName;
            SectionId = sectionId;
            Name = name;
            Visible = visible;
        }

        public int Cmid { get; }

        public string ModuleName { get; }

        public int SectionId { get; }

        public string Name { get; }

        public bool Visible { get; }

        public int ContextId { get; set; }

        public int Completion { get; set; }

        public bool CompletionView { get; set; }

        public bool CompletionUseGrade { get; set; }

        public bool CompletionPassGrade { get; set; }

        public decimal? GradeMax { get; set; }

        public decimal? GradePass { get; set; }

        public Dictionary<string, string> Fields { get; } = new(StringComparer.Ordinal);

        public List<int> FileIds { get; } = [];

        public List<QuizSlot> Slots { get; } = [];

        public string Availability { get; set; }

        public List<(string Container, string Element, Dictionary<string, string> Values)> Records { get; } = [];
    }
}

/// <summary>
/// A slot of a synthetic quiz: a question of the bank, or a random question from a category.
/// </summary>
public sealed record QuizSlot(int BankEntryId, int Page = 1, decimal MaxMark = 1, int? RandomCategoryId = null);

/// <summary>
/// A question of a synthetic Moodle question bank, in the shape <c>questions.xml</c> keeps it.
/// </summary>
public sealed class Question
{
    private static readonly string Null = "$@NULL@$";

    public Question(int id, string type, string name, string text)
    {
        Id = id;
        BankEntryId = id + 10000;
        Type = type;
        Name = name;
        Text = text;
    }

    public int Id { get; }

    public int BankEntryId { get; init; }

    public int Version { get; init; } = 1;

    public string Type { get; }

    public string Name { get; }

    public string Text { get; }

    public string GeneralFeedback { get; init; } = string.Empty;

    public decimal DefaultMark { get; init; } = 1;

    public IList<(string Text, decimal Fraction, string Feedback)> Answers { get; init; } = [];

    public bool Single { get; init; } = true;

    public bool UseCase { get; init; }

    public decimal Tolerance { get; init; }

    public IList<(string Question, string Answer)> Matches { get; init; } = [];

    public string GraderInfo { get; init; } = string.Empty;

    public IList<Question> OlderVersions { get; init; } = [];

    public static Question MultipleChoice(int id, string text, bool single, params (string Text, decimal Fraction)[] answers)
        => new(id, "multichoice", "Choice " + id, text) { Single = single, Answers = answers.Select(answer => (answer.Text, answer.Fraction, string.Empty)).ToList() };

    public static Question TrueFalse(int id, string text, bool answer)
        => new(id, "truefalse", "True or false " + id, text) { Answers = [("True", answer ? 1 : 0, string.Empty), ("False", answer ? 0 : 1, string.Empty)] };

    internal string Xml()
    {
        var builder = new StringBuilder();
        builder.Append($"              <question id=\"{Id}\"><parent>0</parent><name>{E(Name)}</name><questiontext>{E(Text)}</questiontext><questiontextformat>1</questiontextformat>");
        builder.Append($"<generalfeedback>{E(GeneralFeedback)}</generalfeedback><generalfeedbackformat>1</generalfeedbackformat><defaultmark>{DefaultMark.ToString("0.0000000", CultureInfo.InvariantCulture)}</defaultmark><penalty>0.3333333</penalty><qtype>{Type}</qtype><length>1</length><hidden>0</hidden>\n");
        builder.Append($"                <plugin_qtype_{Type}_question>");

        if (Answers.Count > 0)
        {
            builder.Append("<answers>");
            var answerId = Id * 10;

            foreach (var (text, fraction, feedback) in Answers)
            {
                builder.Append($"<answer id=\"{answerId++}\"><answertext>{E(text)}</answertext><answerformat>{(Type == "multichoice" ? 1 : 0)}</answerformat><fraction>{fraction.ToString("0.0000000", CultureInfo.InvariantCulture)}</fraction><feedback>{E(feedback)}</feedback><feedbackformat>1</feedbackformat></answer>");
            }

            builder.Append("</answers>");
        }

        switch (Type)
        {
            case "multichoice":
                builder.Append($"<multichoice id=\"{Id}\"><layout>0</layout><single>{(Single ? 1 : 0)}</single><shuffleanswers>1</shuffleanswers><correctfeedback></correctfeedback><answernumbering>abc</answernumbering></multichoice>");
                break;
            case "truefalse":
                builder.Append($"<truefalse id=\"{Id}\"><trueanswer>{Id * 10}</trueanswer><falseanswer>{Id * 10 + 1}</falseanswer></truefalse>");
                break;
            case "shortanswer":
                builder.Append($"<shortanswer id=\"{Id}\"><usecase>{(UseCase ? 1 : 0)}</usecase></shortanswer>");
                break;
            case "numerical":
                builder.Append($"<numerical_records><numerical_record id=\"{Id}\"><answer>{Id * 10}</answer><tolerance>{Tolerance.ToString(CultureInfo.InvariantCulture)}</tolerance></numerical_record></numerical_records><numerical_options></numerical_options><numerical_units></numerical_units>");
                break;
            case "essay":
                builder.Append($"<essay id=\"{Id}\"><responseformat>editor</responseformat><responserequired>1</responserequired><responsefieldlines>15</responsefieldlines><attachments>0</attachments><graderinfo>{E(GraderInfo)}</graderinfo><graderinfoformat>1</graderinfoformat><responsetemplate></responsetemplate></essay>");
                break;
            case "match":
                builder.Append($"<matchoptions id=\"{Id}\"><shuffleanswers>1</shuffleanswers></matchoptions><matches>");
                var matchId = Id * 10;

                foreach (var (question, answer) in Matches)
                {
                    builder.Append($"<match id=\"{matchId++}\"><questiontext>{E(question)}</questiontext><questiontextformat>1</questiontextformat><answertext>{E(answer)}</answertext></match>");
                }

                builder.Append("</matches>");
                break;
            case "ddwtos" or "gapselect":
                builder.Append($"<{Type} id=\"{Id}\"><shuffleanswers>1</shuffleanswers></{Type}>");
                break;
        }

        builder.Append($"</plugin_qtype_{Type}_question>\n                <question_hints></question_hints><tags></tags>\n              </question>\n");

        return builder.ToString();

        static string E(string value)
            => SecurityElement.Escape(value ?? string.Empty) ?? Null;
    }
}

/// <summary>
/// Synthetic backups the Moodle tests share.
/// </summary>
public static class MoodleBackupSamples
{
    /// <summary>
    /// A course with every activity and setting the import maps since the merged gaps: a label with a picture, an
    /// assignment, a feedback, a choice, an IMS package, a quiz with pages, immediate feedback, review options and the
    /// new question types, a SCORM package in a new window with attempts and grading, a page open only to one person,
    /// a hidden page, completion by role, the course's picture, and a glossary it still does not import.
    /// </summary>
    public static MoodleBackupBuilder EveryActivity(int courseId, string fullName)
    {
        var builder = new MoodleBackupBuilder(courseId, fullName, "EA-" + courseId)
        {
            Summary = "<p>Course with a picture <img src=\"@@PLUGINFILE@@/summary.png\" alt=\"summary\"></p>",
        };

        return builder
            .Section(300, 0, null)
            .Section(301, 1, "Everything")
            .Enrolment("manual")
            .RoleCriterion(3, "editingteacher")
            .CourseImage("cover.png", MoodleBackupBuilder.Png())
            .SummaryImage("summary.png", MoodleBackupBuilder.Png())
            .Label(301, 401, "<p>Read this first <img src=\"@@PLUGINFILE@@/exit.png\" alt=\"exit\"></p>", pictureName: "exit.png", picture: MoodleBackupBuilder.Png())
            .Assign(301, 402, "Safety plan", grade: 20, dueUtc: new DateTime(2025, 3, 1, 17, 0, 0, DateTimeKind.Utc), maxFiles: 5)
            .Feedback(301, 403, "Course feedback",
                ("multichoice", "How did you hear of it?", "r>>>>>Friend|Web", true),
                ("textarea", "What would you change?", "30|5", false),
                ("captcha", "Captcha", string.Empty, true))
            .Choice(301, 404, "Meeting day", "<p>Which day suits you?</p>", "Monday", "Friday")
            .Imscp(301, 405, "Handbook", MoodleBackupBuilder.ImsPackage())
            .Quiz(301, 406, "Checks", [new QuizSlot(20501), new QuizSlot(20502), new QuizSlot(20503), new QuizSlot(20504)], gradePass: null, passGrade: false)
            .Setting(406, "questionsperpage", "2")
            .Setting(406, "preferredbehaviour", "immediatefeedback")
            .Setting(406, "reviewattempt", "69904")
            .Setting(406, "reviewmarks", "69904")
            .Setting(406, "reviewrightanswer", "16")
            .Scorm(301, 407, "Window package", "window.zip", MoodleBackupBuilder.ScormPackage("Window package"), scoreRequired: 80, popup: true, maxAttempt: 2)
            .Setting(407, "whatgrade", "1")
            .Page(301, 408, "For Ana only", "<p>Private notes.</p>")
            .Restrict(408, "{\"op\":\"&\",\"c\":[{\"type\":\"profile\",\"sf\":\"email\",\"op\":\"isequalto\",\"v\":\"admin@example.com\"}],\"showc\":[true]}")
            .Page(301, 409, "Hidden notes", "<p>Hidden.</p>", completion: 2)
            .Hide(409)
            .Other(301, 410, "glossary", "Terms")
            .Questions(
                50,
                "Default for EA",
                new Question(10501, "multichoice", "Survey 10501", "<p>How confident are you?</p>") { Single = true, Answers = [("Very", 0, string.Empty), ("Not much", 0, string.Empty)], BankEntryId = 20501 },
                new Question(10502, "ddwtos", "Drag 10502", "<p>A fire needs fuel, [[1]] and [[2]].</p>") { Answers = [("heat", 0, string.Empty), ("oxygen", 0, string.Empty), ("water", 0, string.Empty)], BankEntryId = 20502 },
                new Question(10503, "gapselect", "Select 10503", "<p>Exits are [[1]].</p>") { Answers = [("green", 0, string.Empty), ("red", 0, string.Empty)], BankEntryId = 20503 },
                new Question(10504, "description", "Description 10504", "<p>Read the next questions carefully.</p>") { BankEntryId = 20504, DefaultMark = 0 });
    }

    /// <summary>
    /// A course with a section of every activity the importer maps, and a forum it does not.
    /// </summary>
    public static MoodleBackupBuilder WorkplaceSafety(int courseId = 12, string fullName = "Workplace Safety (Moodle)")
    {
        var builder = new MoodleBackupBuilder(courseId, fullName, "WS-M")
        {
            CategoryName = "30 Day Learning Plan",
            EndUtc = new DateTime(2025, 1, 15, 8, 0, 0, DateTimeKind.Utc),
        };

        return builder
            .Section(100, 0, null)
            .Section(101, 1, "Basics", "<p>Start here.</p>")
            .Enrolment("manual")
            .Enrolment("self", password: "never-imported", enrolPeriodDays: 30)
            .ActivityCriterion(202, "scorm")
            .ActivityCriterion(205, "quiz")
            .GradeCriterion(70)
            .Label(101, 201, "<p>Welcome to the course.</p>")
            .Scorm(101, 202, "Fire safety package", "fire.zip", MoodleBackupBuilder.ScormPackage("Fire safety"), scoreRequired: 90)
            .Resource(101, 203, "Evacuation guide", "guide.pdf", System.Text.Encoding.UTF8.GetBytes("%PDF-1.4 synthetic"))
            .Url(101, 204, "Policy", "https://example.com/policy")
            .Quiz(101, 205, "Fire safety check", [new QuizSlot(10301), new QuizSlot(10302), new QuizSlot(0, RandomCategoryId: 40)], timeLimitSeconds: 1800, attempts: 2)
            .Page(101, 206, "Site rules", "<p>Rules of the site.</p>")
            .Other(101, 207, "forum", "Discussion")
            .Questions(
                40,
                "Default for WS-M",
                Question.MultipleChoice(301, "<p>Which extinguisher is for electrical fires?</p>", true, ("CO2", 1), ("Water", 0)),
                Question.TrueFalse(302, "<p>Lifts are safe in a fire.</p>", false));
    }
}
