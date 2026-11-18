namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// Every file of a ticket in one place: the files of its replies and notes and of the tickets merged into it, newest
/// first.
/// </summary>
public class TicketFilesViewModel
{
    /// <summary>
    /// Gets or sets the files shown.
    /// </summary>
    public List<TicketFileViewModel> Files { get; set; } = [];

    /// <summary>
    /// Gets or sets how many files the ticket has in all.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the link that shows every file, when only the newest are shown.
    /// </summary>
    public string ShowAllUrl { get; set; }

    /// <summary>
    /// Gets or sets whether the files are shown to the requester, in the portal.
    /// </summary>
    public bool ForRequester { get; set; }
}

/// <summary>
/// A file of a ticket.
/// </summary>
public class TicketFileViewModel
{
    /// <summary>
    /// Gets or sets the file's name.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the media type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size, in bytes.
    /// </summary>
    public long Length { get; set; }

    /// <summary>
    /// Gets or sets whether the file is a picture shown as a preview.
    /// </summary>
    public bool IsImage { get; set; }

    /// <summary>
    /// Gets or sets who added it, by name; empty for the site.
    /// </summary>
    public string AddedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was added, in UTC.
    /// </summary>
    public DateTime AddedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether it came with an internal note.
    /// </summary>
    public bool Internal { get; set; }

    /// <summary>
    /// Gets or sets the number of the ticket it came with, when that was a ticket merged into this one.
    /// </summary>
    public string FromNumber { get; set; }

    /// <summary>
    /// Gets or sets the link that downloads it.
    /// </summary>
    public string DownloadUrl { get; set; }

    /// <summary>
    /// Gets or sets the link that shows the picture, for a preview.
    /// </summary>
    public string PreviewUrl { get; set; }

    /// <summary>
    /// Gets or sets the link to the reply or note it came with.
    /// </summary>
    public string EntryUrl { get; set; }
}
