using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Bug: the activity editor and the telephony extension editor loaded every enabled user of the tenant into a
/// drop-down on each render, which times out on a tenant with many users. Editors pick a user with the searchable
/// user picker (<c>UserPicker</c> over the <c>CrestApps.Users.Search</c> endpoint) instead. This keeps an editor in
/// these modules from loading the whole user table again.
/// <para>
/// Assets and teams grow the same way, so a driver, controller or endpoint may not load every asset or every team
/// either (CQ-4 in <c>docs/engineering/review-2026-11/04-code-quality.md</c>). The files that did when that rule was
/// set are listed with their count, which may only shrink.
/// </para>
/// </summary>
public sealed partial class UserListLoadArchitectureTests
{
    private static readonly string[] _scannedFolders =
    [
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Omnichannel.Managements", "Drivers"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Omnichannel.Messaging", "Drivers"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.ContactCenter", "Drivers"),
        Path.Combine("src", "Modules", "CrestApps.OrchardCore.Telephony", "Drivers"),
    ];

    /// <summary>
    /// The drivers, controllers and endpoints that loaded every asset when the rule was set. Use the asset picker.
    /// </summary>
    private static readonly Dictionary<string, int> _knownAssetListLoads = new(StringComparer.Ordinal)
    {
        ["src/Modules/CrestApps.OrchardCore.Assets/Drivers/AssetDisplayDriver.cs"] = 1,
    };

    /// <summary>
    /// The drivers, controllers and endpoints that loaded every team when the rule was set. Load the teams the user's
    /// scope covers, or the ids on the page, instead.
    /// </summary>
    private static readonly Dictionary<string, int> _knownTeamListLoads = new(StringComparer.Ordinal)
    {
        ["src/Modules/CrestApps.OrchardCore.Workforce/Api/WorkforceApiEndpoints.cs"] = 3,
        ["src/Modules/CrestApps.OrchardCore.Workforce/Controllers/WorkforceSetupController.cs"] = 1,
    };

    private static readonly Lazy<(Dictionary<string, int> Assets, Dictionary<string, int> Teams)> _actual = new(ScanRequestPaths);

    [Fact]
    public void NoEditorDriver_LoadsEveryEnabledUser()
    {
        // Arrange
        var root = Ratchet.RepositoryRoot;
        var offenders = new List<string>();

        // Act
        foreach (var folder in _scannedFolders)
        {
            var path = Path.Combine(root, folder);

            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                if (AllEnabledUsersQuery().IsMatch(File.ReadAllText(file)))
                {
                    offenders.Add(Path.GetRelativePath(root, file));
                }
            }
        }

        // Assert
        Assert.True(offenders.Count == 0, "These drivers load every enabled user; use the UserPicker view component instead: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoRequestPath_LoadsEveryAsset()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value.Assets,
            _knownAssetListLoads,
            "These drivers, controllers or endpoints load every asset.",
            "Pick an asset with the asset picker, or query the assets the page needs by id.");
        Ratchet.AssertNothingStale(_actual.Value.Assets, _knownAssetListLoads, $"{nameof(UserListLoadArchitectureTests)}.{nameof(_knownAssetListLoads)}");
    }

    [Fact]
    public void NoRequestPath_LoadsEveryTeam()
    {
        Ratchet.AssertNothingAdded(
            _actual.Value.Teams,
            _knownTeamListLoads,
            "These drivers, controllers or endpoints load every team.",
            "Load the teams the user's scope covers, or the ids the page shows, instead of ITeamManager.GetAllAsync().");
        Ratchet.AssertNothingStale(_actual.Value.Teams, _knownTeamListLoads, $"{nameof(UserListLoadArchitectureTests)}.{nameof(_knownTeamListLoads)}");
    }

    [Theory]
    [InlineData("model.ParentOptions = [.. (await _session.QueryIndex<AssetIndex>(index => index.ItemId != model.AssetId, collection: c).ListAsync())];", 1, 0)]
    [InlineData("var parents = await _session.QueryIndex<AssetIndex>(index => index.ItemId.IsIn(parentIds), collection: c).ListAsync(token);", 0, 0)]
    [InlineData("var page = await _session.Query<Asset, AssetIndex>(collection: c).Skip(skip).Take(size).ListAsync(token);", 0, 0)]
    [InlineData("var teams = (await services.GetRequiredService<ITeamManager>().GetAllAsync()).ToList();", 0, 1)]
    [InlineData("var teams = await _teamManager.GetAllAsync(HttpContext.RequestAborted);", 0, 1)]
    [InlineData("var team = await _teamManager.FindByIdAsync(teamId);", 0, 0)]
    public void Count_FindsWholeAssetAndTeamLists(string code, int assets, int teams)
    {
        // Act
        var masked = CSharpSourceMask.CommentsAndLiterals(code);

        // Assert
        Assert.Equal(assets, CountAssetLoads(masked));
        Assert.Equal(teams, AllTeamsQuery().Count(masked));
    }

    private static (Dictionary<string, int> Assets, Dictionary<string, int> Teams) ScanRequestPaths()
    {
        var assets = new Dictionary<string, int>(StringComparer.Ordinal);
        var teams = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in Ratchet.EnumerateFiles("src"))
        {
            if (!UnboundedQueryRatchetTests.IsRequestPathFile(file.RelativePath))
            {
                continue;
            }

            var code = CSharpSourceMask.CommentsAndLiterals(Ratchet.ReadAllText(file));
            var assetLoads = CountAssetLoads(code);
            var teamLoads = AllTeamsQuery().Count(code);

            if (assetLoads > 0)
            {
                assets[file.RelativePath] = assetLoads;
            }

            if (teamLoads > 0)
            {
                teams[file.RelativePath] = teamLoads;
            }
        }

        return (assets, teams);
    }

    // An asset query listed in the same statement, with no Take and no key that narrows it to some records.
    private static int CountAssetLoads(string code)
    {
        var count = 0;

        foreach (Match query in AssetQuery().Matches(code))
        {
            var open = query.Index + query.Length - 1;
            var close = CSharpSourceMask.FindClosing(code, open);
            var filter = code[(open + 1)..close];

            if (filter.Contains("==", StringComparison.Ordinal) || filter.Contains("IsIn(", StringComparison.Ordinal))
            {
                continue;
            }

            var end = code.IndexOf(';', close);
            var chain = end < 0 ? code[close..] : code[close..end];
            var list = chain.IndexOf(".ListAsync(", StringComparison.Ordinal);

            if (list >= 0 && !chain[..list].Contains(".Take(", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(@"Query<User,\s*UserIndex>\(\s*\w+\s*=>\s*\w+\.IsEnabled\s*\)\s*\.ListAsync\(")]
    private static partial Regex AllEnabledUsersQuery();

    [GeneratedRegex(@"\b(?:QueryIndex<(?:\w+\.)*AssetIndex>|Query<(?:\w+\.)*\w+,\s*(?:\w+\.)*AssetIndex>)\s*\(")]
    private static partial Regex AssetQuery();

    [GeneratedRegex(@"(?:ITeamManager>\(\)|\b_?[tT]eamManager)\s*\.\s*GetAllAsync\s*\(")]
    private static partial Regex AllTeamsQuery();
}
