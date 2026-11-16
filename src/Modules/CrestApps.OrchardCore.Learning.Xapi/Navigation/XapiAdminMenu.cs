using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Xapi.Navigation;

/// <summary>
/// Adds Learning statements to the Learning menu.
/// </summary>
internal sealed class XapiAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    public XapiAdminMenu(IStringLocalizer<XapiAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Learning"], learning => learning
            .Add(S["Learning Statements"], S["Learning Statements"].PrefixPosition(), statements => statements
                .AddClass("learning-statements")
                .Id("learningStatements")
                .Action("Index", "StatementsAdmin", XapiConstants.Features.LearningRecordStore)
                .Permission(LearningXapiPermissions.ReadAllLearningStatements)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
