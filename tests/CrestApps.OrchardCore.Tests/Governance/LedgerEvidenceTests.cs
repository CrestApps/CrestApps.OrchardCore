using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Governance;

/// <summary>
/// Binds the Contact Center governance ledgers to real, executing CI.
/// <para>
/// A release ledger is only worth the evidence behind it. These tests make an unproven claim fail the build:
/// a gate may not say it is enforced unless the CI job it names exists, and a plan item may not be marked
/// complete unless it names a CI job that exists and cites test classes that job actually runs.
/// </para>
/// </summary>
public sealed partial class LedgerEvidenceTests
{
    private const string PlannedPrefix = "planned:";
    private const string AuthoritativeMarker = "<!-- ledger-authority: release-authoritative -->";
    private const string HistoricalMarker = "<!-- ledger-authority: historical -->";

    /// <summary>
    /// Pins the workflow parser against the jobs the repository is known to declare.
    /// <para>
    /// Without this pin every other test in this class could pass vacuously: a parser that silently returned
    /// no jobs would make "planned gates do not resolve" trivially true, and a parser that returned every YAML
    /// key would make "enforced gates resolve" trivially true.
    /// </para>
    /// </summary>
    /// <param name="workflow">The workflow file name.</param>
    /// <param name="expectedJobs">The comma-separated job identifiers the workflow declares.</param>
    [Theory]
    [InlineData("pr_ci.yml", "build_test")]
    [InlineData("main_ci.yml", "test")]
    [InlineData("preview_ci.yml", "test")]
    [InlineData("release_ci.yml", "test")]
    [InlineData("codeql.yml", "analyze-csharp,analyze-javascript")]
    [InlineData("deploy_docs.yml", "prepare,build,deploy")]
    [InlineData("contact_center_operations_gates.yml", "redis-backplane-two-node")]
    [InlineData("contact_center_feature_activation_matrix.yml", "fresh-tenant-activation")]
    [InlineData("validate_docs.yml", "validate-docs")]
    [InlineData("assets_validation.yml", "test-npm-build")]
    public void WorkflowJobParser_ResolvesExactlyTheJobsEachWorkflowDeclares(string workflow, string expectedJobs)
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();

        // Act
        var jobs = catalog.GetJobs(workflow).OrderBy(job => job, StringComparer.Ordinal);

        // Assert
        Assert.Equal(
            expectedJobs.Split(',').OrderBy(job => job, StringComparer.Ordinal),
            jobs);
    }

    /// <summary>
    /// Proves the parser reads the top-level <c>jobs:</c> mapping rather than every indented YAML key,
    /// so sibling keys such as <c>on:</c>, <c>env:</c>, and <c>permissions:</c> can never be mistaken for jobs.
    /// </summary>
    [Fact]
    public void WorkflowJobParser_NeverMistakesSiblingYamlKeysForJobs()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var nonJobKeys = new[] { "pull_request", "push", "contents", "workflow_dispatch", "schedule", "branches" };

        // Act & Assert
        foreach (var workflow in catalog.WorkflowPaths)
        {
            var jobs = catalog.GetJobs(workflow);

            Assert.True(jobs.Count > 0, $"Workflow '{workflow}' declares no jobs, which means the parser failed.");

            foreach (var nonJobKey in nonJobKeys)
            {
                Assert.False(
                    jobs.Contains(nonJobKey),
                    $"Workflow '{workflow}' reported '{nonJobKey}' as a job, so the parser is reading YAML keys outside the top-level 'jobs:' mapping.");
            }
        }
    }

    /// <summary>
    /// Proves the parser attributes workflow steps to the correct job by resolving the test projects each job runs.
    /// </summary>
    /// <param name="workflow">The workflow file name.</param>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="expectedProject">The test project directory name the job executes.</param>
    [Theory]
    [InlineData("pr_ci.yml", "build_test", "CrestApps.OrchardCore.Tests")]
    [InlineData("contact_center_operations_gates.yml", "redis-backplane-two-node", "CrestApps.OrchardCore.ContactCenter.DistributedTests")]
    [InlineData("contact_center_feature_activation_matrix.yml", "fresh-tenant-activation", "CrestApps.OrchardCore.ContactCenter.FeatureActivationTests")]
    public void WorkflowJobParser_AttributesTestProjectsToTheJobThatRunsThem(string workflow, string jobId, string expectedProject)
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();

        // Act
        var projects = catalog.GetExecutedTestProjects(workflow, jobId);

        // Assert
        Assert.Contains(expectedProject, projects);
    }

    /// <summary>
    /// Proves every gate the control matrix claims is enforced resolves to a CI job that really exists.
    /// </summary>
    [Fact]
    public void ControlMatrix_EveryEnforcedGateResolvesToARealWorkflowJob()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var gates = LoadGates(catalog.RepositoryRoot);
        var failures = new List<string>();

        // Act
        foreach (var gate in gates)
        {
            var status = Status(gate);

            if (status is not ("implemented" or "partial"))
            {
                continue;
            }

            foreach (var (workflow, jobId) in JobReferences(gate))
            {
                if (workflow.StartsWith(PlannedPrefix, StringComparison.Ordinal))
                {
                    failures.Add($"Gate '{Id(gate)}' is '{status}' but its workflow is still marked '{PlannedPrefix}'.");

                    continue;
                }

                if (!catalog.WorkflowExists(workflow))
                {
                    failures.Add($"Gate '{Id(gate)}' is '{status}' but workflow '{workflow}' does not exist.");

                    continue;
                }

                if (!catalog.JobExists(workflow, jobId))
                {
                    failures.Add($"Gate '{Id(gate)}' is '{status}' but job '{jobId}' does not exist in '{workflow}'. Declared jobs: {string.Join(", ", catalog.GetJobs(workflow))}.");
                }
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Proves every gate the control matrix admits is not yet enforced is honestly marked, and that the job it
    /// anticipates does not already exist. This is what makes the ledger self-correcting: once the anticipated job
    /// lands, this test fails until the gate's status is upgraded to reflect reality.
    /// </summary>
    [Fact]
    public void ControlMatrix_EveryPlannedGateIsMarkedPlannedAndDoesNotYetResolve()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var gates = LoadGates(catalog.RepositoryRoot);
        var failures = new List<string>();

        // Act
        foreach (var gate in gates)
        {
            if (Status(gate) != "planned")
            {
                continue;
            }

            var workflow = Workflow(gate);
            var jobId = JobId(gate);

            if (!workflow.StartsWith(PlannedPrefix, StringComparison.Ordinal))
            {
                failures.Add($"Gate '{Id(gate)}' is 'planned' so its workflow must carry the '{PlannedPrefix}' prefix, but found '{workflow}'.");

                continue;
            }

            var declaredWorkflow = workflow.Substring(PlannedPrefix.Length);

            if (catalog.JobExists(declaredWorkflow, jobId))
            {
                failures.Add($"Gate '{Id(gate)}' is still 'planned' but job '{jobId}' now exists in '{declaredWorkflow}'. Upgrade the gate status and remove the '{PlannedPrefix}' prefix.");
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Proves the evidence cited by every enforced, test-backed gate names real test classes that live in a
    /// project the gate's own CI job executes. A gate may not borrow credibility from tests that never run in it.
    /// </summary>
    [Fact]
    public void ControlMatrix_TestEvidenceCitesRealClassesRunByTheGatesOwnJobs()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var gates = LoadGates(catalog.RepositoryRoot);
        var projectsByTestClass = IndexTestClasses(catalog.RepositoryRoot);
        var failures = new List<string>();

        Assert.True(projectsByTestClass.Count > 100, "The test-class index is implausibly small, so the indexer failed.");

        // Act
        foreach (var gate in gates)
        {
            if (Status(gate) is not ("implemented" or "partial") || EvidenceKind(gate) != "tests")
            {
                continue;
            }

            var executedProjects = new HashSet<string>(StringComparer.Ordinal);

            foreach (var (workflow, jobId) in JobReferences(gate))
            {
                executedProjects.UnionWith(catalog.GetExecutedTestProjects(workflow, jobId));
            }

            var citedClasses = TestClassRegex()
                .Matches(EvidenceLocation(gate))
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal);
            var resolvedClasses = citedClasses
                .Where(projectsByTestClass.ContainsKey)
                .ToList();

            if (resolvedClasses.Count == 0)
            {
                failures.Add($"Gate '{Id(gate)}' is '{Status(gate)}' with test evidence, but its evidence cites no test class that exists in the repository.");

                continue;
            }

            foreach (var resolvedClass in resolvedClasses)
            {
                if (!projectsByTestClass[resolvedClass].Overlaps(executedProjects))
                {
                    failures.Add($"Gate '{Id(gate)}' cites '{resolvedClass}', which lives in [{string.Join(", ", projectsByTestClass[resolvedClass])}], but its CI job only runs [{string.Join(", ", executedProjects)}].");
                }
            }
        }

        // Assert
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Proves that a gate whose evidence is a workflow rather than a test suite cites workflows that exist and
    /// that include the job the gate names.
    /// </summary>
    [Fact]
    public void ControlMatrix_WorkflowEvidenceCitesRealWorkflowsIncludingTheGatesJob()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var gates = LoadGates(catalog.RepositoryRoot);
        var failures = new List<string>();
        var evaluated = 0;

        // Act
        foreach (var gate in gates)
        {
            if (Status(gate) is not ("implemented" or "partial") || EvidenceKind(gate) != "workflow")
            {
                continue;
            }

            evaluated++;

            var citedWorkflows = WorkflowReferenceRegex()
                .Matches(EvidenceLocation(gate))
                .Select(match => match.Value)
                .Distinct(StringComparer.Ordinal)
                .Where(cited => !cited.StartsWith(PlannedPrefix, StringComparison.Ordinal))
                .ToList();

            if (citedWorkflows.Count == 0)
            {
                failures.Add($"Gate '{Id(gate)}' declares workflow evidence but cites no workflow file.");

                continue;
            }

            foreach (var citedWorkflow in citedWorkflows)
            {
                if (!catalog.WorkflowExists(citedWorkflow))
                {
                    failures.Add($"Gate '{Id(gate)}' cites workflow '{citedWorkflow}', which does not exist.");
                }
            }

            if (!citedWorkflows.Any(cited => catalog.JobExists(cited, JobId(gate))))
            {
                failures.Add($"Gate '{Id(gate)}' names job '{JobId(gate)}' but none of its cited workflows declare it.");
            }
        }

        // Assert
        Assert.True(evaluated > 0, "No gate declares workflow evidence, so this rule would pass vacuously.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Proves exactly one Contact Center ledger claims release authority, and that every other ledger declares
    /// itself historical. Two competing "authoritative" plans is how a release ships against the wrong checklist.
    /// </summary>
    [Fact]
    public void ContactCenterLedgers_DeclareExactlyOneReleaseAuthoritativeDocument()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var ledgers = LedgerFiles(catalog.RepositoryRoot);
        var authoritative = new List<string>();
        var undeclared = new List<string>();

        // Act
        foreach (var ledger in ledgers)
        {
            var text = File.ReadAllText(ledger);
            var isAuthoritative = text.Contains(AuthoritativeMarker, StringComparison.Ordinal);
            var isHistorical = text.Contains(HistoricalMarker, StringComparison.Ordinal);

            if (isAuthoritative)
            {
                authoritative.Add(Path.GetFileName(ledger));
            }

            if (isAuthoritative == isHistorical)
            {
                undeclared.Add(Path.GetFileName(ledger));
            }
        }

        // Assert
        Assert.True(ledgers.Count > 1, "Fewer than two ledgers were discovered, so this rule would pass vacuously.");
        Assert.True(
            undeclared.Count == 0,
            $"Every Contact Center ledger must declare exactly one authority marker, but these declare none or both: {string.Join(", ", undeclared)}.");
        Assert.True(
            authoritative.Count == 1,
            $"Exactly one Contact Center ledger may be release-authoritative, but found {authoritative.Count}: {string.Join(", ", authoritative)}.");
    }

    /// <summary>
    /// Proves every item the release-authoritative ledger marks complete or partially complete names a CI job
    /// that really exists. This is the rule that stops a plan from being closed out by assertion.
    /// </summary>
    [Fact]
    public void AuthoritativeLedger_BacksEveryCompletedItemWithARealCiJob()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var ledger = LedgerFiles(catalog.RepositoryRoot)
            .Single(file => File.ReadAllText(file).Contains(AuthoritativeMarker, StringComparison.Ordinal));
        var failures = new List<string>();
        var completed = 0;

        // Act
        foreach (var line in File.ReadAllLines(ledger))
        {
            var item = CompletedLedgerItemRegex().Match(line);

            if (!item.Success)
            {
                continue;
            }

            completed++;

            var label = item.Groups["label"].Value.Trim();
            var annotation = GateAnnotationRegex().Match(line);

            if (!annotation.Success)
            {
                failures.Add($"'{label}' is marked complete but carries no `gate:` annotation.");

                continue;
            }

            var references = JobReferenceRegex()
                .Matches(annotation.Groups["refs"].Value)
                .ToList();

            if (references.Count == 0)
            {
                failures.Add($"'{label}' carries a `gate:` annotation that names no '<workflow>.yml#<job>' reference.");

                continue;
            }

            foreach (var reference in references)
            {
                var workflow = reference.Groups["workflow"].Value;
                var jobId = reference.Groups["job"].Value;

                if (!catalog.JobExists(workflow, jobId))
                {
                    failures.Add($"'{label}' cites '{workflow}#{jobId}', which does not resolve to a real CI job.");
                }
            }
        }

        // Assert
        Assert.True(completed > 0, "The authoritative ledger reports no completed items, so this rule would pass vacuously.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Proves the authoritative ledger never leaves an open item carrying a gate annotation that would imply it is
    /// already enforced, and that open items keep the empty annotation placeholder so the ledger stays uniform.
    /// </summary>
    [Fact]
    public void AuthoritativeLedger_LeavesOpenItemsWithoutEnforcementEvidence()
    {
        // Arrange
        var catalog = WorkflowJobCatalog.Load();
        var ledger = LedgerFiles(catalog.RepositoryRoot)
            .Single(file => File.ReadAllText(file).Contains(AuthoritativeMarker, StringComparison.Ordinal));
        var failures = new List<string>();
        var open = 0;

        // Act
        foreach (var line in File.ReadAllLines(ledger))
        {
            if (!OpenLedgerItemRegex().IsMatch(line))
            {
                continue;
            }

            open++;

            var annotation = GateAnnotationRegex().Match(line);

            if (annotation.Success && JobReferenceRegex().IsMatch(annotation.Groups["refs"].Value))
            {
                failures.Add($"'{line.Trim()}' is still open but cites an enforcing CI job. Mark it complete or remove the citation.");
            }
        }

        // Assert
        Assert.True(open > 0, "The authoritative ledger reports no open items, so this rule would pass vacuously.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static List<string> LedgerFiles(string repositoryRoot)
    {
        return [.. Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, ".github", "contact-center"), "*.md")
            .OrderBy(file => file, StringComparer.Ordinal)];
    }

    private static Dictionary<string, HashSet<string>> IndexTestClasses(string repositoryRoot)
    {
        var index = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var testsRoot = Path.Combine(repositoryRoot, "tests");

        foreach (var file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(testsRoot, file).Replace('\\', '/');
            var project = relative.Split('/')[0];

            foreach (Match match in TestClassDeclarationRegex().Matches(File.ReadAllText(file)))
            {
                var name = match.Groups["name"].Value;

                if (!index.TryGetValue(name, out var projects))
                {
                    projects = new HashSet<string>(StringComparer.Ordinal);
                    index[name] = projects;
                }

                projects.Add(project);
            }
        }

        return index;
    }

    private static JsonArray LoadGates(string repositoryRoot)
    {
        var matrixPath = Path.Combine(repositoryRoot, ".github", "contact-center", "pr-test-control-matrix.v1.json");
        var matrix = JsonNode.Parse(File.ReadAllText(matrixPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center PR-to-test control matrix is invalid.");

        return matrix["gates"]?.AsArray() ??
            throw new InvalidOperationException("The Contact Center PR-to-test control matrix must define gates.");
    }

    private static IEnumerable<(string Workflow, string JobId)> JobReferences(JsonNode gate)
    {
        yield return (Workflow(gate), JobId(gate));

        var additionalJobs = gate["ciJob"]?["additionalJobs"]?.AsArray();

        if (additionalJobs is null)
        {
            yield break;
        }

        foreach (var additionalJob in additionalJobs)
        {
            yield return (
                additionalJob?["workflow"]?.GetValue<string>() ?? string.Empty,
                additionalJob?["id"]?.GetValue<string>() ?? string.Empty);
        }
    }

    private static string Id(JsonNode gate)
        => gate["id"]?.GetValue<string>() ?? string.Empty;

    private static string Status(JsonNode gate)
        => gate["ciJob"]?["status"]?.GetValue<string>() ?? string.Empty;

    private static string Workflow(JsonNode gate)
        => gate["ciJob"]?["workflow"]?.GetValue<string>() ?? string.Empty;

    private static string JobId(JsonNode gate)
        => gate["ciJob"]?["id"]?.GetValue<string>() ?? string.Empty;

    private static string EvidenceKind(JsonNode gate)
        => gate["evidenceKind"]?.GetValue<string>() ?? "tests";

    private static string EvidenceLocation(JsonNode gate)
        => gate["evidenceLocation"]?.GetValue<string>() ?? string.Empty;

    [GeneratedRegex(@"\b[A-Za-z0-9_]+Tests\b")]
    private static partial Regex TestClassRegex();

    [GeneratedRegex(@"\bclass\s+(?<name>[A-Za-z0-9_]+Tests)\b")]
    private static partial Regex TestClassDeclarationRegex();

    [GeneratedRegex(@"(?:planned:)?\.github/workflows/[A-Za-z0-9_.-]+\.yml")]
    private static partial Regex WorkflowReferenceRegex();

    [GeneratedRegex(@"^\s*- \[[x~]\] (?<label>[^`]+)")]
    private static partial Regex CompletedLedgerItemRegex();

    [GeneratedRegex(@"^\s*- \[ \] ")]
    private static partial Regex OpenLedgerItemRegex();

    [GeneratedRegex(@"`gate:(?<refs>[^`]*)`")]
    private static partial Regex GateAnnotationRegex();

    [GeneratedRegex(@"(?<workflow>[A-Za-z0-9_.-]+\.yml)#(?<job>[A-Za-z0-9_-]+)")]
    private static partial Regex JobReferenceRegex();
}
