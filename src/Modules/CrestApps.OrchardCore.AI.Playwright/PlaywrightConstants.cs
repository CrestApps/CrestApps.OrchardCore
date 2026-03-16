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
        public const string OpenEditorTab = "playwright_open_editor_tab";
        public const string SetContentTitle = "playwright_set_content_title";
        public const string SetFieldValue = "playwright_set_field_value";
        public const string SetBodyField = "playwright_set_body_field";
        public const string SaveDraft = "playwright_save_draft";
        public const string PublishContent = "playwright_publish_content";
        public const string PublishAndVerify = "playwright_publish_and_verify";
        public const string InspectPageContent = "playwright_get_page_content";
        public const string FindElement = "playwright_find_element";
        public const string CheckElementExists = "playwright_check_element_exists";
        public const string GetVisibleWidgets = "playwright_get_visible_widgets";
        public const string TakeScreenshot = "playwright_take_screenshot";
        public const string DiagnoseOrchardAction = "playwright_diagnose_orchard_action";
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
            ToolNames.OpenEditorTab,
            ToolNames.SetContentTitle,
            ToolNames.SetFieldValue,
            ToolNames.SetBodyField,
            ToolNames.SaveDraft,
            ToolNames.PublishContent,
            ToolNames.PublishAndVerify,
            ToolNames.InspectPageContent,
            ToolNames.FindElement,
            ToolNames.CheckElementExists,
            ToolNames.GetVisibleWidgets,
            ToolNames.TakeScreenshot,
            ToolNames.DiagnoseOrchardAction,
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
            ToolNames.OpenEditorTab => "Opens an OrchardCore editor tab, section, or expander by name.",
            ToolNames.SetContentTitle => "Sets the Title field on the current Orchard content editor.",
            ToolNames.SetFieldValue => "Updates a labeled OrchardCore field using a typed field strategy.",
            ToolNames.SetBodyField => "Updates a body-like OrchardCore field using rich-editor-aware append or replace behavior.",
            ToolNames.SaveDraft => "Saves the current Orchard content item as a draft and captures the resulting page state.",
            ToolNames.PublishContent => "Publishes the current Orchard content item and captures the resulting page state.",
            ToolNames.PublishAndVerify => "Publishes the current Orchard content item and returns structured verification evidence.",
            ToolNames.InspectPageContent => "Returns the visible content of the current page for grounded follow-up questions.",
            ToolNames.FindElement => "Finds visible page elements that match a requested widget name, label, or text snippet.",
            ToolNames.CheckElementExists => "Checks whether a requested control, widget, or text snippet is currently visible on the page.",
            ToolNames.GetVisibleWidgets => "Lists visible widget-like cards, headings, and editor sections on the current page.",
            ToolNames.TakeScreenshot => "Captures a screenshot of the current page and returns the saved file path.",
            ToolNames.DiagnoseOrchardAction => "Attempts to find a named OrchardCore admin action using priority-ordered locator strategies. Captures full evidence (screenshots, HTML, URL, attempts) when the action is not found.",
            _ => toolName,
        };
}

