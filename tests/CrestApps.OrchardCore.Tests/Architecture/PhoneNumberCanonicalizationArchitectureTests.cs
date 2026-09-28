using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Guards the rule that a phone number is canonicalized in exactly one place.
/// <para>
/// The dialer used to canonicalize a destination with its own helper that fell back to the digits of the
/// number when the number plan did not recognize it, and the local do-not-call registry canonicalized the
/// same number again and dropped it when that failed. A national-format destination therefore reached the
/// registry as digits, matched nothing, and the dialer placed the call — a number on a do-not-call registry
/// was dialed because two pieces of code disagreed about what the number was. That is the failure this guard
/// exists to keep out: within the calling and compliance path, canonicalization happens through the one
/// entry point that produces a <c>PhoneNumber</c>, and nothing invents a second answer beside it. The contact
/// import is guarded with it, because that is the other place a number is screened, and matching a number
/// that was never canonical belongs to <c>PhoneNumberComparisonKey</c>, which says in its name that it is
/// comparing and not identifying.
/// </para>
/// </summary>
public sealed class PhoneNumberCanonicalizationArchitectureTests
{
    private const string CanonicalParserRule = "Direct IPhoneNumberService.TryFormatToE164 call in the calling and compliance path";
    private const string HandRolledShapeRule = "Hand-rolled E.164 shape or digit-stripping normalization in the calling and compliance path";

    private static readonly string[] _guardedRoots =
    [
        "src/Abstractions/CrestApps.OrchardCore.DncRegistry.Abstractions",
        "src/Abstractions/CrestApps.OrchardCore.Telephony.Abstractions",
        "src/Core/CrestApps.OrchardCore.ContactCenter.Core",
        "src/Core/CrestApps.OrchardCore.Telephony.Core",
        "src/Modules/CrestApps.OrchardCore.ContactCenter",
        "src/Modules/CrestApps.OrchardCore.DncRegistry",
        "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements",
    ];

    private static readonly Regex _directParserRegex = new(
        @"\bTryFormatToE164\b",
        RegexOptions.Compiled);

    private static readonly Regex _digitStrippingRegex = new(
        @"\bWhere\s*\(\s*char\.IsDigit\b|\bIsDigit\s*\)\s*\.ToArray|\bTrimStart\s*\(\s*'\+'\s*\)|\bReplace\s*\(\s*""\+""\s*,\s*(?:""""|string\.Empty)\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex _shapeCheckRegex = new(
        @"StartsWith\s*\(\s*'\+'\s*\)|StartsWith\s*\(\s*""\+""",
        RegexOptions.Compiled);

    [Fact]
    public void TheCallingAndCompliancePath_CanonicalizesThroughASingleEntryPoint()
    {
        // Act
        var violations = Scan(CanonicalParserRule, _directParserRegex);

        // Assert
        // TryParse is the one entry point. It returns a PhoneNumber, so a caller that uses it cannot
        // accidentally carry a half-canonicalized string forward; a caller that reaches past it to the raw
        // service gets a string back and has to decide for itself what to do when parsing fails, which is
        // precisely how the five divergent fallbacks came to exist.
        Assert.Empty(violations);
    }

    [Fact]
    public void TheCallingAndCompliancePath_DoesNotReimplementNormalization()
    {
        // Act
        var violations = Scan(HandRolledShapeRule, _digitStrippingRegex)
            .Concat(Scan(HandRolledShapeRule, _shapeCheckRegex))
            .ToList();

        // Assert
        // Stripping a number down to its digits, or deciding for yourself what a leading plus sign means, is
        // a second definition of canonical form no matter how small it looks.
        Assert.Empty(violations);
    }

    [Fact]
    public void TheGuardedRoots_Exist()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();

        // Assert
        // A guard that silently scans nothing because a project moved is worse than no guard at all.
        foreach (var root in _guardedRoots)
        {
            Assert.True(
                Directory.Exists(Path.Combine(repositoryRoot, root.Replace('/', Path.DirectorySeparatorChar))),
                $"The guarded root '{root}' no longer exists. Update {nameof(PhoneNumberCanonicalizationArchitectureTests)} so the rule keeps covering the calling and compliance path.");
        }
    }

    [Fact]
    public void TheGuard_ScansTheFilesItClaimsTo()
    {
        // Act
        var files = EnumerateGuardedFiles();

        // Assert
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/DefaultDialerEligibilityService.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/LocalDncRegistry.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/InboundContactLookup.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("ExternalDestinationPolicy.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Handlers/OmnichannelContactImportRowFilter.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Services/OmnichannelContactDuplicateLookupService.cs", StringComparison.Ordinal));
        Assert.Contains(files, file => file.RelativePath.EndsWith("Handlers/OmnichannelContactPartContentImportHandler.cs", StringComparison.Ordinal));
    }

    private static List<string> Scan(string rule, Regex regex)
    {
        var violations = new List<string>();

        foreach (var file in EnumerateGuardedFiles())
        {
            var lines = File.ReadAllLines(file.FullPath);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                if (regex.IsMatch(line))
                {
                    violations.Add($"{rule}: {file.RelativePath}({i + 1}): {trimmed}");
                }
            }
        }

        return violations;
    }

    private static List<(string FullPath, string RelativePath)> EnumerateGuardedFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var files = new List<(string FullPath, string RelativePath)>();

        foreach (var root in _guardedRoots)
        {
            var directory = Path.Combine(repositoryRoot, root.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/');

                if (relativePath.Contains("/obj/", StringComparison.Ordinal) || relativePath.Contains("/bin/", StringComparison.Ordinal))
                {
                    continue;
                }

                files.Add((file, relativePath));
            }
        }

        return files;
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
