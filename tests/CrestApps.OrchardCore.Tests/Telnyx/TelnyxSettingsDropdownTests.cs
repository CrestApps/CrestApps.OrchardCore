using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Drivers;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Environment.Options;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Telnyx;

/// <summary>
/// The Telnyx settings page's "Calls with no local record" and "Answering machine detection" dropdowns showed the raw
/// enum names. They now list localized labels, and each option posts the enum member's name (the "Off" option posts
/// "Disabled"). These pin that every posted name binds to its member, that the driver stores it, and that the stored
/// member is what the editor is given back to select. Rendering the view itself is not covered here: Razor views are
/// compiled at runtime, so only a real request exercises the markup.
/// </summary>
public sealed class TelnyxSettingsDropdownTests
{
    [Theory]
    [InlineData("Report", TelnyxOrphanedCallHandling.Report)]
    [InlineData("EndCall", TelnyxOrphanedCallHandling.EndCall)]
    public async Task ThePostedOrphanedCallHandlingName_BindsToItsMember(string posted, TelnyxOrphanedCallHandling expected)
    {
        // Act
        var bound = await BindEnumAsync<TelnyxOrphanedCallHandling>(posted);

        // Assert
        Assert.Equal(expected, bound);
        Assert.Equal(posted, expected.ToString());
    }

    [Theory]
    [InlineData("Premium", TelnyxAnsweringMachineDetection.Premium)]
    [InlineData("Standard", TelnyxAnsweringMachineDetection.Standard)]
    [InlineData("Disabled", TelnyxAnsweringMachineDetection.Disabled)]
    public async Task ThePostedAnsweringMachineDetectionName_BindsToItsMember(string posted, TelnyxAnsweringMachineDetection expected)
    {
        // Act
        var bound = await BindEnumAsync<TelnyxAnsweringMachineDetection>(posted);

        // Assert
        Assert.Equal(expected, bound);
        Assert.Equal(posted, expected.ToString());
    }

    // The option is labelled "Off" but must post the member's name: the label is not a member and does not bind.
    [Fact]
    public async Task TheOffLabel_IsNotAValueThatBinds()
    {
        // Arrange
        var bindingContext = CreateBindingContext<TelnyxAnsweringMachineDetection>("Off");

        // Act
        await CreateEnumBinder<TelnyxAnsweringMachineDetection>().BindModelAsync(bindingContext);

        // Assert
        Assert.False(bindingContext.Result.IsModelSet);
    }

    [Theory]
    [InlineData(TelnyxOrphanedCallHandling.Report, TelnyxOrphanedCallHandling.EndCall)]
    [InlineData(TelnyxOrphanedCallHandling.EndCall, TelnyxOrphanedCallHandling.Report)]
    public async Task UpdateAsync_StoresTheOrphanedCallHandlingAndGivesItBackToTheEditor(TelnyxOrphanedCallHandling posted, TelnyxOrphanedCallHandling stored)
    {
        // Arrange
        var settings = CreateStoredSettings();
        settings.OrphanedCallHandling = stored;

        // Act
        var model = await SaveAsync(settings, new TelnyxSettingsViewModel
        {
            IsEnabled = true,
            OrphanedCallHandling = posted,
            AnsweringMachineDetection = settings.AnsweringMachineDetection,
        });

        // Assert
        Assert.Equal(posted, settings.OrphanedCallHandling);
        Assert.Equal(posted, model.OrphanedCallHandling);
    }

    [Theory]
    [InlineData(TelnyxAnsweringMachineDetection.Premium, TelnyxAnsweringMachineDetection.Disabled)]
    [InlineData(TelnyxAnsweringMachineDetection.Standard, TelnyxAnsweringMachineDetection.Premium)]
    [InlineData(TelnyxAnsweringMachineDetection.Disabled, TelnyxAnsweringMachineDetection.Standard)]
    public async Task UpdateAsync_StoresTheAnsweringMachineDetectionAndGivesItBackToTheEditor(TelnyxAnsweringMachineDetection posted, TelnyxAnsweringMachineDetection stored)
    {
        // Arrange
        var settings = CreateStoredSettings();
        settings.AnsweringMachineDetection = stored;

        // Act
        var model = await SaveAsync(settings, new TelnyxSettingsViewModel
        {
            IsEnabled = true,
            OrphanedCallHandling = settings.OrphanedCallHandling,
            AnsweringMachineDetection = posted,
        });

        // Assert
        Assert.Equal(posted, settings.AnsweringMachineDetection);
        Assert.Equal(posted, model.AnsweringMachineDetection);
    }

    [Theory]
    [InlineData(TelnyxOrphanedCallHandling.EndCall, TelnyxAnsweringMachineDetection.Disabled)]
    [InlineData(TelnyxOrphanedCallHandling.Report, TelnyxAnsweringMachineDetection.Standard)]
    public async Task Edit_GivesTheEditorTheStoredMembers(TelnyxOrphanedCallHandling orphanedCallHandling, TelnyxAnsweringMachineDetection answeringMachineDetection)
    {
        // Arrange
        var settings = CreateStoredSettings();
        settings.OrphanedCallHandling = orphanedCallHandling;
        settings.AnsweringMachineDetection = answeringMachineDetection;

        // Act
        var result = CreateDriver().Edit(Mock.Of<ISite>(), settings, PostedFormUpdateModel.CreateContext(null));
        var model = Assert.Single(await DisplayResultModels.BuildAsync<TelnyxSettingsViewModel>(result));

        // Assert
        Assert.Equal(orphanedCallHandling, model.OrphanedCallHandling);
        Assert.Equal(answeringMachineDetection, model.AnsweringMachineDetection);
    }

    private static TelnyxSettings CreateStoredSettings()
        => new()
        {
            IsEnabled = true,
            ApiKey = "stored-protected-key",
        };

    private static async Task<TelnyxSettingsViewModel> SaveAsync(TelnyxSettings settings, TelnyxSettingsViewModel posted)
    {
        var site = new Mock<ISite>();
        site
            .Setup(candidate => candidate.GetOrCreate<TelephonySettings>())
            .Returns(new TelephonySettings { DefaultProviderName = TelnyxConstants.ProviderTechnicalName });

        var context = PostedFormUpdateModel.CreateContext(posted);
        var result = await CreateDriver().UpdateAsync(site.Object, settings, context);

        Assert.True(context.Updater.ModelState.IsValid);

        return Assert.Single(await DisplayResultModels.BuildAsync<TelnyxSettingsViewModel>(result));
    }

    private static TelnyxSettingsDisplayDriver CreateDriver()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "admin")], "Test")),
            },
        };

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return new TelnyxSettingsDisplayDriver(
            new Mock<IOptionsUpdateNotifier> { DefaultValue = DefaultValue.Mock }.Object,
            httpContextAccessor,
            authorizationService.Object,
            new EphemeralDataProtectionProvider(),
            Mock.Of<INotifier>(),
            [],
            Mock.Of<IHtmlLocalizer<TelnyxSettingsDisplayDriver>>(),
            new PassThroughStringLocalizer<TelnyxSettingsDisplayDriver>());
    }

    // MVC's own enum binder, which is what binds a select's posted value to an enum property of the view model.
    private static async Task<TEnum> BindEnumAsync<TEnum>(string posted)
        where TEnum : struct, Enum
    {
        var bindingContext = CreateBindingContext<TEnum>(posted);

        await CreateEnumBinder<TEnum>().BindModelAsync(bindingContext);

        // Binding alone leaves the entry unvalidated, so the check is that the binder recorded no error.
        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(0, bindingContext.ModelState.ErrorCount);

        return Assert.IsType<TEnum>(bindingContext.Result.Model);
    }

    private static EnumTypeModelBinder CreateEnumBinder<TEnum>()
        where TEnum : struct, Enum
        => new(suppressBindingUndefinedValueToEnumType: true, typeof(TEnum), NullLoggerFactory.Instance);

    private static ModelBindingContext CreateBindingContext<TEnum>(string posted)
        where TEnum : struct, Enum
    {
        const string fieldName = "Value";

        return DefaultModelBindingContext.CreateBindingContext(
            new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
            new QueryStringValueProvider(
                BindingSource.Form,
                new QueryCollection(new Dictionary<string, StringValues> { [fieldName] = posted }),
                CultureInfo.InvariantCulture),
            new EmptyModelMetadataProvider().GetMetadataForType(typeof(TEnum)),
            bindingInfo: null,
            modelName: fieldName);
    }
}
