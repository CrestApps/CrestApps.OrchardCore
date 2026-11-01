// Globals the WebMCP scripts share with the page, for the type check (`npm run check`). Nothing here is built.

// The page's list of WebMCP tools, which webmcp.js creates and the page tools and chat bridge use.
interface Window {
    crestAppsWebMcp: any;
}

// The scripts' testable helpers, which the unit tests read.
declare var CrestAppsWebMcp: any;
declare var CrestAppsWebMcpPageTools: any;
declare var CrestAppsWebMcpChatBridge: any;

// @microsoft/signalr, loaded ahead of the chat bridge.
declare const signalR: any;
