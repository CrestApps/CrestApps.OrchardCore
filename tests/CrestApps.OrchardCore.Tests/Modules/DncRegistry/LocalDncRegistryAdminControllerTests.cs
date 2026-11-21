using System.Reflection;
using CrestApps.OrchardCore.DncRegistry.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Tests.Modules.DncRegistry;

/// <summary>
/// SEC-P10g: the local Do Not Call list actions that change state answered GET, so a link or an image on any page an
/// administrator opened could start, pause or delete an import. They answer POST only (the row actions post them with
/// the antiforgery token).
/// </summary>
public sealed class LocalDncRegistryAdminControllerTests
{
    [Theory]
    [InlineData(nameof(LocalDncRegistryAdminController.ProcessNow))]
    [InlineData(nameof(LocalDncRegistryAdminController.PauseImport))]
    [InlineData(nameof(LocalDncRegistryAdminController.Delete))]
    public void AnActionThatChangesAList_AnswersPostOnly(string action)
    {
        var method = typeof(LocalDncRegistryAdminController).GetMethod(action, BindingFlags.Public | BindingFlags.Instance);

        Assert.NotNull(method);
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(method.GetCustomAttribute<HttpGetAttribute>());
    }
}
