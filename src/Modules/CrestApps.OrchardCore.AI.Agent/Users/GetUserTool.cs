using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.AI.Agent.Users;

internal sealed class GetUserTool : AIFunction
{
    public const string TheName = "getUserInfo";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly UserManager<IUser> _userManager;

    public GetUserTool(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        UserManager<IUser> userManager)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _userManager = userManager;
        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
           """
            {
              "type": "object",
              "properties": {
                "userId": {
                  "type": "string",
                  "description": "The userId to get user info for."
                },
                "username": {
                  "type": "string",
                  "description": "The username to get user info for."
                },
                "email": {
                  "type": "string",
                  "description": "The email to get user info for."
                }
              },
              "additionalProperties": false,
              "required": []
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Gets users information.";

    public override JsonElement JsonSchema { get; }

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, UsersPermissions.ViewUsers))
        {
            return "The current user does not have permission to view users";
        }

        var userId = arguments.GetFirstValueOrDefault<string>("userId");
        var username = arguments.GetFirstValueOrDefault<string>("username");
        var email = arguments.GetFirstValueOrDefault<string>("email");

        var hasUserId = !string.IsNullOrEmpty(userId);
        var hasUsername = !string.IsNullOrEmpty(username);
        var hasEmail = !string.IsNullOrEmpty(email);

        if (!hasUserId && !hasUsername && !hasEmail)
        {
            return "You must provide at least one of the following arguments: userId, username, or email.";
        }

        IUser user = null;

        if (hasUserId)
        {
            user = await _userManager.FindByIdAsync(userId);
        }
        else if (hasUsername)
        {
            user = await _userManager.FindByNameAsync(username);
        }
        else if (hasEmail)
        {
            user = await _userManager.FindByEmailAsync(email);
        }

        if (user is null)
        {
            return "Unable to find a user with the provided arguments.";
        }

        if (user is User u)
        {
            return JsonSerializer.Serialize(u.AsAIObject());
        }

        return JsonSerializer.Serialize(user.UserName);
    }
}
