// @ts-check
/*
 * WebMCP for Orchard Core.
 *
 * The page keeps one list of tools at `window.crestAppsWebMcp`. Every tool on it is also given to the browser's WebMCP
 * `ModelContext` when the browser has one, so a browser agent can call it. The list starts with the AI tools the site
 * lets this user call: each one is a small proxy that posts to the site with the user's antiforgery token. Other
 * scripts add tools that work on the page itself with `window.crestAppsWebMcp.registerTool(...)`.
 *
 * Without WebMCP in the browser the list still works, so the Orchard Core AI chat can use the page's tools.
 */
import { isErrorResult, serializeResult, textResult } from './webmcp-results.js';

var CONFIG_ELEMENT_ID = 'crestapps-webmcp-config';

/**
 * Finds the browser's WebMCP object: `document.modelContext` in the current draft, `navigator.modelContext` in
 * earlier browser previews.
 *
 * @param {any} doc The document.
 * @param {any} nav The navigator.
 */
function getModelContext(doc, nav) {
  if (doc && doc.modelContext) {
    return doc.modelContext;
  }

  if (nav && nav.modelContext) {
    return nav.modelContext;
  }

  return null;
}

/**
 * Creates the page's list of tools.
 *
 * @param {any} modelContext The browser's WebMCP object, or null.
 */
function createRegistry(modelContext) {
  /** @type {Map<string, { tool: any, controller: AbortController }>} */
  var tools = new Map();
  /** @type {Set<Function>} */
  var listeners = new Set();

  function notify() {
    listeners.forEach(function (listener) {
      try {
        listener();
      } catch (error) {
        console.error('A WebMCP tool listener failed.', error);
      }
    });
  }

  function toBrowserTool(tool) {
    var browserTool = {
      name: tool.name,
      description: tool.description,
      inputSchema: tool.inputSchema || { type: 'object', properties: {} },
      execute: tool.execute,
    };

    if (tool.title) {
      browserTool.title = tool.title;
    }

    if (tool.annotations) {
      browserTool.annotations = tool.annotations;
    }

    return browserTool;
  }

  function release(name, entry) {
    entry.controller.abort();

    if (modelContext && typeof modelContext.unregisterTool === 'function') {
      try {
        modelContext.unregisterTool(name);
      } catch (error) {
        // The browser already let go of it.
      }
    }
  }

  function registerTool(tool) {
    if (!tool || typeof tool.name !== 'string' || !tool.name) {
      throw new TypeError('A WebMCP tool needs a name.');
    }

    if (typeof tool.description !== 'string' || !tool.description) {
      throw new TypeError('A WebMCP tool needs a description.');
    }

    if (typeof tool.execute !== 'function') {
      throw new TypeError('A WebMCP tool needs an execute function.');
    }

    var existing = tools.get(tool.name);

    if (existing) {
      release(tool.name, existing);
    }

    var controller = new AbortController();

    tools.set(tool.name, { tool: tool, controller: controller });

    if (modelContext && typeof modelContext.registerTool === 'function') {
      try {
        Promise.resolve(modelContext.registerTool(toBrowserTool(tool), { signal: controller.signal })).catch(function (error) {
          console.warn('The browser did not accept the WebMCP tool "' + tool.name + '".', error);
        });
      } catch (error) {
        console.warn('The browser did not accept the WebMCP tool "' + tool.name + '".', error);
      }
    }

    notify();
  }

  function unregisterTool(name) {
    var entry = tools.get(name);

    if (!entry) {
      return false;
    }

    tools.delete(name);
    release(name, entry);
    notify();

    return true;
  }

  function getTools() {
    var list = [];

    tools.forEach(function (entry) {
      var tool = entry.tool;
      var item = {
        name: tool.name,
        description: tool.description,
        inputSchema: tool.inputSchema || { type: 'object', properties: {} },
        source: tool.source || 'page',
      };

      if (tool.title) {
        item.title = tool.title;
      }

      if (tool.annotations) {
        item.annotations = tool.annotations;
      }

      list.push(item);
    });

    return list;
  }

  async function executeTool(name, input, options) {
    var entry = tools.get(name);

    if (!entry) {
      return textResult('The tool "' + name + '" is not on this page.', true);
    }

    var signal = options && options.signal ? options.signal : new AbortController().signal;

    return await entry.tool.execute(input || {}, { signal: signal });
  }

  function onToolsChanged(listener) {
    listeners.add(listener);

    return function () {
      listeners.delete(listener);
    };
  }

  function isSupported() {
    return !!modelContext;
  }

  return {
    registerTool: registerTool,
    unregisterTool: unregisterTool,
    getTools: getTools,
    executeTool: executeTool,
    onToolsChanged: onToolsChanged,
    isSupported: isSupported,
  };
}

/**
 * Creates the page tool that calls one of the site's AI tools.
 *
 * @param {{ name: string, title?: string, description: string, inputSchema?: any }} descriptor The tool the site lists.
 * @param {{ invokeUrlTemplate: string, antiforgeryHeaderName?: string, antiforgeryToken?: string }} config The page configuration.
 * @param {Function} fetchImpl The fetch function.
 */
function createServerToolProxy(descriptor, config, fetchImpl) {
  if (!descriptor || !descriptor.name || !config.invokeUrlTemplate) {
    throw new TypeError('A site tool needs a name and an address to call.');
  }

  var url = config.invokeUrlTemplate.replace('{name}', encodeURIComponent(descriptor.name));

  return {
    name: descriptor.name,
    title: descriptor.title,
    description: descriptor.description,
    inputSchema: descriptor.inputSchema || { type: 'object', properties: {} },
    annotations: { untrustedContentHint: true },
    source: 'server',
    execute: async function (input) {
      var headers = { 'Content-Type': 'application/json' };

      if (config.antiforgeryHeaderName && config.antiforgeryToken) {
        headers[config.antiforgeryHeaderName] = config.antiforgeryToken;
      }

      try {
        var response = await fetchImpl(url, {
          method: 'POST',
          credentials: 'same-origin',
          headers: headers,
          body: JSON.stringify(input || {}),
        });

        if (!response.ok) {
          return textResult('The site refused the tool call (' + response.status + ').', true);
        }

        var data = await response.json();

        return {
          content: Array.isArray(data.content) ? data.content : [],
          isError: !!data.isError,
        };
      } catch (error) {
        return textResult('The tool call did not reach the site.', true);
      }
    },
  };
}

/**
 * Fetches the AI tools the site lets this user call and registers a proxy for each.
 *
 * @param {ReturnType<typeof createRegistry>} registry The page's list of tools.
 * @param {any} config The page configuration.
 * @param {Function} fetchImpl The fetch function.
 * @returns {Promise<number>} How many tools were registered.
 */
async function loadServerTools(registry, config, fetchImpl) {
  if (!config || !config.toolsUrl) {
    return 0;
  }

  try {
    var response = await fetchImpl(config.toolsUrl, { credentials: 'same-origin', headers: { Accept: 'application/json' } });

    if (!response.ok) {
      return 0;
    }

    var data = await response.json();
    var count = 0;

    (data && Array.isArray(data.tools) ? data.tools : []).forEach(function (descriptor) {
      try {
        registry.registerTool(createServerToolProxy(descriptor, config, fetchImpl));
        count++;
      } catch (error) {
        console.warn('The site\'s WebMCP tool could not be registered.', error);
      }
    });

    return count;
  } catch (error) {
    console.warn('The site\'s WebMCP tools could not be loaded.', error);

    return 0;
  }
}

/**
 * Reads the configuration block the site writes into the page.
 *
 * @param {any} doc The document.
 */
function readConfig(doc) {
  var element = doc && doc.getElementById ? doc.getElementById(CONFIG_ELEMENT_ID) : null;

  if (!element) {
    return null;
  }

  try {
    return JSON.parse(element.textContent);
  } catch (error) {
    return null;
  }
}

function init(win) {
  if (win.crestAppsWebMcp) {
    return win.crestAppsWebMcp;
  }

  var config = readConfig(win.document) || {};
  var registry = createRegistry(getModelContext(win.document, win.navigator));
  var api = Object.assign({}, registry, {
    config: config,
    textResult: textResult,
    serializeResult: serializeResult,
    isErrorResult: isErrorResult,
  });

  win.crestAppsWebMcp = api;

  if (config.toolsUrl) {
    loadServerTools(registry, config, win.fetch.bind(win));
  }

  win.document.dispatchEvent(new CustomEvent('crestapps-webmcp-ready', { detail: api }));

  return api;
}

globalThis.CrestAppsWebMcp = {
  getModelContext: getModelContext,
  createRegistry: createRegistry,
  createServerToolProxy: createServerToolProxy,
  loadServerTools: loadServerTools,
  readConfig: readConfig,
  init: init,
};

if (typeof window !== 'undefined' && typeof document !== 'undefined') {
  init(window);
}
