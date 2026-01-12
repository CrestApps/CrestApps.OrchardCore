using System.ComponentModel.DataAnnotations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Core.Handlers;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.OpenAI.Azure.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Handlers;

public sealed class AzureAISearchAIDataSourceHandler : CatalogEntryHandlerBase<AIDataSource>
{
    internal readonly IStringLocalizer S;

    public AzureAISearchAIDataSourceHandler(IStringLocalizer<AzureAISearchAIDataSourceHandler> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task ValidatedAsync(ValidatedContext<AIDataSource> context)
    {
        if (context.Model.ProfileSource != AzureOpenAIConstants.AzureOpenAIOwnData ||
            context.Model.Type != AzureOpenAIConstants.DataSourceTypes.AzureAISearch)
        {
            return Task.CompletedTask;
        }

        var metadata = context.Model.As<AzureAIProfileAISearchMetadata>();

        if (string.IsNullOrWhiteSpace(metadata.IndexName))
        {
            context.Result.Fail(new ValidationResult(S["The Index is required."], [nameof(metadata.IndexName)]));
        }

        if (!string.IsNullOrWhiteSpace(metadata.Filter) && !IsValidODataFilter(metadata.Filter))
        {
            context.Result.Fail(new ValidationResult(S["The Filter must be a valid OData filter expression."], [nameof(metadata.Filter)]));
        }

        return Task.CompletedTask;
    }

    private static bool IsValidODataFilter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        // Basic OData filter validation
        // Check for common OData operators and ensure basic syntax
        var odataOperators = new[] { " eq ", " ne ", " gt ", " ge ", " lt ", " le ", " and ", " or ", " not " };
        var hasOperator = odataOperators.Any(op => filter.Contains(op, StringComparison.OrdinalIgnoreCase));

        if (!hasOperator)
        {
            // If no operator found, it might be a function call like search.in() or geo.distance()
            // Allow basic function syntax
            return filter.Contains('(') && filter.Contains(')');
        }

        // Check for balanced quotes
        var singleQuotes = filter.Count(c => c == '\'');
        if (singleQuotes % 2 != 0)
        {
            return false;
        }

        // Check for balanced parentheses
        var openParen = filter.Count(c => c == '(');
        var closeParen = filter.Count(c => c == ')');
        if (openParen != closeParen)
        {
            return false;
        }

        return true;
    }
}
