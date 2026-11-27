using CrestApps.OrchardCore.Learning.Packages.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Packages.Drivers;

/// <summary>
/// The editor of the import page of a learning package item: the package file. The file itself is posted next to the
/// editor, under the name <c>package</c>.
/// </summary>
internal sealed class PackageUploadDisplayDriver : DisplayDriver<PackageUploadViewModel>
{
    internal readonly IStringLocalizer S;

    public PackageUploadDisplayDriver(IStringLocalizer<PackageUploadDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(PackageUploadViewModel model, BuildEditorContext context)
    {
        var title = model.AcceptCmi5 ? S["SCORM or cmi5 Package"] : S["SCORM Package"];

        return Initialize<PackageUploadViewModel>("PackageUpload_Edit", m => Populate(model, m))
            .Location(l => l.Zone("Content", "1").Card(title, "10"));
    }

    // The page posts a file next to the editor and no field of the view model, so there is nothing to bind.
    /// <inheritdoc />
    public override Task<IDisplayResult> UpdateAsync(PackageUploadViewModel model, UpdateEditorContext context)
        => Task.FromResult(Edit(model, context));

    // The file keeps the name the controller validates.
    /// <inheritdoc />
    protected override void BuildPrefix(PackageUploadViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(PackageUploadViewModel source, PackageUploadViewModel model)
    {
        model.ContentItemId = source.ContentItemId;
        model.Title = source.Title;
        model.Part = source.Part;
        model.MaxUploadMb = source.MaxUploadMb;
        model.AcceptCmi5 = source.AcceptCmi5;
        model.HasContentHost = source.HasContentHost;
        model.CanManageSettings = source.CanManageSettings;
    }
}
