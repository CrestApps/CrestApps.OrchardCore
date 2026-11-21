using CrestApps.OrchardCore.Learning.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Learning.ViewModels;

/// <summary>
/// The course catalog: the published courses of a category (and its sub-categories), or of every category, with the
/// category tree to browse.
/// </summary>
public class CourseCatalogViewModel
{
    /// <summary>
    /// Gets or sets the category browsed, or <see langword="null"/> for every course.
    /// </summary>
    public string Category { get; set; }

    /// <summary>
    /// Gets or sets the words searched for in the course titles.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the name of the category browsed, with the categories above it.
    /// </summary>
    [BindNever]
    public string CategoryPath { get; set; }

    /// <summary>
    /// Gets or sets the categories, in tree order.
    /// </summary>
    [BindNever]
    public IList<CourseCatalogCategoryViewModel> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the courses of the page.
    /// </summary>
    [BindNever]
    public IList<CourseCatalogRowViewModel> Courses { get; set; } = [];

    /// <summary>
    /// Gets or sets how many courses match.
    /// </summary>
    [BindNever]
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    [BindNever]
    public dynamic Pager { get; set; }
}

/// <summary>
/// A category in the catalog's tree.
/// </summary>
public class CourseCatalogCategoryViewModel
{
    /// <summary>
    /// Gets or sets the category's id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets its name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets its depth, 0 at the top.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Gets or sets whether it is the category browsed.
    /// </summary>
    public bool Selected { get; set; }
}

/// <summary>
/// A course in the catalog.
/// </summary>
public class CourseCatalogRowViewModel
{
    /// <summary>
    /// Gets or sets the course's content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets its title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets its code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets its summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets or sets its categories, each as its path of names.
    /// </summary>
    public IList<string> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the address of its cover picture, or <see langword="null"/> without one.
    /// </summary>
    public string CoverUrl { get; set; }

    /// <summary>
    /// Gets or sets when it starts, in UTC.
    /// </summary>
    public DateTime? StartsUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ends, in UTC.
    /// </summary>
    public DateTime? EndsUtc { get; set; }

    /// <summary>
    /// Gets or sets where it is between its dates.
    /// </summary>
    public CourseSchedule Schedule { get; set; }

    /// <summary>
    /// Gets or sets the route of its page.
    /// </summary>
    public RouteValueDictionary DisplayRouteValues { get; set; }
}
