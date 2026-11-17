using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Agent.Contents;
using CrestApps.OrchardCore.Tests.Agent.Roles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Documents;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Environment.Extensions;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Json;
using OrchardCore.Localization;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Events;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using OrchardCore.Users.ViewModels;
using YesSql;
using YesSql.Filters.Query;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Agent.Users;

/// <summary>
/// Builds the services the user and role tools use: users and roles kept in memory behind the real user manager and
/// role manager, a user service that enables and disables, the filter parser of the Users admin page, the site
/// settings, the permissions of two features and the custom user settings service. Every sensitive field of the user
/// Jane holds a value that starts with <see cref="Canary"/>, so a test can tell that no tool result carries one.
/// </summary>
internal sealed class UserRoleToolHarness
{
    /// <summary>
    /// The text every sensitive value of the fixture starts with.
    /// </summary>
    public const string Canary = "CANARY";

    /// <summary>
    /// The ID of the site owner (the super user of the site settings).
    /// </summary>
    public const string OwnerId = "user-owner";

    /// <summary>
    /// The names of the roles of the fixture, with the casing a site shows.
    /// </summary>
    public static readonly string[] RoleNames = ["Administrator", "Anonymous", "Authenticated", "Editor", "Author", "Moderator"];

    private readonly Mock<ISite> _site = new();
    private readonly Mock<YSession> _session = new();
    private readonly Mock<IDocumentStore> _documentStore = new();

    public UserRoleToolHarness()
    {
        Caller = AddUser(AgentToolTestContext.UserId, AgentToolTestContext.UserName, "admin@contoso.com");
        Owner = AddUser(OwnerId, "owner", "owner@contoso.com");
        Root = AddUser("user-root", "root", "root@contoso.com", "Administrator");
        FormerAdministrator = AddUser("user-former", "former", "former@contoso.com", "Administrator");
        FormerAdministrator.IsEnabled = false;
        Sam = AddUser("user-sam", "sam", "sam@fabrikam.com");
        Sam.IsEnabled = false;

        Jane = AddUser("user-jane", "jane", "jane@contoso.com", "Editor");
        Jane.PhoneNumber = "+14155550100";
        Jane.PasswordHash = $"{Canary}-PASSWORD-HASH";
        Jane.SecurityStamp = $"{Canary}-SECURITY-STAMP";
        Jane.ResetToken = $"{Canary}-RESET-TOKEN";
        Jane.TwoFactorEnabled = true;
        Jane.IsLockoutEnabled = true;
        Jane.LockoutEndUtc = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Jane.AccessFailedCount = 5;
        Jane.UserTokens.Add(new UserToken { LoginProvider = "[AspNetUserStore]", Name = "AuthenticatorKey", Value = $"{Canary}-AUTHENTICATOR-KEY" });
        Jane.UserTokens.Add(new UserToken { LoginProvider = "[AspNetUserStore]", Name = "RecoveryCodes", Value = $"{Canary}-RECOVERY-CODE-1;{Canary}-RECOVERY-CODE-2" });
        Jane.UserClaims.Add(new UserClaim { ClaimType = "api-key", ClaimValue = $"{Canary}-USER-CLAIM" });
        Jane.LoginInfos.Add(new UserLoginInfo("Google", $"{Canary}-PROVIDER-KEY", "Google"));
        Jane.Properties["ExternalAccount"] = new JsonObject { ["AccessToken"] = $"{Canary}-PROPERTY-SECRET" };
        Jane.Properties["UserTimeZone"] = new JsonObject { ["TimeZoneId"] = "Europe/Paris" };
        Jane.Properties["UserLocalizationSettings"] = new JsonObject { ["Culture"] = "fr-FR" };
        Jane.Properties["UserProfile"] = JsonNode.Parse(
            """
            {
              "ContentItemId": "settings-1",
              "ContentType": "UserProfile",
              "Owner": "owner-1",
              "UserProfile": {
                "Department": {
                  "Text": "Sales"
                },
                "Office": {
                  "Text": "Paris"
                }
              }
            }
            """);

        AddRole("Administrator", "Holds every permission.");
        AddRole("Anonymous", "Every visitor.", "ViewContent");
        AddRole("Authenticated", "Every signed-in user.", "ViewOwnProfile");
        AddRole("Editor", "Edits content.", "EditContent", "RetiredPermission");
        AddRole("Author", "Writes content.");
        AddRole("Moderator", "Moderates users.", UsersPermissions.ManageUsers.Name);

        Permissions =
        [
            new Permission("ViewContent", LocalizationSource.Create("View content")),
            new Permission("ViewOwnProfile", LocalizationSource.Create("View own profile")),
            new Permission("EditContent", LocalizationSource.Create("Edit content")),
            new Permission("PublishContent", LocalizationSource.Create("Publish content")),
            UsersPermissions.ManageUsers,
            UsersPermissions.EditUsers,
            UsersPermissions.ViewUsers,
        ];

        _site.SetupGet(site => site.SuperUser).Returns(OwnerId);
        _site.Setup(site => site.GetOrCreate<LoginSettings>()).Returns(() => LoginSettings);
    }

    /// <summary>
    /// Gets or sets whether a user is signed in. The default is signed in, as <see cref="Caller"/>.
    /// </summary>
    public bool SignedIn { get; set; } = true;

    /// <summary>
    /// Gets or sets the rule that grants or refuses a permission on a resource. The default grants everything.
    /// </summary>
    public Func<Permission, object, bool> Authorize { get; set; } = (_, _) => true;

    /// <summary>
    /// Gets or sets the roles of the sign-in of the caller.
    /// </summary>
    public string[] CallerRoleClaims { get; set; } = [];

    /// <summary>
    /// Gets the account of the signed-in caller, who is not an administrator.
    /// </summary>
    public User Caller { get; }

    /// <summary>
    /// Gets the site owner, who holds no role.
    /// </summary>
    public User Owner { get; }

    /// <summary>
    /// Gets the only enabled user in the administrator role.
    /// </summary>
    public User Root { get; }

    /// <summary>
    /// Gets a disabled user in the administrator role.
    /// </summary>
    public User FormerAdministrator { get; }

    /// <summary>
    /// Gets an enabled editor whose sensitive fields all hold a canary value. She is locked out and has two-factor
    /// authentication on.
    /// </summary>
    public User Jane { get; }

    /// <summary>
    /// Gets a disabled user without a role, as a moderated registration is.
    /// </summary>
    public User Sam { get; }

    /// <summary>
    /// Gets the user store, which records what was stored and deleted.
    /// </summary>
    public FakeUserStore Store { get; } = new();

    /// <summary>
    /// Gets the role store and role service, which records what was created, updated and deleted.
    /// </summary>
    public FakeRoleStore Roles { get; } = new();

    /// <summary>
    /// Gets the IDs of the users the user service enabled, in order.
    /// </summary>
    public List<string> Enabled { get; } = [];

    /// <summary>
    /// Gets the IDs of the users the user service disabled, in order.
    /// </summary>
    public List<string> Disabled { get; } = [];

    /// <summary>
    /// Gets the permissions the enabled features provide.
    /// </summary>
    public List<Permission> Permissions { get; }

    /// <summary>
    /// Gets the options the list tool gave the query service of the Users admin page.
    /// </summary>
    public UserIndexOptions QueriedOptions { get; private set; }

    /// <summary>
    /// Gets or sets the users the query service of the Users admin page returns. The default is every user.
    /// </summary>
    public List<User> QueryResult { get; set; }

    /// <summary>
    /// Gets or sets whether the site requires two-factor authentication.
    /// </summary>
    public bool RequireTwoFactor { get; set; }

    /// <summary>
    /// Gets or sets the login settings of the site. The default lets the user name, the email and the phone number of
    /// a user be changed.
    /// </summary>
    public LoginSettings LoginSettings { get; set; } = new()
    {
        AllowChangingUsername = true,
        AllowChangingEmail = true,
        AllowChangingPhoneNumber = true,
    };

    /// <summary>
    /// Builds the services of a tool call.
    /// </summary>
    /// <returns>The service provider.</returns>
    public ServiceProvider Build()
    {
        var services = AgentToolTestContext.CreateServices(SignedIn, (permission, resource) => Authorize(permission, resource));

        var identityOptions = new IdentityOptions();
        identityOptions.User.RequireUniqueEmail = true;
        identityOptions.Tokens.ProviderMap[TokenOptions.DefaultProvider] = new TokenProviderDescriptor(typeof(CanaryTokenProvider))
        {
            ProviderInstance = new CanaryTokenProvider(),
        };

        var normalizer = new UpperInvariantLookupNormalizer();
        var errorDescriber = new IdentityErrorDescriber();
        var userManager = new UserManager<IUser>(
            Store,
            Options.Create(identityOptions),
            new PasswordHasher<IUser>(),
            [new UserValidator<IUser>()],
            [],
            normalizer,
            errorDescriber,
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<IUser>>.Instance);

        var roleManager = new RoleManager<IRole>(
            Roles,
            [new RoleValidator<IRole>()],
            normalizer,
            errorDescriber,
            NullLogger<RoleManager<IRole>>.Instance);

        services.AddSingleton(userManager);
        services.AddSingleton<IUserRoleStore<IUser>>(Store);
        services.AddSingleton(roleManager);
        services.AddSingleton<IRoleService>(Roles);
        services.AddSingleton(CreateUserService(userManager));
        services.AddSingleton(CreateSiteService());
        services.AddSingleton<IPhoneFormatValidator>(new DigitsPhoneFormatValidator());
        services.AddSingleton<ITwoFactorAuthenticationHandler>(new SiteTwoFactorHandler(this));
        services.AddSingleton<IPermissionGrantingService>(new NamePermissionGrantingService());
        services.AddSingleton<IPermissionProvider>(new FixedPermissionProvider(Permissions));
        services.AddSingleton(_session.Object);
        services.AddSingleton(_documentStore.Object);

        AddUsersAdminList(services);
        AddFeatures(services);
        AddCustomUserSettings(services);

        var provider = services.BuildServiceProvider();

        if (SignedIn && CallerRoleClaims.Length > 0)
        {
            var httpContext = provider.GetRequiredService<IHttpContextAccessor>().HttpContext;
            var identity = (ClaimsIdentity)httpContext.User.Identity;

            identity.AddClaims(CallerRoleClaims.Select(role => new Claim(identity.RoleClaimType, role)));
        }

        return provider;
    }

    /// <summary>
    /// Gets a stored role.
    /// </summary>
    /// <param name="roleName">The name of the role.</param>
    /// <returns>The role, or <see langword="null"/> when there is none.</returns>
    public Role GetRole(string roleName)
        => Roles.Roles.FirstOrDefault(role => string.Equals(role.RoleName, roleName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets the names of the permissions that are assigned to a stored role.
    /// </summary>
    /// <param name="roleName">The name of the role.</param>
    /// <returns>The permission names.</returns>
    public string[] GetPermissions(string roleName)
        => GetRole(roleName).RoleClaims.Where(claim => claim.ClaimType == Permission.ClaimType).Select(claim => claim.ClaimValue).ToArray();

    /// <summary>
    /// Asserts that a refused call stored, enabled, disabled and deleted no user, and created, updated and deleted no
    /// role.
    /// </summary>
    public void VerifyNothingChanged()
    {
        Assert.Empty(Store.Updated);
        Assert.Empty(Store.Deleted);
        Assert.Empty(Enabled);
        Assert.Empty(Disabled);
        Assert.Empty(Roles.Created);
        Assert.Empty(Roles.Updated);
        Assert.Empty(Roles.Deleted);
    }

    private User AddUser(string userId, string userName, string email, params string[] roleNames)
    {
        var user = new User
        {
            UserId = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = false,
            SecurityStamp = $"{Canary}-STAMP-{userId}",
            RoleNames = [.. roleNames],
        };

        Store.Users.Add(user);

        return user;
    }

    private void AddRole(string roleName, string description, params string[] permissionNames)
    {
        Roles.Roles.Add(new Role
        {
            RoleName = roleName,
            NormalizedRoleName = roleName.ToUpperInvariant(),
            RoleDescription = description,
            RoleClaims = [.. permissionNames.Select(RoleClaim.Create)],
        });
    }

    private IUserService CreateUserService(UserManager<IUser> userManager)
    {
        // The user service of Orchard Core changes the flag, stores the user and raises the user events.
        var userService = new Mock<IUserService>();

        userService
            .Setup(service => service.EnableAsync(It.IsAny<IUser>()))
            .Returns(async (IUser user) =>
            {
                ((User)user).IsEnabled = true;
                Enabled.Add(((User)user).UserId);

                return (await userManager.UpdateAsync(user)).Succeeded;
            });

        userService
            .Setup(service => service.DisableAsync(It.IsAny<IUser>()))
            .Returns(async (IUser user) =>
            {
                ((User)user).IsEnabled = false;
                Disabled.Add(((User)user).UserId);

                return (await userManager.UpdateAsync(user)).Succeeded;
            });

        return userService.Object;
    }

    private ISiteService CreateSiteService()
    {
        var siteService = new Mock<ISiteService>();
        siteService.Setup(service => service.GetSiteSettingsAsync()).ReturnsAsync(_site.Object);

        return siteService.Object;
    }

    private void AddUsersAdminList(ServiceCollection services)
    {
        // The parser is the one Orchard Core builds for the Users admin page; the query service returns the users
        // of the test and keeps the options it was given.
        var builder = new QueryEngineBuilder<User>();
        new DefaultUsersAdminListFilterProvider().Build(builder);

        var query = new Mock<IQuery<User>>();
        query.Setup(users => users.Skip(It.IsAny<int>())).Returns(query.Object);
        query.Setup(users => users.Take(It.IsAny<int>())).Returns(query.Object);
        query.Setup(users => users.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => (QueryResult ?? Store.Users).Count);
        query.Setup(users => users.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => QueryResult ?? Store.Users);

        var queryService = new Mock<IUsersAdminListQueryService>();
        queryService
            .Setup(service => service.QueryAsync(It.IsAny<UserIndexOptions>(), It.IsAny<IUpdateModel>()))
            .ReturnsAsync((UserIndexOptions options, IUpdateModel _) =>
            {
                QueriedOptions = options;

                return query.Object;
            });

        services.AddSingleton<IUsersAdminListFilterParser>(new DefaultUsersAdminListFilterParser(builder.Build()));
        services.AddSingleton(queryService.Object);
    }

    private static void AddFeatures(ServiceCollection services)
    {
        var feature = new Mock<IFeatureInfo>();
        feature.SetupGet(info => info.Id).Returns("OrchardCore.Contents");
        feature.SetupGet(info => info.Name).Returns("Contents");

        IEnumerable<IFeatureInfo> features = [feature.Object];

        var typeFeatureProvider = new Mock<ITypeFeatureProvider>();
        typeFeatureProvider.Setup(provider => provider.GetFeaturesForDependency(It.IsAny<Type>())).Returns(features);

        var shellFeaturesManager = new Mock<IShellFeaturesManager>();
        shellFeaturesManager.Setup(manager => manager.GetEnabledFeaturesAsync()).ReturnsAsync(features);

        services.AddSingleton(typeFeatureProvider.Object);
        services.AddSingleton(shellFeaturesManager.Object);
    }

    private void AddCustomUserSettings(ServiceCollection services)
    {
        IEnumerable<ContentTypeDefinition> definitions =
        [
            new ContentTypeDefinitionBuilder().WithName("UserProfile").Stereotype("CustomUserSettings").WithPart("UserProfile").Build(),
            new ContentTypeDefinitionBuilder().WithName("Article").WithPart("TitlePart").Build(),
        ];

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(definitions);
        contentDefinitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => definitions.FirstOrDefault(definition => string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase)));

        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.NewAsync(It.IsAny<string>()))
            .ReturnsAsync((string contentType) => new ContentItem { ContentType = contentType });

        contentManager
            .Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>()))
            .ReturnsAsync(() => SettingsValidation ?? new ContentValidateResult());

        services.AddSingleton(contentManager.Object);
        services.AddSingleton(contentDefinitionManager.Object);
        services.AddSingleton(new CustomUserSettingsService(contentManager.Object, contentDefinitionManager.Object, _session.Object));
        services.AddSingleton<IContentItemPayloadAssistanceService>(new AcceptingPayloadAssistanceService());
        services.AddSingleton(new ContentItemPreparationService(
            contentManager.Object,
            contentDefinitionManager.Object,
            [],
            Options.Create(new DocumentJsonSerializerOptions())));
    }

    /// <summary>
    /// Gets or sets what the content manager answers when it validates custom user settings. The default is valid.
    /// </summary>
    public ContentValidateResult SettingsValidation { get; set; }

    private sealed class CanaryTokenProvider : IUserTwoFactorTokenProvider<IUser>
    {
        private const string Token = $"{Canary}-CONFIRMATION-TOKEN";

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<IUser> manager, IUser user)
            => Task.FromResult(false);

        public Task<string> GenerateAsync(string purpose, UserManager<IUser> manager, IUser user)
            => Task.FromResult(Token);

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<IUser> manager, IUser user)
            => Task.FromResult(token == Token);
    }

    private sealed class DigitsPhoneFormatValidator : IPhoneFormatValidator
    {
        public bool IsValid(string phoneNumber)
            => phoneNumber.StartsWith('+') && phoneNumber.Skip(1).All(char.IsAsciiDigit);
    }

    private sealed class SiteTwoFactorHandler : ITwoFactorAuthenticationHandler
    {
        private readonly UserRoleToolHarness _harness;

        public SiteTwoFactorHandler(UserRoleToolHarness harness)
        {
            _harness = harness;
        }

        public Task<bool> IsRequiredAsync(IUser user)
            => Task.FromResult(_harness.RequireTwoFactor);
    }

    // Grants a permission to claims that name it or a permission that implies it, as Orchard Core does.
    private sealed class NamePermissionGrantingService : IPermissionGrantingService
    {
        public bool IsGranted(PermissionRequirement requirement, IEnumerable<Claim> claims)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Collect(requirement.Permission, names);

            return claims.Any(claim => claim.Type == Permission.ClaimType && names.Contains(claim.Value));
        }

        private static void Collect(Permission permission, HashSet<string> names)
        {
            if (permission is null || !names.Add(permission.Name))
            {
                return;
            }

            foreach (var impliedBy in permission.ImpliedBy ?? [])
            {
                Collect(impliedBy, names);
            }
        }
    }

    private sealed class FixedPermissionProvider : IPermissionProvider
    {
        private readonly List<Permission> _permissions;

        public FixedPermissionProvider(List<Permission> permissions)
        {
            _permissions = permissions;
        }

        public Task<IEnumerable<Permission>> GetPermissionsAsync()
            => Task.FromResult<IEnumerable<Permission>>(_permissions);

        public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
            => [];
    }

    private sealed class AcceptingPayloadAssistanceService : IContentItemPayloadAssistanceService
    {
        public ValueTask<ContentItemPayloadValidationResult> ValidateAsync(
            ContentTypeDefinition contentDefinition,
            JsonNode inputNode,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(ContentItemPayloadValidationResult.Success);

        public ValueTask<string> GetGuidanceAsync(ContentTypeDefinition contentDefinition, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(string.Empty);
    }
}
