using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Navigation;
using OrchardCore.Routing;

namespace CrestApps.OrchardCore.AI.Controllers;

public sealed class ProfilesController : Controller
{
    private const string _optionsSearch = "Options.Search";

    private readonly IAIProfileManager _profileManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IDisplayManager<AIProfile> _profileDisplayManager;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public ProfilesController(
        IAIProfileManager profileManager,
        IAuthorizationService authorizationService,
        IUpdateModelAccessor updateModelAccessor,
        IDisplayManager<AIProfile> profileDisplayManager,
        INotifier notifier,
        IHtmlLocalizer<ProfilesController> htmlLocalizer,
        IStringLocalizer<ProfilesController> stringLocalizer)
    {
        _profileManager = profileManager;
        _authorizationService = authorizationService;
        _updateModelAccessor = updateModelAccessor;
        _profileDisplayManager = profileDisplayManager;
        _notifier = notifier;
        H = htmlLocalizer;
        S = stringLocalizer;
    }

    [Admin("ai/profiles", "AIProfilesIndex")]
    public async Task<IActionResult> Index(
        CatalogEntryOptions options,
        PagerParameters pagerParameters,
        [FromServices] IOptions<PagerOptions> pagerOptions,
        [FromServices] IShapeFactory shapeFactory)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var pager = new Pager(pagerParameters, pagerOptions.Value.GetPageSize());

        var result = await _profileManager.PageAsync(pager.Page, pager.PageSize, new AIProfileQueryContext
        {
            Name = options.Search,
            IsListableOnly = true,
        });

        // Maintain previous route data when generating page links.
        var routeData = new RouteData();

        if (!string.IsNullOrEmpty(options.Search))
        {
            routeData.Values.TryAdd(_optionsSearch, options.Search);
        }

        var viewModel = new ListCatalogEntryViewModel<CatalogEntryViewModel<AIProfile>>
        {
            Models = [],
            Options = options,
            Pager = await shapeFactory.PagerAsync(pager, result.Count, routeData),
        };

        foreach (var model in result.Entries)
        {
            viewModel.Models.Add(new CatalogEntryViewModel<AIProfile>
            {
                Model = model,
                Shape = await _profileDisplayManager.BuildDisplayAsync(model, _updateModelAccessor.ModelUpdater, "SummaryAdmin")
            });
        }

        viewModel.Options.BulkActions =
        [
            new SelectListItem(S["Delete"], nameof(CatalogEntryAction.Remove)),
        ];

        return View(viewModel);
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.Filter")]
    [Admin("ai/profiles", "AIProfilesIndex")]
    public async Task<ActionResult> IndexFilterPost(ListCatalogEntryViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        return RedirectToAction(nameof(Index), new RouteValueDictionary
        {
            { _optionsSearch, model.Options?.Search },
        });
    }

    [Admin("ai/profile/create", "AIProfilesCreate")]
    public async Task<ActionResult> Create([FromQuery] string templateId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.NewAsync();

        if (profile == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new profile."]);

            return RedirectToAction(nameof(Index));
        }

        if (!string.IsNullOrEmpty(templateId))
        {
            var templateManager = HttpContext.RequestServices.GetService<IAIProfileTemplateManager>();
            var template = templateManager != null ? await templateManager.FindByIdAsync(templateId) : null;

            if (template != null)
            {
                await ApplyTemplateToProfileAsync(profile, template);
            }
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Profile"],
            Editor = await _profileDisplayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Create))]
    [Admin("ai/profile/create", "AIProfilesCreate")]
    public async Task<ActionResult> CreatePost()
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.NewAsync();

        if (profile == null)
        {
            await _notifier.ErrorAsync(H["Unable to create a new profile."]);

            return RedirectToAction(nameof(Index));
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = S["New Profile"],
            Editor = await _profileDisplayManager.UpdateEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: true),
        };

        if (ModelState.IsValid)
        {
            await _profileManager.CreateAsync(profile);

            await _notifier.SuccessAsync(H["Profile has been created successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [Admin("ai/profile/edit/{id}", "AIProfilesEdit")]
    public async Task<ActionResult> Edit(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = profile.Name,
            Editor = await _profileDisplayManager.BuildEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        return View(model);
    }

    [HttpPost]
    [ActionName(nameof(Edit))]
    [Admin("ai/profile/edit/{id}", "AIProfilesEdit")]
    public async Task<ActionResult> EditPost(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var model = new EditCatalogEntryViewModel
        {
            DisplayName = profile.DisplayText,
            Editor = await _profileDisplayManager.UpdateEditorAsync(profile, _updateModelAccessor.ModelUpdater, isNew: false),
        };

        if (ModelState.IsValid)
        {
            await _profileManager.UpdateAsync(profile);

            await _notifier.SuccessAsync(H["Profile has been updated successfully."]);

            return RedirectToAction(nameof(Index));
        }

        return View(model);
    }

    [HttpPost]
    [Admin("ai/profile/delete/{id}", "AIProfilesDelete")]
    public async Task<IActionResult> Delete(string id)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        var profile = await _profileManager.FindByIdAsync(id);

        if (profile == null)
        {
            return NotFound();
        }

        var settings = profile.GetSettings<AIProfileSettings>();

        if (!settings.IsRemovable)
        {
            await _notifier.ErrorAsync(H["The profile cannot be removed."]);

            return RedirectToAction(nameof(Index));
        }

        if (await _profileManager.DeleteAsync(profile))
        {
            await _notifier.SuccessAsync(H["Profile has been deleted successfully."]);
        }
        else
        {
            await _notifier.ErrorAsync(H["Unable to remove the profile."]);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ActionName(nameof(Index))]
    [FormValueRequired("submit.BulkAction")]
    [Admin("ai/profiles", "AIProfilesIndex")]

    public async Task<ActionResult> IndexPost(CatalogEntryOptions options, IEnumerable<string> itemIds)
    {
        if (!await _authorizationService.AuthorizeAsync(User, AIPermissions.ManageAIProfiles))
        {
            return Forbid();
        }

        if (itemIds?.Count() > 0)
        {
            switch (options.BulkAction)
            {
                case CatalogEntryAction.None:
                    break;
                case CatalogEntryAction.Remove:
                    var counter = 0;
                    var itemIdsSet = itemIds.ToHashSet();
                    var allProfiles = await _profileManager.GetAllAsync();

                    foreach (var profile in allProfiles.Where(p => itemIdsSet.Contains(p.ItemId)))
                    {
                        var settings = profile.GetSettings<AIProfileSettings>();

                        if (!settings.IsRemovable)
                        {
                            continue;
                        }

                        if (await _profileManager.DeleteAsync(profile))
                        {
                            counter++;
                        }
                    }
                    if (counter == 0)
                    {
                        await _notifier.WarningAsync(H["No profiles were removed."]);
                    }
                    else
                    {
                        await _notifier.SuccessAsync(H.Plural(counter, "1 profile has been removed successfully.", "{0} profiles have been removed successfully."));
                    }
                    break;
                default:
                    return BadRequest();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ApplyTemplateToProfileAsync(AIProfile profile, AIProfileTemplate template)
    {
        // Copy all extensibility properties from the template to the profile.
        // This transfers settings stored by external module drivers (e.g., analytics,
        // data extraction, post-session, MCP connections, data sources, etc.).
        // Template drivers store settings in template.Properties (via Entity.As<T>/Put<T>).
        // Profile drivers may read from either profile.Properties or profile.Settings,
        // so we copy to both to ensure all drivers can read the applied values.
        if (template.Properties != null)
        {
            foreach (var property in template.Properties)
            {
                // Skip source-specific metadata keys; they are handled below.
                if (string.Equals(property.Key, nameof(ProfileTemplateMetadata), StringComparison.Ordinal) ||
                    string.Equals(property.Key, nameof(SystemPromptTemplateMetadata), StringComparison.Ordinal))
                {
                    continue;
                }

                profile.Properties[property.Key] = property.Value?.DeepClone();
                profile.Settings[property.Key] = property.Value?.DeepClone();
            }
        }

        if (!string.IsNullOrEmpty(template.DisplayText))
        {
            profile.DisplayText = template.DisplayText;
        }

        if (!string.IsNullOrEmpty(template.Name))
        {
            profile.Name = template.Name;
        }

        var templateMetadata = template.As<ProfileTemplateMetadata>();

        if (templateMetadata.ProfileType.HasValue)
        {
            profile.Type = templateMetadata.ProfileType.Value;
        }

        if (!string.IsNullOrEmpty(templateMetadata.ChatDeploymentName))
        {
            profile.ChatDeploymentName = templateMetadata.ChatDeploymentName;
        }

        if (!string.IsNullOrEmpty(templateMetadata.UtilityDeploymentName))
        {
            profile.UtilityDeploymentName = templateMetadata.UtilityDeploymentName;
        }

        if (!string.IsNullOrEmpty(templateMetadata.OrchestratorName))
        {
            profile.OrchestratorName = templateMetadata.OrchestratorName;
        }

        if (templateMetadata.TitleType.HasValue)
        {
            profile.TitleType = templateMetadata.TitleType;
        }

        if (!string.IsNullOrEmpty(templateMetadata.WelcomeMessage))
        {
            profile.WelcomeMessage = templateMetadata.WelcomeMessage;
        }

        if (!string.IsNullOrEmpty(templateMetadata.PromptSubject))
        {
            profile.PromptSubject = templateMetadata.PromptSubject;
        }

        if (!string.IsNullOrEmpty(templateMetadata.PromptTemplate))
        {
            profile.PromptTemplate = templateMetadata.PromptTemplate;
        }

        var metadata = profile.As<AIProfileMetadata>();

        if (!string.IsNullOrEmpty(templateMetadata.SystemMessage))
        {
            metadata.SystemMessage = templateMetadata.SystemMessage;
        }

        if (templateMetadata.Temperature.HasValue)
        {
            metadata.Temperature = templateMetadata.Temperature;
        }

        if (templateMetadata.TopP.HasValue)
        {
            metadata.TopP = templateMetadata.TopP;
        }

        if (templateMetadata.FrequencyPenalty.HasValue)
        {
            metadata.FrequencyPenalty = templateMetadata.FrequencyPenalty;
        }

        if (templateMetadata.PresencePenalty.HasValue)
        {
            metadata.PresencePenalty = templateMetadata.PresencePenalty;
        }

        if (templateMetadata.MaxOutputTokens.HasValue)
        {
            metadata.MaxTokens = templateMetadata.MaxOutputTokens;
        }

        if (templateMetadata.PastMessagesCount.HasValue)
        {
            metadata.PastMessagesCount = templateMetadata.PastMessagesCount;
        }

        profile.Put(metadata);

        if (templateMetadata.ToolNames != null && templateMetadata.ToolNames.Length > 0)
        {
            var toolMetadata = profile.As<FunctionInvocationMetadata>();
            toolMetadata.Names = [.. templateMetadata.ToolNames];
            profile.Put(toolMetadata);
        }

        if (templateMetadata.AgentNames != null && templateMetadata.AgentNames.Length > 0)
        {
            var agentMetadata = profile.As<AgentInvocationMetadata>();
            agentMetadata.Names = [.. templateMetadata.AgentNames];
            profile.Put(agentMetadata);
        }

        if (!string.IsNullOrEmpty(templateMetadata.Description))
        {
            profile.Description = templateMetadata.Description;
        }

        if (templateMetadata.AgentAvailability.HasValue)
        {
            var agentMeta = profile.As<AgentMetadata>() ?? new AgentMetadata();
            agentMeta.Availability = templateMetadata.AgentAvailability.Value;
            profile.Put(agentMeta);
        }

        // Clone documents from the template to the profile when the Documents feature is enabled.
        await CloneTemplateDocumentsAsync(profile, template);
    }

    private async Task CloneTemplateDocumentsAsync(AIProfile profile, AIProfileTemplate template)
    {
        var documentsMetadata = template.As<DocumentsMetadata>();

        if (documentsMetadata?.Documents == null || documentsMetadata.Documents.Count == 0)
        {
            return;
        }

        var documentStore = HttpContext.RequestServices.GetService<IAIDocumentStore>();
        var chunkStore = HttpContext.RequestServices.GetService<IAIDocumentChunkStore>();

        if (documentStore == null || chunkStore == null)
        {
            return;
        }

        var profileDocuments = new DocumentsMetadata
        {
            DocumentTopN = documentsMetadata.DocumentTopN,
            Documents = [],
        };

        foreach (var docInfo in documentsMetadata.Documents)
        {
            var templateDocument = await documentStore.FindByIdAsync(docInfo.DocumentId);

            if (templateDocument == null)
            {
                continue;
            }

            // Clone the document record with a new ID and profile reference.
            var clonedDocument = new AIDocument
            {
                ItemId = IdGenerator.GenerateId(),
                ReferenceId = profile.ItemId,
                ReferenceType = AIConstants.DocumentReferenceTypes.Profile,
                FileName = templateDocument.FileName,
                ContentType = templateDocument.ContentType,
                FileSize = templateDocument.FileSize,
                UploadedUtc = templateDocument.UploadedUtc,
            };

            await documentStore.CreateAsync(clonedDocument);

            // Clone associated chunks with new IDs and updated references.
            var templateChunks = await chunkStore.GetChunksByAIDocumentIdAsync(templateDocument.ItemId);

            foreach (var templateChunk in templateChunks)
            {
                var clonedChunk = new AIDocumentChunk
                {
                    ItemId = IdGenerator.GenerateId(),
                    AIDocumentId = clonedDocument.ItemId,
                    ReferenceId = profile.ItemId,
                    ReferenceType = AIConstants.DocumentReferenceTypes.Profile,
                    Content = templateChunk.Content,
                    Embedding = templateChunk.Embedding,
                    Index = templateChunk.Index,
                };

                await chunkStore.CreateAsync(clonedChunk);
            }

            profileDocuments.Documents.Add(new ChatDocumentInfo
            {
                DocumentId = clonedDocument.ItemId,
                FileName = clonedDocument.FileName,
                ContentType = clonedDocument.ContentType,
                FileSize = clonedDocument.FileSize,
            });
        }

        if (profileDocuments.Documents.Count > 0)
        {
            profile.Put(profileDocuments);

            // Also update Settings for drivers that read from profile.Settings.
            var serialized = JsonSerializer.SerializeToNode(profileDocuments);
            if (serialized is JsonObject jsonObj)
            {
                profile.Settings[nameof(DocumentsMetadata)] = jsonObj;
            }
        }
    }
}
