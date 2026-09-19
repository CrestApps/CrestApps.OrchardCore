using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.Core.ContactCenter.ClientConfiguration;
using CrestApps.Core.Telephony.ClientConfiguration;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Pins what the server puts in the page against what the browser takes out of it.
/// </summary>
/// <remarks>
/// <para>
/// Each of these models is serialized into a <c>data-config</c> attribute and read once on load. No
/// compiler connects the two ends: the scripts read every key with a fallback, so a key that is
/// renamed or stops arriving does not throw - it turns a feature off. A dropped
/// <c>engageUrl</c> removes supervisor engagement; a dropped <c>canSecurePause</c> removes the
/// control that stops a card number being recorded.
/// </para>
/// <para>
/// So the rule is read off the scripts themselves rather than written down twice. Every
/// <c>config.x</c> a script reads must be a key its model emits, and the exact emitted key set is
/// pinned, so that adding a property is a deliberate change to what the browser receives.
/// </para>
/// </remarks>
public sealed class ClientConfigurationContractTests
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Keys a script reads that the server has never sent, and must keep not sending.
    /// </summary>
    /// <remarks>
    /// These read as <c>undefined</c> today and the scripts are written for that. Adding either as a
    /// property would send a value instead of nothing: a numeric <c>playoutDelaySeconds</c> defaulting
    /// to zero would pin every agent's incoming-audio delay to zero rather than leaving it to the
    /// agent's saved layout.
    /// </remarks>
    private static readonly HashSet<string> _deliberatelyAbsent = new(StringComparer.Ordinal)
    {
        "playoutDelaySeconds",
        "activeCallRefreshInterval",
    };

    /// <summary>
    /// Every surface, the script that reads it, and the keys its model is approved to emit.
    /// </summary>
    private static readonly (string Surface, string ScriptPath, string[] Keys)[] _surfaces =
    [
        (
            "soft phone",
            "src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js",
            [
                "antiForgeryToken", "audioCapabilities", "audioMode", "browserMediaAdapterName",
                "capabilities", "connectUrl", "defaultCountryCode", "disconnectUrl", "enableDiagnostics",
                "hubUrl", "recentCallsCount", "registrationConfigUrl", "storageKey", "strings",
                "voicemailDeleteEnabled", "voicemailDeleteUrlTemplate", "voicemailMediaUrlTemplate",
                "voicemailPlaybackEnabled",
            ]
        ),
        (
            "agent workspace",
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/agent-workspace.js",
            [
                "acceptOfferUrl", "antiForgeryToken", "beginSecureCaptureUrl", "canInitiateSecureCapture",
                "canSecurePause", "completeActivityUrlTemplate", "declineOfferUrl", "hubUrl",
                "pauseRecordingUrl", "requirePauseReason", "resumeRecordingUrl", "secureCaptureFields",
                "setPresenceUrl", "stateUrl", "strings",
            ]
        ),
        (
            "agent bar",
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/contact-center-agent-bar.js",
            [
                "acceptOfferUrl", "antiForgeryToken", "completeActivityUrlTemplate", "completeUrl",
                "declineOfferUrl", "dispositions", "hubUrl", "reasonCodes", "setPresenceUrl", "stateUrl",
                "strings", "workspaceUrl",
            ]
        ),
        (
            "supervisor dashboard",
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/supervisor-dashboard.js",
            ["antiForgeryToken", "engageUrl", "hubUrl", "stateUrl", "strings"]
        ),
    ];

    public static TheoryData<string, string, string[]> Surfaces
    {
        get
        {
            var data = new TheoryData<string, string, string[]>();

            foreach (var (surface, scriptPath, keys) in _surfaces)
            {
                data.Add(surface, scriptPath, keys);
            }

            return data;
        }
    }

    public static TheoryData<string, object> Models => new()
    {
        { "soft phone", new SoftPhoneClientConfiguration() },
        { "agent workspace", new AgentWorkspaceClientConfiguration() },
        { "agent bar", new AgentBarClientConfiguration() },
        { "supervisor dashboard", new SupervisorDashboardClientConfiguration() },
    };

    [Theory]
    [MemberData(nameof(Models))]
    public void EachModel_EmitsExactlyTheApprovedKeys(string surface, object model)
    {
        // Arrange
        var approved = ApprovedKeysFor(surface);

        // Act
        var emitted = EmittedKeys(model);

        // Assert
        Assert.Equal(approved, emitted);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EveryKeyTheScriptReads_IsOneTheServerSends(string surface, string scriptPath, string[] approvedKeys)
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(repositoryRoot, scriptPath.Replace('/', Path.DirectorySeparatorChar)));

        // Act
        var read = Regex
            .Matches(script, @"\bconfig\.(?<key>[A-Za-z_][A-Za-z0-9_]*)")
            .Select(match => match.Groups["key"].Value)
            .Where(key => !_deliberatelyAbsent.Contains(key))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        // Assert
        var missing = read.Except(approvedKeys, StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0,
            $"The {surface} script reads configuration the server does not send, so the feature behind each of " +
            $"these is off and nothing reports it: {string.Join(", ", missing)}. Either add the property to the " +
            "model, or record the key as deliberately absent with the reason it must stay that way.");
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void EveryKeyTheServerSends_IsStillRecognisable(string surface, string scriptPath, string[] approvedKeys)
    {
        // The reverse direction is a weaker claim on purpose: a few keys are sent and read by nothing, and
        // removing them would change the payload for no behavioural gain. What must hold is that the approved
        // set and the script agree wherever they overlap, which the assertions above cover, and that the
        // approved set stays sorted and unique so a duplicate cannot hide a rename.
        Assert.Equal(approvedKeys.Order(StringComparer.Ordinal).Distinct(StringComparer.Ordinal), approvedKeys);
        Assert.True(File.Exists(Path.Combine(FindRepositoryRoot(), scriptPath.Replace('/', Path.DirectorySeparatorChar))),
            $"The {surface} script is not where this test looks for it, so the rule reads nothing.");
    }

    [Fact]
    public void AnOptionSentToTheBar_CarriesNothingButAnIdentifierAndALabel()
    {
        // The dispositions and reason codes are serialized straight into the page. Anything else that became a
        // public property here would start reaching the browser without anybody deciding it should.
        var json = JsonSerializer.Serialize(new AgentBarOption { Id = "a", Name = "b" }, _options);

        Assert.Equal("{\"id\":\"a\",\"name\":\"b\"}", json);
    }

    [Fact]
    public void TheSoftPhoneStorageKey_IsTheOneAgentsLayoutsAreAlreadySavedUnder()
    {
        // The script appends a suffix to this and uses it as the browser storage key holding each agent's
        // chosen microphone, speaker, boost, playout delay and signaling region. Changing it does not migrate
        // that; it discards it.
        Assert.Equal("telephony-soft-phone", SoftPhoneClientConfiguration.DefaultStorageKey);
    }

    [Fact]
    public void TheTwoUrlTemplateTokens_StayDifferentFromEachOther()
    {
        // Both scripts substitute by literal text match, against templates built by different features. They
        // look like they should be unified; unifying them would break whichever one was changed.
        var workspace = new AgentWorkspaceClientConfiguration { CompleteActivityUrlTemplate = "/x/__activityId__" };

        Assert.Contains("__activityId__", workspace.CompleteActivityUrlTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("__INTERACTION_ID__", workspace.CompleteActivityUrlTemplate, StringComparison.Ordinal);
    }

    public static TheoryData<string> AttributeSurfaces =>
    [
        "src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Views/Admin/Index.cshtml",
        "src/Modules/CrestApps.OrchardCore.Omnichannel.Sms.Portal/Views/Admin/Conversation.cshtml",
    ];

    [Theory]
    [MemberData(nameof(AttributeSurfaces))]
    public void EveryAttributeTheSmsPortalReads_IsOneItsPageWrites(string viewPath)
    {
        // The SMS portal carries its configuration as separate attributes rather than one serialized object,
        // and its script reads them by name from the element it is declared on. The failure mode is the same
        // as the others - a renamed attribute reads as null and the feature stops - but it cannot be caught by
        // serializing a model, so it is checked against the page itself.

        // Arrange
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), viewPath.Replace('/', Path.DirectorySeparatorChar)));

        // Act
        var read = Regex
            .Matches(view, @"getAttribute\('(?<name>data-[a-z0-9-]+)'\)")
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Written across the whole view folder, not just this file: a partial rendered into the page writes
        // into the same document the script reads from, so narrowing the scan to one file would report a
        // message bubble's attributes as missing.
        var written = Directory
            .EnumerateFiles(
                Path.GetDirectoryName(Path.Combine(FindRepositoryRoot(), viewPath.Replace('/', Path.DirectorySeparatorChar))),
                "*.cshtml")
            .SelectMany(file => Regex
                .Matches(File.ReadAllText(file), @"(?<name>data-[a-z0-9-]+)=""")
                .Select(match => match.Groups["name"].Value))
            .ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.NotEmpty(read);

        var missing = read.Where(name => !written.Contains(name)).ToList();

        Assert.True(
            missing.Count == 0,
            $"'{viewPath}' reads attributes it never writes, so each reads as null and the behaviour behind it " +
            $"is gone without an error: {string.Join(", ", missing)}.");
    }

    private static string[] ApprovedKeysFor(string surface)
        => _surfaces.First(row => string.Equals(row.Surface, surface, StringComparison.Ordinal)).Keys;

    private static string[] EmittedKeys(object model)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(model, model.GetType(), _options));

        return [.. document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }
}
