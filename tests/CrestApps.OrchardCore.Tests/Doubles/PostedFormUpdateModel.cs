using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Stands in for the form post an admin editor's display driver binds from: the view model the driver asks for is
/// filled with the values of a prepared one, as model binding fills it from the posted fields. What the driver then
/// stores is its own doing, which is what a test of the editor pins.
/// </summary>
internal sealed class PostedFormUpdateModel : IUpdateModel
{
    private readonly object _posted;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostedFormUpdateModel"/> class.
    /// </summary>
    /// <param name="posted">The view model whose values the form posts.</param>
    public PostedFormUpdateModel(object posted)
    {
        _posted = posted;
    }

    public ModelStateDictionary ModelState { get; } = new();

    /// <summary>
    /// Builds the context a driver's <c>UpdateAsync</c> runs in, posting <paramref name="posted"/>.
    /// </summary>
    /// <param name="posted">The view model whose values the form posts.</param>
    /// <param name="isNew">Whether the editor is creating the record.</param>
    /// <returns>The update context.</returns>
    public static UpdateEditorContext CreateContext(object posted, bool isNew = false)
        => new(
            Mock.Of<IShape>(),
            groupId: string.Empty,
            isNew,
            htmlFieldPrefix: string.Empty,
            Mock.Of<IShapeFactory>(),
            layout: null,
            new PostedFormUpdateModel(posted));

    public Task<bool> TryUpdateModelAsync<TModel>(TModel model)
        where TModel : class
        => TryUpdateModelAsync(model, string.Empty);

    public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix)
        where TModel : class
    {
        // A driver that binds a model this post does not carry binds nothing, as a form without its fields would.
        if (_posted is not TModel posted)
        {
            return Task.FromResult(false);
        }

        foreach (var property in typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
            {
                property.SetValue(model, property.GetValue(posted));
            }
        }

        return Task.FromResult(true);
    }

    public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params Expression<Func<TModel, object>>[] includeExpressions)
        where TModel : class
        => TryUpdateModelAsync(model, prefix);

    public bool TryValidateModel(object model)
        => true;

    public bool TryValidateModel(object model, string prefix)
        => true;
}
