window.collapsiblePanel = (function () {
    'use strict';

    const storagePrefix = 'crestapps.collapsible-panel.';
    const expandedState = 'expanded';
    const collapsedState = 'collapsed';
    const initializedAttribute = 'data-collapsible-panel-initialized';

    const ready = (callback) => {
        if (document.readyState !== 'loading') {
            callback();

            return;
        }

        document.addEventListener('DOMContentLoaded', callback, { once: true });
    };

    const readState = (key) => {
        try {
            return window.localStorage.getItem(storagePrefix + key);
        } catch {
            return null;
        }
    };

    const writeState = (key, state) => {
        try {
            window.localStorage.setItem(storagePrefix + key, state);
        } catch {
            // Storage can be unavailable (private mode or disabled cookies). The panel still works, it just won't remember its state.
        }
    };

    const findTriggers = (panel) => {
        if (!panel.id) {
            return [];
        }

        const selector = '[data-bs-toggle="collapse"][data-bs-target="#' + panel.id + '"], [data-bs-toggle="collapse"][href="#' + panel.id + '"]';

        return Array.from(document.querySelectorAll(selector));
    };

    const syncTriggers = (panel, isExpanded) => {
        findTriggers(panel).forEach((trigger) => {
            trigger.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
            trigger.classList.toggle('collapsed', !isExpanded);

            trigger.querySelectorAll('[data-collapsible-panel-icon]').forEach((icon) => {
                icon.classList.toggle('fa-chevron-up', isExpanded);
                icon.classList.toggle('fa-chevron-down', !isExpanded);
            });
        });
    };

    const initialize = (panel) => {
        const key = panel.getAttribute('data-collapsible-panel');

        if (!key || panel.hasAttribute(initializedAttribute)) {
            return;
        }

        panel.setAttribute(initializedAttribute, 'true');

        const storedState = readState(key);

        if (storedState === collapsedState) {
            panel.classList.remove('show');
        } else if (storedState === expandedState) {
            panel.classList.add('show');
        }

        syncTriggers(panel, panel.classList.contains('show'));

        panel.addEventListener('shown.bs.collapse', (event) => {
            if (event.target === panel) {
                writeState(key, expandedState);
                syncTriggers(panel, true);
            }
        });

        panel.addEventListener('hidden.bs.collapse', (event) => {
            if (event.target === panel) {
                writeState(key, collapsedState);
                syncTriggers(panel, false);
            }
        });
    };

    const initializeAll = (container) => {
        (container || document).querySelectorAll('[data-collapsible-panel]').forEach(initialize);
    };

    ready(() => initializeAll(document));

    return {
        initialize: initialize,
        initializeAll: initializeAll
    };
})();
