using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.Core;
using CrestApps.OrchardCore.AI.Chat.Interactions.Models;
using CrestApps.OrchardCore.AI.Chat.Interactions.Services;
using CrestApps.OrchardCore.AI.Chat.Interactions.ViewModels;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Controllers;

/// <summary>
/// The AI Profile Builder: sessions in which an author role-plays a conversation, gives feedback on replies, and
/// has the instructions rewritten until the assistant behaves as intended.
/// </summary>
[Feature(ChatInteractionsConstants.Feature.ProfileBuilder)]
[Admin("ai/profile-builder/{action}/{id?}", "ProfileBuilder{action}")]
public sealed class ProfileBuilderController : Controller
{
    private readonly IProfileBuilderSessionStore _sessionStore;
    private readonly ProfileBuilderService _builderService;
    private readonly ICatalogManager<ChatInteraction> _interactionManager;
    private readonly IAIProfileManager _profileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IDisplayManager<ChatInteraction> _interactionDisplayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileBuilderController"/> class.
    /// </summary>
    public ProfileBuilderController(
        IProfileBuilderSessionStore sessionStore,
        ProfileBuilderService builderService,
        ICatalogManager<ChatInteraction> interactionManager,
        IAIProfileManager profileManager,
        IAuthorizationService authorizationService,
        IDisplayManager<ChatInteraction> interactionDisplayManager,
        IUpdateModelAccessor updateModelAccessor,
        INotifier notifier,
        IHtmlLocalizer<ProfileBuilderController> htmlLocalizer,
        IStringLocalizer<ProfileBuilderController> stringLocalizer)
    {
        _sessionStore = sessionStore;
        _builderService = builderService;
        _interactionManager = interactionManager;
        _profileManager = profileManager;
        _authorizationService = authorizationService;
        _interactionDisplayManager = interactionDisplayManager;
        _updateModelAccessor = updateModelAccessor;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    /// <summary>
    /// Lists the current user's builder sessions.
    /// </summary>
    [Admin("ai/profile-builder", "ProfileBuilderIndex")]
    public async Task<IActionResult> Index()
    {
        if (!await CanUseBuilderAsync())
        {
            return Forbid();
        }

        var sessions = await _sessionStore.ListAsync(GetUserId());
        var interactions = new Dictionary<string, ChatInteraction>(StringComparer.Ordinal);

        foreach (var session in sessions)
        {
            var interaction = await _interactionManager.FindByIdAsync(session.ChatInteractionId);

            if (interaction is not null)
            {
                interactions[interaction.ItemId] = interaction;
            }
        }

        var profiles = await _profileManager.GetAsync(AIProfileType.Chat);

        var model = new ProfileBuilderIndexViewModel
        {
            Sessions = sessions
                .Select(session => new ProfileBuilderSessionEntry
                {
                    Session = session,
                    Title = interactions.TryGetValue(session.ChatInteractionId, out var interaction) && !string.IsNullOrWhiteSpace(interaction.Title)
                        ? interaction.Title
                        : S["Untitled"],
                })
                .ToArray(),
            Profiles = profiles
                .OrderBy(profile => profile.DisplayText ?? profile.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(profile => new SelectListItem(profile.DisplayText ?? profile.Name, profile.ItemId))
                .ToArray(),
        };

        return View(model);
    }

    /// <summary>
    /// Starts a new session, empty or from an existing AI profile.
    /// </summary>
    /// <param name="profileId">The AI profile to start from, or empty to start empty.</param>
    [HttpPost]
    public async Task<IActionResult> New(string profileId)
    {
        if (!await CanUseBuilderAsync())
        {
            return Forbid();
        }

        AIProfile profile = null;

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            profile = await _profileManager.FindByIdAsync(profileId);

            if (profile is null)
            {
                await _notifier.ErrorAsync(H["The AI profile could not be found."]);

                return RedirectToAction(nameof(Index));
            }
        }

        var (session, _) = await _builderService.CreateAsync(GetUserId(), User.Identity?.Name, title: null, profile);

        return RedirectToAction(nameof(Session), new { id = session.ItemId });
    }

    /// <summary>
    /// Shows a session: the settings panel, the role-play chat and the feedback panel.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    public async Task<IActionResult> Session(string id)
    {
        var (session, interaction, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        if (_builderService.CaptureManualChanges(session, interaction))
        {
            await _builderService.SaveAsync(session);
        }

        // The chat shape reads these to show the feedback buttons on each reply and to word the empty chat as a
        // role-play rather than a question. The person played is not always a customer, so the wording is neutral.
        HttpContext.Features.Set(new ChatInteractionChatClientOverrides
        {
            MessageTemplate = ProfileBuilderClientTemplates.MessageTemplate,
            PlaceholderHeading = S["Play the person the AI will talk to, and see how it responds."],
            PromptPlaceholder = S["Write a message as the person the AI is talking to"],
        });

        // The page lays out the editor's settings and chat zones itself, beside the feedback panel, and leaves out
        // the chat interaction header, whose actions belong to the Chat Interactions list.
        var editor = await _interactionDisplayManager.BuildEditorAsync(interaction, _updateModelAccessor.ModelUpdater, isNew: false);

        var profile = string.IsNullOrEmpty(session.ProfileId) ? null : await _profileManager.FindByIdAsync(session.ProfileId);

        var model = new ProfileBuilderSessionViewModel
        {
            Session = session,
            Interaction = interaction,
            DisplayName = string.IsNullOrWhiteSpace(interaction.Title) ? S["Untitled"] : interaction.Title,
            Editor = editor,
            Profile = profile,
        };

        return View(model);
    }

    /// <summary>
    /// Deletes a session with its conversation.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Delete(string id)
    {
        var (session, _, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        await _builderService.DeleteAsync(session);
        await _notifier.SuccessAsync(H["The session has been deleted."]);

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Returns the session's state for the feedback panel.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    [HttpGet]
    public async Task<IActionResult> State(string id)
    {
        var (session, interaction, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        if (_builderService.CaptureManualChanges(session, interaction))
        {
            await _builderService.SaveAsync(session);
        }

        return Json(await BuildStateAsync(session, interaction));
    }

    /// <summary>
    /// Records feedback on a reply and rewrites the instructions from it.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="request">The feedback.</param>
    [HttpPost]
    public async Task<IActionResult> Feedback(string id, [FromBody] ProfileBuilderFeedbackRequest request)
    {
        var (session, interaction, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        if (request is null)
        {
            return BadRequest();
        }

        var outcome = await _builderService.SubmitFeedbackAsync(session, interaction, request, HttpContext.RequestAborted);

        if (!outcome.Succeeded)
        {
            return Json(new { succeeded = false, error = outcome.Error });
        }

        return Json(new
        {
            succeeded = true,
            regenerateMessage = outcome.RegenerateMessage,
            revisionSummary = outcome.Revision?.Summary,
            state = await BuildStateAsync(session, interaction),
        });
    }

    /// <summary>
    /// Makes an earlier revision current again.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="number">The revision number.</param>
    [HttpPost]
    public async Task<IActionResult> Restore(string id, int number)
    {
        var (session, interaction, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        var revision = await _builderService.RestoreAsync(session, interaction, number, HttpContext.RequestAborted);

        if (revision is null)
        {
            return Json(new { succeeded = false, error = S["The revision could not be found."].Value });
        }

        return Json(new { succeeded = true, state = await BuildStateAsync(session, interaction) });
    }

    /// <summary>
    /// Replays every situation the author gave feedback on against the current instructions.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    [HttpPost]
    public async Task<IActionResult> Replay(string id)
    {
        var (session, interaction, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        await _builderService.ReplayAsync(session, interaction, HttpContext.RequestAborted);

        return Json(new { succeeded = true, state = await BuildStateAsync(session, interaction) });
    }

    /// <summary>
    /// Saves the session's own settings: the opening instruction and the coach deployment.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="request">The settings.</param>
    [HttpPost]
    public async Task<IActionResult> Settings(string id, [FromBody] ProfileBuilderSessionSettingsRequest request)
    {
        var (session, interaction, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        if (request is null)
        {
            return BadRequest();
        }

        // Record the instructions as they were first, so the change to the opening instruction is its own revision.
        _builderService.CaptureManualChanges(session, interaction);

        session.InitialPrompt = string.IsNullOrWhiteSpace(request.InitialPrompt) ? null : request.InitialPrompt.Trim();
        session.CoachDeploymentName = string.IsNullOrWhiteSpace(request.CoachDeploymentName) ? null : request.CoachDeploymentName.Trim();

        _builderService.CaptureManualChanges(session, interaction);
        await _builderService.SaveAsync(session);

        return Json(new { succeeded = true, state = await BuildStateAsync(session, interaction) });
    }

    /// <summary>
    /// Saves the session as a new AI profile, or onto the profile it is linked to.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="request">Where to save, and the new profile's name and title.</param>
    [HttpPost]
    public async Task<IActionResult> SaveProfile(string id, [FromBody] ProfileBuilderSaveProfileRequest request)
    {
        var (session, interaction, denied) = await LoadAsync(id);

        if (denied is not null)
        {
            return denied;
        }

        if (request is null)
        {
            return BadRequest();
        }

        AIProfile profile;
        var isNew = !request.UpdateExisting;

        if (isNew)
        {
            profile = await _profileManager.NewAsync();
            profile.Type = AIProfileType.Chat;
            profile.Name = request.Name?.Trim();
            profile.DisplayText = request.DisplayText?.Trim();

            var errors = new List<string>();

            if (string.IsNullOrEmpty(profile.Name))
            {
                errors.Add(S["Technical name is required."]);
            }

            if (string.IsNullOrEmpty(profile.DisplayText))
            {
                errors.Add(S["Title is required."]);
            }

            if (errors.Count > 0)
            {
                return Json(new { succeeded = false, error = string.Join(' ', errors) });
            }
        }
        else
        {
            profile = string.IsNullOrEmpty(session.ProfileId) ? null : await _profileManager.FindByIdAsync(session.ProfileId);

            if (profile is null)
            {
                return Json(new { succeeded = false, error = S["The linked AI profile no longer exists. Save as a new profile instead."].Value });
            }
        }

        ProfileBuilderProfileMapper.CopyToProfile(interaction, session, profile);

        var validation = await _profileManager.ValidateAsync(profile);

        if (!validation.Succeeded)
        {
            return Json(new { succeeded = false, error = string.Join(' ', validation.Errors.Select(error => error.ErrorMessage)) });
        }

        if (isNew)
        {
            await _profileManager.CreateAsync(profile);
        }
        else
        {
            await _profileManager.UpdateAsync(profile);
        }

        session.ProfileId = profile.ItemId;
        await _builderService.SaveAsync(session);

        await _notifier.SuccessAsync(isNew
            ? H["The AI profile <em>{0}</em> has been created from the builder session.", profile.DisplayText]
            : H["The AI profile <em>{0}</em> has been updated from the builder session.", profile.DisplayText]);

        return Json(new
        {
            succeeded = true,
            redirectUrl = Url.Action("Edit", "Profiles", new { area = "CrestApps.OrchardCore.AI", id = profile.ItemId }),
        });
    }

    private async Task<object> BuildStateAsync(ProfileBuilderSession session, ChatInteraction interaction)
    {
        var conversation = await _builderService.GetConversationAsync(interaction.ItemId);
        var settings = ProfileBuilderService.ReadSettings(interaction, session);

        return new
        {
            sessionId = session.ItemId,
            interactionId = interaction.ItemId,
            profileId = session.ProfileId,
            coachDeploymentName = session.CoachDeploymentName,
            settings,
            conversation = conversation.Select(prompt => new
            {
                id = prompt.ItemId,
                role = prompt.Role.Value,
                text = prompt.Text,
            }),
            feedback = session.Feedback.Select(entry => new
            {
                entry.Id,
                kind = entry.Kind.ToString(),
                entry.PromptId,
                entry.CustomerMessage,
                entry.AssistantReply,
                entry.Comment,
                entry.WasRegenerated,
                entry.RevisionNumber,
                entry.CreatedUtc,
            }),
            revisions = session.Revisions.Select(revision => new
            {
                revision.Number,
                source = revision.Source.ToString(),
                revision.Summary,
                revision.FeedbackId,
                revision.CreatedUtc,
                revision.Settings,
            }),
            lastReplay = session.LastReplay is null ? null : new
            {
                session.LastReplay.RevisionNumber,
                session.LastReplay.CreatedUtc,
                cases = session.LastReplay.Cases.Select(replayCase => new
                {
                    replayCase.FeedbackId,
                    replayCase.CustomerMessage,
                    replayCase.Reply,
                    verdict = replayCase.Verdict.ToString(),
                    replayCase.Reason,
                }),
            },
        };
    }

    private async Task<(ProfileBuilderSession Session, ChatInteraction Interaction, IActionResult Denied)> LoadAsync(string id)
    {
        if (!await CanUseBuilderAsync())
        {
            return (null, null, Forbid());
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return (null, null, NotFound());
        }

        var session = await _sessionStore.FindByIdAsync(id);

        if (session is null)
        {
            return (null, null, NotFound());
        }

        var interaction = await _interactionManager.FindByIdAsync(session.ChatInteractionId);

        if (interaction is null)
        {
            return (null, null, NotFound());
        }

        // The session is reached through its conversation, so whoever may not use that interaction may not use it.
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions, interaction))
        {
            return (null, null, Forbid());
        }

        return (session, interaction, null);
    }

    private async Task<bool> CanUseBuilderAsync()
        => await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles) &&
            await _authorizationService.AuthorizeAsync(User, AIPermissions.EditChatInteractions);

    private string GetUserId()
        => User.FindFirstValue(ClaimTypes.NameIdentifier);
}

/// <summary>
/// The session's own settings, saved from the feedback panel.
/// </summary>
public sealed class ProfileBuilderSessionSettingsRequest
{
    /// <summary>
    /// Gets or sets the opening instruction.
    /// </summary>
    public string InitialPrompt { get; set; }

    /// <summary>
    /// Gets or sets the deployment that rewrites the instructions.
    /// </summary>
    public string CoachDeploymentName { get; set; }
}

/// <summary>
/// Where to save a session as an AI profile.
/// </summary>
public sealed class ProfileBuilderSaveProfileRequest
{
    /// <summary>
    /// Gets or sets a value indicating whether the linked profile is updated rather than a new one created.
    /// </summary>
    public bool UpdateExisting { get; set; }

    /// <summary>
    /// Gets or sets the technical name of a new profile.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title of a new profile.
    /// </summary>
    public string DisplayText { get; set; }
}
