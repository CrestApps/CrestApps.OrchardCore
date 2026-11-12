using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// DRY-7: every outbound HTTP client whose host comes from stored configuration or request data goes through the egress
/// guard (<c>AddEgressGuardedHttpClient</c>, or a primary handler from <c>EgressNetwork</c>), so it can't be pointed at
/// the site's own network. A plain <c>AddHttpClient</c> is allowed only for a fixed-host provider client on the list
/// below, with the reason. The list may only shrink; a registration that becomes guarded must leave it.
/// </summary>
public sealed class OutboundHttpArchitectureTests
{
    private static readonly Regex _addHttpClientRegex = new(@"\.AddHttpClient\b(?:<[^>(]*>)?\s*\(", RegexOptions.Compiled);

    private static readonly Regex _guardedRegex = new(
        @"ConfigurePrimaryHttpMessageHandler[\s\S]*\b(?:EgressNetwork|DataConnectorNetwork)\.CreateHandler\b",
        RegexOptions.Compiled);

    private static readonly Regex _resilienceRegex = new(@"\bAdd(?:Standard)?ResilienceHandler\b", RegexOptions.Compiled);

    // The guard's own implementation, which every guarded registration goes through.
    private static readonly HashSet<string> _exemptFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "src/Core/CrestApps.OrchardCore.Core/Http/EgressNetwork.cs",
    };

    /// <summary>
    /// The plain registrations allowed, by file and client name, with the reason. Never add one for a client whose host
    /// an administrator, a workflow or a request supplies: register it with <c>AddEgressGuardedHttpClient</c>.
    /// </summary>
    private static readonly Dictionary<string, string> _allowed = new(StringComparer.Ordinal)
    {
        ["src/Core/CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Core/Twilio/TwilioSmsWebhook.cs|TwilioSmsProvider.TechnicalName"] = "Fixed host: api.twilio.com.",
        ["src/Modules/CrestApps.OrchardCore.Addresses/GooglePlaces/GooglePlacesStartup.cs|GooglePlacesAddressAutocompleteProvider.HttpClientName"] = "Fixed host: places.googleapis.com.",
        ["src/Modules/CrestApps.OrchardCore.Asterisk/Startup.cs|AsteriskConstants.HttpClientName"] = "The operator's own Asterisk server (ARI), set in host or site configuration and normally on the private network by design; live call control, kept as it is.",
        ["src/Modules/CrestApps.OrchardCore.DncRegistry/Startup.cs|nameof(UsaFtcDncRegistry)"] = "Fixed host: the US FTC Do Not Call registry.",
        ["src/Modules/CrestApps.OrchardCore.DncRegistry/Startup.cs|nameof(CanadaDnclRegistry)"] = "Fixed host: the Canadian DNCL registry.",
        ["src/Modules/CrestApps.OrchardCore.Geo.AzureMaps/Startup.cs|AzureMapsProvider.HttpClientName"] = "Fixed host: atlas.microsoft.com.",
        ["src/Modules/CrestApps.OrchardCore.Geo.GoogleMaps/Startup.cs|GoogleMapsProvider.HttpClientName"] = "Fixed host: maps.googleapis.com.",
        ["src/Modules/CrestApps.OrchardCore.Geo.Osrm/Startup.cs|OsrmProvider.HttpClientName"] = "Pending SEC-P10b.",
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging.Sms/Startup.cs|TwilioSmsMediaSender.HttpClientName"] = "Fixed host: api.twilio.com.",
        ["src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Startup.cs|MessagingInboundMediaIngestor.HttpClientName"] = "Pending SEC-P10f.",
        ["src/Modules/CrestApps.OrchardCore.PhoneNumbers.Verifications/Startup.cs|nameof(AbstractApiPhoneNumberVerificationProvider)"] = "Fixed host: the AbstractAPI phone validation service.",
        ["src/Modules/CrestApps.OrchardCore.PhoneNumbers.Verifications/Startup.cs|nameof(VeriphonePhoneNumberVerificationProvider)"] = "Fixed host: api.veriphone.io.",
        ["src/Modules/CrestApps.OrchardCore.PhoneNumbers.Verifications/Startup.cs|nameof(TwilioPhoneNumberVerificationProvider)"] = "Fixed host: lookups.twilio.com.",
        ["src/Modules/CrestApps.OrchardCore.Shipping/Startup.cs|EasyPostCarrierProvider.HttpClientName"] = "Fixed host: api.easypost.com.",
        ["src/Modules/CrestApps.OrchardCore.Stripe/Startup.cs|StripeConstants.HttpClientName"] = "Fixed host: api.stripe.com.",
        ["src/Modules/CrestApps.OrchardCore.Taxation/TaxJar/TaxJarStartup.cs|TaxJarTaxDeterminationProvider.HttpClientName"] = "Fixed host: api.taxjar.com.",
        ["src/Modules/CrestApps.OrchardCore.Telnyx/Startup.cs|TelnyxConstants.ProviderTechnicalName"] = "Fixed host: api.telnyx.com (an administrator may only point it at another Telnyx API base).",
        ["src/Modules/CrestApps.OrchardCore.Telnyx/Startup.cs|<TelnyxApiClient>"] = "Fixed host: api.telnyx.com (an administrator may only point it at another Telnyx API base).",
        ["src/Startup/CrestApps.OrchardCore.Asterisk.Web/Program.cs|"] = "A development-only sample host that calls the developer's own Asterisk and site.",
        ["src/Startup/CrestApps.OrchardCore.Samples.A2AClient/Program.cs|"] = "A sample console host that calls the developer's own site.",
        ["src/Startup/CrestApps.OrchardCore.Samples.McpClient/Program.cs|"] = "A sample host that calls the developer's own site.",
    };

    private static readonly Lazy<IReadOnlyList<Registration>> _registrations = new(Scan);

    [Fact]
    public void EveryHttpClient_IsEgressGuarded_OrAnAllowedFixedHostClient()
    {
        // Act
        var unguarded = _registrations.Value
            .Where(registration => !registration.IsGuarded && !_allowed.ContainsKey(registration.Key))
            .Select(registration => registration.Key)
            .ToList();

        // Assert
        Assert.True(_registrations.Value.Count > 10, "The scan found almost no AddHttpClient registrations; it may be scanning the wrong folder.");
        Assert.True(unguarded.Count == 0, BuildMessage(
            "These HttpClient registrations are neither egress-guarded nor allowed fixed-host provider clients. Register them with AddEgressGuardedHttpClient (CrestApps.OrchardCore.Core.Http):",
            unguarded));
    }

    [Fact]
    public void EveryAllowedClient_IsStillAPlainRegistration()
    {
        // Act
        var plain = _registrations.Value.Where(registration => !registration.IsGuarded).Select(registration => registration.Key).ToHashSet(StringComparer.Ordinal);
        var stale = _allowed.Keys.Where(key => !plain.Contains(key)).ToList();

        // Assert
        Assert.True(stale.Count == 0, BuildMessage(
            $"These clients are guarded or gone. Delete them from {nameof(OutboundHttpArchitectureTests)}._allowed so the list only shrinks:",
            stale));
    }

    [Fact]
    public void EveryTypedClient_IsOnANamedClient_OrHasItsOwnResilienceHandler()
    {
        // A typed client's name is its type name: handlers added to another named client never reach it (the Telnyx
        // defect DRY-7 fixed).
        var bare = _registrations.Value
            .Where(registration => registration.IsTyped && !registration.HasName && !registration.HasResilience && !registration.Key.StartsWith("src/Startup/", StringComparison.Ordinal))
            .Select(registration => registration.Key)
            .ToList();

        Assert.True(bare.Count == 0, BuildMessage(
            "These typed HttpClient registrations get no resilience handler. Register them on the named client that has one, or add one:",
            bare));
    }

    private static IReadOnlyList<Registration> Scan()
    {
        var repositoryRoot = ProjectReferenceRulesTests.FindRepositoryRoot();
        var registrations = new List<Registration>();

        foreach (var file in SourceTreeFiles.Enumerate(Path.Combine(repositoryRoot, "src"), "*.cs", SourceTreeFiles.IsBuildOutputDirectory))
        {
            var relative = Path.GetRelativePath(repositoryRoot, file).Replace(Path.DirectorySeparatorChar, '/');

            if (_exemptFiles.Contains(relative))
            {
                continue;
            }

            var source = StripComments(File.ReadAllText(file));

            foreach (Match match in _addHttpClientRegex.Matches(source))
            {
                var statement = source[match.Index..StatementEnd(source, match.Index)];
                var typed = match.Value.Contains('<', StringComparison.Ordinal);
                var name = FirstArgument(source, match.Index + match.Length);
                var hasName = name.Length > 0 && !name.StartsWith('(') && !name.Contains("=>", StringComparison.Ordinal);
                var key = typed && !hasName
                    ? relative + "|" + match.Value[match.Value.IndexOf('<', StringComparison.Ordinal)..(match.Value.IndexOf('>', StringComparison.Ordinal) + 1)]
                    : relative + "|" + (hasName ? name : string.Empty);

                registrations.Add(new Registration(key, _guardedRegex.IsMatch(statement), typed, hasName, _resilienceRegex.IsMatch(statement)));
            }
        }

        return registrations;
    }

    // Where the statement ends: the first semicolon outside parentheses, brackets and braces (a lambda body's statements
    // are inside them).
    private static int StatementEnd(string source, int start)
    {
        var depth = 0;

        for (var index = start; index < source.Length; index++)
        {
            switch (source[index])
            {
                case '(' or '[' or '{':
                    depth++;
                    break;
                case ')' or ']' or '}':
                    if (--depth < 0)
                    {
                        return index;
                    }

                    break;
                case ';' when depth == 0:
                    return index;
            }
        }

        return source.Length;
    }

    // The first argument's text, up to a comma or the closing parenthesis at the same depth.
    private static string FirstArgument(string source, int start)
    {
        var depth = 0;

        for (var index = start; index < source.Length; index++)
        {
            var character = source[index];

            if (character is '(' or '<' or '[' or '{')
            {
                depth++;
            }
            else if (character is ')' or '>' or ']' or '}')
            {
                if (depth == 0)
                {
                    return source[start..index].Trim();
                }

                depth--;
            }
            else if (character == ',' && depth == 0)
            {
                return source[start..index].Trim();
            }
        }

        return string.Empty;
    }

    private static string StripComments(string source)
        => Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline), @"(?<![:""\w])//[^\r\n]*", string.Empty);

    private static string BuildMessage(string header, IEnumerable<string> lines)
    {
        var message = new StringBuilder().AppendLine(header);

        foreach (var line in lines.Order(StringComparer.Ordinal))
        {
            message.Append("    ").AppendLine(line);
        }

        return message.ToString();
    }

    private sealed record Registration(string Key, bool IsGuarded, bool IsTyped, bool HasName, bool HasResilience);
}
