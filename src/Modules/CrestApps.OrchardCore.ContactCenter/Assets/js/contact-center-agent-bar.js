/*
 * Contact Center persistent docked agent bar.
 *
 * The bar is injected into the admin chrome on every page for a signed-in agent. It is the CRM-side bridge to
 * the call router while the soft phone runs in a separate window or the browser extension: it keeps a live
 * Contact Center hub connection, so a work assignment made while the agent is anywhere in the CRM still reaches
 * them. It alerts, pops the record, drives disposition, and exposes presence -- all without owning any call
 * media, which stays with the soft phone.
 *
 * It reuses the agent workspace state endpoint and the shared offer/presence/complete endpoints, and depends on
 * the "contact-center-realtime" helper (which itself pulls in "telephony-client" for escapeHtml/formatDuration).
 */
(function (window, document) {
    'use strict';

    var escapeHtml = (window.telephonyClient && window.telephonyClient.escapeHtml) || function (value) {
        var div = document.createElement('div');
        div.textContent = value == null ? '' : String(value);
        return div.innerHTML;
    };

    var formatDuration = (window.telephonyClient && window.telephonyClient.formatDuration) || function (seconds) {
        var total = Math.max(0, Math.floor(seconds || 0));
        var mins = Math.floor(total / 60);
        var secs = total % 60;
        return mins + ':' + (secs < 10 ? '0' : '') + secs;
    };

    // Present a raw destination in a human-readable form. North-American numbers get the familiar grouping; other
    // formats are left untouched so we never mangle an international number we cannot confidently parse.
    function formatPhone(value) {
        if (!value) {
            return '';
        }

        var digits = String(value).replace(/\D/g, '');

        if (digits.length === 11 && digits.charAt(0) === '1') {
            return '+1 (' + digits.slice(1, 4) + ') ' + digits.slice(4, 7) + '-' + digits.slice(7);
        }

        if (digits.length === 10) {
            return '(' + digits.slice(0, 3) + ') ' + digits.slice(3, 6) + '-' + digits.slice(6);
        }

        return String(value);
    }

    function parseConfig(root) {
        var raw = root.getAttribute('data-config');

        if (!raw) {
            return { strings: {} };
        }

        try {
            var config = JSON.parse(raw);
            config.strings = config.strings || {};
            config.dispositions = config.dispositions || [];
            config.reasonCodes = config.reasonCodes || [];
            return config;
        } catch (error) {
            return { strings: {} };
        }
    }

    function parseUtc(value) {
        if (!value) {
            return null;
        }

        var time = Date.parse(value);
        return isNaN(time) ? null : time;
    }

    function post(url, token, payload) {
        var body = new URLSearchParams();

        Object.keys(payload || {}).forEach(function (key) {
            if (payload[key] !== undefined && payload[key] !== null) {
                body.append(key, payload[key]);
            }
        });

        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': token || ''
            },
            body: body.toString()
        });
    }

    // Screen pop must never destroy unsaved work. Treat the page as unsafe to navigate away from when any form
    // control has been changed from its initial value, so the bar falls back to a sticky "Open activity" prompt
    // the agent clicks instead of yanking them off a half-filled form.
    function isPageDirty() {
        var controls = document.querySelectorAll('input, textarea, select');

        for (var i = 0; i < controls.length; i++) {
            var control = controls[i];
            var type = (control.type || '').toLowerCase();

            if (type === 'hidden' || type === 'submit' || type === 'button' || control.disabled) {
                continue;
            }

            if (type === 'checkbox' || type === 'radio') {
                if (control.checked !== control.defaultChecked) {
                    return true;
                }
            } else if (control.tagName === 'SELECT') {
                for (var o = 0; o < control.options.length; o++) {
                    if (control.options[o].selected !== control.options[o].defaultSelected) {
                        return true;
                    }
                }
            } else if (typeof control.defaultValue === 'string' && control.value !== control.defaultValue) {
                return true;
            }
        }

        return false;
    }

    function resolveSafeSameOriginUrl(candidate) {
        var resolved;

        try {
            resolved = new URL(candidate, window.location.origin);
        } catch (error) {
            return null;
        }

        if (resolved.protocol !== 'http:' && resolved.protocol !== 'https:') {
            return null;
        }

        if (resolved.origin !== window.location.origin) {
            return null;
        }

        return resolved.href;
    }

    function beep() {
        try {
            var Ctx = window.AudioContext || window.webkitAudioContext;

            if (!Ctx) {
                return;
            }

            var ctx = new Ctx();
            var oscillator = ctx.createOscillator();
            var gain = ctx.createGain();
            oscillator.connect(gain);
            gain.connect(ctx.destination);
            oscillator.type = 'sine';
            oscillator.frequency.value = 880;
            gain.gain.setValueAtTime(0.0001, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.2, ctx.currentTime + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.4);
            oscillator.start();
            oscillator.stop(ctx.currentTime + 0.42);
            oscillator.onended = function () {
                try { ctx.close(); } catch (e) { /* ignore */ }
            };
        } catch (error) {
            /* Audio is a courtesy; never let it break the bar. */
        }
    }

    function init(root) {
        var config = parseConfig(root);
        var strings = config.strings;
        var state = null;
        var serverOffsetMs = 0;
        var activeSignature = null;
        var connectionStatusKey = null;
        var lastPoppedActivityId = null;
        var lastOfferReservationId = null;

        var inner = root.querySelector('[data-cc-agent-bar-inner]');
        var handle = root.querySelector('[data-cc-agent-bar-handle]');
        root.hidden = false;

        // The bar stays out of the way by default: it is collapsed to a small tab the agent clicks to open. A new
        // phone offer forces it open on its own (tracked by reservation id so any one offer only auto-opens once,
        // and the agent can still collapse it back down); every other state — an active call, a post-call wrap-up
        // prompt — waits behind the tab until the agent opens it, so a lingering record never sits in their way.
        var collapsed = true;
        var autoOpenedOfferId = null;

        applyCollapsed();

        if (handle) {
            handle.addEventListener('click', function () { setCollapsed(false); });
        }

        function label(key, fallback) {
            return strings[key] || fallback;
        }

        function applyCollapsed() {
            root.classList.toggle('is-collapsed', collapsed);

            if (handle) {
                handle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
                handle.setAttribute('title', collapsed
                    ? label('expandBar', 'Open the Contact Center bar')
                    : label('collapseBar', 'Hide the Contact Center bar'));
            }

            updateHandleDot();
        }

        function setCollapsed(value) {
            if (collapsed === value) {
                return;
            }

            collapsed = value;
            applyCollapsed();
        }

        // Keep the collapsed tab's dot in step with the live presence colour so the agent can read their status
        // without opening the bar.
        function updateHandleDot() {
            var dot = handle ? handle.querySelector('[data-cc-handle-dot]') : null;

            if (dot) {
                dot.className = 'cc-bar__dot is-' + presenceStatus().toLowerCase();
            }
        }

        // A ringing/preview offer pulls the bar open on its own; once the offer clears the auto-open latch resets so
        // the next distinct offer opens it again, while an offer the agent has deliberately collapsed stays down.
        function syncCollapsedWithOffer() {
            var offer = state && state.offer;

            if (offer && offer.reservationId) {
                if (offer.reservationId !== autoOpenedOfferId) {
                    autoOpenedOfferId = offer.reservationId;
                    collapsed = false;
                }
            } else {
                autoOpenedOfferId = null;
            }

            applyCollapsed();
        }

        // The bar is fixed and horizontally centered entirely in CSS (see contact-center-agent-bar.scss).

        function computeOffset(serverTimeUtc) {
            var serverMs = parseUtc(serverTimeUtc);

            if (serverMs !== null) {
                serverOffsetMs = Date.now() - serverMs;
            }
        }

        function serverNow() {
            return Date.now() - serverOffsetMs;
        }

        function refresh() {
            if (!config.stateUrl) {
                return Promise.resolve();
            }

            return fetch(config.stateUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (data) {
                    if (data) {
                        render(data);
                    }
                })
                .catch(function () { });
        }

        function completeActivityUrl(activityId) {
            if (!config.completeActivityUrlTemplate || !activityId) {
                return null;
            }

            return resolveSafeSameOriginUrl(
                config.completeActivityUrlTemplate.replace('__activityId__', encodeURIComponent(activityId)));
        }

        // Pop the activity screen. When the page is dirty we never navigate; the caller keeps the sticky prompt so
        // the agent can choose to go when ready.
        function popActivity(activityId, force) {
            var target = completeActivityUrl(activityId);

            if (!target) {
                return false;
            }

            if (!force && isPageDirty()) {
                return false;
            }

            lastPoppedActivityId = activityId;
            window.location.assign(target);
            return true;
        }

        function render(data) {
            state = data;
            computeOffset(data.serverTimeUtc);
            syncCollapsedWithOffer();
            renderBar();
            tick();
        }

        function presenceStatus() {
            return (state && state.presence && state.presence.status) || 'Offline';
        }

        function renderBar() {
            if (!inner || !state) {
                return;
            }

            var status = presenceStatus();
            var reason = state.presence && state.presence.reason;
            var offer = state.offer;
            var active = state.activeInteraction;

            var signature = [
                status,
                reason || '',
                offer ? offer.reservationId : '',
                active ? active.interactionId + ':' + active.status : ''
            ].join('|');

            if (signature === activeSignature) {
                return;
            }

            activeSignature = signature;

            // The bar stacks vertically: a compact top row carries the read-only status chip and the connection
            // tail (headphone), and the work context — the ringing offer, active call, or wrap-up — expands as a
            // full-width block *below* that row rather than being squeezed between the two. The context collapses
            // to nothing when idle, so the bar is just the top row until work arrives.
            inner.innerHTML =
                '<div class="cc-agent-bar__top">' +
                    renderPresence(status, reason) +
                    renderTail() +
                '</div>' +
                '<div class="cc-bar__context" data-cc-context>' + renderContext(offer, active) + '</div>';

            bindEvents();
        }

        // Presence is read-only here: the soft phone is the single place an agent changes their status, so the
        // bar only reflects it (no picker) to avoid two competing status controls.
        function renderPresence(status, reason) {
            return '<div class="cc-bar__presence" data-cc-presence>' +
                '<span class="cc-bar__presence-chip" title="' + escapeHtml(label('presenceReadonly', 'Change your status from the soft phone')) + '">' +
                    '<span class="cc-bar__dot is-' + escapeHtml(status.toLowerCase()) + '"></span>' +
                    '<span class="cc-bar__presence-label">' + escapeHtml(reason || status) + '</span>' +
                '</span>' +
            '</div>';
        }

        function renderContext(offer, active) {
            if (offer) {
                return renderOffer(offer);
            }

            // A direct inbound call carries no CRM activity, so once it ends there is nothing to complete and the
            // bar returns to idle. Only dialer/queue work (which the call router assigned an activity) keeps a
            // "Complete activity" prompt after the call ends.
            if (active && !(isEnded(active) && !active.activityItemId)) {
                return renderActive(active);
            }

            // Idle renders nothing so the bar shrinks to the status chip; a call/assignment expands it again.
            return '';
        }

        // When work is offered the bar expands into a taller card: a heading with the countdown, then the offer
        // details stacked as labelled rows (method, contact, number, and the queue or campaign), and finally the
        // Dial/Skip (or Accept/Decline) actions along the bottom. The extra room is what lets the bar carry the
        // context an agent needs to decide before dialing, instead of a single cramped line.
        function renderOffer(offer) {
            var preview = offer.kind === 'PreviewDial';
            var contactRaw = offer.customerLabel || '';
            var contact = contactRaw
                ? (/[a-z]/i.test(contactRaw) ? contactRaw : formatPhone(contactRaw))
                : label('unknownCaller', 'Unknown caller');
            var number = offer.customerAddress ? formatPhone(offer.customerAddress) : '';
            var heading = preview ? label('previewDial', 'Preview — review then dial') : label('incomingCall', 'Incoming call');
            var acceptLabel = preview ? label('dial', 'Dial') : label('accept', 'Accept');
            var declineLabel = preview ? label('skip', 'Skip') : label('decline', 'Decline');

            var rows = detailRow(label('method', 'Method'), offerMethodLabel(offer.kind)) +
                detailRow(label('contact', 'Contact'), contact) +
                (number ? detailRow(label('number', 'Number'), number) : '') +
                (offer.queueName ? detailRow(preview ? label('campaign', 'Campaign') : label('queue', 'Queue'), offer.queueName) : '');

            return '<div class="cc-bar__offer is-expanded' + (preview ? ' is-preview' : ' is-ringing') + '" data-cc-offer>' +
                '<div class="cc-bar__offer-head">' + escapeHtml(heading) +
                    '<span class="cc-bar__countdown" data-cc-countdown></span>' +
                '</div>' +
                '<div class="cc-bar__offer-details">' + rows + '</div>' +
                '<div class="cc-bar__offer-actions">' +
                    '<button type="button" class="btn btn-success btn-sm" data-cc-accept><i class="fa-solid fa-phone" aria-hidden="true"></i> ' + escapeHtml(acceptLabel) + '</button>' +
                    '<button type="button" class="btn btn-outline-danger btn-sm" data-cc-decline>' + escapeHtml(declineLabel) + '</button>' +
                '</div>' +
            '</div>';
        }

        function detailRow(labelText, value) {
            if (!value) {
                return '';
            }

            return '<div class="cc-bar__offer-row">' +
                '<span class="cc-bar__offer-label">' + escapeHtml(labelText) + '</span>' +
                '<span class="cc-bar__offer-value">' + escapeHtml(value) + '</span>' +
            '</div>';
        }

        function offerMethodLabel(kind) {
            switch (kind) {
                case 'PreviewDial': return label('previewDialMethod', 'Preview dial');
                case 'AutoDial': return label('autoDialMethod', 'Automatic dial');
                case 'InboundCall': return label('inboundCallMethod', 'Inbound call');
                default: return label('callMethod', 'Call');
            }
        }

        // A live or just-ended call uses the same expanded card shape as an offer: a heading, the call details
        // stacked as labelled rows, and full-width actions along the bottom. Keeping the two states structurally
        // identical means the bar does not reshape itself the moment an offer is accepted, and the details have
        // room to grow vertically rather than being squeezed onto one line.
        function renderActive(active) {
            var inbound = active.direction === 'Inbound';
            // Prefer the contact's name; otherwise show the destination formatted for readability. The server falls
            // back to the raw address as the label when there is no contact, so format any label that is just a
            // number (no letters) and leave real names alone.
            var contactRaw = active.customerLabel || active.customerAddress || '';
            var contact = contactRaw
                ? (/[a-z]/i.test(contactRaw) ? contactRaw : formatPhone(contactRaw))
                : label('unknownCaller', 'Unknown caller');
            var number = active.customerAddress ? formatPhone(active.customerAddress) : '';
            var ended = isEnded(active);
            var heading = ended
                ? label('callEnded', 'Call ended — complete the activity')
                : (inbound ? label('inboundCall', 'Inbound call') : label('outboundCall', 'Outbound call'));

            var rows = detailRow(label('direction', 'Direction'), inbound ? label('inbound', 'Inbound') : label('outbound', 'Outbound')) +
                detailRow(label('contact', 'Contact'), contact) +
                (number ? detailRow(label('number', 'Number'), number) : '') +
                (active.queueName ? detailRow(label('queue', 'Queue'), active.queueName) : '') +
                (ended ? '' : detailRow(label('status', 'Status'), active.status));

            // "Complete activity" only appears when the call router assigned a CRM activity (dialer or queue work).
            // The agent never creates an activity or picks a contact from the bar, so a direct inbound call — which
            // has no activity — shows no such button.
            var activityButton = active.activityItemId
                ? '<button type="button" class="btn btn-primary btn-sm" data-cc-open-activity="' + escapeHtml(active.activityItemId) + '"><i class="fa-solid fa-up-right-from-square" aria-hidden="true"></i> ' + escapeHtml(ended ? label('completeActivity', 'Complete activity') : label('openActivity', 'Open activity')) + '</button>'
                : '';
            var contactButton = active.contactUrl
                ? '<a class="btn btn-outline-secondary btn-sm" href="' + escapeHtml(active.contactUrl) + '" target="_blank" rel="noopener"><i class="fa-solid fa-address-card" aria-hidden="true"></i> ' + escapeHtml(label('openContact', 'Customer record')) + '</a>'
                : '';
            var actions = contactButton + activityButton;

            return '<div class="cc-bar__offer cc-bar__active is-expanded' + (ended ? ' is-ended' : ' is-connected') + '" data-cc-active>' +
                '<div class="cc-bar__offer-head">' + escapeHtml(heading) +
                    (ended ? '' : '<span class="cc-bar__countdown" data-cc-talk-time>0:00</span>') +
                '</div>' +
                '<div class="cc-bar__offer-details">' + rows + '</div>' +
                (actions ? '<div class="cc-bar__offer-actions">' + actions + '</div>' : '') +
            '</div>';
        }

        function renderTail() {
            var collapseTitle = label('collapseBar', 'Hide the Contact Center bar');

            return '<div class="cc-bar__tail">' +
                '<span class="cc-bar__conn" data-cc-connection role="status" aria-live="polite"></span>' +
                (config.workspaceUrl ? '<a class="cc-bar__workspace" href="' + escapeHtml(config.workspaceUrl) + '" title="' + escapeHtml(label('openWorkspace', 'Open full workspace')) + '"><i class="fa-solid fa-headset" aria-hidden="true"></i></a>' : '') +
                '<button type="button" class="cc-bar__collapse" data-cc-collapse title="' + escapeHtml(collapseTitle) + '" aria-label="' + escapeHtml(collapseTitle) + '"><i class="fa-solid fa-chevron-down" aria-hidden="true"></i></button>' +
            '</div>';
        }

        function isEnded(active) {
            return active.status === 'Ended' || active.status === 'Failed';
        }

        function bindEvents() {
            var collapseButton = inner.querySelector('[data-cc-collapse]');

            if (collapseButton) {
                collapseButton.addEventListener('click', function () { setCollapsed(true); });
            }

            var acceptButton = inner.querySelector('[data-cc-accept]');
            var declineButton = inner.querySelector('[data-cc-decline]');

            if (acceptButton && state.offer) {
                acceptButton.addEventListener('click', function () { accept(state.offer.reservationId); });
            }

            if (declineButton && state.offer) {
                declineButton.addEventListener('click', function () { decline(state.offer.reservationId); });
            }

            var openButtons = inner.querySelectorAll('[data-cc-open-activity]');
            Array.prototype.forEach.call(openButtons, function (button) {
                button.addEventListener('click', function () {
                    popActivity(button.getAttribute('data-cc-open-activity'), true);
                });
            });
        }

        function accept(reservationId) {
            if (!config.acceptOfferUrl || !reservationId) {
                return;
            }

            setOfferButtonsDisabled(true);

            post(config.acceptOfferUrl, config.antiForgeryToken, { reservationId: reservationId })
                .then(function () { return refresh(); })
                .catch(function () { })
                .finally(function () { setOfferButtonsDisabled(false); });
        }

        function decline(reservationId) {
            if (!config.declineOfferUrl || !reservationId) {
                return;
            }

            setOfferButtonsDisabled(true);

            post(config.declineOfferUrl, config.antiForgeryToken, { reservationId: reservationId })
                .then(function () { return refresh(); })
                .catch(function () { })
                .finally(function () { setOfferButtonsDisabled(false); });
        }

        function setOfferButtonsDisabled(disabled) {
            var offer = inner.querySelector('[data-cc-offer]');

            if (offer) {
                offer.querySelectorAll('button').forEach(function (button) { button.disabled = disabled; });
            }
        }

        function setConnectionStatus(key, fallback, modifier) {
            var el = inner ? inner.querySelector('[data-cc-connection]') : null;

            if (!el) {
                connectionStatusKey = key;
                return;
            }

            connectionStatusKey = key;
            el.textContent = label(key, fallback);
            el.className = 'cc-bar__conn' + (modifier ? ' ' + modifier : '');
        }

        function tick() {
            if (!state || !inner) {
                return;
            }

            if (state.offer) {
                var countdown = inner.querySelector('[data-cc-countdown]');

                if (countdown) {
                    var expires = parseUtc(state.offer.expiresUtc);
                    var remaining = expires ? Math.round((expires - serverNow()) / 1000) : null;
                    countdown.textContent = remaining !== null && remaining >= 0
                        ? ' · ' + label('respondIn', 'Respond in') + ' ' + remaining + 's'
                        : '';
                }
            }

            if (state.activeInteraction && !isEnded(state.activeInteraction)) {
                var talk = inner.querySelector('[data-cc-talk-time]');
                var since = parseUtc(state.activeInteraction.answeredUtc) || parseUtc(state.activeInteraction.startedUtc);

                if (talk && since) {
                    talk.textContent = formatDuration((serverNow() - since) / 1000);
                }
            }
        }

        // An offer arrived for this agent. Preview and inbound offers wait for the agent to act (they are rendered
        // by the state refresh with dial/skip or accept/decline). An auto-paced dial is answered by the dialer, so
        // it only pops the record. A preview also pops the record so the agent can review before dialing.
        function onOfferReceived(notification) {
            if (!notification) {
                refresh();
                return;
            }

            var isNew = notification.reservationId && notification.reservationId !== lastOfferReservationId;
            lastOfferReservationId = notification.reservationId;

            if (notification.kind === 'InboundCall' && isNew) {
                beep();
            }

            var shouldPop = notification.autoOpenActivity &&
                notification.activityItemId &&
                notification.activityItemId !== lastPoppedActivityId;

            if (shouldPop) {
                // Auto-paced dials pop unconditionally (the call is already connected); a preview pop yields to a
                // dirty form so the agent does not lose work while reviewing.
                popActivity(notification.activityItemId, notification.kind === 'AutoDial');
            }

            refresh();
        }

        // The offer was taken. When the agent accepted it, pop the activity so they land on the record for the call
        // they just took (pop-on-answer). Other revoke reasons (expired, released) just refresh.
        function onOfferRevoked(notification) {
            if (notification && notification.reason === 'Accepted' && notification.activityItemId) {
                popActivity(notification.activityItemId, false);
            }

            lastOfferReservationId = null;
            refresh();
        }

        if (window.contactCenterRealTime && config.hubUrl) {
            window.contactCenterRealTime.connect({
                hubUrl: config.hubUrl,
                onConnected: function () { setConnectionStatus('connected', 'Connected', 'is-connected'); },
                onReconnecting: function () { setConnectionStatus('reconnecting', 'Reconnecting…', 'is-reconnecting'); },
                onDisconnected: function () { setConnectionStatus('disconnected', 'Disconnected', 'is-disconnected'); },
                onError: function () { setConnectionStatus('disconnected', 'Disconnected', 'is-disconnected'); },
                onSnapshot: refresh,
                onPresenceChanged: refresh,
                onOfferReceived: onOfferReceived,
                onOfferRevoked: onOfferRevoked,
                onQueueStatsChanged: refresh,
                onRecordingStateChanged: refresh
            });
        }

        refresh();
        window.setInterval(tick, 1000);

        // Backstop reconciliation: completing an activity happens on the activity screen and does not push a hub
        // event to this bar, so without this the post-call "Complete activity" prompt could linger until the next
        // unrelated event. Re-poll only while an interaction is showing (never when idle), so the bar clears itself
        // shortly after the activity is completed or the call ends, with no polling cost the rest of the time.
        window.setInterval(function () {
            if (state && state.activeInteraction) {
                refresh();
            }
        }, 12000);
    }

    function boot() {
        var roots = document.querySelectorAll('[data-cc-agent-bar]');
        Array.prototype.forEach.call(roots, init);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

    window.contactCenterAgentBar = { init: init };
})(window, document);
