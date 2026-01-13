using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class ListContentFieldsTool : AIFunction
{
    public const string TheName = "listContentFieldDefinitions";

    private readonly ContentMetadataService _contentMetadataService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;

    public ListContentFieldsTool(
        ContentMetadataService contentMetadataService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _contentMetadataService = contentMetadataService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override string Name => TheName;

    public override string Description => "Retrieves the available content fields which can be used to create content parts.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.ViewContentTypes))
        {
            return "You do not have permission to view content types.";
        }

        var fieldTypes = await _contentMetadataService.GetFieldsAsync();

        return JsonSerializer.Serialize(fieldTypes.Select(fieldType => fieldType.Name));
    }
}
