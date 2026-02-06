using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core;
using CrestApps.OrchardCore.OpenAI.Azure.Core.MongoDB;
using CrestApps.OrchardCore.OpenAI.Azure.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.OpenAI.Azure.Drivers;

public sealed class AzureOpenAIMongoDBDataSourceDisplayDriver : DisplayDriver<AIDataSource>
{
    private readonly IDataProtectionProvider _dataProtectionProvider;

    internal readonly IStringLocalizer S;

    public AzureOpenAIMongoDBDataSourceDisplayDriver(
        IDataProtectionProvider dataProtectionProvider,
        IStringLocalizer<AzureOpenAIMongoDBDataSourceDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
        _dataProtectionProvider = dataProtectionProvider;
    }

    public override IDisplayResult Edit(AIDataSource dataSource, BuildEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.MongoDB)
        {
            return null;
        }

        return Initialize<AzureMongoDBDataSourceViewModel>("AzureOpenAIMongoDBDataSource_Edit", model =>
        {
            var metadata = dataSource.As<AzureMongoDBDataSourceMetadata>();
            model.IndexName = metadata?.IndexName;
            model.EndpointName = metadata?.EndpointName;
            model.AppName = metadata?.AppName;
            model.CollectionName = metadata?.CollectionName;
            model.Username = metadata?.Authentication?.Username;
            model.HasPassword = !string.IsNullOrEmpty(metadata?.Authentication?.Password);
            model.DatabaseName = metadata?.DatabaseName;
        }).Location("Content:3");
    }

    public override async Task<IDisplayResult> UpdateAsync(AIDataSource dataSource, UpdateEditorContext context)
    {
        if (dataSource.ProfileSource != AzureOpenAIConstants.ProviderName ||
            dataSource.Type != AzureOpenAIConstants.DataSourceTypes.MongoDB)
        {
            return null;
        }

        var model = new AzureMongoDBDataSourceViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (string.IsNullOrWhiteSpace(model.IndexName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.IndexName), S["The index name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.EndpointName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.EndpointName), S["The endpoint name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.CollectionName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.CollectionName), S["The collection name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.AppName))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.AppName), S["The app name is required."]);
        }

        if (string.IsNullOrWhiteSpace(model.Username))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Username), S["The username is required."]);
        }

        var metadata = dataSource.As<AzureMongoDBDataSourceMetadata>();

        metadata.Authentication ??= new AzureAIProfileMongoDBAuthenticationType();

        var hasNewPassword = !string.IsNullOrWhiteSpace(model.Password);

        if (!hasNewPassword && string.IsNullOrWhiteSpace(metadata.Authentication?.Password))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Password), S["The password is required."]);
        }

        if (hasNewPassword)
        {
            var protector = _dataProtectionProvider.CreateProtector(AzureOpenAIConstants.MongoDataProtectionPurpose);

            metadata.Authentication.Password = protector.Protect(model.Password);
        }

        metadata.IndexName = model.IndexName;
        metadata.EndpointName = model.EndpointName;
        metadata.AppName = model.AppName;
        metadata.CollectionName = model.CollectionName;
        metadata.Authentication.Username = model.Username;
        metadata.DatabaseName = model.DatabaseName;
        dataSource.Put(metadata);

        return Edit(dataSource, context);
    }
}
