using System.Text.Json;
using CrestApps.OrchardCore.Users.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Entities;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Media;
using OrchardCore.Media.Fields;
using OrchardCore.Media.Services;
using OrchardCore.Media.Settings;
using OrchardCore.Media.ViewModels;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class UserAvatarPartDisplayDriver : SectionDisplayDriver<User, UserAvatarPart>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly AttachedMediaFieldFileService _attachedMediaFieldFileService;
    private readonly MediaOptions _mediaOptions;
    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly UserAvatarOptions _userAvatarOptions;

    internal readonly IStringLocalizer S;

    public UserAvatarPartDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        AttachedMediaFieldFileService attachedMediaFieldFileService,
        IContentTypeProvider contentTypeProvider,
        IOptions<UserAvatarOptions> userAvatarOptions,
        IOptions<MediaOptions> mediaOptions,
        IStringLocalizer<UserAvatarPartDisplayDriver> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _attachedMediaFieldFileService = attachedMediaFieldFileService;
        _mediaOptions = mediaOptions.Value;
        _contentTypeProvider = contentTypeProvider;
        _userAvatarOptions = userAvatarOptions.Value;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(User user, UserAvatarPart part, BuildEditorContext context)
    {
        var itemPaths = part.Avatar?.Paths?.ToList().Select(p => new EditMediaFieldItemInfo { Path = p })
            .ToArray() ?? [];

        return Initialize<EditMediaFieldViewModel>("UserAvatarPart_Edit", model =>
        {
            part.Avatar ??= new MediaField();
            var settings = GetDefaultSettings();
            for (var i = 0; i < itemPaths.Length; i++)
            {
                if (settings.AllowMediaText && i < part.Avatar.MediaTexts?.Length)
                {
                    itemPaths[i].MediaText = part.Avatar.MediaTexts[i];
                }

                if (settings.AllowAnchors)
                {
                    var anchors = part.Avatar.GetAnchors();
                    if (anchors != null && i < anchors.Length)
                    {
                        itemPaths[i].Anchor = anchors[i];
                    }
                }

                var filenames = part.Avatar.GetAttachedFileNames();
                if (filenames != null && i < filenames.Length)
                {
                    itemPaths[i].AttachedFileName = filenames[i];
                }
            }

            var fieldName = nameof(UserAvatarPart.Avatar);
            var fieldDefinition = new ContentFieldDefinition(fieldName);

            model.Paths = JsonSerializer.Serialize(itemPaths, JOptions.CamelCase);
            model.TempUploadFolder = _attachedMediaFieldFileService.MediaFieldsTempSubFolder;
            model.Field = part.Avatar;
            model.Part = part;
            model.AllowedExtensions = settings.AllowedExtensions;
            model.PartFieldDefinition = new ContentPartFieldDefinition(fieldDefinition, fieldName, null)
            {
                PartDefinition = new ContentPartDefinition(nameof(UserAvatarPart)),
            };
        }).Location("Content:1.5")
        .RenderWhen(() => _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, MediaPermissions.ManageMedia));
    }

    public override async Task<IDisplayResult> UpdateAsync(User user, UserAvatarPart part, UpdateEditorContext context)
    {
        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, MediaPermissions.ManageMedia))
        {
            return null;
        }

        var model = new EditMediaFieldViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix, f => f.Paths);

        // Deserializing an empty string doesn't return an array
        var items = string.IsNullOrWhiteSpace(model.Paths)
            ? []
            : JsonSerializer.Deserialize<List<EditMediaFieldItemInfo>>(model.Paths, JOptions.CamelCase);

        part.Avatar ??= new MediaField();
        part.Avatar.Paths = items.Where(p => !p.IsRemoved).Select(p => p.Path).ToArray() ?? [];
        var field = part.Avatar;
        var settings = GetDefaultSettings();

        if (settings.AllowedExtensions?.Length > 0)
        {
            for (var i = 0; i < field.Paths.Length; i++)
            {
                var extension = Path.GetExtension(field.Paths[i]);

                if (!settings.AllowedExtensions.Contains(extension))
                {
                    context.Updater.ModelState.AddModelError(Prefix, nameof(model.Paths), S["Media extension is not allowed. Only media with '{0}' extensions are allowed.", string.Join(", ", settings.AllowedExtensions)]);
                }
            }
        }

        if (settings.Required && field.Paths.Length < 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Paths), S["An avatar is required."]);
        }

        if (field.Paths.Length > 1 && !settings.Multiple)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Paths), S["Selecting multiple avatars is forbidden."]);
        }

        if (settings.AllowMediaText)
        {
            field.MediaTexts = items.Select(t => t.MediaText).ToArray();
        }
        else
        {
            field.MediaTexts = [];
        }

        if (settings.AllowAnchors)
        {
            field.SetAnchors(items.Select(t => t.Anchor).ToArray());
        }
        else if (field.Content.ContainsKey("Anchors")) // Less well known properties should be self healing.
        {
            field.Content.Remove("Anchors");
        }

        return Edit(user, part, context);
    }

    private MediaFieldSettings GetDefaultSettings()
    {
        if (_mediaFieldSettings == null)
        {
            var extensions = new List<string>();

            foreach (var extension in _mediaOptions.AllowedFileExtensions)
            {
                if (_contentTypeProvider.TryGetContentType(extension, out var contentType) && contentType.StartsWith("image/"))
                {
                    extensions.Add(extension);
                }
            }

            _mediaFieldSettings = new MediaFieldSettings()
            {

                AllowedExtensions = extensions.ToArray(),
                AllowAnchors = true,
                Required = _userAvatarOptions.Required,
            };
        }

        return _mediaFieldSettings;
    }

    private MediaFieldSettings _mediaFieldSettings;
}
