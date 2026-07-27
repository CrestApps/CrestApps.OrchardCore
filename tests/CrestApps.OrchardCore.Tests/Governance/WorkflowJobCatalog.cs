using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Governance;

/// <summary>
/// Resolves the jobs that GitHub Actions workflows actually declare, so governance ledgers can be checked
/// against real CI rather than against prose that merely claims a job exists.
/// </summary>
public sealed partial class WorkflowJobCatalog
{
    private readonly Dictionary<string, Dictionary<string, string>> _jobBodiesByWorkflow = new(StringComparer.OrdinalIgnoreCase);

    private WorkflowJobCatalog(string repositoryRoot)
    {
        RepositoryRoot = repositoryRoot;

        var workflowDirectory = Path.Combine(repositoryRoot, ".github", "workflows");

        foreach (var file in Directory.EnumerateFiles(workflowDirectory, "*.yml"))
        {
            var relativePath = ".github/workflows/" + Path.GetFileName(file);

            _jobBodiesByWorkflow[relativePath] = ParseJobs(File.ReadAllLines(file));
        }
    }

    /// <summary>
    /// Gets the absolute path of the repository root.
    /// </summary>
    public string RepositoryRoot { get; }

    /// <summary>
    /// Gets the workflow paths that were discovered, relative to the repository root.
    /// </summary>
    public IEnumerable<string> WorkflowPaths => _jobBodiesByWorkflow.Keys;

    /// <summary>
    /// Creates a catalog by locating the repository root from the current test binary.
    /// </summary>
    /// <returns>A catalog covering every workflow file in the repository.</returns>
    public static WorkflowJobCatalog Load()
        => new(FindRepositoryRoot());

    /// <summary>
    /// Determines whether the given workflow file was discovered.
    /// </summary>
    /// <param name="workflowPath">The workflow path relative to the repository root.</param>
    /// <returns><c>true</c> when the workflow file exists; otherwise <c>false</c>.</returns>
    public bool WorkflowExists(string workflowPath)
        => _jobBodiesByWorkflow.ContainsKey(Normalize(workflowPath));

    /// <summary>
    /// Gets the job identifiers declared by the given workflow.
    /// </summary>
    /// <param name="workflowPath">The workflow path relative to the repository root.</param>
    /// <returns>The declared job identifiers, or an empty set when the workflow does not exist.</returns>
    public IReadOnlyCollection<string> GetJobs(string workflowPath)
    {
        return _jobBodiesByWorkflow.TryGetValue(Normalize(workflowPath), out var jobs)
            ? jobs.Keys
            : [];
    }

    /// <summary>
    /// Determines whether the given workflow declares the given job.
    /// </summary>
    /// <param name="workflowPath">The workflow path relative to the repository root.</param>
    /// <param name="jobId">The job identifier.</param>
    /// <returns><c>true</c> when the workflow declares the job; otherwise <c>false</c>.</returns>
    public bool JobExists(string workflowPath, string jobId)
    {
        return _jobBodiesByWorkflow.TryGetValue(Normalize(workflowPath), out var jobs) &&
            jobs.ContainsKey(jobId);
    }

    /// <summary>
    /// Gets the test project directory names that the given job executes.
    /// </summary>
    /// <param name="workflowPath">The workflow path relative to the repository root.</param>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The test project directory names referenced by the job body.</returns>
    public IReadOnlyCollection<string> GetExecutedTestProjects(string workflowPath, string jobId)
    {
        if (!_jobBodiesByWorkflow.TryGetValue(Normalize(workflowPath), out var jobs) ||
            !jobs.TryGetValue(jobId, out var body))
        {
            return [];
        }

        var projects = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in TestProjectReferenceRegex().Matches(body))
        {
            projects.Add(match.Groups["project"].Value);
        }

        return projects;
    }

    private static string Normalize(string workflowPath)
    {
        ArgumentNullException.ThrowIfNull(workflowPath);

        var normalized = workflowPath.Replace('\\', '/');

        if (!normalized.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase))
        {
            normalized = string.Concat(".github/workflows/", normalized.AsSpan(normalized.LastIndexOf('/') + 1));
        }

        return normalized;
    }

    private static Dictionary<string, string> ParseJobs(string[] lines)
    {
        var jobs = new Dictionary<string, string>(StringComparer.Ordinal);
        var insideJobsBlock = false;
        var currentJob = string.Empty;
        var currentBody = new List<string>();

        foreach (var line in lines)
        {
            if (TopLevelJobsKeyRegex().IsMatch(line))
            {
                insideJobsBlock = true;

                continue;
            }

            if (!insideJobsBlock)
            {
                continue;
            }

            // Any other column-zero key ends the top-level `jobs:` mapping.
            if (TopLevelKeyRegex().IsMatch(line))
            {
                if (currentJob.Length > 0)
                {
                    jobs[currentJob] = string.Join('\n', currentBody);
                }

                insideJobsBlock = false;
                currentJob = string.Empty;
                currentBody.Clear();

                continue;
            }

            var jobMatch = JobKeyRegex().Match(line);

            if (jobMatch.Success)
            {
                if (currentJob.Length > 0)
                {
                    jobs[currentJob] = string.Join('\n', currentBody);
                }

                currentJob = jobMatch.Groups["job"].Value;
                currentBody.Clear();

                continue;
            }

            if (currentJob.Length > 0)
            {
                currentBody.Add(line);
            }
        }

        if (currentJob.Length > 0)
        {
            jobs[currentJob] = string.Join('\n', currentBody);
        }

        return jobs;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }

    [GeneratedRegex(@"^jobs:\s*$")]
    private static partial Regex TopLevelJobsKeyRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\s*:")]
    private static partial Regex TopLevelKeyRegex();

    [GeneratedRegex(@"^  (?<job>[A-Za-z0-9_-]+):\s*$")]
    private static partial Regex JobKeyRegex();

    [GeneratedRegex(@"tests/(?<project>[A-Za-z0-9_.]+)/\k<project>\.csproj")]
    private static partial Regex TestProjectReferenceRegex();
}
