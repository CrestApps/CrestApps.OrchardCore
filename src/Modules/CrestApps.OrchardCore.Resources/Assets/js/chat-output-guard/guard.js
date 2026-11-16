// The chat scripts (ai-chat.js and chat-interaction.js, vendored from @crestapps/ai-chat-ui) render a model's answer as
// HTML through DOMPurify and v-html, and start a chat from any element added to the page that carries a chat
// configuration. DOMPurify's defaults keep data-* attributes and id, so an answer could carry its own chat configuration,
// whose messageTemplate Vue then compiled and ran as script in the site's origin. Other scripts on the page act on data
// attributes too: Orchard's admin theme posts a form with the page's antiforgery token for a click on
// data-url-af="UnsafeUrl", and writes data-title into its confirm dialog as HTML. This guard closes that from outside
// the vendored bundles, so it holds whether the page loads the local copy or the CDN one and survives a package bump.

// The attributes that configure or start a chat or a chat widget. The watcher removes them from anything rendered.
const chatAttributePattern = /^data-(?:coreai-chat|chat-interaction)(?:-|$)/i;

// The only data attributes an answer keeps: the ones the chat scripts write into the markup they pass through DOMPurify
// themselves, and act on afterwards. The chart download button (data-chart-id), the AG-UI tool cards and approval
// buttons (data-agui-*), the document bar's remove and dismiss buttons (data-doc-index, data-error-index), and the image
// carousel's controls (data-carousel-*, data-stack-offset, data-active-index, data-image-count).
const allowedDataAttributePattern = /^data-(?:chart-id|agui-(?:tool-call|interrupt|approved)|doc-index|error-index|carousel-(?:key|size|size-option|step|target)|stack-offset|active-index|image-count)$/i;

// The chart renderer gives each chart's canvas an id, which the chart is drawn into; every other id is refused.
const chartCanvasIdPattern = /^chat_chart_\d+$/;

const sanitizerMarker = '__crestAppsChatOutputGuard';
const watcherMarker = '__crestAppsChatOutputWatcher';

/**
 * Returns whether an answer may keep an attribute: no data attribute but the chat's own, and no id except a chart's
 * canvas.
 * @param {string} elementName The element's tag name.
 * @param {string} attributeName The attribute's name.
 * @param {string} attributeValue The attribute's value.
 * @returns {boolean} Whether the attribute is kept.
 */
export function isAllowedAttribute(elementName, attributeName, attributeValue) {
    const name = String(attributeName || '').toLowerCase();

    if (name.startsWith('data-')) {
        return allowedDataAttributePattern.test(name);
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
    for (const name of element.getAttributeNames()) {
        if (chatAttributePattern.test(name)) {
            element.removeAttribute(name);
        }
    }
}

/**
 * Removes every chat and chat widget attribute from anything added inside a mounted chat app (a rendered message),
 * before the chat scripts' own watchers see it. It uses the attribute pattern rather than a list of names, so an
 * attribute a later package version starts on is covered too. It leaves the other data attributes alone: the chat's own
 * template renders some inside the app. It must be installed before those scripts run: mutation observers are called in the
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
                node.querySelectorAll('*').forEach(disarm);
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
