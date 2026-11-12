using CrestApps.OrchardCore.Staff.Core.Services;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Staff.AI;

/// <summary>
/// The base of the Staff AI tools: they answer only signed-in people who may see staff.
/// </summary>
public abstract class StaffAIToolBase : AIFunction
{
    /// <summary>
    /// The JSON options of the answers.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>
    {
        ["Strict"] = false,
    };

    /// <inheritdoc />
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var user = arguments.Services?.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return "Staff tools can only be used by a signed-in person.";
        }

        if (!await arguments.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, StaffPermissions.ViewStaff))
        {
            return "You aren't allowed to see staff.";
        }

        return await InvokeAsync(arguments, arguments.Services, user, cancellationToken);
    }

    /// <summary>
    /// Runs the tool.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="services">The services.</param>
    /// <param name="user">The signed-in person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The answer.</returns>
    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a text argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument name.</param>
    /// <returns>The text, or <see langword="null"/>.</returns>
    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element
            ? element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()
            : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema.</param>
    /// <returns>The schema element.</returns>
    protected static JsonElement Schema(string schema)
        => JsonSerializer.Deserialize<JsonElement>(schema);
}

/// <summary>
/// Finds staff by name, job title, team or capability.
/// </summary>
public sealed class FindStaffTool : StaffAIToolBase
{
    /// <summary>
    /// The tool name.
    /// </summary>
    public const string ToolName = "staffFind";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "text": { "type": "string", "description": "Part of a name, e-mail, job title or employee number; empty for everyone." },
            "teamName": { "type": "string", "description": "A team; its members and those of the teams under it match." },
            "capability": { "type": "string", "description": "A kind of work, such as Technician, Agent or Cashier." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Finds the people who work for the business by name, job title, team or capability, with their job, main team, manager and status.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var teams = await services.GetRequiredService<StaffTeamReader>().GetAllAsync(cancellationToken);
        var teamName = Read(arguments, "teamName");
        var team = teamName is null ? null : teams.FirstOrDefault(item => string.Equals(item.Name, teamName, StringComparison.OrdinalIgnoreCase));

        if (teamName is not null && team is null)
        {
            return $"There is no team called {teamName}.";
        }

        var (count, people) = await services.GetRequiredService<IStaffMemberManager>().SearchAsync(new StaffQuery
        {
            Search = Read(arguments, "text"),
            TeamId = team?.ItemId,
            Capability = Read(arguments, "capability"),
        }, 0, 25, cancellationToken);

        var directory = services.GetRequiredService<IStaffDirectory>();
        var managers = await directory.GetNamesAsync(people.Select(person => person.ManagerId), cancellationToken);
        var teamNames = teams.ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);

        return JsonSerializer.Serialize(new
        {
            total = count,
            staff = people.Select(person => new
            {
                name = person.DisplayName,
                jobTitle = person.JobTitle,
                team = person.PrimaryTeamId is { } id ? teamNames.GetValueOrDefault(id) : null,
                manager = person.ManagerId is { } manager ? managers.GetValueOrDefault(manager) : null,
                status = person.Status.ToString(),
                capabilities = person.Capabilities,
                email = person.Email,
            }),
        }, JsonOptions);
    }
}

/// <summary>
/// Gives a person's reporting line: who they report to, up to the top, and who reports to them.
/// </summary>
public sealed class ReportingLineTool : StaffAIToolBase
{
    /// <summary>
    /// The tool name.
    /// </summary>
    public const string ToolName = "staffReportingLine";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "name": { "type": "string", "description": "The person's name or employee number; empty for the signed-in person." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Gives a person's reporting line: their manager, their manager's manager and so on, and the people who report to them.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IStaffMemberManager>();
        var name = Read(arguments, "name");
        var person = name is null
            ? await manager.FindByUserIdAsync(user.FindFirstValue(ClaimTypes.NameIdentifier), cancellationToken)
            : await manager.FindByEmployeeNumberAsync(name, cancellationToken) ?? await FindFirstMatchAsync(manager, name, cancellationToken);

        if (person is null)
        {
            return name is null ? "You aren't a staff member." : $"Nobody called {name} works here.";
        }

        var hierarchy = services.GetRequiredService<IStaffHierarchy>();
        var chain = await hierarchy.GetManagerChainAsync(person.ItemId, cancellationToken);
        var reports = await hierarchy.GetReportsAsync(person.ItemId, includeIndirect: false, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            name = person.DisplayName,
            jobTitle = person.JobTitle,
            reportsTo = chain.Select(item => new { name = item.DisplayName, jobTitle = item.JobTitle }),
            directReports = reports.Select(item => new { name = item.DisplayName, jobTitle = item.JobTitle }),
        }, JsonOptions);
    }

    private static async Task<StaffMember> FindFirstMatchAsync(IStaffMemberManager manager, string name, CancellationToken cancellationToken)
    {
        var page = await manager.SearchAsync(new StaffQuery { Search = name }, 0, 1, cancellationToken);

        return page.Items.Count > 0 ? page.Items[0] : null;
    }
}
