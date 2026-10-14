using System.Net;
using CrestApps.OrchardCore.TenantHierarchy.Core.Guards;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Models;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Proves that one parent can never reach another parent or the other parent's child tenants, through any door: the
/// services and the broker that run in the parent, every page and action of its admin, the delegated access flow,
/// its sign-in cookies, and its outbound requests. The other parent's children never reach this parent's either.
/// </summary>
/// <remarks>
/// Firm A plays the attacker and firm B the victim. Every attempt uses each identifier firm A could learn about
/// firm B's child: its registry entry identifier, its tenant identifier and its tenant name. After each attempt the
/// test checks that firm B's child is unchanged, so a refusal that still changed something fails the test.
/// </remarks>
[Collection(TenantHierarchyCollection.Name)]
public sealed class ParentIsolationTests
{
    private const string FirmBAddress = "http://firmb.localhost";
    private const string BusinessOneAddress = "http://business1.firma.localhost";
    private const string BusinessFourAddress = "http://business4.firmb.localhost";
    private const string ProbeFeature = "OrchardCore.Apis.GraphQL";

    private readonly TenantHierarchyFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentIsolationTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared hierarchy.</param>
    public ParentIsolationTests(TenantHierarchyFixture fixture)
    {
        _fixture = fixture;
    }

    // The broker and the services of a parent.

    [Fact]
    public async Task Broker_InOneParent_FindsNothingOfAnotherParentsChild_ByAnyIdentifier()
    {
        // Act
        var results = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var broker = services.GetRequiredService<ITenantHierarchyBroker>();
            var found = new List<string>();

            foreach (var id in ForeignIdentifiers())
            {
                if (await broker.GetChildAsync(id) is not null)
                {
                    found.Add($"child {id}");
                }

                if ((await broker.GetChildFeaturesAsync(id)).Count > 0)
                {
                    found.Add($"features {id}");
                }

                if ((await broker.GetChildRolesAsync(id)).Count > 0)
                {
                    found.Add($"roles {id}");
                }
            }

            var listed = (await broker.ListChildrenAsync()).Select(child => child.Entry.TenantName).ToList();

            return (found, listed);
        });

        // Assert
        Assert.Empty(results.found);
        Assert.DoesNotContain(_fixture.BusinessFour.TenantName, results.listed);
        Assert.Contains(_fixture.BusinessOne.TenantName, results.listed);
    }

    [Fact]
    public async Task Broker_InOneParent_CannotChangeAnotherParentsChild_ByAnyIdentifier()
    {
        // Arrange
        var before = await SnapshotBusinessFourAsync();

        // Act
        var succeeded = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var broker = services.GetRequiredService<ITenantHierarchyBroker>();
            var changes = new List<string>();

            foreach (var id in ForeignIdentifiers())
            {
                var attempts = new Dictionary<string, Func<Task<TenantHierarchyResult>>>
                {
                    ["suspend"] = () => broker.SuspendChildAsync(id),
                    ["resume"] = () => broker.ResumeChildAsync(id),
                    ["reload"] = () => broker.ReloadChildAsync(id),
                    ["update"] = () => broker.UpdateChildSettingsAsync(id),
                    ["setup"] = () => broker.SetupChildAsync(id),
                    ["features"] = () => broker.SetChildFeaturesAsync(id, [ProbeFeature], enable: true),
                    ["remove"] = () => broker.RemoveChildAsync(id),
                };

                foreach (var (name, attempt) in attempts)
                {
                    if (await SucceededAsync(attempt))
                    {
                        changes.Add($"{name} {id}");
                    }
                }
            }

            return changes;
        });

        // Assert
        Assert.Empty(succeeded);
        await AssertBusinessFourUnchangedAsync(before);
    }

    [Fact]
    public async Task ChildTenantManager_InOneParent_CannotChangeAnotherParentsChild_ByAnyIdentifier()
    {
        // Arrange
        var before = await SnapshotBusinessFourAsync();

        // Act
        var succeeded = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var manager = services.GetRequiredService<ChildTenantManager>();
            var changes = new List<string>();

            foreach (var id in ForeignIdentifiers())
            {
                var attempts = new Dictionary<string, Func<Task<TenantHierarchyResult>>>
                {
                    ["suspend"] = () => manager.SuspendAsync(id),
                    ["resume"] = () => manager.ResumeAsync(id),
                    ["reload"] = () => manager.ReloadAsync(id),
                    ["restore"] = () => manager.RestoreAsync(id),
                    ["remove"] = () => manager.RemoveAsync(id),
                };

                foreach (var (name, attempt) in attempts)
                {
                    if (await SucceededAsync(attempt))
                    {
                        changes.Add($"{name} {id}");
                    }
                }
            }

            return changes;
        });

        // Assert
        Assert.Empty(succeeded);
        await AssertBusinessFourUnchangedAsync(before);
    }

    [Fact]
    public async Task AccessGrants_InOneParent_CannotNameAnotherParentsChild()
    {
        // Act
        var results = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var manager = services.GetRequiredService<AccessGrantManager>();
            var outcomes = new List<bool>();

            foreach (var id in ForeignIdentifiers())
            {
                outcomes.Add((await manager.AddAsync(AccessGrantPrincipalType.Role, "Administrator", id, ["Administrator"])).Succeeded);
            }

            return outcomes;
        });
        var grants = await ListAllGrantsAsync(TenantHierarchyFixture.FirmA);

        // Assert
        Assert.All(results, Assert.False);
        Assert.DoesNotContain(grants, grant => ForeignIdentifiers().Contains(grant.ChildEntryId));
    }

    [Fact]
    public async Task AccessGrants_InOneParent_CannotRemoveAnotherParentsGrant()
    {
        // Arrange: firm B's own grants, which firm A must not be able to remove by their identifiers.
        var victimGrants = await ListAllGrantsAsync(TenantHierarchyFixture.FirmB);
        Assert.NotEmpty(victimGrants);

        // Act
        var results = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmA, async services =>
        {
            var manager = services.GetRequiredService<AccessGrantManager>();
            var outcomes = new List<bool>();

            foreach (var grant in victimGrants)
            {
                outcomes.Add((await manager.RemoveAsync(grant.GrantId)).Succeeded);
            }

            return outcomes;
        });

        // Assert
        Assert.All(results, Assert.False);
        Assert.Equal(
            victimGrants.Select(grant => grant.GrantId).Order(),
            (await ListAllGrantsAsync(TenantHierarchyFixture.FirmB)).Select(grant => grant.GrantId).Order());
    }

    [Fact]
    public async Task DelegatedAccess_OneParentCannotIssueACodeForAnotherParentOrItsChild()
    {
        // Act: firm A's administrator asks for codes to firm B, to its child, and to them by every identifier.
        var callbacks = new List<string>();

        foreach (var target in ForeignIdentifiers().Append(TenantHierarchyFixture.FirmB).Append(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmB).TenantId))
        {
            var (code, callback) = await DelegatedAccessHelper.IssueCodeAsync(
                _fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, target, DelegatedAccessTokens.CreateToken());

            if (code is not null)
            {
                callbacks.Add(callback);
            }
        }

        // Assert
        Assert.Empty(callbacks);
    }

    [Fact]
    public async Task DelegatedAccess_ACodeOfTheOtherParent_CannotBeRedeemedInThisParentsChild()
    {
        // Arrange: firm B's administrator gets a valid code for firm B's own child.
        var verifier = DelegatedAccessTokens.CreateToken();
        var (code, _) = await DelegatedAccessHelper.IssueCodeAsync(
            _fixture.Host, TenantHierarchyFixture.FirmB, TenantHierarchyFixture.Bob, _fixture.BusinessFour.TenantId, verifier);
        Assert.NotNull(code);

        // Act: the code is presented to firm A's child, with the right verifier.
        var redemption = await DelegatedAccessHelper.RedeemAsync(_fixture.Host, _fixture.BusinessOne.TenantName, code, verifier);

        // Assert
        Assert.Null(redemption);
    }

    [Fact]
    public async Task DelegatedSessions_OfOneParent_AreUnknownToTheOtherParentsChildren_AndCannotBeEndedByThem()
    {
        // Arrange: Alice of firm A works in her firm's child.
        var redemption = await DelegatedAccessHelper.EnterAsync(_fixture.Host, TenantHierarchyFixture.FirmA, TenantHierarchyFixture.Alice, _fixture.BusinessOne);

        // Act: firm B's child presents Alice's session, then tries to end it.
        var seenByTheOtherChild = await _fixture.Host.InTenantAsync(_fixture.BusinessFour.TenantName, services =>
            services.GetRequiredService<ITenantHierarchyBroker>().ValidateSessionAsync(redemption.SessionId));
        await _fixture.Host.InTenantAsync(_fixture.BusinessFour.TenantName, services =>
            services.GetRequiredService<ITenantHierarchyBroker>().EndSessionAsync(redemption.SessionId, DelegatedSessionRules.ChildSignOut));
        var stillActive = await _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, services =>
            services.GetRequiredService<ITenantHierarchyBroker>().ValidateSessionAsync(redemption.SessionId));

        // Assert
        Assert.False(seenByTheOtherChild.IsActive);
        Assert.True(stillActive.IsActive);
    }

    // The shell host, between the two hierarchies.

    [Theory]
    [InlineData(TenantHierarchyFixture.FirmB)]
    [InlineData(TenantHierarchyFixture.FirmA)]
    public async Task ShellHost_FromEitherParent_NeitherListsNorFindsTheOtherHierarchy(string parent)
    {
        // Arrange
        var other = parent == TenantHierarchyFixture.FirmA ? TenantHierarchyFixture.FirmB : TenantHierarchyFixture.FirmA;
        var otherChildren = parent == TenantHierarchyFixture.FirmA
            ? new[] { _fixture.BusinessFour.TenantName }
            : [_fixture.BusinessOne.TenantName, _fixture.BusinessTwo.TenantName];

        // Act
        var (listed, found) = await _fixture.Host.InTenantAsync(parent, services =>
        {
            var shellHost = services.GetRequiredService<IShellHost>();
            var names = shellHost.GetAllSettings().Select(settings => settings.Name).ToList();
            var visible = otherChildren.Append(other).Where(name => shellHost.TryGetSettings(name, out _)).ToList();

            return Task.FromResult((names, visible));
        });

        // Assert
        Assert.DoesNotContain(other, listed);
        Assert.All(otherChildren, child => Assert.DoesNotContain(child, listed));
        Assert.Empty(found);
    }

    [Theory]
    [InlineData(TenantHierarchyFixture.FirmA)]
    [InlineData("business1")]
    public async Task ShellHost_FromTheOtherParentsChild_CannotOpenThisParentOrItsChild(string target)
    {
        // Arrange
        var settings = _fixture.Host.GetSettings(target == "business1" ? _fixture.BusinessOne.TenantName : target);

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(_fixture.BusinessFour.TenantName, async services =>
        {
            await services.GetRequiredService<IShellHost>().GetScopeAsync(settings);
        }));
        var found = await _fixture.Host.InTenantAsync(_fixture.BusinessFour.TenantName, services =>
            Task.FromResult(services.GetRequiredService<IShellHost>().TryGetSettings(settings.Name, out _)));

        // Assert
        Assert.IsType<TenantHierarchyAccessDeniedException>(exception);
        Assert.False(found);
    }

    [Fact]
    public async Task ShellHost_FromOneParentsChild_CannotOpenTheOtherParentOrItsChild()
    {
        // Act
        var exceptions = new List<Exception>();

        foreach (var target in new[] { TenantHierarchyFixture.FirmB, _fixture.BusinessFour.TenantName })
        {
            var settings = _fixture.Host.GetSettings(target);
            exceptions.Add(await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(_fixture.BusinessOne.TenantName, async services =>
            {
                await services.GetRequiredService<IShellHost>().GetScopeAsync(settings);
            })));
        }

        // Assert
        Assert.All(exceptions, exception => Assert.IsType<TenantHierarchyAccessDeniedException>(exception));
    }

    // The admin of a parent, over HTTP.

    [Fact]
    public async Task AdminPages_OfOneParent_ShowNothingOfAnotherParentsChild_ByAnyIdentifier()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        var statuses = new List<string>();

        // Act
        foreach (var id in ForeignIdentifiers())
        {
            foreach (var page in new[] { "edit", "features", "access", "remove" })
            {
                var response = await browser.GetAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/{id}/{page}");
                var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

                if (response.StatusCode != HttpStatusCode.NotFound || html.Contains("Business Four", StringComparison.Ordinal))
                {
                    statuses.Add($"{page} {id}: {(int)response.StatusCode}");
                }
            }
        }

        // Assert
        Assert.Empty(statuses);
    }

    [Fact]
    public async Task AdminLists_OfOneParent_NeverShowAnotherParentsChild()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var pages = new List<string>();

        foreach (var path in new[]
        {
            "/Admin/children",
            "/Admin/children?Options.Search=Business%20Four",
            "/Admin/children?Options.Search=business4",
            "/Admin/children/access",
            "/Admin/children/switch",
            "/delegated-access/switch",
            "/Admin/AuditTrail?q=category:TenantHierarchy",
            $"/Admin/AuditTrail/{_fixture.BusinessFour.EntryId}",
        })
        {
            var response = await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}{path}");
            pages.Add($"{path} {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");
        }

        // Assert: the searches echo what was typed in their search box, so only the other child's name and address
        // count, and only outside that box.
        Assert.All(pages, page =>
        {
            var shown = page.Replace("value=\"Business Four\"", string.Empty, StringComparison.Ordinal)
                .Replace("value=\"business4\"", string.Empty, StringComparison.Ordinal);

            Assert.DoesNotContain("Business Four", shown, StringComparison.Ordinal);
            Assert.DoesNotContain("business4.firmb", shown, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task AdminActions_OfOneParent_CannotChangeAnotherParentsChild_ByAnyIdentifier()
    {
        // Arrange
        var before = await SnapshotBusinessFourAsync();
        using var browser = await SignInAsAliceAsync();
        var token = await browser.GetAntiforgeryTokenAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children");
        var accepted = new List<string>();

        // Act
        foreach (var id in ForeignIdentifiers())
        {
            var posts = new Dictionary<string, IEnumerable<KeyValuePair<string, string>>>
            {
                [$"/Admin/children/{id}/suspend"] = Fields(token),
                [$"/Admin/children/{id}/resume"] = Fields(token),
                [$"/Admin/children/{id}/reload"] = Fields(token),
                [$"/Admin/children/{id}/restore"] = Fields(token),
                [$"/Admin/children/{id}/discard"] = Fields(token),
                [$"/Admin/children/{id}/dismiss"] = Fields(token),
                [$"/Admin/children/{id}/retry"] = Fields(token),
                [$"/Admin/children/{id}/remove"] = Fields(token, ("ConfirmName", "Business Four")),
                [$"/Admin/children/{id}/features"] = Fields(token, ("featureIds", ProbeFeature), ("enable", "true")),
                [$"/Admin/children/{id}/edit"] = Fields(token, ("DisplayName", "Taken Over"), ("Slug", "business4")),
                [$"/delegated-access/favorite/{id}"] = Fields(token),
            };

            foreach (var (path, fields) in posts)
            {
                var response = await browser.PostAsync($"{TenantHierarchyFixture.FirmAAddress}{path}", fields);

                // A refusal is a 404, or a redirect back to the list with an error; it never changes firm B's child,
                // which the snapshot below checks.
                if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.Redirect or HttpStatusCode.BadRequest))
                {
                    accepted.Add($"{path}: {(int)response.StatusCode}");
                }
            }
        }

        // Assert
        Assert.Empty(accepted);
        await AssertBusinessFourUnchangedAsync(before);
    }

    [Theory]
    [InlineData("Suspend")]
    [InlineData("Remove")]
    [InlineData("Reload")]
    public async Task BulkActions_OfOneParent_IgnoreAnotherParentsChildren(string action)
    {
        // Arrange
        var before = await SnapshotBusinessFourAsync();
        using var browser = await SignInAsAliceAsync();
        var token = await browser.GetAntiforgeryTokenAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children");
        var fields = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("submit.BulkAction", "BulkAction"),
            new("Options.BulkAction", action),
        };
        fields.AddRange(ForeignIdentifiers().Select(id => new KeyValuePair<string, string>("itemIds", id)));

        // Act
        await browser.PostAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children", fields);

        // Assert
        await AssertBusinessFourUnchangedAsync(before);
    }

    [Fact]
    public async Task AccessActions_OfOneParent_CannotGrantInOrRemoveFromAnotherParent()
    {
        // Arrange
        var victimGrants = await ListAllGrantsAsync(TenantHierarchyFixture.FirmB);
        using var browser = await SignInAsAliceAsync();
        var token = await browser.GetAntiforgeryTokenAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/access");

        // Act
        foreach (var id in ForeignIdentifiers())
        {
            await browser.PostAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/access/add?id={Uri.EscapeDataString(id)}", Fields(token,
                ("PrincipalType", "Role"), ("PrincipalRole", "Administrator"), ("SelectedChildRoles", "Administrator")));
        }

        foreach (var grant in victimGrants)
        {
            await browser.PostAsync($"{TenantHierarchyFixture.FirmAAddress}/Admin/children/access/{grant.GrantId}/remove", Fields(token));
        }

        // Assert
        Assert.DoesNotContain(await ListAllGrantsAsync(TenantHierarchyFixture.FirmA), grant => ForeignIdentifiers().Contains(grant.ChildEntryId));
        Assert.Equal(
            victimGrants.Select(grant => grant.GrantId).Order(),
            (await ListAllGrantsAsync(TenantHierarchyFixture.FirmB)).Select(grant => grant.GrantId).Order());
    }

    [Fact]
    public async Task Authorize_InOneParent_ForAnotherParentOrItsChild_SendsTheBrowserNowhere()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();
        var challenge = DelegatedAccessTokens.CreateCodeChallenge(DelegatedAccessTokens.CreateToken());
        var redirects = new List<string>();

        // Act
        foreach (var clientId in ForeignIdentifiers().Append(_fixture.Host.GetSettings(TenantHierarchyFixture.FirmB).TenantId))
        {
            var response = await browser.GetAsync(
                $"{TenantHierarchyFixture.FirmAAddress}/delegated-access/authorize?client_id={Uri.EscapeDataString(clientId)}&state=x&code_challenge={challenge}&code_challenge_method=S256");

            if (response.Headers.Location is not null)
            {
                redirects.Add(response.Headers.Location.ToString());
            }
        }

        // Assert
        Assert.Empty(redirects);
    }

    [Fact]
    public async Task OpenChild_OfAnotherParent_ByAnyIdentifier_NeverReachesIt()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act + Assert
        foreach (var id in ForeignIdentifiers())
        {
            await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{Uri.EscapeDataString(id)}");

            Assert.DoesNotContain(browser.LastHops, hop => hop.Host == "business4.firmb.localhost");
            Assert.False(browser.HasCookie("business4.firmb.localhost", "orchauth_"));
        }
    }

    // Sign-in between the two hierarchies.

    [Fact]
    public async Task SignInToOneParent_DoesNotSignInToTheOtherParentOrItsChild()
    {
        // Arrange
        using var browser = await SignInAsAliceAsync();

        // Act
        var otherParent = await browser.GetAsync($"{FirmBAddress}/Admin");
        var otherChild = await browser.GetAsync($"{BusinessFourAddress}/Admin");

        // Assert
        AssertSentToLogin(otherParent);
        AssertSentToLogin(otherChild);
    }

    [Fact]
    public async Task UsersOfOneParent_CannotSignInToTheOtherParent()
    {
        // Arrange
        using var browser = _fixture.Host.CreateBrowser();

        // Act: Alice's user name and password exist only in firm A.
        await browser.SignInAsync(FirmBAddress, TenantHierarchyFixture.Alice, _fixture.Password);
        var admin = await browser.GetAsync($"{FirmBAddress}/Admin");

        // Assert
        Assert.False(browser.HasCookie("firmb.localhost", "orchauth_"));
        AssertSentToLogin(admin);
    }

    [Fact]
    public async Task ACookieOfOneParent_CopiedToTheOtherParent_DoesNotSignIn()
    {
        // Arrange: Alice's sign-in cookie of firm A, renamed to firm B's cookie name and sent to firm B.
        using var browser = await SignInAsAliceAsync();
        var stolen = browser.GetCookies("firma.localhost", "orchauth_").Single();

        using var attacker = _fixture.Host.CreateBrowser();
        attacker.SetCookie("firmb.localhost", stolen.Key, stolen.Value);
        attacker.SetCookie("firmb.localhost", $"orchauth_{TenantHierarchyFixture.FirmB}", stolen.Value);

        // Act
        var admin = await attacker.GetAsync($"{FirmBAddress}/Admin");

        // Assert
        AssertSentToLogin(admin);
    }

    [Fact]
    public async Task AChildSessionCookie_CopiedToTheOtherParentsChild_DoesNotSignIn()
    {
        // Arrange: Alice opens firm A's child, and her session cookie there is copied to firm B's child.
        using var browser = await SignInAsAliceAsync();
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");
        var stolen = browser.GetCookies("business1.firma.localhost", "orchauth_").Single();

        using var attacker = _fixture.Host.CreateBrowser();
        attacker.SetCookie("business4.firmb.localhost", stolen.Key, stolen.Value);
        attacker.SetCookie("business4.firmb.localhost", $"orchauth_{_fixture.BusinessFour.TenantName}", stolen.Value);

        // Act
        var admin = await attacker.GetAsync($"{BusinessFourAddress}/Admin");

        // Assert
        AssertSentToLogin(admin);
    }

    [Fact]
    public async Task EnteringTheOtherParentsChild_WithOnlyThisParentsSignIn_EndsAtTheOtherParentsLogin()
    {
        // Arrange: Alice is signed in to firm A and to firm A's child.
        using var browser = await SignInAsAliceAsync();
        await browser.NavigateAsync($"{TenantHierarchyFixture.FirmAAddress}/delegated-access/open/{_fixture.BusinessOne.EntryId}");

        // Act: she goes to firm B's child, whose entry asks firm B, not firm A, who she is.
        var response = await browser.NavigateAsync($"{BusinessFourAddress}/delegated-access/enter");
        var last = browser.LastHops[browser.LastHops.Count - 1];

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("firmb.localhost", last.Host);
        Assert.Contains("/Login", last.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(browser.LastHops, hop => hop.Host == "firma.localhost");
        Assert.False(browser.HasCookie("business4.firmb.localhost", "orchauth_"));
    }

    // Outbound requests between the two hierarchies.

    [Theory]
    [InlineData(TenantHierarchyFixture.FirmA, FirmBAddress)]
    [InlineData(TenantHierarchyFixture.FirmA, BusinessFourAddress)]
    [InlineData(TenantHierarchyFixture.FirmB, TenantHierarchyFixture.FirmAAddress)]
    [InlineData(TenantHierarchyFixture.FirmB, BusinessOneAddress)]
    public async Task OutboundRequest_FromOneParent_ToTheOtherHierarchy_IsRefused(string from, string to)
    {
        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(from, async services =>
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
            await client.GetAsync($"{to}/");
        }));

        // Assert: refused because the host is a tenant's, before its address is even looked up.
        Assert.IsType<HttpRequestException>(exception);
        Assert.Contains("tenant of this application", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("business1", FirmBAddress)]
    [InlineData("business1", BusinessFourAddress)]
    [InlineData("business4", TenantHierarchyFixture.FirmAAddress)]
    [InlineData("business4", BusinessOneAddress)]
    public async Task OutboundRequest_FromOneParentsChild_ToTheOtherHierarchy_IsRefused(string from, string to)
    {
        // Arrange
        var tenant = from == "business1" ? _fixture.BusinessOne.TenantName : _fixture.BusinessFour.TenantName;

        // Act
        var exception = await Record.ExceptionAsync(() => _fixture.Host.InTenantAsync(tenant, async services =>
        {
            var client = services.GetRequiredService<IHttpClientFactory>().CreateClient();
            await client.GetAsync($"{to}/");
        }));

        // Assert: refused because the host is a tenant's, before its address is even looked up.
        Assert.IsType<HttpRequestException>(exception);
        Assert.Contains("tenant of this application", exception.Message, StringComparison.Ordinal);
    }

    // The control: the same doors work for the parent that owns the child, so the refusals above are real.

    [Fact]
    public async Task TheOwningParent_StillReachesItsChild_ThroughTheSameDoors()
    {
        // Act
        var (child, features) = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmB, async services =>
        {
            var broker = services.GetRequiredService<ITenantHierarchyBroker>();

            return (await broker.GetChildAsync(_fixture.BusinessFour.EntryId), await broker.GetChildFeaturesAsync(_fixture.BusinessFour.EntryId));
        });

        using var browser = _fixture.Host.CreateBrowser();
        await browser.SignInAsync(FirmBAddress, TenantHierarchyFixture.Bob, _fixture.Password);
        var edit = await browser.GetAsync($"{FirmBAddress}/Admin/children/{_fixture.BusinessFour.EntryId}/edit");

        // Assert
        Assert.NotNull(child);
        Assert.NotEmpty(features);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    private IEnumerable<string> ForeignIdentifiers()
        => [_fixture.BusinessFour.EntryId, _fixture.BusinessFour.TenantId, _fixture.BusinessFour.TenantName];

    private static async Task<bool> SucceededAsync(Func<Task<TenantHierarchyResult>> attempt)
    {
        try
        {
            return (await attempt()).Succeeded;
        }
        catch (TenantHierarchyAccessDeniedException)
        {
            return false;
        }
    }

    private async Task<BusinessFourSnapshot> SnapshotBusinessFourAsync()
    {
        var (entry, features) = await _fixture.Host.InTenantAsync(TenantHierarchyFixture.FirmB, async services =>
        {
            var found = await services.GetRequiredService<ChildTenantEntryStore>().FindByEntryIdAsync(_fixture.BusinessFour.EntryId);
            var enabled = (await services.GetRequiredService<ITenantHierarchyBroker>().GetChildFeaturesAsync(_fixture.BusinessFour.EntryId))
                .Where(feature => feature.IsEnabled)
                .Select(feature => feature.Id)
                .Order()
                .ToArray();

            return (found, enabled);
        });
        var settings = _fixture.Host.GetSettings(_fixture.BusinessFour.TenantName);

        return new BusinessFourSnapshot(entry.DisplayName, entry.Slug, entry.Status, settings.State, settings.RequestUrlHost, features);
    }

    private async Task AssertBusinessFourUnchangedAsync(BusinessFourSnapshot before)
    {
        var after = await SnapshotBusinessFourAsync();

        Assert.Equal(before.DisplayName, after.DisplayName);
        Assert.Equal(before.Slug, after.Slug);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.State, after.State);
        Assert.Equal(before.Host, after.Host);
        Assert.Equal(before.EnabledFeatures, after.EnabledFeatures);
        Assert.True(_fixture.Host.GetSettings(_fixture.BusinessFour.TenantName).IsRunning());
    }

    private Task<List<AccessGrant>> ListAllGrantsAsync(string parent)
        => _fixture.Host.InTenantAsync(parent, async services =>
        {
            var store = services.GetRequiredService<AccessGrantStore>();
            var grants = (await store.ListParentWideAsync()).ToList();

            foreach (var id in ForeignIdentifiers().Append(_fixture.BusinessOne.EntryId).Append(_fixture.BusinessTwo.EntryId))
            {
                grants.AddRange(await store.ListForChildAsync(id));
            }

            return grants;
        });

    private async Task<TestBrowser> SignInAsAliceAsync()
    {
        var browser = _fixture.Host.CreateBrowser();
        var response = await browser.SignInAsync(TenantHierarchyFixture.FirmAAddress, TenantHierarchyFixture.Alice, _fixture.Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.True(browser.HasCookie("firma.localhost", "orchauth_"), "The parent sign-in did not set its cookie.");

        return browser;
    }

    private static List<KeyValuePair<string, string>> Fields(string token, params (string Name, string Value)[] fields)
    {
        var list = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token) };
        list.AddRange(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));

        return list;
    }

    private static void AssertSentToLogin(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Login", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private sealed record BusinessFourSnapshot(
        string DisplayName,
        string Slug,
        ChildTenantStatus Status,
        TenantState State,
        string Host,
        string[] EnabledFeatures);
}
