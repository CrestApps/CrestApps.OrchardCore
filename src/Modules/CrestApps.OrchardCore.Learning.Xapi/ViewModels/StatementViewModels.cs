using CrestApps.OrchardCore.Learning.Xapi.Core;
using CrestApps.OrchardCore.Learning.Xapi.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Learning.Xapi.ViewModels;

/// <summary>
/// A statement as a row of the admin list.
/// </summary>
public class XapiStatementRow
{
    /// <summary>Gets or sets the record.</summary>
    public XapiStatementRecord Record { get; set; }

    /// <summary>Gets or sets the summary of the statement.</summary>
    public XapiStatementSummary Summary { get; set; }

    /// <summary>Gets or sets who the actor is: a user name, the statement's name for the actor, or its identifier.</summary>
    public string Actor { get; set; }
}

/// <summary>
/// The filters of the admin list of statements, from the query string.
/// </summary>
public class StatementListOptions
{
    /// <summary>Gets or sets the actor: a user name, an e-mail address, a mailto: IRI or an account name.</summary>
    public string Actor { get; set; }

    /// <summary>Gets or sets the verb id.</summary>
    public string Verb { get; set; }

    /// <summary>Gets or sets the activity id of the object.</summary>
    public string Activity { get; set; }

    /// <summary>Gets or sets the registration.</summary>
    public string Registration { get; set; }

    /// <summary>Gets or sets the first day, in the user's time zone, as yyyy-MM-dd.</summary>
    public string From { get; set; }

    /// <summary>Gets or sets the last day, in the user's time zone, as yyyy-MM-dd.</summary>
    public string To { get; set; }
}

/// <summary>
/// The admin list of statements.
/// </summary>
public class StatementListViewModel
{
    /// <summary>Gets or sets the filters.</summary>
    public StatementListOptions Options { get; set; } = new();

    /// <summary>Gets or sets the verbs the verb filter offers.</summary>
    [BindNever]
    public IList<SelectListItem> Verbs { get; set; } = [];

    /// <summary>Gets or sets the rows.</summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// The page of one statement.
/// </summary>
public class StatementDetailViewModel
{
    /// <summary>Gets or sets the record.</summary>
    public XapiStatementRecord Record { get; set; }

    /// <summary>Gets or sets the summary.</summary>
    public XapiStatementSummary Summary { get; set; }

    /// <summary>Gets or sets who the actor is.</summary>
    public string Actor { get; set; }

    /// <summary>Gets or sets the statement as indented JSON.</summary>
    public string Json { get; set; }

    /// <summary>Gets or sets the statement's timestamp as it was sent, which may carry the sender's own zone.</summary>
    public string Timestamp { get; set; }
}
