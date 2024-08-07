using Microsoft.Extensions.Localization;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentFields.Settings;
using OrchardCore.ContentFields.ViewModels;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Users.Drivers;

public sealed class DisplayNameUserPickerFieldDisplayDriver : ContentFieldDisplayDriver<UserPickerField>
{
    private readonly ISession _session;
    private readonly IDisplayNameProvider _displayNameProvider;

    internal readonly IStringLocalizer S;

    public DisplayNameUserPickerFieldDisplayDriver(
        ISession session,
        IDisplayNameProvider displayNameProvider,
        IStringLocalizer<DisplayNameUserPickerFieldDisplayDriver> stringLocalizer)
    {
        _session = session;
        _displayNameProvider = displayNameProvider;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(UserPickerField field, BuildFieldDisplayContext context)
    {
        return Initialize<DisplayUserPickerFieldViewModel>(GetDisplayShapeType(context), model =>
        {
            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
        }).Location("Detail", "Content")
        .Location("Summary", "Content");
    }

    public override IDisplayResult Edit(UserPickerField field, BuildFieldEditorContext context)
    {
        return Initialize<EditUserPickerFieldViewModel>(GetEditorShapeType(context), async model =>
        {
            model.UserIds = string.Join(',', field.UserIds);

            model.Field = field;
            model.Part = context.ContentPart;
            model.PartFieldDefinition = context.PartFieldDefinition;
            model.TypePartDefinition = context.TypePartDefinition;

            if (field.UserIds.Length > 0)
            {
                var users = (await _session.Query<User, UserIndex>().Where(x => x.UserId.IsIn(field.UserIds)).ListAsync())
                    .OrderBy(o => Array.FindIndex(field.UserIds, x => string.Equals(o.UserId, x, StringComparison.OrdinalIgnoreCase)));

                foreach (var user in users)
                {
                    var displayName = await _displayNameProvider.GetAsync(user);

                    model.SelectedUsers.Add(new VueMultiselectUserViewModel
                    {
                        Id = user.UserId,
                        DisplayText = displayName,
                        IsEnabled = user.IsEnabled
                    });
                }
            }
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(UserPickerField field, UpdateFieldEditorContext context)
    {
        var viewModel = new EditUserPickerFieldViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix, f => f.UserIds);

        field.UserIds = viewModel.UserIds == null
            ? []
            : viewModel.UserIds.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var settings = context.PartFieldDefinition.GetSettings<UserPickerFieldSettings>();

        if (settings.Required && field.UserIds.Length == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.UserIds), S["The {0} field is required.", context.PartFieldDefinition.DisplayName()]);
        }

        if (!settings.Multiple && field.UserIds.Length > 1)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(field.UserIds), S["The {0} field cannot contain multiple items.", context.PartFieldDefinition.DisplayName()]);
        }

        var users = await _session.Query<User, UserIndex>().Where(x => x.UserId.IsIn(field.UserIds)).ListAsync();
        field.SetUserNames(users.Select(t => t.UserName).ToArray());

        return Edit(field, context);
    }
}
