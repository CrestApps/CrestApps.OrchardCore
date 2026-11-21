// The chat scripts (ai-chat.js and chat-interaction.js, vendored from @crestapps/ai-chat-ui) render a model's answer as
// HTML through DOMPurify and v-html, and start a chat from any element added to the page that carries a chat
// configuration. DOMPurify's defaults keep data-* attributes and id, so an answer could carry its own chat configuration,
// whose messageTemplate Vue then compiled and ran as script in the site's origin. Other scripts on the page act on data
// attributes too: Orchard's admin theme posts a form with the page's antiforgery token for a click on
// data-url-af="UnsafeUrl", and writes data-title into its confirm dialog as HTML. This guard closes that from outside
// the vendored bundles, so it holds whether the page loads the local copy or the CDN one and survives a package bump.
//
// An answer could also reach the page's own form controls: a label stretched over the page with for set to a control's
// id (a submit button, a role checkbox) turns the next click anywhere into a click on that control, and a form of the
// answer's own can post off-site. On Chat Interactions the settings script collects prompt templates and tool
// checkboxes from the whole document, by class and by name, so markup in an answer was saved as the user's settings.
// The guard removes those elements and attributes from every call; none of the chat's own markup uses them.
//
// The AG-UI tool cards and approval buttons carry the call's and the approval's ids, and a click on a button in that
// shape answers the approval. The chat sanitizes its own tool cards with no config and every answer with one (through
// CoreAIImages.sanitize), so the guard keeps the AG-UI attributes only in a call made without a config: an answer that
// writes the whole box itself, ids included, keeps none of them.

// The attributes that configure or start a chat or a chat widget. The watcher removes them from anything rendered.
const chatAttributePattern = /^data-(?:coreai-chat|chat-interaction)(?:-|$)/i;

// The only data attributes an answer keeps: the ones the chat scripts write into the markup they pass through DOMPurify
// themselves, and act on afterwards. The chart download button (data-chart-id), the document bar's remove and dismiss buttons (data-doc-index, data-error-index), and the image
// carousel's controls (data-carousel-*, data-stack-offset, data-active-index, data-image-count).
const allowedDataAttributePattern = /^data-(?:chart-id|doc-index|error-index|carousel-(?:key|size|size-option|step|target)|stack-offset|active-index|image-count)$/i;

// The chart renderer gives each chart's canvas an id, which the chart is drawn into; every other id is refused.
const chartCanvasIdPattern = /^chat_chart_\d+$/;

// Form elements: an answer shows no form, field or label of its own. Their text is kept.
const formElementNames = ['form', 'textarea', 'select', 'option', 'optgroup', 'datalist', 'output', 'label'];

// The attributes that tie an element to another control of the page (for, list, form, the invoker and popover targets),
// or name a value a form or a settings script reads (name).
const linkingAttributeNames = new Set(['for', 'name', 'list', 'form', 'formaction', 'formmethod', 'formenctype', 'formtarget',
    'formnovalidate', 'command', 'commandfor', 'popovertarget', 'popovertargetaction']);

// The classes Chat Interactions' settings script selects on, anywhere in the document, to collect and save settings.
const settingsClassPattern = /^(?:prompt-template-[\w-]+|remove-prompt-template-btn|capability-checkbox|setting-input|group-toggle|ci-agent-global-toggle)$/i;

// The AG-UI attributes: the tool card's call id, and the approval buttons' attributes, which the chat answers a pending
// tool approval from. Kept only in the chat's own markup.
const toolCallAttributePattern = /^data-agui-tool-call$/i;
const approvalAttributePattern = /^data-agui-(?:interrupt|approved)$/i;

const sanitizerMarker = '__crestAppsChatOutputGuard';
const ownMarkupMarker = '__crestAppsChatOutputGuardOwnMarkup';
const watcherMarker = '__crestAppsChatOutputWatcher';

/**
 * Returns whether an answer may keep an attribute: no data attribute but the chat's own, no id except a chart's canvas,
 * nothing that ties an element to another control of the page, checked only on a disabled checkbox (a task list), and
 * the AG-UI attributes only in the chat's own markup, the approval ones only on a button of its approval box.
 * @param {string} elementName The element's tag name.
 * @param {string} attributeName The attribute's name.
 * @param {string} attributeValue The attribute's value.
 * @param {Element} [element] The element, for the rules that look at it; without it those attributes are refused.
 * @param {boolean} [ownMarkup] Whether the call sanitizes the chat's own markup (made without a config); otherwise the
 * AG-UI attributes are refused.
 * @returns {boolean} Whether the attribute is kept.
 */
export function isAllowedAttribute(elementName, attributeName, attributeValue, element, ownMarkup) {
    const name = String(attributeName || '').toLowerCase();

    if (approvalAttributePattern.test(name)) {
        return ownMarkup === true && isApprovalButton(element);
    }

    if (toolCallAttributePattern.test(name)) {
        return ownMarkup === true;
    }

    if (name.startsWith('data-')) {
        return allowedDataAttributePattern.test(name);
    }

    if (name === 'id') {
        return String(elementName || '').toUpperCase() === 'CANVAS' && chartCanvasIdPattern.test(String(attributeValue || ''));
    }

    if (name === 'checked') {
        return isTaskListCheckbox(element);
    }

    return !linkingAttributeNames.has(name);
}

/**
 * Removes the classes Chat Interactions' settings script selects on from an answer's class attribute.
 * @param {string} value The class attribute's value.
 * @returns {string} The classes that are kept.
 */
export function withoutSettingsClasses(value) {
    return String(value || '').split(/\s+/).filter(token => token && !settingsClassPattern.test(token)).join(' ');
}

function isApprovalButton(element) {
    const parent = element && element.parentElement;

    return !!parent && String(element.nodeName).toUpperCase() === 'BUTTON' && !!parent.classList && parent.classList.contains('ai-chat-agui-approval');
}

function isTaskListCheckbox(element) {
    return !!element
        && String(element.nodeName).toUpperCase() === 'INPUT'
        && String(element.getAttribute('type') || '').trim().toLowerCase() === 'checkbox'
        && element.hasAttribute('disabled');
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

    // A form element is dropped from the allowed tags of the call, so DOMPurify removes it and keeps its text.
    purify.addHook('uponSanitizeElement', (node, data) => {
        if (data && data.allowedTags) {
            for (const name of formElementNames) {
                delete data.allowedTags[name];
            }
        }
    });

    // The chat passes its own tool cards to DOMPurify with no config, and every answer with one: the call is marked so
    // the attribute hook can tell them apart.
    const sanitize = purify.sanitize;

    purify.sanitize = function (dirty, config) {
        const previous = purify[ownMarkupMarker];

        purify[ownMarkupMarker] = config === undefined;

        try {
            return sanitize.apply(this, arguments);
        } finally {
            purify[ownMarkupMarker] = previous;
        }
    };

    purify.addHook('uponSanitizeAttribute', (node, data) => {
        if (!isAllowedAttribute(node && node.nodeName, data.attrName, data.attrValue, node, purify[ownMarkupMarker] === true)) {
            data.keepAttr = false;

            return;
        }

        if (String(data.attrName).toLowerCase() === 'class') {
            data.attrValue = withoutSettingsClasses(data.attrValue);
        }
    });

    // Every button the chat's markup renders is type="button"; any other one would submit a form.
    purify.addHook('afterSanitizeAttributes', node => {
        if (node && String(node.nodeName).toUpperCase() === 'BUTTON' && typeof node.setAttribute === 'function') {
            node.setAttribute('type', 'button');
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
