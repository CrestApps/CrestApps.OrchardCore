/*
 * Contact Center agent desktop client.
 *
 * Binds the agent workspace page to the real-time Contact Center hub and the workspace state endpoint.
 * It renders presence, live queue depth, the ringing offer with a countdown, the active interaction with
 * a live talk-time timer, the wrap-up disposition form, and recent history. It reads its configuration
 * (endpoint URLs, the antiforgery token, and localized strings) from the root element's data-config
 * attribute and depends on the shared "contact-center-realtime" helper for the hub connection.
 */
(function (window, document) {
    'use strict';

    function parseConfig(root) {
        var raw = root.getAttribute('data-config');

        if (!raw) {
            return { strings: {} };
        }

        try {
            var config = JSON.parse(raw);
            config.strings = config.strings || {};

            return config;
        } catch (error) {
            return { strings: {} };
        }
    }

    var escapeHtml = window.telephonyClient.escapeHtml;

    var formatDuration = window.telephonyClient.formatDuration;

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

    function init(root) {
        var config = parseConfig(root);
        var strings = config.strings;
        var state = null;
        var serverOffsetMs = 0;
        var activeSignature = null;
        var offerSignature = null;
        var queuesSignature = null;
        var connectionStatusKey = null;

        var refs = {
            presence: root.querySelector('[data-cc-presence]'),
            presenceButton: root.querySelector('[data-cc-presence-button]'),
            presenceDot: root.querySelector('[data-cc-presence-dot]'),
            presenceLabel: root.querySelector('[data-cc-presence-label]'),
            presenceMenu: root.querySelector('[data-cc-presence-menu]'),
            queues: root.querySelector('[data-cc-queues]'),
            offer: root.querySelector('[data-cc-offer]'),
            active: root.querySelector('[data-cc-active]'),
            history: root.querySelector('[data-cc-history]'),
            connection: root.querySelector('[data-cc-connection]'),
            error: root.querySelector('[data-cc-error]')
        };

        function label(key, fallback) {
            return strings[key] || fallback;
        }

        function showError(message) {
            if (!refs.error) {
                return;
            }

            refs.error.textContent = message;
            refs.error.hidden = false;
        }

        function clearError() {
            if (!refs.error) {
                return;
            }

            refs.error.textContent = '';
            refs.error.hidden = true;
        }

        function setConnectionStatus(key, fallback, modifier) {
            if (!refs.connection || connectionStatusKey === key) {
                return;
            }

            connectionStatusKey = key;
            refs.connection.textContent = label(key, fallback);
            refs.connection.className = 'cc-connection' + (modifier ? ' ' + modifier : '');
        }

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
            return fetch(config.stateUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (data) {
                    if (data) {
                        render(data);
                    }
                })
                .catch(function () { });
        }

        function render(data) {
            state = data;
            computeOffset(data.serverTimeUtc);
            renderPresence();
            renderQueues();
            renderOffer();
            renderActive();
            renderHistory();
            tick();
        }

        function renderPresence() {
            if (!refs.presenceLabel || !state) {
                return;
            }

            var status = (state.presence && state.presence.status) || 'Offline';
            var reason = state.presence && state.presence.reason;
            refs.presenceLabel.textContent = reason || status;

            if (refs.presenceDot) {
                refs.presenceDot.className = 'cc-presence__dot is-' + status.toLowerCase();
            }
        }

        function renderQueues() {
            if (!refs.queues || !state) {
                return;
            }

            var queues = state.queues || [];
            var queuesHtml;

            if (!queues.length) {
                queuesHtml = '<span class="cc-queue-chip">' + escapeHtml(label('noQueues', 'Not signed in to any queue')) + '</span>';
            } else {
                queuesHtml = queues.map(function (queue) {
                    var empty = queue.waitingCount > 0 ? '' : ' is-empty';

                    return '<span class="cc-queue-chip">' + escapeHtml(queue.name) +
                        '<span class="cc-queue-chip__count' + empty + '">' + queue.waitingCount + '</span></span>';
                }).join('');
            }

            if (queuesHtml === queuesSignature) {
                return;
            }

            queuesSignature = queuesHtml;
            refs.queues.innerHTML = queuesHtml;
        }

        function renderOffer() {
            if (!refs.offer || !state) {
                return;
            }

            var offer = state.offer;
            var signature = offer ? offer.reservationId : null;

            if (signature === offerSignature) {
                return;
            }

            offerSignature = signature;

            if (!offer) {
                refs.offer.innerHTML = '';
                refs.offer.hidden = true;

                return;
            }

            refs.offer.hidden = false;
            refs.offer.innerHTML =
                '<div class="cc-offer">' +
                    '<div class="cc-offer__label">' + escapeHtml(label('incomingOffer', 'Incoming')) + '</div>' +
                    '<div class="cc-offer__customer">' + escapeHtml(offer.customerLabel || offer.customerAddress || label('unknownCaller', 'Unknown caller')) + '</div>' +
                    (offer.queueName ? '<div class="cc-offer__meta">' + escapeHtml(offer.queueName) + '</div>' : '') +
                    '<div class="cc-offer__countdown" data-cc-offer-countdown aria-hidden="true"></div>' +
                    '<div class="cc-offer__actions">' +
                        '<button type="button" class="btn btn-success" data-cc-accept><i class="fa-solid fa-phone"></i> ' + escapeHtml(label('accept', 'Accept')) + '</button>' +
                        '<button type="button" class="btn btn-outline-danger" data-cc-decline><i class="fa-solid fa-phone-slash"></i> ' + escapeHtml(label('decline', 'Decline')) + '</button>' +
                    '</div>' +
                '</div>';

            var acceptButton = refs.offer.querySelector('[data-cc-accept]');
            var declineButton = refs.offer.querySelector('[data-cc-decline]');

            if (acceptButton) {
                acceptButton.addEventListener('click', function () { accept(offer.reservationId); });
                acceptButton.focus();
            }

            if (declineButton) {
                declineButton.addEventListener('click', function () { decline(offer.reservationId); });
            }
        }

        function renderActive() {
            if (!refs.active || !state) {
                return;
            }

            var active = state.activeInteraction;
            var signature = active ? active.interactionId + ':' + active.status + ':' + (active.recordingState || '') : null;

            if (signature === activeSignature) {
                return;
            }

            activeSignature = signature;

            if (!active) {
                refs.active.innerHTML =
                    '<div class="cc-empty">' +
                        '<div class="cc-empty__icon"><i class="fa-regular fa-circle-check"></i></div>' +
                        '<div>' + escapeHtml(label('noActiveCall', 'You have no active interaction. Available work will ring here.')) + '</div>' +
                    '</div>';

                return;
            }

            var inbound = active.direction === 'Inbound';
            var isPaused = active.isRecordingPaused === true;
            var showSecurePause = config.canSecurePause === true && active.supportsSecurePause === true;
            var recordingBadge = showSecurePause
                ? '<span class="cc-recording ' + (isPaused ? 'is-paused' : 'is-active') + '" data-cc-recording-badge>' +
                    '<i class="fa-solid ' + (isPaused ? 'fa-circle-pause' : 'fa-circle') + '" aria-hidden="true"></i> ' +
                    escapeHtml(isPaused
                        ? label('recordingPaused', 'Recording paused for sensitive-data capture')
                        : label('recordingActive', 'Recording')) +
                  '</span>'
                : '';
            var secureButton = showSecurePause
                ? '<button type="button" class="btn btn-sm ' + (isPaused ? 'btn-success' : 'btn-outline-warning') + '" data-cc-secure-pause="' + (isPaused ? 'resume' : 'pause') + '" data-cc-interaction-id="' + escapeHtml(active.interactionId) + '">' +
                    '<i class="fa-solid ' + (isPaused ? 'fa-play' : 'fa-pause') + '"></i> ' +
                    escapeHtml(isPaused ? label('secureResume', 'Resume recording') : label('securePause', 'Pause recording')) +
                  '</button>'
                : '';
            var secureCaptureButton = config.canInitiateSecureCapture === true
                ? '<button type="button" class="btn btn-sm btn-outline-primary" data-cc-secure-capture="begin" data-cc-interaction-id="' + escapeHtml(active.interactionId) + '">' +
                    '<i class="fa-solid fa-shield-halved"></i> ' +
                    escapeHtml(label('secureCapture', 'Collect data securely')) +
                  '</button>'
                : '';
            refs.active.innerHTML =
                '<div class="cc-active">' +
                    '<div class="cc-active__headline">' +
                        '<span class="cc-active__dir"><i class="fa-solid ' + (inbound ? 'fa-arrow-down-left' : 'fa-arrow-up-right') + '"></i></span>' +
                        '<div>' +
                            '<div class="cc-active__customer">' + escapeHtml(active.customerLabel || active.customerAddress || label('unknownCaller', 'Unknown caller')) + '</div>' +
                            '<div class="cc-active__sub">' +
                                escapeHtml(inbound ? label('inbound', 'Inbound') : label('outbound', 'Outbound')) +
                                (active.queueName ? ' &middot; ' + escapeHtml(active.queueName) : '') +
                                (active.customerAddress ? ' &middot; ' + escapeHtml(active.customerAddress) : '') +
                            '</div>' +
                        '</div>' +
                    '</div>' +
                    (recordingBadge ? '<div class="cc-active__recording">' + recordingBadge + '</div>' : '') +
                    '<div class="cc-active__stats">' +
                        '<div class="cc-stat"><div class="cc-stat__label">' + escapeHtml(label('status', 'Status')) + '</div><div class="cc-stat__value">' + escapeHtml(active.status) + '</div></div>' +
                        '<div class="cc-stat"><div class="cc-stat__label">' + escapeHtml(label('talkTime', 'Talk time')) + '</div><div class="cc-stat__value" data-cc-talk-time aria-hidden="true">0:00</div></div>' +
                    '</div>' +
                    '<div class="cc-active__actions">' +
                        (active.contactUrl ? '<a class="btn btn-sm btn-outline-secondary" href="' + escapeHtml(active.contactUrl) + '" target="_blank" rel="noopener"><i class="fa-solid fa-up-right-from-square"></i> ' + escapeHtml(label('openContact', 'Open customer record')) + '</a>' : '') +
                        secureButton +
                        secureCaptureButton +
                        (active.completeUrl ? '<a class="btn btn-sm btn-primary" href="' + escapeHtml(active.completeUrl) + '"><i class="fa-solid fa-check"></i> ' + escapeHtml(label('completeWork', 'Complete activity')) + '</a>' : '') +
                    '</div>' +
                '</div>';
        }

        function renderHistory() {
            if (!refs.history || !state) {
                return;
            }

            var history = state.recentHistory || [];

            if (!history.length) {
                refs.history.innerHTML = '<li class="cc-empty">' + escapeHtml(label('noHistory', 'No recent interactions.')) + '</li>';

                return;
            }

            refs.history.innerHTML = history.map(function (entry) {
                var inbound = entry.direction === 'Inbound';
                var when = parseUtc(entry.endedUtc || entry.createdUtc);
                var formattedNumber = window.telephonySoftPhone &&
                    typeof window.telephonySoftPhone.formatPhoneNumber === 'function'
                    ? window.telephonySoftPhone.formatPhoneNumber(entry.customerLabel)
                    : entry.customerLabel;

                return '<li class="cc-history__item">' +
                    '<span class="cc-history__dir"><i class="fa-solid ' + (inbound ? 'fa-arrow-down-left' : 'fa-arrow-up-right') + '"></i></span>' +
                    '<span class="cc-history__body">' +
                        '<span class="cc-history__summary">' +
                            '<span class="cc-history__customer">' + escapeHtml(formattedNumber || label('unknownCaller', 'Unknown caller')) + '</span>' +
                            '<span class="badge text-bg-secondary">' + escapeHtml(entry.status) + '</span>' +
                        '</span>' +
                        (when ? '<span class="cc-history__meta">' + escapeHtml(new Date(when).toLocaleString()) + '</span>' : '') +
                    '</span>' +
                '</li>';
            }).join('');
        }

        function tick() {
            if (!state) {
                return;
            }

            if (state.offer && refs.offer) {
                var countdown = refs.offer.querySelector('[data-cc-offer-countdown]');

                if (countdown) {
                    var expires = parseUtc(state.offer.expiresUtc);
                    var remaining = expires ? Math.round((expires - serverNow()) / 1000) : null;
                    countdown.textContent = remaining !== null && remaining >= 0
                        ? label('respondIn', 'Respond in') + ' ' + remaining + 's'
                        : '';
                }
            }

            if (state.activeInteraction && refs.active) {
                var talk = refs.active.querySelector('[data-cc-talk-time]');
                var since = parseUtc(state.activeInteraction.answeredUtc) || parseUtc(state.activeInteraction.startedUtc);

                if (talk && since) {
                    talk.textContent = formatDuration((serverNow() - since) / 1000);
                }
            }
        }

        function accept(reservationId) {
            if (!config.acceptOfferUrl) {
                return;
            }

            setOfferButtonsDisabled(true);

            post(config.acceptOfferUrl, config.antiForgeryToken, { reservationId: reservationId })
                .then(function (response) {
                    if (!response.ok) {
                        showError(label('acceptFailed', 'The offer could not be accepted. It may have been re-offered.'));
                    } else {
                        clearError();
                    }

                    return refresh();
                })
                .catch(function () {
                    showError(label('acceptFailed', 'The offer could not be accepted. It may have been re-offered.'));
                })
                .finally(function () {
                    setOfferButtonsDisabled(false);
                });
        }

        function decline(reservationId) {
            if (!config.declineOfferUrl) {
                return;
            }

            setOfferButtonsDisabled(true);

            post(config.declineOfferUrl, config.antiForgeryToken, { reservationId: reservationId })
                .then(function (response) {
                    if (!response.ok) {
                        showError(label('declineFailed', 'The offer could not be declined. Refresh the workspace and try again.'));
                    } else {
                        clearError();
                    }

                    return refresh();
                })
                .catch(function () {
                    showError(label('declineFailed', 'The offer could not be declined. Refresh the workspace and try again.'));
                })
                .finally(function () {
                    setOfferButtonsDisabled(false);
                });
        }

        function setOfferButtonsDisabled(disabled) {
            if (!refs.offer) {
                return;
            }

            refs.offer.querySelectorAll('button').forEach(function (button) {
                button.disabled = disabled;
            });
        }

        function setPresence(status, reason) {
            post(config.setPresenceUrl, config.antiForgeryToken, { status: status, reason: reason || '' })
                .then(function () { return refresh(); })
                .catch(function () { });
        }

        function securePause(interactionId) {
            if (!config.pauseRecordingUrl || !interactionId) {
                return;
            }

            var reason = '';

            if (config.requirePauseReason === true) {
                reason = window.prompt(label('pauseReasonPrompt', 'Enter a reason for pausing recording'), '') || '';

                if (!reason.trim()) {
                    return;
                }
            }

            setSecureButtonDisabled(true);

            post(config.pauseRecordingUrl, config.antiForgeryToken, { interactionId: interactionId, reason: reason })
                .then(function (response) { return handleSecureResponse(response, 'securePauseFailed', 'The recording could not be paused. Refresh the workspace and try again.'); })
                .catch(function () {
                    showError(label('securePauseFailed', 'The recording could not be paused. Refresh the workspace and try again.'));
                    setSecureButtonDisabled(false);
                });
        }

        function secureResume(interactionId) {
            if (!config.resumeRecordingUrl || !interactionId) {
                return;
            }

            setSecureButtonDisabled(true);

            post(config.resumeRecordingUrl, config.antiForgeryToken, { interactionId: interactionId })
                .then(function (response) { return handleSecureResponse(response, 'secureResumeFailed', 'The recording could not be resumed. Refresh the workspace and try again.'); })
                .catch(function () {
                    showError(label('secureResumeFailed', 'The recording could not be resumed. Refresh the workspace and try again.'));
                    setSecureButtonDisabled(false);
                });
        }

        function beginSecureCapture(interactionId) {
            if (!config.beginSecureCaptureUrl || !interactionId) {
                return;
            }

            setSecureButtonDisabled(true);

            post(config.beginSecureCaptureUrl, config.antiForgeryToken, { interactionId: interactionId, fields: config.secureCaptureFields || '' })
                .then(function (response) {
                    if (!response.ok) {
                        showError(label('secureCaptureFailed', 'The secure capture could not be started. Refresh the workspace and try again.'));

                        return refresh();
                    }

                    return response.json().then(function (result) {
                        if (!result || result.succeeded !== true || !result.captureUrl) {
                            showError(label('secureCaptureFailed', 'The secure capture could not be started. Refresh the workspace and try again.'));
                        } else {
                            clearError();
                            presentSecureCaptureLink(absoluteUrl(result.captureUrl));
                        }

                        return refresh();
                    }, function () {
                        return refresh();
                    });
                })
                .catch(function () {
                    showError(label('secureCaptureFailed', 'The secure capture could not be started. Refresh the workspace and try again.'));
                    setSecureButtonDisabled(false);
                });
        }

        function presentSecureCaptureLink(url) {
            if (window.navigator && window.navigator.clipboard && window.navigator.clipboard.writeText) {
                window.navigator.clipboard.writeText(url).then(function () { }, function () { });
            }

            window.prompt(label('secureCaptureStarted', 'Share this one-time secure link with the customer:'), url);
        }

        function absoluteUrl(url) {
            if (!url) {
                return url;
            }

            try {
                return new URL(url, window.location.origin).toString();
            } catch (error) {
                return url;
            }
        }

        function handleSecureResponse(response, failureKey, failureFallback) {
            if (!response.ok) {
                showError(label(failureKey, failureFallback));

                return refresh();
            }

            return response.json().then(function (result) {
                if (!result || result.succeeded !== true) {
                    showError(label(failureKey, failureFallback));
                } else {
                    clearError();
                }

                return refresh();
            }, function () {
                return refresh();
            });
        }

        function setSecureButtonDisabled(disabled) {
            if (!refs.active) {
                return;
            }

            refs.active.querySelectorAll('[data-cc-secure-pause],[data-cc-secure-capture]').forEach(function (button) {
                button.disabled = disabled;
            });
        }

        function bindSecureControls() {
            if (!refs.active) {
                return;
            }

            refs.active.addEventListener('click', function (event) {
                var captureButton = event.target.closest ? event.target.closest('[data-cc-secure-capture]') : null;

                if (captureButton) {
                    event.preventDefault();
                    beginSecureCapture(captureButton.getAttribute('data-cc-interaction-id'));

                    return;
                }

                var button = event.target.closest ? event.target.closest('[data-cc-secure-pause]') : null;

                if (!button) {
                    return;
                }

                event.preventDefault();
                var interactionId = button.getAttribute('data-cc-interaction-id');

                if (button.getAttribute('data-cc-secure-pause') === 'resume') {
                    secureResume(interactionId);
                } else {
                    securePause(interactionId);
                }
            });
        }

        function bindPresenceMenu() {
            var button = refs.presenceButton;
            var menu = refs.presenceMenu;

            if (!refs.presence || !button || !menu) {
                return;
            }

            var items = Array.prototype.slice.call(menu.querySelectorAll('[data-cc-set-presence]'));

            function isOpen() {
                return menu.classList.contains('is-open');
            }

            function openMenu() {
                menu.classList.add('is-open');
                button.setAttribute('aria-expanded', 'true');

                if (items.length) {
                    items[0].focus();
                }
            }

            function closeMenu(returnFocus) {
                menu.classList.remove('is-open');
                button.setAttribute('aria-expanded', 'false');

                if (returnFocus) {
                    button.focus();
                }
            }

            button.addEventListener('click', function () {
                if (isOpen()) {
                    closeMenu(false);
                } else {
                    openMenu();
                }
            });

            document.addEventListener('click', function (event) {
                if (!refs.presence.contains(event.target)) {
                    closeMenu(false);
                }
            });

            refs.presence.addEventListener('keydown', function (event) {
                if (event.key === 'Escape' && isOpen()) {
                    event.preventDefault();
                    closeMenu(true);
                }
            });

            items.forEach(function (item, index) {
                item.addEventListener('click', function () {
                    setPresence(item.getAttribute('data-cc-set-presence'), item.getAttribute('data-cc-reason'));
                    closeMenu(true);
                });

                item.addEventListener('keydown', function (event) {
                    if (event.key === 'ArrowDown') {
                        event.preventDefault();
                        (items[index + 1] || items[0]).focus();
                    } else if (event.key === 'ArrowUp') {
                        event.preventDefault();
                        (items[index - 1] || items[items.length - 1]).focus();
                    } else if (event.key === 'Home') {
                        event.preventDefault();
                        items[0].focus();
                    } else if (event.key === 'End') {
                        event.preventDefault();
                        items[items.length - 1].focus();
                    }
                });
            });
        }

        bindPresenceMenu();
        bindSecureControls();

        if (window.contactCenterRealTime && config.hubUrl) {
            window.contactCenterRealTime.connect({
                hubUrl: config.hubUrl,
                onConnected: function () {
                    setConnectionStatus('connected', 'Connected', 'is-connected');
                },
                onReconnecting: function () {
                    setConnectionStatus('reconnecting', 'Connection lost. Reconnecting...', 'is-reconnecting');
                },
                onDisconnected: function () {
                    setConnectionStatus('disconnected', 'Disconnected. Live updates are paused.', 'is-disconnected');
                },
                onError: function () {
                    setConnectionStatus('disconnected', 'Disconnected. Live updates are paused.', 'is-disconnected');
                },
                onSnapshot: refresh,
                onPresenceChanged: refresh,
                onOfferReceived: function (notification) {
                    if (notification && notification.autoOpenActivity && notification.activityItemId && config.completeActivityUrlTemplate) {
                        var targetUrl = resolveSafeSameOriginUrl(config.completeActivityUrlTemplate.replace('__activityId__', encodeURIComponent(notification.activityItemId)));

                        if (targetUrl) {
                            window.location.assign(targetUrl);
                        }

                        return;
                    }

                    refresh();
                },
                onOfferRevoked: refresh,
                onQueueStatsChanged: refresh,
                onRecordingStateChanged: refresh
            });
        }

        refresh();
        window.setInterval(tick, 1000);
    }

    function boot() {
        var roots = document.querySelectorAll('[data-cc-workspace]');
        Array.prototype.forEach.call(roots, init);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

    window.contactCenterWorkspace = { init: init };
})(window, document);
