using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Controllers;

/// <summary>
/// Provides administration of Contact Center queues.
/// </summary>
[Admin]
[Feature(ContactCenterConstants.Feature.Queues)]
public sealed class QueuesController : Controller
{
    private readonly IActivityQueueManager _manager;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuesController"/> class.
    /// </summary>
    /// <param name="manager">The queue manager.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public QueuesController(
        IActivityQueueManager manager,
        IAuthorizationService authorizationService)
    {
        _manager = manager;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Lists the queues.
    /// </summary>
    /// <returns>The queues list view.</returns>
    [Admin("contact-center/queues", "ContactCenterQueuesIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        var queues = await _manager.GetAllAsync();

        return View(queues);
    }

    /// <summary>
    /// Displays the queue create form.
    /// </summary>
    /// <returns>The create view.</returns>
    [Admin("contact-center/queues/create", "ContactCenterQueuesCreate")]
    public async Task<IActionResult> Create()
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        return View(new QueueViewModel());
    }

    /// <summary>
    /// Persists a new queue.
    /// </summary>
    /// <param name="model">The submitted queue.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Create))]
    public async Task<IActionResult> CreatePost(QueueViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var queue = await _manager.NewAsync();
        Apply(queue, model);
        await _manager.CreateAsync(queue);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Displays the queue edit form.
    /// </summary>
    /// <param name="id">The queue identifier.</param>
    /// <returns>The edit view.</returns>
    [Admin("contact-center/queues/edit/{id}", "ContactCenterQueuesEdit")]
    public async Task<IActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        var queue = await _manager.FindByIdAsync(id);

        if (queue is null)
        {
            return NotFound();
        }

        return View(new QueueViewModel
        {
            Id = queue.ItemId,
            Name = queue.Name,
            Description = queue.Description,
            DefaultPriority = queue.DefaultPriority,
            SlaThresholdSeconds = queue.SlaThresholdSeconds,
            ReservationTimeoutSeconds = queue.ReservationTimeoutSeconds,
            RequiredSkills = ContactCenterFormHelpers.FormatList(queue.RequiredSkills),
            InboundChannelEndpointId = queue.InboundChannelEndpointId,
            Enabled = queue.Enabled,
        });
    }

    /// <summary>
    /// Persists changes to a queue.
    /// </summary>
    /// <param name="model">The submitted queue.</param>
    /// <returns>A redirect to the list or the form when invalid.</returns>
    [HttpPost]
    [ActionName(nameof(Edit))]
    public async Task<IActionResult> EditPost(QueueViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        var queue = await _manager.FindByIdAsync(model.Id);

        if (queue is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        Apply(queue, model);
        await _manager.UpdateAsync(queue);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Deletes a queue.
    /// </summary>
    /// <param name="id">The queue identifier.</param>
    /// <returns>A redirect to the list.</returns>
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ContactCenterPermissions.ManageQueues))
        {
            return Forbid();
        }

        var queue = await _manager.FindByIdAsync(id);

        if (queue is not null)
        {
            await _manager.DeleteAsync(queue);
        }

        return RedirectToAction(nameof(Index));
    }

    private static void Apply(Core.Models.ActivityQueue queue, QueueViewModel model)
    {
        queue.Name = model.Name;
        queue.Description = model.Description;
        queue.DefaultPriority = model.DefaultPriority;
        queue.SlaThresholdSeconds = model.SlaThresholdSeconds;
        queue.ReservationTimeoutSeconds = model.ReservationTimeoutSeconds;
        queue.RequiredSkills = ContactCenterFormHelpers.ParseList(model.RequiredSkills);
        queue.InboundChannelEndpointId = model.InboundChannelEndpointId;
        queue.Enabled = model.Enabled;
    }
}
