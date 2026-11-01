// @ts-check
/*
 * WebMCP chat bridge.
 *
 * Lets the site's AI chat call the tools on the page the user has open. The script tells the site which page tools
 * this tab offers when it connects, when the tools change, and when the tab gets focus, so the chat uses the tab the
 * user is looking at. When the chat calls a tool, the site asks this tab to run it and the script sends back the text.
 *
 * Only page tools are sent: the chat reaches the site's own AI tools directly.
 */
import { isErrorResult, serializeResult, textResult } from './webmcp-results.js';

var MAX_TOOLS = 64;
var TOOL_NAME = /^[a-zA-Z0-9_-]{1,64}$/;

// The chat input every AI chat renders, which carries its profile.
var CHAT_SELECTOR = '[data-profile-id]';

/**
 * Turns the page's tools into what the site accepts: page tools only, with valid names and their schemas as text.
 *
 * @param {Array<any>} tools The page's tools.
 */
function toBrowserToolDescriptors(tools) {
  var descriptors = [];

  (tools || []).forEach(function (tool) {
    if (descriptors.length >= MAX_TOOLS || !tool || tool.source === 'server' || !TOOL_NAME.test(tool.name || '')) {
      return;
    }

    descriptors.push({
      name: tool.name,
      title: tool.title || null,
      description: tool.description,
      inputSchema: JSON.stringify(tool.inputSchema || { type: 'object', properties: {} }),
    });
  });

  return descriptors;
}

/**
 * Connects the page's tools to the site's chat.
 *
 * @param {{ api: any, connection: any, doc: any, win: any }} options The page's tools, the hub connection, the document and the window.
 */
function createBridge(options) {
  var api = options.api;
  var connection = options.connection;
  var doc = options.doc;
  var win = options.win;
  var connected = false;

  function isPageTool(name) {
    return api.getTools().some(function (tool) {
      return tool.name === name && tool.source !== 'server';
    });
  }

  async function sync() {
    if (!connected) {
      return;
    }

    var descriptors = toBrowserToolDescriptors(api.getTools());

    try {
      if (descriptors.length === 0) {
        await connection.invoke('ClearPageTools');
      } else {
        await connection.invoke('RegisterPageTools', descriptors);
      }
    } catch (error) {
      console.warn('The page tools could not be sent to the chat.', error);
    }
  }

  async function submit(callId, result) {
    try {
      await connection.invoke('SubmitToolResult', callId, serializeResult(result), isErrorResult(result));
    } catch (error) {
      console.warn('The page tool result could not be sent to the chat.', error);
    }
  }

  async function handleExecute(callId, name, argumentsJson) {
    var input;

    try {
      input = argumentsJson ? JSON.parse(argumentsJson) : {};
    } catch (error) {
      await submit(callId, textResult('The tool input is not valid JSON.', true));

      return;
    }

    var known = api.getTools().some(function (tool) {
      return tool.name === name;
    });

    if (known && !isPageTool(name)) {
      await submit(callId, textResult('The tool "' + name + '" is not a page tool.', true));

      return;
    }

    try {
      await submit(callId, await api.executeTool(name, input));
    } catch (error) {
      await submit(callId, textResult('The tool failed: ' + (error && error.message ? error.message : String(error)), true));
    }
  }

  connection.on('ExecuteTool', handleExecute);

  if (typeof connection.onreconnected === 'function') {
    connection.onreconnected(function () {
      connected = true;

      return sync();
    });
  }

  if (typeof connection.onclose === 'function') {
    connection.onclose(function () {
      connected = false;
    });
  }

  api.onToolsChanged(function () {
    sync();
  });

  win.addEventListener('focus', function () {
    return sync();
  });

  doc.addEventListener('visibilitychange', function () {
    if (doc.visibilityState === 'visible') {
      return sync();
    }
  });

  async function start() {
    await connection.start();
    connected = true;
    await sync();
  }

  return {
    start: start,
    sync: sync,
    handleExecute: handleExecute,
  };
}

/**
 * Calls `start` once the page has an AI chat: at once when it already shows one, otherwise when the user moves into a
 * chat or a chat session begins. Pages without a chat never open a connection.
 *
 * @param {any} doc The document.
 * @param {Function} start The function that connects the bridge.
 */
function startWhenChatAppears(doc, start) {
  if (doc.querySelector(CHAT_SELECTOR)) {
    start();

    return;
  }

  function stopListening() {
    doc.removeEventListener('focusin', onFocus);
    doc.removeEventListener('initializingSessionCoreAIChat', onSession);
  }

  function onFocus(event) {
    var target = event && event.target;

    if (target && typeof target.closest === 'function' && target.closest(CHAT_SELECTOR)) {
      stopListening();
      start();
    }
  }

  function onSession() {
    stopListening();
    start();
  }

  doc.addEventListener('focusin', onFocus);
  doc.addEventListener('initializingSessionCoreAIChat', onSession);
}

globalThis.CrestAppsWebMcpChatBridge = {
  CHAT_SELECTOR: CHAT_SELECTOR,
  toBrowserToolDescriptors: toBrowserToolDescriptors,
  createBridge: createBridge,
  startWhenChatAppears: startWhenChatAppears,
};

if (typeof window !== 'undefined' && window.crestAppsWebMcp && typeof signalR !== 'undefined') {
  var hubUrl = window.crestAppsWebMcp.config && window.crestAppsWebMcp.config.chatHubUrl;

  if (hubUrl) {
    var bridge = createBridge({
      api: window.crestAppsWebMcp,
      connection: new signalR.HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build(),
      doc: document,
      win: window,
    });

    startWhenChatAppears(document, function () {
      bridge.start().catch(function (error) {
        console.warn('The WebMCP chat bridge could not connect.', error);
      });
    });
  }
}
