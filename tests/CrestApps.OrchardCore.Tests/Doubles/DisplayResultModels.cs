using System.Reflection;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// Reads the view models a display driver's <c>Initialize&lt;TModel&gt;</c> results would hand to their views, without a
/// shape factory: each result's initializer runs on a stand-in for the shape the factory would build, a subclass of the
/// view model that is also an <see cref="IShape"/>, which is what the factory's proxy is.
/// </summary>
internal static class DisplayResultModels
{
    /// <summary>
    /// Builds the view model of every shape in <paramref name="result"/>, flattening a combined result.
    /// </summary>
    /// <typeparam name="TModel">The view model type the driver initializes.</typeparam>
    /// <param name="result">The driver's display result.</param>
    /// <returns>The initialized view models, in the order the driver returned their shapes.</returns>
    public static async Task<List<TModel>> BuildAsync<TModel>(IDisplayResult result)
        where TModel : class
    {
        var models = new List<TModel>();

        foreach (var shapeResult in ShapeResults(result))
        {
            var shape = new Mock<TModel>();
            shape.As<IShape>();

            var initialize = shapeResult.GetType().GetMethod(
                "InitializeShapeAsync",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                [typeof(IShape)]);

            switch (initialize.Invoke(shapeResult, [shape.Object]))
            {
                case ValueTask valueTask:
                    await valueTask;
                    break;

                case Task task:
                    await task;
                    break;
            }

            models.Add(shape.Object);
        }

        return models;
    }

    private static IEnumerable<ShapeResult> ShapeResults(IDisplayResult result)
    {
        if (result is null)
        {
            return [];
        }

        if (result is ShapeResult shapeResult)
        {
            return [shapeResult];
        }

        var getResults = result.GetType().GetMethod("GetResults", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var results = (IEnumerable<IDisplayResult>)getResults.Invoke(result, null);

        return results.SelectMany(ShapeResults);
    }
}
