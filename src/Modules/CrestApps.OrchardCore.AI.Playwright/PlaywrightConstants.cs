namespace CrestApps.OrchardCore.AI.Playwright;

public static class PlaywrightConstants
{
    public const int DefaultSessionInactivityTimeoutInMinutes = 30;

    public static class Feature
    {
        public const string AdminWidget = "CrestApps.OrchardCore.AI.Playwright";
    }

    public const string ProtectorName = "PlaywrightProfilePassword";

    public static class PromptIds
    {
        public const string Operator = "playwright-operator";
    }

    public static class CompletionContextKeys
    {
        public const string SessionMetadata = "PlaywrightSessionMetadata";
    }

    public static class ToolNames
    {
        public const string CaptureState = "playwright_capture_state";
        public const string OpenAdminHome = "playwright_open_admin_home";
        public const string OpenContentItems = "playwright_open_content_items";
        public const string ListContentItems = "playwright_list_content_items";
        public const string OpenContentItemEditor = "playwright_open_content_item_editor";
        public const string OpenNewContentItem = "playwright_open_new_content_item";
        public const string SetContentTitle = "playwright_set_content_title";
        public const string SaveDraft = "playwright_save_draft";
        public const string PublishContent = "playwright_publish_content";
        public const string ClickByRole = "playwright_click_by_role";
        public const string FillByLabel = "playwright_fill_by_label";
        public const string WaitForUrl = "playwright_wait_for_url";
        public const string InspectPageContent = "playwright_get_page_content";
        public const string FindElement = "playwright_find_element";
        public const string CheckElementExists = "playwright_check_element_exists";
        public const string GetVisibleWidgets = "playwright_get_visible_widgets";
        public const string TakeScreenshot = "playwright_take_screenshot";

        public const string Navigate = "browser_navigate";
        public const string Click = "browser_click";
        public const string Fill = "browser_fill";
        public const string Select = "browser_select";
        public const string GetPageContent = "browser_get_page_content";
        public const string WaitFor = "browser_wait_for";
    }

    public static class ToolSets
    {
        public static readonly string[] Deterministic =
        [
            ToolNames.CaptureState,
            ToolNames.OpenAdminHome,
            ToolNames.OpenContentItems,
            ToolNames.ListContentItems,
            ToolNames.OpenContentItemEditor,
            ToolNames.OpenNewContentItem,
            ToolNames.SetContentTitle,
            ToolNames.SaveDraft,
            ToolNames.PublishContent,
            ToolNames.ClickByRole,
            ToolNames.FillByLabel,
            ToolNames.WaitForUrl,
            ToolNames.InspectPageContent,
            ToolNames.FindElement,
            ToolNames.CheckElementExists,
            ToolNames.GetVisibleWidgets,
            ToolNames.TakeScreenshot,
        ];

        public static readonly string[] RawFallback =
        [
            ToolNames.Navigate,
            ToolNames.Click,
            ToolNames.Fill,
            ToolNames.Select,
            ToolNames.GetPageContent,
            ToolNames.WaitFor,
        ];
    }

    public static string GetToolDescription(string toolName)
        => toolName switch
        {
            ToolNames.CaptureState => "Captures the current browser state for deterministic Orchard admin planning.",
            ToolNames.OpenAdminHome => "Ensures the Orchard admin shell is available for the current tenant.",
            ToolNames.OpenContentItems => "Opens the Orchard content items list.",
            ToolNames.ListContentItems => "Lists the visible Orchard content items from the current content items screen.",
            ToolNames.OpenContentItemEditor => "Opens the editor for an existing Orchard content item by title, using the current list page before restarting navigation.",
            ToolNames.OpenNewContentItem => "Starts the create-content flow for a requested Orchard content type.",
            ToolNames.SetContentTitle => "Sets the Title field on the current Orchard content editor.",
            ToolNames.SaveDraft => "Saves the current Orchard content item as a draft.",
            ToolNames.PublishContent => "Publishes the current Orchard content item.",
            ToolNames.ClickByRole => "Clicks a control using stable accessible-name and role matching.",
            ToolNames.FillByLabel => "Fills a control using its visible label.",
            ToolNames.WaitForUrl => "Waits until the browser reaches the expected URL.",
            ToolNames.InspectPageContent => "Returns the visible content of the current page for grounded follow-up questions.",
            ToolNames.FindElement => "Finds visible page elements that match a requested widget name, label, or text snippet.",
            ToolNames.CheckElementExists => "Checks whether a requested control, widget, or text snippet is currently visible on the page.",
            ToolNames.GetVisibleWidgets => "Lists visible widget-like cards, headings, and editor sections on the current page.",
            ToolNames.TakeScreenshot => "Captures a screenshot of the current page and returns the saved file path.",
            ToolNames.Navigate => "Advanced fallback: navigate the browser to a raw URL.",
            ToolNames.Click => "Advanced fallback: click an element using a raw selector.",
            ToolNames.Fill => "Advanced fallback: fill a field using a raw selector.",
            ToolNames.Select => "Advanced fallback: select an option using a raw selector.",
            ToolNames.GetPageContent => "Advanced fallback: return the visible text of the current page.",
            ToolNames.WaitFor => "Advanced fallback: wait until a raw selector appears.",
            _ => toolName,
        };
}
