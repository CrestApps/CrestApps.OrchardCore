using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AdminTheme.Models;
using CrestApps.OrchardCore.AdminTheme.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AdminTheme.Deployments;

/// <summary>
/// The names shared by the deployment step and the recipe step of the color schemes admins added.
/// </summary>
public static class AdminColorSchemesSteps
{
    public const string StepName = "AdminColorSchemes";

    public const string EntriesName = "Schemes";
}

/// <summary>
/// Exports every color scheme of the admin, the theme's defaults included.
/// </summary>
public sealed class AdminColorSchemesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AdminColorSchemesDeploymentStep>("Admin");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AdminColorSchemesDeploymentStep>("Admin color schemes");

    public override string Name => AdminColorSchemesSteps.StepName;

    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class AdminColorSchemesDeploymentSource : DeploymentSourceBase<AdminColorSchemesDeploymentStep>
{
    private readonly AdminColorSchemeStore _store;

    public AdminColorSchemesDeploymentSource(AdminColorSchemeStore store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(AdminColorSchemesDeploymentStep step, DeploymentPlanResult result)
    {
        var schemes = new JsonArray();

        foreach (var scheme in await _store.ListAsync())
        {
            var entry = new JsonObject
            {
                ["ItemId"] = scheme.Id,
                [nameof(AdminColorScheme.Name)] = scheme.Name,
                [nameof(AdminColorScheme.Position)] = scheme.Position,
                [nameof(AdminColorScheme.Primary)] = scheme.Primary,
                [nameof(AdminColorScheme.Deep)] = scheme.Deep,
                [nameof(AdminColorScheme.Ink)] = scheme.Ink,
                [nameof(AdminColorScheme.Accent)] = scheme.Accent,
                [nameof(AdminColorScheme.LightMenu)] = scheme.LightMenu,
            };

            // The optional settings only when they are set, so an export shows what was tuned.
            AddIfSet(entry, nameof(AdminColorScheme.Hue), scheme.Hue);
            AddIfSet(entry, nameof(AdminColorScheme.Saturation), scheme.Saturation);
            AddIfSet(entry, nameof(AdminColorScheme.OnPrimary), scheme.OnPrimary);
            AddIfSet(entry, nameof(AdminColorScheme.Page), scheme.Page);
            AddIfSet(entry, nameof(AdminColorScheme.Toolbar), scheme.Toolbar);
            AddIfSet(entry, nameof(AdminColorScheme.Tint), scheme.Tint);
            AddIfSet(entry, nameof(AdminColorScheme.TintDark), scheme.TintDark);

            schemes.Add(entry);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = AdminColorSchemesSteps.StepName,
            [AdminColorSchemesSteps.EntriesName] = schemes,
        });
    }

    private static void AddIfSet(JsonObject entry, string name, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            entry[name] = value;
        }
    }

    private static void AddIfSet(JsonObject entry, string name, double? value)
    {
        if (value is not null)
        {
            entry[name] = value.Value;
        }
    }
}

/// <summary>
/// Imports color schemes: a scheme whose ItemId matches a stored one replaces it, any other is added (with a new id
/// when it has none).
/// </summary>
internal sealed class AdminColorSchemesRecipeStep : NamedRecipeStepHandler
{
    private readonly AdminColorSchemeStore _store;

    internal readonly IStringLocalizer S;

    public AdminColorSchemesRecipeStep(AdminColorSchemeStore store, IStringLocalizer<AdminColorSchemesRecipeStep> stringLocalizer)
        : base(AdminColorSchemesSteps.StepName)
    {
        _store = store;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        if (context.Step[AdminColorSchemesSteps.EntriesName] is not JsonArray entries)
        {
            return;
        }

        var position = await _store.NextPositionAsync();

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var id = entry["ItemId"]?.GetValue<string>();

            if (string.IsNullOrEmpty(id))
            {
                id = _store.NewId();
            }

            var scheme = new AdminColorScheme
            {
                Id = id,
                Name = entry[nameof(AdminColorScheme.Name)]?.GetValue<string>()?.Trim(),
                Primary = entry[nameof(AdminColorScheme.Primary)]?.GetValue<string>(),
                Deep = entry[nameof(AdminColorScheme.Deep)]?.GetValue<string>(),
                Ink = entry[nameof(AdminColorScheme.Ink)]?.GetValue<string>(),
                Accent = entry[nameof(AdminColorScheme.Accent)]?.GetValue<string>(),
                LightMenu = entry[nameof(AdminColorScheme.LightMenu)]?.GetValue<bool>() ?? false,
                Position = entry[nameof(AdminColorScheme.Position)]?.GetValue<int>() ?? position++,
                Hue = entry[nameof(AdminColorScheme.Hue)]?.GetValue<double>(),
                Saturation = entry[nameof(AdminColorScheme.Saturation)]?.GetValue<double>(),
                OnPrimary = entry[nameof(AdminColorScheme.OnPrimary)]?.GetValue<string>(),
                Page = entry[nameof(AdminColorScheme.Page)]?.GetValue<string>(),
                Toolbar = entry[nameof(AdminColorScheme.Toolbar)]?.GetValue<string>(),
                Tint = entry[nameof(AdminColorScheme.Tint)]?.GetValue<string>(),
                TintDark = entry[nameof(AdminColorScheme.TintDark)]?.GetValue<string>(),
            };

            if (!AdminColorSchemeStore.IsValidId(scheme.Id))
            {
                context.Errors.Add(S["The color scheme id '{0}' is not valid: it must start with a lowercase letter and hold only lowercase letters, digits and dashes.", scheme.Id]);
                continue;
            }

            if (string.IsNullOrEmpty(scheme.Name) || AdminColorSchemeStore.ToDefinition(scheme) is null)
            {
                context.Errors.Add(S["The color scheme '{0}' needs a name, four '#rrggbb' colors (Primary, Deep, Ink, Accent), and '#rrggbb' optional colors.", scheme.Id]);
                continue;
            }

            await _store.SaveAsync(scheme);
        }
    }
}
