// The chat scripts (ai-chat.js and chat-interaction.js, vendored from @crestapps/ai-chat-ui) render a model's answer as
// HTML through DOMPurify and v-html, and start a chat from any element added to the page that carries a chat
// configuration. DOMPurify's defaults keep data-* attributes and id, so an answer could carry its own chat configuration,
// whose messageTemplate Vue then compiled and ran as script in the site's origin. This guard closes that from outside
// the vendored bundles, so it holds whether the page loads the local copy or the CDN one and survives a package bump.

// The attributes that configure or start a chat. Any of them in an answer is refused.
const chatAttributePattern = /^data-(?:coreai-chat|chat-interaction)(?:-|$)/i;

// The attributes the chat scripts' watchers look for to start a chat.
const autoStartAttributes = ['data-coreai-chat-config', 'data-coreai-chat-app-element-selector', 'data-chat-interaction-config'];

// The chart renderer gives each chart's canvas an id, which the chart is drawn into; every other id is refused.
const chartCanvasIdPattern = /^chat_chart_\d+$/;

const sanitizerMarker = '__crestAppsChatOutputGuard';
const watcherMarker = '__crestAppsChatOutputWatcher';

/**
 * Returns whether an answer may keep an attribute: no chat configuration, and no id except a chart's canvas.
 * @param {string} elementName The element's tag name.
 * @param {string} attributeName The attribute's name.
 * @param {string} attributeValue The attribute's value.
 * @returns {boolean} Whether the attribute is kept.
 */
export function isAllowedAttribute(elementName, attributeName, attributeValue) {
    const name = String(attributeName || '').toLowerCase();

    if (chatAttributePattern.test(name)) {
        return false;
    }

    if (name === 'id') {
        return String(elementName || '').toUpperCase() === 'CANVAS' && chartCanvasIdPattern.test(String(attributeValue || ''));
    }

    return true;
}

/**
 * Makes DOMPurify drop the attributes an answer must not carry, from every call the chat scripts make.
 * @param {object} purify The page's DOMPurify.
 * @returns {boolean} Whether the hook is in place.
 */
export function hardenSanitizer(purify) {
    if (!purify || typeof purify.addHook !== 'function') {
        return false;
    }

    if (purify[sanitizerMarker]) {
        return true;
    }

    purify.addHook('uponSanitizeAttribute', (node, data) => {
        if (!isAllowedAttribute(node && node.nodeName, data.attrName, data.attrValue)) {
            data.keepAttr = false;
        }
    });

    purify[sanitizerMarker] = true;

    return true;
}

function isInsideMountedApp(element) {
    for (let current = element.parentElement; current; current = current.parentElement) {
        if (current.__vue_app__) {
            return true;
        }
    }

    return false;
}

function disarm(element) {
    for (const name of autoStartAttributes) {
        if (element.hasAttribute(name)) {
            element.removeAttribute(name);
        }
    }
}

/**
 * Removes the auto-start attributes from anything added inside a mounted chat app (a rendered message), before the chat
 * scripts' own watchers see it. It must be installed before those scripts run: mutation observers are called in the
 * order they were created.
 * @param {Window} window The page's window.
 * @returns {MutationObserver|null} The observer, or null when the page has none.
 */
export function watchRenderedOutput(window) {
    if (!window || typeof window.MutationObserver !== 'function') {
        return null;
    }

    if (window[watcherMarker]) {
        return window[watcherMarker];
    }

    const observer = new window.MutationObserver(mutations => {
        for (const mutation of mutations) {
            for (const node of mutation.addedNodes) {
                if (!node || node.nodeType !== 1 || !isInsideMountedApp(node)) {
                    continue;
                }

                disarm(node);
                node.querySelectorAll(autoStartAttributes.map(name => `[${name}]`).join(',')).forEach(disarm);
            }
        }
    });

    observer.observe(window.document.documentElement, { childList: true, subtree: true });
    window[watcherMarker] = observer;

    return observer;
}

/**
 * Installs both parts of the guard on a page.
 * @param {Window} window The page's window.
 */
export function installChatOutputGuard(window) {
    hardenSanitizer(window && window.DOMPurify);
    watchRenderedOutput(window);
}
