using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using CrestApps.OrchardCore.Learning.Core.Archives;

namespace CrestApps.OrchardCore.Learning.Core.Moodle;

/// <summary>
/// Reads a Moodle course backup (<c>.mbz</c>): a zip archive, or a gzip-compressed tar archive as newer Moodle versions
/// write, with <c>moodle_backup.xml</c>, <c>course/</c>, <c>sections/</c>, <c>activities/</c>, <c>questions.xml</c>,
/// <c>files.xml</c> and the files under <c>files/</c> named by their content hash. The archive is extracted by the safe
/// archive rules of learning packages (no absolute path, no <c>..</c>, no link, no duplicate, entry and size limits) and
/// every XML file is read with DTDs prohibited, no resolver and a size limit. Values Moodle writes as
/// <c>$@NULL@$</c> are read as missing. An enrolment password is never read, only whether there is one.
/// </summary>
public static class MoodleBackupReader
{
    /// <summary>
    /// How Moodle writes a missing value.
    /// </summary>
    public const string NullValue = "$@NULL@$";

    /// <summary>
    /// Extracts a backup into an empty folder and reads it. On a refusal the caller deletes the folder; nothing outside
    /// it was written.
    /// </summary>
    /// <param name="archive">The backup, readable and seekable.</param>
    /// <param name="folder">The empty folder to extract into.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The backup, or why it was refused.</returns>
    public static Task<MoodleBackupReadResult> ExtractAndReadAsync(Stream archive, string folder, MoodleBackupLimits limits, CancellationToken cancellationToken = default)
        => Task.FromResult(ExtractAndRead(archive, folder, limits, cancellationToken));

    /// <summary>
    /// Extracts a backup into an empty folder and reads it, synchronously, as a ContentTransfer file reader reads. On a
    /// refusal the caller deletes the folder; nothing outside it was written.
    /// </summary>
    /// <param name="archive">The backup, readable and seekable.</param>
    /// <param name="folder">The empty folder to extract into.</param>
    /// <param name="limits">The limits.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The backup, or why it was refused.</returns>
    public static MoodleBackupReadResult ExtractAndRead(Stream archive, string folder, MoodleBackupLimits limits, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(limits);

        var archiveLimits = new PackageArchiveLimits
        {
            MaxEntries = limits.MaxEntries,
            MaxExpandedBytes = limits.MaxExpandedBytes,
        };

        PackageArchiveResult extracted;

        switch (Detect(archive))
        {
            case ArchiveKind.Zip:
                extracted = PackageArchiveExtractor.Extract(archive, folder, archiveLimits, cancellationToken);
                break;

            case ArchiveKind.TarGz:
                extracted = PackageArchiveExtractor.ExtractTarGz(archive, folder, archiveLimits, cancellationToken);
                break;

            default:
                return Fail(MoodleBackupErrors.NotAnArchive);
        }

        if (!extracted.Succeeded)
        {
            return extracted.Error == PackageArchiveErrors.InvalidArchive
                ? Fail(MoodleBackupErrors.NotAnArchive)
                : Fail(MoodleBackupErrors.UnsafeArchive, string.IsNullOrEmpty(extracted.Entry) ? extracted.Error : extracted.Error + ": " + extracted.Entry);
        }

        return Read(folder, limits);
    }

    /// <summary>
    /// Reads a backup that was extracted into a folder.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <param name="limits">The limits.</param>
    /// <returns>The backup, or why it was refused.</returns>
    public static MoodleBackupReadResult Read(string folder, MoodleBackupLimits limits)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(limits);

        var reader = new Reader(Path.GetFullPath(folder), limits);

        return reader.Read();
    }

    /// <summary>
    /// Reads a Moodle value: <see langword="null"/> for a missing element or <see cref="NullValue"/>.
    /// </summary>
    /// <param name="element">The element whose child to read.</param>
    /// <param name="name">The child's name.</param>
    /// <returns>The value.</returns>
    public static string Value(XElement element, string name)
    {
        var value = element?.Element(name)?.Value;

        return value == NullValue ? null : value;
    }

    private static MoodleBackupReadResult Fail(string error, string detail = null)
        => new() { Error = error, ErrorDetail = detail };

    private static ArchiveKind Detect(Stream archive)
    {
        if (!archive.CanSeek)
        {
            return ArchiveKind.None;
        }

        var start = archive.Position;
        Span<byte> head = stackalloc byte[4];
        var read = 0;

        while (read < head.Length)
        {
            var count = archive.Read(head[read..]);

            if (count == 0)
            {
                break;
            }

            read += count;
        }

        archive.Position = start;

        if (read >= 2 && head[0] == 0x1F && head[1] == 0x8B)
        {
            return ArchiveKind.TarGz;
        }

        return read == 4 && head[0] == (byte)'P' && head[1] == (byte)'K' && head[2] is 3 or 5 && head[3] is 4 or 6
            ? ArchiveKind.Zip
            : ArchiveKind.None;
    }

    private enum ArchiveKind
    {
        None,
        Zip,
        TarGz,
    }

    private sealed class Reader
    {
        private readonly string _folder;
        private readonly MoodleBackupLimits _limits;

        public Reader(string folder, MoodleBackupLimits limits)
        {
            _folder = folder;
            _limits = limits;
        }

        public MoodleBackupReadResult Read()
        {
            if (!File.Exists(Path.Combine(_folder, "moodle_backup.xml")))
            {
                return Fail(MoodleBackupErrors.NoBackupFile);
            }

            if (Load("moodle_backup.xml") is not { } backupXml)
            {
                return Fail(MoodleBackupErrors.InvalidXml, "moodle_backup.xml");
            }

            var information = backupXml.Root?.Element("information");
            var type = information?.Element("details")?.Elements("detail").Select(detail => Value(detail, "type")).FirstOrDefault();

            if (information is null || !string.Equals(type, "course", StringComparison.Ordinal))
            {
                return Fail(MoodleBackupErrors.NotACourse, type);
            }

            var backup = new MoodleBackup
            {
                Folder = _folder,
                Name = Value(information, "name"),
                WwwRoot = Value(information, "original_wwwroot"),
                Release = Value(information, "moodle_release"),
            };

            backup.SiteKey = SiteKey(Value(information, "original_site_identifier_hash") ?? backup.WwwRoot ?? "moodle");

            var failure = ReadCourse(backup, information)
                ?? ReadFiles(backup)
                ?? ReadQuestions(backup)
                ?? ReadSections(backup, information)
                ?? ReadActivities(backup, information);

            return failure ?? new MoodleBackupReadResult { Backup = backup };
        }

        private MoodleBackupReadResult ReadCourse(MoodleBackup backup, XElement information)
        {
            var directory = information.Element("contents")?.Element("course")?.Element("directory")?.Value ?? "course";
            var coursePath = Combine(directory, "course.xml");

            if (coursePath is null || Load(coursePath) is not { Root: { } root })
            {
                return Fail(MoodleBackupErrors.InvalidXml, coursePath ?? directory);
            }

            var category = root.Element("category");
            var course = new MoodleCourse
            {
                Id = root.Attribute("id")?.Value ?? Value(information, "original_course_id"),
                ContextId = root.Attribute("contextid")?.Value ?? Value(information, "original_course_contextid"),
                FullName = Value(root, "fullname") ?? Value(information, "original_course_fullname"),
                ShortName = Value(root, "shortname") ?? Value(information, "original_course_shortname"),
                IdNumber = Value(root, "idnumber"),
                Summary = Value(root, "summary"),
                Format = Value(root, "format"),
                StartUtc = Time(Value(root, "startdate")),
                EndUtc = Time(Value(root, "enddate")),
                Visible = Value(root, "visible") != "0",
                EnableCompletion = Value(root, "enablecompletion") == "1",
                CategoryId = category?.Attribute("id")?.Value,
                CategoryName = Value(category, "name"),
            };

            backup.Course = course;

            if (Combine(directory, "enrolments.xml") is { } enrolmentsPath && File.Exists(Path.Combine(_folder, enrolmentsPath)))
            {
                if (Load(enrolmentsPath) is not { Root: { } enrolments })
                {
                    return Fail(MoodleBackupErrors.InvalidXml, enrolmentsPath);
                }

                foreach (var enrol in enrolments.Element("enrols")?.Elements("enrol") ?? [])
                {
                    course.Enrolments.Add(new MoodleEnrolment
                    {
                        Method = Value(enrol, "enrol"),
                        Enabled = Value(enrol, "status") == "0",
                        // Only whether there is a key; its value is never kept.
                        HasPassword = !string.IsNullOrEmpty(Value(enrol, "password")),
                        EnrolPeriodSeconds = Long(Value(enrol, "enrolperiod")),
                        AllowsNewEnrolments = Value(enrol, "enrol") != "self" || Value(enrol, "customint6") != "0",
                        Name = Value(enrol, "name"),
                        CohortId = Value(enrol, "enrol") == "cohort" ? Value(enrol, "customint1") : null,
                        CohortIdNumber = Value(enrol, "cohortidnumber"),
                        CohortName = Value(enrol, "cohortname"),
                    });
                }
            }

            if (Combine(directory, "completion.xml") is { } completionPath && File.Exists(Path.Combine(_folder, completionPath)))
            {
                if (Load(completionPath) is not { Root: { } completion })
                {
                    return Fail(MoodleBackupErrors.InvalidXml, completionPath);
                }

                foreach (var criterion in completion.Elements("course_completion_criteria"))
                {
                    course.CompletionCriteria.Add(new MoodleCompletionCriterion
                    {
                        Type = (int)Long(Value(criterion, "criteriatype")),
                        ModuleId = Value(criterion, "moduleinstance"),
                        GradePass = Decimal(Value(criterion, "gradepass")),
                        RoleShortName = Value(criterion, "roleshortname"),
                    });
                }

                course.CompletionAny = completion.Elements("course_completion_aggr_methd")
                    .Any(method => Value(method, "criteriatype") is null && Value(method, "method") == "2");
            }

            if (File.Exists(Path.Combine(_folder, "gradebook.xml")))
            {
                if (Load("gradebook.xml") is not { Root: { } gradebook })
                {
                    return Fail(MoodleBackupErrors.InvalidXml, "gradebook.xml");
                }

                course.GradeMax = gradebook.Element("grade_items")?.Elements("grade_item")
                    .Where(item => Value(item, "itemtype") == "course")
                    .Select(item => Decimal(Value(item, "grademax")))
                    .FirstOrDefault();
            }

            return null;
        }

        private MoodleBackupReadResult ReadFiles(MoodleBackup backup)
        {
            if (!File.Exists(Path.Combine(_folder, "files.xml")))
            {
                return null;
            }

            if (Load("files.xml") is not { Root: { } files })
            {
                return Fail(MoodleBackupErrors.InvalidXml, "files.xml");
            }

            foreach (var file in files.Elements("file"))
            {
                var id = file.Attribute("id")?.Value;
                var name = Value(file, "filename");

                // A folder of a file area is kept as a file named ".".
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || name == ".")
                {
                    continue;
                }

                backup.Files[id] = new MoodleFile
                {
                    Id = id,
                    ContentHash = Value(file, "contenthash"),
                    ContextId = Value(file, "contextid"),
                    Component = Value(file, "component"),
                    FileArea = Value(file, "filearea"),
                    FilePath = Value(file, "filepath"),
                    ItemId = Value(file, "itemid"),
                    FileName = name,
                    FileSize = Long(Value(file, "filesize")),
                    MimeType = Value(file, "mimetype"),
                    SortOrder = (int)Long(Value(file, "sortorder")),
                };
            }

            return null;
        }

        private MoodleBackupReadResult ReadQuestions(MoodleBackup backup)
        {
            if (!File.Exists(Path.Combine(_folder, "questions.xml")))
            {
                return null;
            }

            if (Load("questions.xml") is not { Root: { } categories })
            {
                return Fail(MoodleBackupErrors.InvalidXml, "questions.xml");
            }

            foreach (var element in categories.Elements("question_category"))
            {
                var category = new MoodleQuestionCategory
                {
                    Id = element.Attribute("id")?.Value,
                    Name = Value(element, "name"),
                    ParentId = Value(element, "parent"),
                };

                // Moodle 4 keeps the versions of each question bank entry; Moodle 3 keeps the questions themselves.
                if (element.Element("question_bank_entries") is { } entries)
                {
                    foreach (var entry in entries.Elements("question_bank_entry"))
                    {
                        var versions = entry.Descendants("question_versions").ToList();
                        var latest = versions
                            .Where(version => Value(version, "status") != "draft")
                            .OrderByDescending(version => Long(Value(version, "version")))
                            .FirstOrDefault()
                            ?? versions.OrderByDescending(version => Long(Value(version, "version"))).FirstOrDefault();

                        if (latest?.Element("questions")?.Element("question") is { } question)
                        {
                            category.Questions.Add(Question(question, entry.Attribute("id")?.Value, category.Id));
                        }
                    }
                }
                else
                {
                    foreach (var question in element.Element("questions")?.Elements("question") ?? [])
                    {
                        category.Questions.Add(Question(question, question.Attribute("id")?.Value, category.Id));
                    }
                }

                backup.QuestionCategories.Add(category);
            }

            return null;
        }

        private static MoodleQuestion Question(XElement element, string bankEntryId, string categoryId)
        {
            var type = Value(element, "qtype");
            var question = new MoodleQuestion
            {
                Id = element.Attribute("id")?.Value,
                BankEntryId = bankEntryId,
                CategoryId = categoryId,
                Name = Value(element, "name"),
                Type = type,
                Text = Value(element, "questiontext"),
                GeneralFeedback = Value(element, "generalfeedback"),
                DefaultMark = Decimal(Value(element, "defaultmark")) ?? 1,
            };

            var plugin = element.Element($"plugin_qtype_{type}_question");

            if (plugin is null)
            {
                return question;
            }

            foreach (var answer in plugin.Element("answers")?.Elements("answer") ?? [])
            {
                question.Answers.Add(new MoodleAnswer
                {
                    Id = answer.Attribute("id")?.Value,
                    Text = Value(answer, "answertext"),
                    Fraction = Decimal(Value(answer, "fraction")) ?? 0,
                    Feedback = Value(answer, "feedback"),
                });
            }

            foreach (var match in plugin.Element("matches")?.Elements("match") ?? [])
            {
                question.Matches.Add((Value(match, "questiontext"), Value(match, "answertext")));
            }

            foreach (var record in plugin.Element("numerical_records")?.Elements("numerical_record") ?? [])
            {
                if (Value(record, "answer") is { } answerId && Decimal(Value(record, "tolerance")) is { } tolerance)
                {
                    question.Tolerances[answerId] = tolerance;
                }
            }

            // The type's own options: <multichoice>, <truefalse>, <shortanswer>, <essay>, <matchoptions>, <ddwtos>...
            foreach (var options in plugin.Elements().Where(child => child.Name.LocalName is not ("answers" or "matches" or "numerical_records") && child.HasElements))
            {
                foreach (var option in options.Elements().Where(child => !child.HasElements))
                {
                    if (option.Value != NullValue)
                    {
                        question.Options[option.Name.LocalName] = option.Value;
                    }
                }
            }

            return question;
        }

        private MoodleBackupReadResult ReadSections(MoodleBackup backup, XElement information)
        {
            foreach (var entry in information.Element("contents")?.Element("sections")?.Elements("section") ?? [])
            {
                var id = Value(entry, "sectionid");
                var directory = Value(entry, "directory") ?? $"sections/section_{id}";
                var path = Combine(directory, "section.xml");

                if (path is null || Load(path) is not { Root: { } root })
                {
                    return Fail(MoodleBackupErrors.InvalidXml, path ?? directory);
                }

                backup.Sections.Add(new MoodleSection
                {
                    Id = root.Attribute("id")?.Value ?? id,
                    Number = (int)Long(Value(root, "number")),
                    Name = Value(root, "name"),
                    Summary = Value(root, "summary"),
                    Visible = Value(root, "visible") != "0",
                    Sequence = [.. (Value(root, "sequence") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
                });
            }

            backup.Sections = [.. backup.Sections.OrderBy(section => section.Number)];

            return null;
        }

        private MoodleBackupReadResult ReadActivities(MoodleBackup backup, XElement information)
        {
            var activities = new List<MoodleActivity>();

            foreach (var entry in information.Element("contents")?.Element("activities")?.Elements("activity") ?? [])
            {
                var moduleName = Value(entry, "modulename");
                var moduleId = Value(entry, "moduleid");

                // A module name names a file of the backup, so only letters, digits and underscores are taken.
                if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(moduleId) || !moduleName.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '_'))
                {
                    continue;
                }

                var directory = Value(entry, "directory") ?? $"activities/{moduleName}_{moduleId}";
                var activity = new MoodleActivity
                {
                    ModuleId = moduleId,
                    ModuleName = moduleName,
                    SectionId = Value(entry, "sectionid"),
                    Name = Value(entry, "title"),
                };

                var modulePath = Combine(directory, "module.xml");

                if (modulePath is not null && File.Exists(Path.Combine(_folder, modulePath)))
                {
                    if (Load(modulePath) is not { Root: { } module })
                    {
                        return Fail(MoodleBackupErrors.InvalidXml, modulePath);
                    }

                    activity.SectionId = Value(module, "sectionid") ?? activity.SectionId;
                    activity.Visible = Value(module, "visible") != "0";
                    activity.Completion = (int)Long(Value(module, "completion"));
                    activity.CompletionView = Value(module, "completionview") == "1";
                    activity.CompletionUseGrade = Value(module, "completiongradeitemnumber") is not null;
                    activity.CompletionPassGrade = Value(module, "completionpassgrade") == "1";
                    activity.Availability = Value(module, "availability");
                }

                var settingsPath = Combine(directory, moduleName + ".xml");

                if (settingsPath is not null && File.Exists(Path.Combine(_folder, settingsPath)))
                {
                    if (Load(settingsPath) is not { Root: { } root })
                    {
                        return Fail(MoodleBackupErrors.InvalidXml, settingsPath);
                    }

                    activity.ContextId = root.Attribute("contextid")?.Value;

                    if (root.Element(moduleName) is { } settings)
                    {
                        foreach (var setting in settings.Elements().Where(child => !child.HasElements))
                        {
                            if (setting.Value != NullValue)
                            {
                                activity.Settings[setting.Name.LocalName] = setting.Value;
                            }
                        }

                        activity.Name = Value(settings, "name") ?? activity.Name;

                        // Repeated records under a container: a feedback's <items><item>, a choice's <options><option>,
                        // an assignment's <plugin_configs><plugin_config>. Each record keeps its own simple values.
                        foreach (var container in settings.Elements().Where(child => child.HasElements && child.Name.LocalName != "question_instances"))
                        {
                            foreach (var record in container.Elements().Where(child => child.HasElements))
                            {
                                var values = new Dictionary<string, string>(StringComparer.Ordinal);

                                foreach (var value in record.Elements().Where(child => !child.HasElements && child.Value != NullValue))
                                {
                                    values[value.Name.LocalName] = value.Value;
                                }

                                if (!activity.Records.TryGetValue(record.Name.LocalName, out var list))
                                {
                                    list = [];
                                    activity.Records[record.Name.LocalName] = list;
                                }

                                list.Add(values);
                            }
                        }

                        if (moduleName == "quiz")
                        {
                            ReadSlots(activity, settings);
                        }
                    }
                }

                var inforefPath = Combine(directory, "inforef.xml");

                if (inforefPath is not null && File.Exists(Path.Combine(_folder, inforefPath)))
                {
                    if (Load(inforefPath) is not { Root: { } inforef })
                    {
                        return Fail(MoodleBackupErrors.InvalidXml, inforefPath);
                    }

                    foreach (var id in inforef.Element("fileref")?.Elements("file").Select(file => Value(file, "id")) ?? [])
                    {
                        if (id is not null && backup.Files.TryGetValue(id, out var file))
                        {
                            activity.Files.Add(file);
                        }
                    }
                }

                var gradesPath = Combine(directory, "grades.xml");

                if (gradesPath is not null && File.Exists(Path.Combine(_folder, gradesPath)))
                {
                    if (Load(gradesPath) is not { Root: { } grades })
                    {
                        return Fail(MoodleBackupErrors.InvalidXml, gradesPath);
                    }

                    var item = grades.Element("grade_items")?.Elements("grade_item")
                        .FirstOrDefault(candidate => Value(candidate, "itemtype") == "mod" && (Value(candidate, "itemnumber") ?? "0") == "0");

                    activity.GradeMax = Decimal(Value(item, "grademax"));
                    activity.GradePass = Decimal(Value(item, "gradepass"));
                }

                activities.Add(activity);
            }

            // In the order of the sections and of each section's sequence; any other activity after them.
            var ordered = new List<MoodleActivity>();

            foreach (var section in backup.Sections)
            {
                foreach (var moduleId in section.Sequence)
                {
                    if (activities.FirstOrDefault(activity => activity.ModuleId == moduleId && !ordered.Contains(activity)) is { } activity)
                    {
                        activity.SectionId ??= section.Id;
                        ordered.Add(activity);
                    }
                }
            }

            ordered.AddRange(activities.Where(activity => !ordered.Contains(activity)));
            backup.Activities = ordered;

            return null;
        }

        private static void ReadSlots(MoodleActivity activity, XElement quiz)
        {
            foreach (var instance in quiz.Element("question_instances")?.Elements("question_instance") ?? [])
            {
                var slot = new MoodleQuizSlot
                {
                    Slot = (int)Long(Value(instance, "slot")),
                    Page = (int)Long(Value(instance, "page")),
                    MaxMark = Decimal(Value(instance, "maxmark")) ?? 1,
                };

                if (instance.Element("question_reference") is { } reference)
                {
                    slot.BankEntryId = Value(reference, "questionbankentryid");
                }
                else if (instance.Element("question_set_reference") is { } set)
                {
                    (slot.RandomCategoryId, slot.RandomIncludesSubcategories) = RandomFilter(Value(set, "filtercondition"));
                }
                else
                {
                    // Moodle 3 refers to the question itself.
                    slot.BankEntryId = Value(instance, "questionid");
                }

                activity.Slots.Add(slot);
            }

            activity.Slots = [.. activity.Slots.OrderBy(slot => slot.Slot)];
        }

        // Moodle 4.0-4.2 write {"questioncategoryid":"20","includingsubcategories":"0"}; Moodle 4.3 and later write
        // {"filter":{"category":{"values":[20],"filteroptions":{"includesubcategories":false}}}}.
        private static (string CategoryId, bool Subcategories) RandomFilter(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return (null, false);
            }

            try
            {
                using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return (null, false);
                }

                if (root.TryGetProperty("questioncategoryid", out var category))
                {
                    var subcategories = root.TryGetProperty("includingsubcategories", out var include) && Truthy(include);

                    return (Text(category), subcategories);
                }

                if (root.TryGetProperty("filter", out var filter) &&
                    filter.ValueKind == JsonValueKind.Object &&
                    filter.TryGetProperty("category", out var categoryFilter) &&
                    categoryFilter.ValueKind == JsonValueKind.Object &&
                    categoryFilter.TryGetProperty("values", out var values) &&
                    values.ValueKind == JsonValueKind.Array &&
                    values.GetArrayLength() > 0)
                {
                    var subcategories = categoryFilter.TryGetProperty("filteroptions", out var options) &&
                        options.ValueKind == JsonValueKind.Object &&
                        options.TryGetProperty("includesubcategories", out var include) &&
                        Truthy(include);

                    return (Text(values[0]), subcategories);
                }
            }
            catch (JsonException)
            {
                // A filter Moodle did not write is read as no category: the random slot is then not imported.
            }

            return (null, false);

            static string Text(JsonElement value)
                => value.ValueKind == JsonValueKind.Number ? value.GetRawText() : value.ValueKind == JsonValueKind.String ? value.GetString() : null;

            static bool Truthy(JsonElement value)
                => value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.String && value.GetString() is "1" or "true") || (value.ValueKind == JsonValueKind.Number && value.GetRawText() != "0");
        }

        // A path inside the backup from a directory the backup names: refused when it leaves the backup.
        private string Combine(string directory, string file)
        {
            if (PackagePath.Normalize(directory + "/" + file) is not { } path)
            {
                return null;
            }

            var full = Path.GetFullPath(Path.Combine(_folder, path.Replace('/', Path.DirectorySeparatorChar)));
            var root = _folder.EndsWith(Path.DirectorySeparatorChar) ? _folder : _folder + Path.DirectorySeparatorChar;

            return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? path : null;
        }

        private XDocument Load(string path)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersInDocument = _limits.MaxXmlCharacters,
                MaxCharactersFromEntities = 1024,
            };

            try
            {
                using var stream = new FileStream(Path.Combine(_folder, path.Replace('/', Path.DirectorySeparatorChar)), FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = XmlReader.Create(stream, settings);

                return XDocument.Load(reader);
            }
            catch (Exception exception) when (exception is XmlException or IOException)
            {
                return null;
            }
        }
    }

    private static string SiteKey(string identifier)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identifier)))[..12];

    private static DateTime? Time(string value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;

    private static long Long(string value)
        => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

    private static decimal? Decimal(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
}
