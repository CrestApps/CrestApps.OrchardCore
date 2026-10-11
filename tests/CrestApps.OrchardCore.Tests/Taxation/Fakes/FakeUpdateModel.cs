using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Tests.Taxation.Fakes;

/// <summary>
/// A minimal <see cref="IUpdateModel"/> that copies values from a preconfigured source instance into
/// the target model and exposes a real <see cref="ModelStateDictionary"/> so display driver
/// <c>UpdateAsync</c> logic can be exercised without the MVC model binding pipeline.
/// </summary>
public sealed class FakeUpdateModel : IUpdateModel
{
    private readonly object _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeUpdateModel"/> class.
    /// </summary>
    /// <param name="source">The instance whose public property values are copied into bound models.</param>
    public FakeUpdateModel(object source)
    {
        _source = source;
    }

    /// <summary>
    /// Gets the model state populated by the driver under test.
    /// </summary>
    public ModelStateDictionary ModelState { get; } = new ModelStateDictionary();

    /// <inheritdoc />
    public Task<bool> TryUpdateModelAsync<TModel>(TModel model)
        where TModel : class
        => TryUpdateModelAsync(model, string.Empty);

    /// <inheritdoc />
    public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix)
        where TModel : class
    {
        if (model is not null && _source is TModel typedSource)
        {
            foreach (var property in typeof(TModel).GetProperties())
            {
                if (property.CanRead && property.CanWrite)
                {
                    property.SetValue(model, property.GetValue(typedSource));
                }
            }
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params Expression<Func<TModel, object>>[] includeExpressions)
        where TModel : class
        => TryUpdateModelAsync(model, prefix);

    /// <inheritdoc />
    public bool TryValidateModel(object model)
        => true;

    /// <inheritdoc />
    public bool TryValidateModel(object model, string prefix)
        => true;
}
