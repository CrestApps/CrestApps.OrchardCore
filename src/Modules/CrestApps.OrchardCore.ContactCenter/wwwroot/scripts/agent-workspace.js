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

    function escapeHtml(value) {
        var node = document.createElement('div');
        node.textContent = value == null ? '' : String(value);

        return node.innerHTML;
    }

    function pad(value) {
        return value < 10 ? '0' + value : String(value);
    }

    function formatDuration(totalSeconds) {
        if (!isFinite(totalSeconds) || totalSeconds < 0) {
            totalSeconds = 0;
        }

        var seconds = Math.floor(totalSeconds % 60);
        var minutes = Math.floor((totalSeconds / 60) % 60);
        var hours = Math.floor(totalSeconds / 3600);

        return (hours > 0 ? hours + ':' + pad(minutes) : minutes) + ':' + pad(seconds);
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

    function init(root) {
        var config = parseConfig(root);
        var strings = config.strings;
        var state = null;
        var serverOffsetMs = 0;
        var activeSignature = null;
        var offerSignature = null;
        var activeDrafts = {};

        var refs = {
            presence: root.querySelector('[data-cc-presence]'),
            presenceDot: root.querySelector('[data-cc-presence-dot]'),
            presenceLabel: root.querySelector('[data-cc-presence-label]'),
            presenceMenu: root.querySelector('[data-cc-presence-menu]'),
            queues: root.querySelector('[data-cc-queues]'),
            offer: root.querySelector('[data-cc-offer]'),
            active: root.querySelector('[data-cc-active]'),
            history: root.querySelector('[data-cc-history]')
        };

        function label(key, fallback) {
            return strings[key] || fallback;
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
                .catch(function () {
                    if (error) {
                        error.textContent = label('completeFailed', 'The work could not be completed.');
                        error.hidden = false;
                    }
                });
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

            if (!queues.length) {
                refs.queues.innerHTML = '<span class="cc-queue-chip">' + escapeHtml(label('noQueues', 'Not signed in to any queue')) + '</span>';

                return;
            }

            refs.queues.innerHTML = queues.map(function (queue) {
                var empty = queue.waitingCount > 0 ? '' : ' is-empty';

                return '<span class="cc-queue-chip">' + escapeHtml(queue.name) +
                    '<span class="cc-queue-chip__count' + empty + '">' + queue.waitingCount + '</span></span>';
            }).join('');
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
                    '<div class="cc-offer__countdown" data-cc-offer-countdown></div>' +
                    '<div class="cc-offer__actions">' +
                        '<button type="button" class="btn btn-success" data-cc-accept><i class="fa-solid fa-phone"></i> ' + escapeHtml(label('accept', 'Accept')) + '</button>' +
                        '<button type="button" class="btn btn-outline-danger" data-cc-decline><i class="fa-solid fa-phone-slash"></i> ' + escapeHtml(label('decline', 'Decline')) + '</button>' +
                    '</div>' +
                '</div>';

            var acceptButton = refs.offer.querySelector('[data-cc-accept]');
            var declineButton = refs.offer.querySelector('[data-cc-decline]');

            if (acceptButton) {
                acceptButton.addEventListener('click', function () { accept(offer.reservationId); });
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
            var signature = active ? active.interactionId + ':' + active.status : null;

            if (signature === activeSignature) {
                return;
            }

            saveActiveDraft();

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
            var dispositions = (state.dispositions || []).map(function (item) {
                return '<option value="' + escapeHtml(item.id) + '">' + escapeHtml(item.name) + '</option>';
            }).join('');

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
                    '<div class="cc-active__stats">' +
                        '<div class="cc-stat"><div class="cc-stat__label">' + escapeHtml(label('status', 'Status')) + '</div><div class="cc-stat__value">' + escapeHtml(active.status) + '</div></div>' +
                        '<div class="cc-stat"><div class="cc-stat__label">' + escapeHtml(label('talkTime', 'Talk time')) + '</div><div class="cc-stat__value" data-cc-talk-time>0:00</div></div>' +
                    '</div>' +
                    (active.contactUrl ? '<a class="btn btn-sm btn-outline-secondary align-self-start" href="' + escapeHtml(active.contactUrl) + '" target="_blank" rel="noopener"><i class="fa-solid fa-up-right-from-square"></i> ' + escapeHtml(label('openContact', 'Open customer record')) + '</a>' : '') +
                    '<div class="cc-wrapup">' +
                        '<label class="form-label">' + escapeHtml(label('disposition', 'Disposition')) + '</label>' +
                        '<select class="form-select mb-2" data-cc-disposition>' +
                            '<option value="">' + escapeHtml(label('selectDisposition', 'Select a disposition...')) + '</option>' +
                            dispositions +
                        '</select>' +
                        '<textarea class="form-control mb-2" rows="2" placeholder="' + escapeHtml(label('notes', 'Wrap-up notes (optional)')) + '" data-cc-notes></textarea>' +
                        '<div class="cc-active__error text-danger small mb-2" data-cc-error hidden></div>' +
                        '<button type="button" class="btn btn-primary w-100" data-cc-complete><i class="fa-solid fa-check"></i> ' + escapeHtml(label('completeWork', 'Complete & wrap up')) + '</button>' +
                    '</div>' +
                '</div>';

            var completeButton = refs.active.querySelector('[data-cc-complete]');

            if (completeButton) {
                completeButton.addEventListener('click', function () { complete(active.activityItemId); });
            }

            restoreActiveDraft(active.interactionId);
        }

        function saveActiveDraft() {
            if (!state || !state.activeInteraction || !refs.active) {
                return;
            }

            var select = refs.active.querySelector('[data-cc-disposition]');
            var notes = refs.active.querySelector('[data-cc-notes]');

            if (!select && !notes) {
                return;
            }

            activeDrafts[state.activeInteraction.interactionId] = {
                dispositionId: select ? select.value : '',
                notes: notes ? notes.value : ''
            };
        }

        function restoreActiveDraft(interactionId) {
            var draft = activeDrafts[interactionId];

            if (!draft || !refs.active) {
                return;
            }

            var select = refs.active.querySelector('[data-cc-disposition]');
            var notes = refs.active.querySelector('[data-cc-notes]');

            if (select) {
                select.value = draft.dispositionId || '';
            }

            if (notes) {
                notes.value = draft.notes || '';
            }
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
                var when = parseUtc(entry.createdUtc);
                var meta = escapeHtml(entry.status) + (when ? ' &middot; ' + new Date(when).toLocaleString() : '');

                return '<li class="cc-history__item">' +
                    '<span class="cc-history__dir"><i class="fa-solid ' + (inbound ? 'fa-arrow-down-left' : 'fa-arrow-up-right') + '"></i></span>' +
                    '<span class="cc-history__body">' +
                        '<span class="cc-history__customer">' + escapeHtml(entry.customerLabel || label('unknownCaller', 'Unknown caller')) + '</span>' +
                        '<span class="cc-history__meta">' + meta + '</span>' +
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
                        window.alert(label('acceptFailed', 'The offer could not be accepted. It may have been re-offered.'));
                    }

                    return refresh();
                })
                .catch(function () {
                    window.alert(label('acceptFailed', 'The offer could not be accepted. It may have been re-offered.'));
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
                        window.alert(label('declineFailed', 'The offer could not be declined. Refresh the workspace and try again.'));
                    }

                    return refresh();
                })
                .catch(function () {
                    window.alert(label('declineFailed', 'The offer could not be declined. Refresh the workspace and try again.'));
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

        function complete(activityId) {
            var select = refs.active.querySelector('[data-cc-disposition]');
            var notes = refs.active.querySelector('[data-cc-notes]');
            var error = refs.active.querySelector('[data-cc-error]');

            post(config.completeUrl, config.antiForgeryToken, {
                activityId: activityId,
                dispositionId: select ? select.value : '',
                notes: notes ? notes.value : ''
            })
                .then(function (response) { return response.ok ? response.json() : { succeeded: false }; })
                .then(function (result) {
                    if (result && result.succeeded) {
                        if (state && state.activeInteraction) {
                            delete activeDrafts[state.activeInteraction.interactionId];
                        }

                        activeSignature = null;

                        return refresh();
                    }

                    if (error) {
                        error.textContent = (result && result.errorMessage) || label('completeFailed', 'The work could not be completed.');
                        error.hidden = false;
                    }
                })
                .catch(function () { });
        }

        function setPresence(status, reason) {
            post(config.setPresenceUrl, config.antiForgeryToken, { status: status, reason: reason || '' })
                .then(function () { return refresh(); })
                .catch(function () { });
        }

        function bindPresenceMenu() {
            if (refs.presence) {
                var button = refs.presence.querySelector('[data-cc-presence-button]');

                if (button && refs.presenceMenu) {
                    button.addEventListener('click', function () {
                        refs.presenceMenu.classList.toggle('is-open');
                    });

                    document.addEventListener('click', function (event) {
                        if (!refs.presence.contains(event.target)) {
                            refs.presenceMenu.classList.remove('is-open');
                        }
                    });
                }
            }

            root.querySelectorAll('[data-cc-set-presence]').forEach(function (item) {
                item.addEventListener('click', function () {
                    setPresence(item.getAttribute('data-cc-set-presence'), item.getAttribute('data-cc-reason'));

                    if (refs.presenceMenu) {
                        refs.presenceMenu.classList.remove('is-open');
                    }
                });
            });
        }

        bindPresenceMenu();

        if (window.contactCenterRealTime && config.hubUrl) {
            window.contactCenterRealTime.connect({
                hubUrl: config.hubUrl,
                onSnapshot: refresh,
                onPresenceChanged: refresh,
                onOfferReceived: refresh,
                onOfferRevoked: refresh,
                onQueueStatsChanged: refresh
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
