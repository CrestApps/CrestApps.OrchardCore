/*
 * Hands the incoming-call prompt to a desktop host that notifies for it, and takes the prompt back when the host does
 * not confirm it.
 *
 * A desktop app that hosts the standalone /softphone page in an embedded browser (WebView2) shows its own incoming-call
 * notification. The page's full-screen incoming modal then showed at the same time, so the agent saw the same call
 * twice. The page now tells the host about the ringing call and hides its modal only when the host confirms that its
 * notification for that exact call is on screen. If the host does not confirm within HOST_ACK_TIMEOUT_MS, or it later
 * reports that its notification was closed without an answer, the page shows its modal again. The page never hides
 * the modal on a host's word alone: a host that is missing, old, or broken leaves the modal exactly as before.
 *
 * Messages travel over window.chrome.webview, which exists only inside WebView2. A normal browser tab or the browser
 * extension never has it, so the modal there is unchanged and shows without delay.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    var HOST_BRIDGE_PROTOCOL = 1;

    // How long the page waits for the host to confirm its notification before showing the modal itself.
    var HOST_ACK_TIMEOUT_MS = 2000;

    // How long the page stays quiet after the agent acted in the host's notification. If the call is still ringing
    // after this (for example, the answer could not start the audio), the page shows its modal so the call is not lost.
    // An answer the page reports as still under way (registering, then accepting) extends the wait; that answer has
    // its own time limit, after which the call rings again and the modal comes back.
    var HOST_ACTION_GRACE_MS = 5000;

    var PAGE_MESSAGES = {
        ready: 'softphone-ready',
        incomingCall: 'incoming-call',
        incomingCallEnded: 'incoming-call-ended',
        actionResult: 'incoming-call-action-result'
    };

    var HOST_MESSAGES = {
        ready: 'host-ready',
        shown: 'incoming-call-shown',
        dismissed: 'incoming-call-dismissed',
        action: 'incoming-call-action'
    };

    var HOST_ACTIONS = ['answer', 'decline', 'voicemail'];

    // The WebView2 message channel, or null when the page does not run inside WebView2.
    function findHostChannel(win) {
        var channel = win && win.chrome ? win.chrome.webview : null;

        if (!channel || typeof channel.postMessage !== 'function' || typeof channel.addEventListener !== 'function') {
            return null;
        }

        return channel;
    }

    function text(value) {
        return typeof value === 'string' ? value : '';
    }

    // An absolute http(s) URL, or '' for anything else. Card URLs are path-only on the server; the host opens them
    // outside the page, so they must be absolute, and only web links are ever handed over.
    function absoluteHttpUrl(url, baseUrl) {
        if (typeof url !== 'string' || !url.trim()) {
            return '';
        }

        try {
            var resolved = new URL(url.trim(), baseUrl);

            return resolved.protocol === 'https:' || resolved.protocol === 'http:' ? resolved.href : '';
        } catch (e) {
            return '';
        }
    }

    function buildCard(card, baseUrl) {
        card = card || {};

        var links = Array.isArray(card.links) ? card.links : [];

        return {
            id: text(card.id),
            title: text(card.title),
            subtitle: text(card.subtitle),
            description: text(card.description),
            badges: (Array.isArray(card.badges) ? card.badges : []).filter(function (badge) {
                return typeof badge === 'string' && badge;
            }),
            url: absoluteHttpUrl(card.url, baseUrl),
            answerAndOpenText: text(card.answerAndOpenText),
            openText: text(card.openText),
            links: links.map(function (link) {
                return link ? { text: text(link.text), url: absoluteHttpUrl(link.url, baseUrl) } : null;
            }).filter(function (link) {
                return !!(link && link.url);
            })
        };
    }

    // The 'incoming-call' message for the offer on screen. offer is { callId, from, queue, heading, canVoicemail,
    // cards }, where each card already carries its resolved action labels. baseUrl resolves the path-only card URLs.
    function buildIncomingCallMessage(offer, baseUrl) {
        offer = offer || {};

        return {
            type: PAGE_MESSAGES.incomingCall,
            protocol: HOST_BRIDGE_PROTOCOL,
            callId: text(offer.callId),
            from: text(offer.from),
            queue: text(offer.queue),
            heading: text(offer.heading),
            canVoicemail: !!offer.canVoicemail,
            cards: (Array.isArray(offer.cards) ? offer.cards : []).map(function (card) {
                return buildCard(card, baseUrl);
            })
        };
    }

    // A message from the host, normalized, or null when it is not one this page understands.
    function readHostMessage(data) {
        if (typeof data === 'string') {
            try {
                data = JSON.parse(data);
            } catch (e) {
                return null;
            }
        }

        if (!data || typeof data !== 'object' || typeof data.type !== 'string') {
            return null;
        }

        var protocol = typeof data.protocol === 'number' ? data.protocol : 0;

        switch (data.type) {
            case HOST_MESSAGES.ready:
                return protocol >= HOST_BRIDGE_PROTOCOL ? { type: HOST_MESSAGES.ready, protocol: protocol } : null;

            case HOST_MESSAGES.shown:
            case HOST_MESSAGES.dismissed:
                if (!text(data.callId)) {
                    return null;
                }

                return { type: data.type, callId: data.callId, ringing: !!data.ringing };

            case HOST_MESSAGES.action:
                if (!text(data.callId) || HOST_ACTIONS.indexOf(data.action) === -1) {
                    return null;
                }

                return { type: HOST_MESSAGES.action, callId: data.callId, action: data.action };

            default:
                return null;
        }
    }

    // What the page shows for a ringing offer while a host may be notifying for it.
    //   status 'pending'  - the host was told and has not confirmed yet: keep whatever was on screen.
    //   status 'shown'    - the host confirmed its notification: hide the modal, ring only if the host does not.
    //   status 'acting'   - the agent acted in the host's notification: stay quiet while the action runs.
    //   status 'fallback' - the host did not confirm, or its notification was closed: show the modal and ring.
    function presentationFor(offerVisible, delegation) {
        if (!offerVisible) {
            return { showModal: false, ring: false };
        }

        if (!delegation) {
            return { showModal: true, ring: true };
        }

        switch (delegation.status) {
            case 'pending':
                return { showModal: delegation.modalOnScreen, ring: delegation.modalOnScreen };
            case 'shown':
                return { showModal: false, ring: !delegation.hostRinging };
            case 'acting':
                return { showModal: false, ring: false };
            default:
                return { showModal: true, ring: true };
        }
    }

    // The per-page handoff state. options:
    //   post(message)        - sends a message to the host.
    //   onChange()           - called when the presentation may have changed (re-render).
    //   schedule(fn, ms)     - optional timer (defaults to setTimeout); returns a handle.
    //   cancel(handle)       - optional timer cancel (defaults to clearTimeout).
    //   ackTimeoutMs         - optional, defaults to HOST_ACK_TIMEOUT_MS.
    //   actionGraceMs        - optional, defaults to HOST_ACTION_GRACE_MS.
    //   isActionUnderWay()   - optional; true while the chosen action is still running (e.g. an answer registering).
    function createHostDelegation(options) {
        options = options || {};

        var post = options.post || function () { };
        var onChange = options.onChange || function () { };
        var isActionUnderWay = options.isActionUnderWay || function () { return false; };
        var schedule = options.schedule || function (fn, ms) { return root.setTimeout(fn, ms); };
        var cancel = options.cancel || function (handle) { root.clearTimeout(handle); };
        var ackTimeoutMs = typeof options.ackTimeoutMs === 'number' ? options.ackTimeoutMs : HOST_ACK_TIMEOUT_MS;
        var actionGraceMs = typeof options.actionGraceMs === 'number' ? options.actionGraceMs : HOST_ACTION_GRACE_MS;

        var hostReady = false;
        var current = null;

        function stopTimer() {
            if (current && current.timer !== null) {
                cancel(current.timer);
                current.timer = null;
            }
        }

        function fallBackAfter(ms) {
            var delegation = current;

            stopTimer();
            delegation.timer = schedule(function () {
                if (current !== delegation) {
                    return;
                }

                delegation.timer = null;

                if (delegation.status === 'acting' && isActionUnderWay()) {
                    fallBackAfter(ms);

                    return;
                }

                if (delegation.status === 'pending' || delegation.status === 'acting') {
                    delegation.status = 'fallback';
                    onChange();
                }
            }, ms);
        }

        function end() {
            if (!current) {
                return;
            }

            stopTimer();
            post({ type: PAGE_MESSAGES.incomingCallEnded, callId: current.callId });
            current = null;
        }

        return {
            // Whether a host answered the page's 'softphone-ready' and takes part in the handoff.
            isHostReady: function () {
                return hostReady;
            },

            // The handoff state for the offer on screen (null when none). Read by the presentation.
            current: function () {
                return current;
            },

            // Called on every render. message is the 'incoming-call' message for the ringing offer, or null when no
            // offer is on screen. modalOnScreen says whether the page's modal is showing right now.
            sync: function (message, modalOnScreen) {
                if (!message || !message.callId) {
                    end();

                    return;
                }

                if (!hostReady) {
                    return;
                }

                if (!current || current.callId !== message.callId) {
                    end();
                    current = {
                        callId: message.callId,
                        status: 'pending',
                        hostRinging: false,
                        modalOnScreen: !!modalOnScreen,
                        signature: '',
                        timer: null
                    };
                    fallBackAfter(ackTimeoutMs);
                }

                // Re-send when the offer's details change (for example, matched records arrive after the ring).
                var signature = JSON.stringify(message);

                if (signature !== current.signature) {
                    current.signature = signature;
                    post(message);
                }
            },

            // Applies a message from the host. Returns { action, callId, matchesOffer } when the agent chose an
            // action in the host's notification, otherwise null.
            receive: function (data) {
                var message = readHostMessage(data);

                if (!message) {
                    return null;
                }

                var matches = !!(current && current.callId === message.callId);

                switch (message.type) {
                    case HOST_MESSAGES.ready:
                        if (!hostReady) {
                            hostReady = true;
                            onChange();
                        }

                        return null;

                    case HOST_MESSAGES.shown:
                        if (matches && current.status !== 'acting') {
                            stopTimer();
                            current.status = 'shown';
                            current.hostRinging = message.ringing;
                            onChange();
                        }

                        return null;

                    case HOST_MESSAGES.dismissed:
                        if (matches && current.status !== 'fallback') {
                            stopTimer();
                            current.status = 'fallback';
                            onChange();
                        }

                        return null;

                    case HOST_MESSAGES.action:
                        if (matches) {
                            current.status = 'acting';
                            fallBackAfter(actionGraceMs);
                            onChange();
                        }

                        return { action: message.action, callId: message.callId, matchesOffer: matches };

                    default:
                        return null;
                }
            },

            // { showModal, ring } for the offer on screen.
            presentation: function (offerVisible) {
                return presentationFor(offerVisible, current);
            },

            // Forgets the offer without telling the host (the page is going away).
            reset: function () {
                stopTimer();
                current = null;
            }
        };
    }

    softPhone.HOST_BRIDGE_PROTOCOL = HOST_BRIDGE_PROTOCOL;
    softPhone.HOST_ACK_TIMEOUT_MS = HOST_ACK_TIMEOUT_MS;
    softPhone.HOST_ACTION_GRACE_MS = HOST_ACTION_GRACE_MS;
    softPhone.HOST_PAGE_MESSAGES = PAGE_MESSAGES;
    softPhone.HOST_MESSAGES = HOST_MESSAGES;
    softPhone.findHostChannel = findHostChannel;
    softPhone.buildIncomingCallMessage = buildIncomingCallMessage;
    softPhone.readHostMessage = readHostMessage;
    softPhone.presentationFor = presentationFor;
    softPhone.createHostDelegation = createHostDelegation;
}(typeof globalThis !== 'undefined' ? globalThis : window));
