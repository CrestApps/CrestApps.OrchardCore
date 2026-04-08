using System.Text.Json.Nodes;
using CrestApps.Core.Models;

namespace CrestApps.Core.Services;

/// <summary>
/// Provides management-level CRUD operations for catalog entries, including
/// initialization with optional JSON data, validation, and handler invocation.
/// Extends read-only management access with write capabilities.
/// </summary>
/// <typeparam name="T">The type of catalog entry.</typeparam>
public interface ICatalogManager<T> : IReadCatalogManager<T>
{
    /// <summary>
    /// Asynchronously deletes the specified model from the catalog.
    /// </summary>
    /// <param name="model">The model to delete.</param>
    /// <returns><see langword="true"/> if the model was successfully deleted; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> DeleteAsync(T model);

    /// <summary>
    /// Asynchronously creates a new model instance, optionally populating it from JSON data.
    /// </summary>
    /// <param name="data">Optional JSON data to seed the new model.</param>
    /// <returns>A newly created and initialized model instance.</returns>
    ValueTask<T> NewAsync(JsonNode data = null);

    /// <summary>
    /// Asynchronously creates the specified model in the catalog.
    /// </summary>
    /// <param name="model">The model to create.</param>
    ValueTask CreateAsync(T model);

    /// <summary>
    /// Asynchronously updates the specified model in the catalog, optionally merging changes from JSON data.
    /// </summary>
    /// <param name="model">The model to update.</param>
    /// <param name="data">Optional JSON data containing fields to merge into the model.</param>
    ValueTask UpdateAsync(T model, JsonNode data = null);

    /// <summary>
    /// Asynchronously validates the specified model and returns the validation result.
    /// </summary>
    /// <param name="model">The model to validate.</param>
    /// <returns>The validation result details indicating success or failure with error messages.</returns>
    ValueTask<ValidationResultDetails> ValidateAsync(T model);
}
