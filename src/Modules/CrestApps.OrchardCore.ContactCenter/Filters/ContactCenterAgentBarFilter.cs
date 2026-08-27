using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContactCenter.Filters;

/// <summary>
/// Injects the persistent docked agent bar into the admin chrome on every admin page for a signed-in agent.
/// The bar is the CRM-side bridge to the call router: it keeps a live Contact Center hub connection outside the
/// soft phone (which runs in its own window or the browser extension), so a work assignment reaches the agent
/// wherever they are in the CRM, pops the record, and drives disposition. It is registered unconditionally as
/// admin chrome rather than as a placeable content widget so a page can never omit it.
/// </summary>
public sealed class ContactCenterAgentBarFilter : IAsyncResultFilter
{
    private readonly ILayoutAccessor _layoutAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly IAuthorizationService _authorizationService;
    private readonly IContactCenterAgentBarBuilder _barBuilder;
    private readonly IResourceManager _resourceManager;
    private readonly AdminOptions _adminOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterAgentBarFilter"/> class.
    /// </summary>
    /// <param name="layoutAccessor">The layout accessor used to add the bar to the footer zone.</param>
    /// <param name="shapeFactory">The shape factory used to create the bar shape.</param>
    /// <param name="authorizationService">The authorization service used to gate the bar to agents.</param>
    /// <param name="barBuilder">The builder that assembles the bar configuration.</param>
    /// <param name="resourceManager">The resource manager used to register the bar's script and style.</param>
    /// <param name="adminOptions">The admin options used to detect admin pages.</param>
    public ContactCenterAgentBarFilter(
        ILayoutAccessor layoutAccessor,
        IShapeFactory shapeFactory,
        IAuthorizationService authorizationService,
        IContactCenterAgentBarBuilder barBuilder,
        IResourceManager resourceManager,
        IOptions<AdminOptions> adminOptions)
    {
        _layoutAccessor = layoutAccessor;
        _shapeFactory = shapeFactory;
        _authorizationService = authorizationService;
        _barBuilder = barBuilder;
        _resourceManager = resourceManager;
        _adminOptions = adminOptions.Value;
    }

    /// <inheritdoc/>
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // The bar renders into a full HTML view. A non-view result (a JSON payload, a file, a redirect) carries no
        // layout to inject into, and an anonymous request is not an agent.
        if (context.Result is not (ViewResult or PageResult) ||
            context.HttpContext.User.Identity?.IsAuthenticated != true ||
            !IsAdminPage(context))
        {
            await next();

            return;
        }

        if (!await _authorizationService.AuthorizeAsync(context.HttpContext.User, ContactCenterPermissions.SignIntoQueues))
        {
            await next();

            return;
        }

        var config = await _barBuilder.BuildAsync(context.HttpContext);

        _resourceManager.RegisterResource("stylesheet", "contact-center-agent-bar").AtHead();
        _resourceManager.RegisterResource("script", "contact-center-agent-bar").AtFoot();

        var shape = await _shapeFactory.CreateAsync("ContactCenterAgentBar");
        shape.Properties["Config"] = config;

        var layout = await _layoutAccessor.GetLayoutAsync();
        await layout.Zones["Footer"].AddAsync(shape, "998");

        await next();
    }

    private bool IsAdminPage(ResultExecutingContext context)
    {
        return context.HttpContext.Request.Path.StartsWithSegments('/' + _adminOptions.AdminUrlPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
