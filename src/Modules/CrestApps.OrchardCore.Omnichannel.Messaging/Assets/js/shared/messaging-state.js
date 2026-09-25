/*
 * The decisions the messaging workspace page makes about what it hears, kept free of the DOM so they can be tested.
 *
 * The page shows a customer list and one customer's conversation, with a tab per channel above it. A new message can
 * belong to the conversation on screen (append it), to another channel of the customer on screen (badge that
 * channel's tab), or to somebody else (reorder the list and raise a toast). Getting that wrong either hides a waiting
 * customer or badges the thread the agent is already reading.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var messaging = root.CrestAppsMessaging = root.CrestAppsMessaging || {};

    // .NET ticks are ~18-digit values beyond JavaScript's safe-integer range, so they are compared as BigInt and
    // returned as the exact decimal string. Rounding them (parseInt) made the server re-send the newest message on
    // every poll, which piled up as duplicates.
    function maxTicks(values) {
        var max = 0n;

        (values || []).forEach(function (raw) {
            if (!raw) {
                return;
            }

            try {
                var ticks = BigInt(raw);

                if (ticks > max) {
                    max = ticks;
                }
            } catch (e) {
                // A malformed value is ignored rather than breaking the poll.
            }
        });

        return max.toString();
    }

    // Classifies an inbound-message notification against what is on screen.
    //   'thread'   - it belongs to the open conversation: append it.
    //   'tab'      - it belongs to the open customer on another channel: badge that channel's tab.
    //   'elsewhere'- it belongs to another customer (or nothing is open).
    function classifyInbound(notification, view) {
        if (!notification) {
            return 'elsewhere';
        }

        var openConversationId = view && view.conversationId;
        var openCustomerKey = view && view.customerKey;

        if (openConversationId && notification.conversationId === openConversationId) {
            return 'thread';
        }

        if (openCustomerKey && notification.customerKey === openCustomerKey && notification.channel) {
            return 'tab';
        }

        return 'elsewhere';
    }

    // The badge a channel tab shows after a notification for that channel. The notification carries the unread count
    // of the conversation it belongs to, which is authoritative for that conversation, so the badge never drifts from
    // what the server holds the way incrementing on every event did.
    function tabBadgeCount(notification) {
        var count = notification && Number(notification.unreadCount);

        return count > 0 ? Math.floor(count) : 1;
    }

    // Whether a list row survives the search box. Every term must appear, in any order and case.
    function rowMatchesFilter(filterValue, term) {
        var terms = (term || '').toLowerCase().split(/\s+/).filter(function (part) { return part.length > 0; });

        if (terms.length === 0) {
            return true;
        }

        var value = (filterValue || '').toLowerCase();

        return terms.every(function (part) { return value.indexOf(part) >= 0; });
    }

    // Picks the incoming bubbles that are not on screen yet. A bubble is recognised by its message id, or by its
    // creation ticks when it has none, and the batch itself cannot add the same bubble twice.
    function selectNewBubbles(existing, incoming) {
        var seenIds = {};
        var seenTicks = {};

        (existing || []).forEach(function (bubble) {
            if (bubble.id) { seenIds[bubble.id] = true; }
            if (bubble.ticks) { seenTicks[bubble.ticks] = true; }
        });

        return (incoming || []).filter(function (bubble) {
            if ((bubble.id && seenIds[bubble.id]) || (!bubble.id && bubble.ticks && seenTicks[bubble.ticks])) {
                return false;
            }

            if (bubble.id) { seenIds[bubble.id] = true; }
            if (bubble.ticks) { seenTicks[bubble.ticks] = true; }

            return true;
        });
    }

    // How many of the newly appended bubbles the agent has not seen yet. A customer's message that lands while the agent
    // is scrolled up reading history, or has switched to another browser tab, is waiting for them even though it is in
    // the open thread, so the open channel's tab counts it until they come back to the bottom.
    function unseenInboundCount(bubbles, seen) {
        if (seen) {
            return 0;
        }

        return (bubbles || []).filter(function (bubble) { return bubble && bubble.inbound; }).length;
    }

    // A link built from page data only ever leads back into this site over http(s), returned as a path, so a crafted
    // value such as a javascript: URL or another site's address can never become a clickable link.
    function sameOriginUrl(value, baseUrl) {
        if (!value) {
            return null;
        }

        try {
            var base = new URL(String(baseUrl));
            var url = new URL(String(value), base);

            if ((url.protocol === 'http:' || url.protocol === 'https:') && url.origin === base.origin) {
                return url.pathname + url.search + url.hash;
            }
        } catch (e) {
            // A value that is not a URL is simply not linked.
        }

        return null;
    }

    messaging.unseenInboundCount = unseenInboundCount;
    messaging.sameOriginUrl = sameOriginUrl;
    messaging.maxTicks = maxTicks;
    messaging.classifyInbound = classifyInbound;
    messaging.tabBadgeCount = tabBadgeCount;
    messaging.rowMatchesFilter = rowMatchesFilter;
    messaging.selectNewBubbles = selectNewBubbles;
}(typeof globalThis !== 'undefined' ? globalThis : window));
