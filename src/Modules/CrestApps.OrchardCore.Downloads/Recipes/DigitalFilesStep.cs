using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Downloads.Services;
using OrchardCore.ContentManagement;
using OrchardCore.FileStorage;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Downloads.Recipes;

/// <summary>
/// The "DigitalFiles" step: puts files in the private store and on products' Digital files, so a sample store can
/// deliver what it sells. Each file is given as <c>Base64</c> bytes or <c>Text</c>. A product that already has a
/// file of that name keeps it, so running the recipe again adds nothing.
/// </summary>
internal sealed class DigitalFilesStep : NamedRecipeStepHandler
{
    public const string RecipeStepName = "DigitalFiles";

    private const int MaxFileBytes = 10 * 1024 * 1024;

    private readonly IContentManager _contentManager;
    private readonly IDigitalFileStore _fileStore;

    public DigitalFilesStep(IContentManager contentManager, IDigitalFileStore fileStore)
        : base(RecipeStepName)
    {
        _contentManager = contentManager;
        _fileStore = fileStore;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        foreach (var token in (context.Step["Files"] as JsonArray ?? []).OfType<JsonObject>())
        {
            var productId = token["ProductId"]?.GetValue<string>();
            var fileName = DigitalFileStore.Sanitize(token["FileName"]?.GetValue<string>());
            var product = string.IsNullOrEmpty(productId) ? null : await _contentManager.GetAsync(productId, VersionOptions.Latest);

            if (product is null)
            {
                context.Errors.Add($"The '{RecipeStepName}' step names the product '{productId}', which doesn't exist.");

                continue;
            }

            var part = product.GetOrCreate<DigitalFilesPart>();

            if (part.Files.Any(file => string.Equals(file.FileName, fileName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            byte[] bytes;

            try
            {
                bytes = token["Base64"]?.GetValue<string>() is { Length: > 0 } base64
                    ? Convert.FromBase64String(base64)
                    : Encoding.UTF8.GetBytes(token["Text"]?.GetValue<string>() ?? string.Empty);
            }
            catch (FormatException)
            {
                context.Errors.Add($"The file '{fileName}' in the '{RecipeStepName}' step isn't valid base64.");

                continue;
            }

            if (bytes.Length == 0 || bytes.Length > MaxFileBytes)
            {
                context.Errors.Add($"The file '{fileName}' in the '{RecipeStepName}' step is empty or larger than 10 MB.");

                continue;
            }

            StoredDigitalFile stored;

            try
            {
                using var stream = new MemoryStream(bytes);
                stored = await _fileStore.SaveAsync(fileName, stream);
            }
            catch (FileStoreException ex)
            {
                context.Errors.Add($"The file '{fileName}' in the '{RecipeStepName}' step was refused: {ex.Message}");

                continue;
            }

            product.Alter<DigitalFilesPart>(digitalFiles => digitalFiles.Files.Add(new DigitalFile { FileId = stored.FileId, FileName = stored.FileName, Size = stored.Size }));

            await _contentManager.UpdateAsync(product);
        }
    }
}
