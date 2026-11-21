using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using CrestApps.OrchardCore.Archives;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Core.Cmi5;

/// <summary>
/// Why a cmi5 course structure was refused.
/// </summary>
public static class Cmi5Errors
{
    /// <summary>The file is not valid XML, or has a DTD.</summary>
    public const string InvalidXml = "Cmi5InvalidXml";

    /// <summary>The file has no course with an id and a title.</summary>
    public const string NoCourse = "Cmi5NoCourse";

    /// <summary>The course has no AU.</summary>
    public const string NoAu = "Cmi5NoAu";

    /// <summary>An AU or a block has no id that is an IRI, or two have the same id.</summary>
    public const string InvalidId = "Cmi5InvalidId";

    /// <summary>An AU has no URL, or a relative URL with a path that is not allowed.</summary>
    public const string InvalidUrl = "Cmi5InvalidUrl";

    /// <summary>An AU has an absolute URL that is not https.</summary>
    public const string NotHttps = "Cmi5NotHttps";

    /// <summary>An AU has a moveOn that cmi5 does not define.</summary>
    public const string InvalidMoveOn = "Cmi5InvalidMoveOn";

    /// <summary>An AU has a masteryScore that is not a number from 0 to 1.</summary>
    public const string InvalidMasteryScore = "Cmi5InvalidMasteryScore";

    /// <summary>An AU has a launchMethod that cmi5 does not define.</summary>
    public const string InvalidLaunchMethod = "Cmi5InvalidLaunchMethod";

    /// <summary>A relative AU URL names a file that is not in the package.</summary>
    public const string LaunchFileMissing = "Cmi5LaunchFileMissing";
}

/// <summary>
/// The course structure read from a <c>cmi5.xml</c>, or why it was refused.
/// </summary>
public sealed class Cmi5ParseResult
{
    /// <summary>Gets the course.</summary>
    public Cmi5Course Course { get; init; }

    /// <summary>Gets the error, one of <see cref="Cmi5Errors"/>.</summary>
    public string Error { get; init; }

    /// <summary>Gets whether the course structure was read.</summary>
    public bool Succeeded => Error is null;
}

/// <summary>
/// Reads a cmi5 course structure (<c>cmi5.xml</c>, cmi5 Quartz section 13): the course with its id and title, the blocks
/// and the AUs in order, each AU with its id, title, URL (relative to the package, or absolute https), <c>moveOn</c>
/// (default NotApplicable), <c>masteryScore</c> (0 to 1), <c>launchMethod</c> (default AnyWindow), launch parameters,
/// entitlement key and activity type. Language strings take English first, else the first one. DTDs are refused.
/// </summary>
public static class Cmi5CourseParser
{
    private static readonly string[] _moveOn = ["Passed", "Completed", "CompletedAndPassed", "CompletedOrPassed", "NotApplicable"];
    private static readonly string[] _launchMethods = ["AnyWindow", "OwnWindow"];

    /// <summary>
    /// Reads a course structure.
    /// </summary>
    /// <param name="content">The XML.</param>
    /// <returns>The course, or why it was refused.</returns>
    public static Cmi5ParseResult Parse(Stream content)
    {
        ArgumentNullException.ThrowIfNull(content);

        XDocument document;

        try
        {
            using var reader = XmlReader.Create(content, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 10_000_000,
            });

            document = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return Fail(Cmi5Errors.InvalidXml);
        }

        var root = document.Root;

        if (root is null || root.Name.LocalName != "courseStructure")
        {
            return Fail(Cmi5Errors.InvalidXml);
        }

        var course = Child(root, "course");
        var courseId = (string)course?.Attribute("id");
        var title = Text(Child(course, "title"));

        if (course is null || !IsIri(courseId) || string.IsNullOrWhiteSpace(title))
        {
            return Fail(Cmi5Errors.NoCourse);
        }

        var result = new Cmi5Course
        {
            Id = courseId,
            Title = title,
            Description = Text(Child(course, "description")),
        };

        var error = ReadChildren(root, result, parentId: null, blockIds: []);

        if (error is not null)
        {
            return Fail(error);
        }

        if (result.Aus.Count == 0)
        {
            return Fail(Cmi5Errors.NoAu);
        }

        var ids = result.Aus.Select(au => au.Id).Concat(result.Blocks.Select(block => block.Id)).ToList();

        return ids.Distinct(StringComparer.Ordinal).Count() != ids.Count
            ? Fail(Cmi5Errors.InvalidId)
            : new Cmi5ParseResult { Course = result };
    }

    private static string ReadChildren(XElement parent, Cmi5Course course, string parentId, IList<string> blockIds)
    {
        foreach (var element in parent.Elements())
        {
            switch (element.Name.LocalName)
            {
                case "au":
                    var (au, error) = ReadAu(element, blockIds);

                    if (error is not null)
                    {
                        return error;
                    }

                    course.Aus.Add(au);
                    break;
                case "block":
                    var id = (string)element.Attribute("id");

                    if (!IsIri(id))
                    {
                        return Cmi5Errors.InvalidId;
                    }

                    course.Blocks.Add(new Cmi5Block
                    {
                        Id = id,
                        Title = Text(Child(element, "title")),
                        ParentId = parentId,
                    });

                    var nested = ReadChildren(element, course, id, [.. blockIds, id]);

                    if (nested is not null)
                    {
                        return nested;
                    }

                    break;
            }
        }

        return null;
    }

    private static (Cmi5Au Au, string Error) ReadAu(XElement element, IList<string> blockIds)
    {
        var id = (string)element.Attribute("id");

        if (!IsIri(id))
        {
            return (null, Cmi5Errors.InvalidId);
        }

        var moveOn = (string)element.Attribute("moveOn") ?? "NotApplicable";

        if (Array.IndexOf(_moveOn, moveOn) < 0)
        {
            return (null, Cmi5Errors.InvalidMoveOn);
        }

        decimal? masteryScore = null;

        if ((string)element.Attribute("masteryScore") is { } masteryText)
        {
            if (!decimal.TryParse(masteryText, NumberStyles.Float, CultureInfo.InvariantCulture, out var mastery) || mastery < 0 || mastery > 1)
            {
                return (null, Cmi5Errors.InvalidMasteryScore);
            }

            masteryScore = mastery;
        }

        var launchMethod = (string)element.Attribute("launchMethod") ?? "AnyWindow";

        if (Array.IndexOf(_launchMethods, launchMethod) < 0)
        {
            return (null, Cmi5Errors.InvalidLaunchMethod);
        }

        var url = Child(element, "url")?.Value?.Trim();

        if (string.IsNullOrEmpty(url))
        {
            return (null, Cmi5Errors.InvalidUrl);
        }

        var au = new Cmi5Au
        {
            Id = id,
            Title = Text(Child(element, "title")) ?? id,
            Description = Text(Child(element, "description")),
            Url = url,
            MoveOn = moveOn,
            MasteryScore = masteryScore,
            LaunchMethod = launchMethod,
            LaunchParameters = Child(element, "launchParameters")?.Value,
            EntitlementKey = Child(element, "entitlementKey")?.Value,
            ActivityType = (string)element.Attribute("activityType"),
            BlockIds = [.. blockIds],
        };

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !string.IsNullOrEmpty(absolute.Scheme) && url.Contains(':', StringComparison.Ordinal))
        {
            if (!string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return (null, Cmi5Errors.NotHttps);
            }

            au.IsAbsolute = true;

            return (au, null);
        }

        var cut = url.IndexOfAny(['?', '#']);
        var path = PackagePath.Normalize(cut < 0 ? url : url[..cut]);

        if (path is null)
        {
            return (null, Cmi5Errors.InvalidUrl);
        }

        au.Path = path;

        if (cut >= 0 && url[cut] == '?')
        {
            var query = url[(cut + 1)..];
            var fragment = query.IndexOf('#');

            au.Query = fragment < 0 ? query : query[..fragment];
        }

        return (au, null);
    }

    private static XElement Child(XElement parent, string name)
        => parent?.Elements().FirstOrDefault(element => element.Name.LocalName == name);

    private static string Text(XElement element)
    {
        if (element is null)
        {
            return null;
        }

        var strings = element.Elements().Where(child => child.Name.LocalName == "langstring").ToList();

        if (strings.Count == 0)
        {
            return string.IsNullOrWhiteSpace(element.Value) ? null : element.Value.Trim();
        }

        var english = strings.FirstOrDefault(child => ((string)child.Attribute("lang"))?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true);

        return (english ?? strings[0]).Value.Trim();
    }

    private static bool IsIri(string value)
        => !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsWhiteSpace) && Uri.TryCreate(value, UriKind.Absolute, out _);

    private static Cmi5ParseResult Fail(string error)
        => new() { Error = error };
}
