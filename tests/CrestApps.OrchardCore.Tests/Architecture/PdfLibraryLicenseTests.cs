using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using CrestApps.OrchardCore.Reports.Pdf.Services;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// The PDF libraries this repository and its packages use must stay MIT, Apache-2.0 or BSD licensed, so that nobody who
/// installs them is pulled into AGPL or commercial terms. PDFsharp and MigraDoc are AGPL-3.0-only from 6.5.0 and check
/// for a license at run time, iText is AGPL, and QuestPDF and the others are commercial or exclude some users.
/// <para>
/// This fails when one of them is referenced by a project, given a version in <c>Directory.Packages.props</c>, or
/// reaches the build through another package. The one exception is PDFsharp-MigraDoc 6.2.4, the last MIT release,
/// which CrestApps.Core.AI.Documents.Pdf still depends on; it is pinned to that version until CrestApps.Core drops it.
/// </para>
/// </summary>
public sealed class PdfLibraryLicenseTests
{
    // Package ids, compared by prefix and case-insensitively, of PDF libraries whose license is not permissive:
    // "PDFsharp" also matches its forks, such as PdfSharpCore, and "itext" matches iTextSharp and iText 7.
    private static readonly string[] _deniedPackages =
    [
        "PDFsharp",
        "MigraDoc",
        "itext",
        "QuestPDF",
        "Aspose.PDF",
        "Syncfusion.Pdf",
        "Syncfusion.HtmlToPdf",
        "IronPdf",
        "Spire.PDF",
        "FreeSpire.PDF",
        "GemBox.Pdf",
        "SelectPdf",
        "EO.Pdf",
    ];

    // The packages CrestApps.Core.AI.Documents.Pdf still brings in, at the last version released under MIT.
    private static readonly Dictionary<string, string> _pinnedUntilCoreDropsThem = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PDFsharp-MigraDoc"] = "6.2.4",
        ["PDFsharp"] = "6.2.4",
    };

    [Fact]
    public void NoProject_ReferencesAPdfLibraryWithANonPermissiveLicense()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();

        // Act
        foreach (var directory in new[] { "src", "tests" })
        {
            foreach (var project in Directory.EnumerateFiles(Path.Combine(repositoryRoot, directory), "*.csproj", SearchOption.AllDirectories))
            {
                foreach (var reference in XDocument.Load(project).Descendants("PackageReference"))
                {
                    var id = (string)reference.Attribute("Include") ?? (string)reference.Attribute("Update");

                    if (IsDenied(id))
                    {
                        violations.Add($"{Path.GetRelativePath(repositoryRoot, project).Replace('\\', '/')} references {id}.");
                    }
                }
            }
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    [Fact]
    public void CentralPackageVersions_HoldNoNonPermissivePdfLibrary_AndPinPdfSharpToItsLastMitRelease()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var violations = new List<string>();

        // Act
        foreach (var package in XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props")).Descendants("PackageVersion"))
        {
            var id = (string)package.Attribute("Include");
            var version = (string)package.Attribute("Version");

            if (IsDenied(id) && !IsPinned(id, version))
            {
                violations.Add($"Directory.Packages.props gives {id} the version {version}.");
            }
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    [Fact]
    public void ResolvedPackages_HoldNoNonPermissivePdfLibrary()
    {
        // Arrange
        var dependencies = Path.Combine(AppContext.BaseDirectory, "CrestApps.OrchardCore.Tests.deps.json");
        var violations = new List<string>();

        using var document = JsonDocument.Parse(File.ReadAllText(dependencies));

        // Act
        // The test project references the modules, so its dependency graph holds every package they bring in.
        foreach (var library in document.RootElement.GetProperty("libraries").EnumerateObject())
        {
            if (library.Value.GetProperty("type").GetString() != "package")
            {
                continue;
            }

            var separator = library.Name.IndexOf('/', StringComparison.Ordinal);
            var id = library.Name[..separator];
            var version = library.Name[(separator + 1)..];

            if (IsDenied(id) && !IsPinned(id, version))
            {
                violations.Add($"{id} {version} reaches the build.");
            }
        }

        // Assert
        Assert.True(violations.Count == 0, BuildMessage(violations));
    }

    [Fact]
    public void ReportsPdf_ReferencesNoPdfSharpOrMigraDocAssembly()
    {
        // Act
        var references = typeof(PdfReportExportFormat).Assembly.GetReferencedAssemblies();

        // Assert
        Assert.DoesNotContain(references, reference =>
            reference.Name.StartsWith("PdfSharp", StringComparison.OrdinalIgnoreCase) ||
            reference.Name.StartsWith("MigraDoc", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDenied(string id)
    {
        return !string.IsNullOrEmpty(id) &&
            _deniedPackages.Any(denied => id.StartsWith(denied, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPinned(string id, string version)
    {
        return _pinnedUntilCoreDropsThem.TryGetValue(id, out var pinned) &&
            string.Equals(version?.Trim('[', ']', ' '), pinned, StringComparison.Ordinal);
    }

    private static string BuildMessage(List<string> violations)
    {
        var builder = new StringBuilder()
            .AppendLine("A PDF library whose license is not MIT, Apache-2.0 or BSD is used. Use PdfPig, or pin PDFsharp-MigraDoc to 6.2.4 while CrestApps.Core depends on it:");

        foreach (var violation in violations)
        {
            builder.Append("  - ").AppendLine(violation);
        }

        return builder.ToString();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
