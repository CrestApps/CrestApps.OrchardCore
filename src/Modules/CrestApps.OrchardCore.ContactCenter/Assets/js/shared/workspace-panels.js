/*
 * How the agent workspace's cards decide to redraw, and what an empty card shows.
 *
 * The "Active interaction" card opened blank: it only redrew when the interaction's signature changed, and "no
 * interaction" had the same signature as "never drawn", so the first state the page loaded was skipped and the
 * empty state never appeared until a call had come and gone. A change gate always lets its first value through.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundles and under the unit tests.
 */
(function (root) {
    'use strict';

    var contactCenter = root.CrestAppsContactCenter = root.CrestAppsContactCenter || {};

    // The signature of the card when the agent has no active interaction.
    var NO_ACTIVE_INTERACTION = 'none';

    function escape(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    // Everything the active-interaction card shows that can change while it is on screen.
    function activeInteractionSignature(active) {
        if (!active) {
            return NO_ACTIVE_INTERACTION;
        }

        return [active.interactionId, active.status, active.recordingState || '', active.isRecordingPaused === true ? 'paused' : ''].join(':');
    }

    // Answers whether a card must redraw for a value. The first value always redraws, whatever it is.
    function createChangeGate() {
        var drawn = false;
        var last;

        return function changed(signature) {
            if (drawn && signature === last) {
                return false;
            }

            drawn = true;
            last = signature;

            return true;
        };
    }

    // An empty card: an icon, a short headline and an optional hint. Matches the markup the server renders first.
    function emptyStateHtml(options, tagName) {
        var settings = options || {};
        var tag = tagName || 'div';

        return '<' + tag + ' class="cc-empty" data-cc-empty>' +
            '<div class="cc-empty__icon" aria-hidden="true"><i class="' + escape(settings.icon || 'fa-regular fa-circle-check') + '"></i></div>' +
            '<div class="cc-empty__title">' + escape(settings.title) + '</div>' +
            (settings.hint ? '<div class="cc-empty__hint">' + escape(settings.hint) + '</div>' : '') +
            '</' + tag + '>';
    }

    contactCenter.NO_ACTIVE_INTERACTION = NO_ACTIVE_INTERACTION;
    contactCenter.activeInteractionSignature = activeInteractionSignature;
    contactCenter.createChangeGate = createChangeGate;
    contactCenter.emptyStateHtml = emptyStateHtml;
}(typeof globalThis !== 'undefined' ? globalThis : window));
