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

        return [
            active.interactionId,
            active.status,
            active.recordingState || '',
            active.isRecordingPaused === true ? 'paused' : '',
            active.recordingDisclosure ? 'disclosure-owed' : ''
        ].join(':');
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

    function format(template, value) {
        return String(template || '').replace('{0}', value);
    }

    // What the "Signed in to" card shows: a group of chips for the queues (with how many are waiting) and one for the
    // campaigns, leaving out a group the agent has nothing in. A long name is cut short on screen, so each chip keeps
    // the whole name in its title. It used to be a row of chips in the top bar, which an agent signed in to many queues
    // and campaigns, or to long-named ones, crowded.
    //   queues    - [{ name, waitingCount }]
    //   campaigns - [{ name }]
    //   labels    - { queues, campaigns, noSignIns, waiting ('{0} waiting') }
    function signInsHtml(queues, campaigns, labels) {
        var text = labels || {};
        var queueList = queues || [];
        var campaignList = campaigns || [];

        if (!queueList.length && !campaignList.length) {
            return '<div class="cc-sign-ins__none">' + escape(text.noSignIns || 'You are not signed in to any queue or campaign.') + '</div>';
        }

        function group(title, chips) {
            return '<div class="cc-sign-ins__group">' +
                '<div class="cc-sign-ins__label">' + escape(title) + '</div>' +
                '<div class="cc-sign-ins__chips">' + chips.join('') + '</div>' +
                '</div>';
        }

        var html = '';

        if (queueList.length) {
            html += group(text.queues || 'Queues', queueList.map(function (queue) {
                var count = Number(queue && queue.waitingCount) || 0;

                return '<span class="cc-queue-chip" title="' + escape(queue && queue.name) + '">' +
                    '<span class="cc-queue-chip__name">' + escape(queue && queue.name) + '</span>' +
                    '<span class="cc-queue-chip__count' + (count > 0 ? '' : ' is-empty') + '" title="' +
                    escape(format(text.waiting || '{0} waiting', count)) + '">' + count + '</span></span>';
            }));
        }

        if (campaignList.length) {
            html += group(text.campaigns || 'Campaigns', campaignList.map(function (campaign) {
                return '<span class="cc-queue-chip cc-queue-chip--campaign" title="' + escape(campaign && campaign.name) + '">' +
                    '<i class="fa-solid fa-bullhorn" aria-hidden="true"></i>' +
                    '<span class="cc-queue-chip__name">' + escape(campaign && campaign.name) + '</span></span>';
            }));
        }

        return html;
    }

    contactCenter.NO_ACTIVE_INTERACTION = NO_ACTIVE_INTERACTION;
    contactCenter.signInsHtml = signInsHtml;
    contactCenter.activeInteractionSignature = activeInteractionSignature;
    contactCenter.createChangeGate = createChangeGate;
    contactCenter.emptyStateHtml = emptyStateHtml;
}(typeof globalThis !== 'undefined' ? globalThis : window));
