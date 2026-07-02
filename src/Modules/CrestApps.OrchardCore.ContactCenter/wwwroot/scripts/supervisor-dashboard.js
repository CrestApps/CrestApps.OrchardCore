/*
 * Contact Center supervisor dashboard client.
 *
 * Binds the supervisor dashboard page to the real-time Contact Center hub and the dashboard state
 * endpoint. It renders live summary metrics, per-queue tiles with service-level health, and an agent
 * presence board. It reads its configuration from the root element's data-config attribute and depends
 * on the shared "contact-center-realtime" helper for the hub connection.
 */
(function (window, document) {
    'use strict';

    var REFRESH_INTERVAL_MS = 10000;

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

    function formatWait(totalSeconds) {
        if (!isFinite(totalSeconds) || totalSeconds <= 0) {
            return '0s';
        }

        var minutes = Math.floor(totalSeconds / 60);
        var seconds = Math.floor(totalSeconds % 60);

        return minutes > 0 ? minutes + 'm ' + seconds + 's' : seconds + 's';
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
        var watched = {};

        var refs = {
            summary: root.querySelector('[data-cc-summary]'),
            tiles: root.querySelector('[data-cc-tiles]'),
            board: root.querySelector('[data-cc-board]')
        };

        var realtime = null;

        function label(key, fallback) {
            return strings[key] || fallback;
        }

        function metric(value, text) {
            return '<div class="cc-metric"><div class="cc-metric__value">' + value + '</div><div class="cc-metric__label">' + escapeHtml(text) + '</div></div>';
        }

        function renderSummary(state) {
            if (!refs.summary) {
                return;
            }

            refs.summary.innerHTML =
                metric(state.totalWaiting, label('waiting', 'Waiting')) +
                metric(state.availableAgents, label('available', 'Available agents')) +
                metric((state.agents || []).length, label('agents', 'Agents')) +
                metric((state.queues || []).length, label('queues', 'Queues'));
        }

        function renderTiles(state) {
            if (!refs.tiles) {
                return;
            }

            var queues = state.queues || [];

            if (!queues.length) {
                refs.tiles.innerHTML = '<div class="cc-empty">' + escapeHtml(label('noQueues', 'No queues are configured.')) + '</div>';

                return;
            }

            refs.tiles.innerHTML = queues.map(function (queue) {
                var cls = 'cc-tile';

                if (queue.slaBreachCount > 0) {
                    cls += ' is-breach';
                } else if (queue.slaThresholdSeconds > 0 && queue.longestWaitSeconds > queue.slaThresholdSeconds / 2) {
                    cls += ' is-warn';
                }

                return '<div class="' + cls + '">' +
                    '<div class="cc-tile__name">' + escapeHtml(queue.name) + '</div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('waiting', 'Waiting')) + '</span><span class="cc-strong">' + queue.waitingCount + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('longestWait', 'Longest wait')) + '</span><span class="cc-strong">' + formatWait(queue.longestWaitSeconds) + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('slaBreaches', 'SLA breaches')) + '</span><span class="cc-strong">' + queue.slaBreachCount + '</span></div>' +
                '</div>';
            }).join('');
        }

        function renderBoard(state) {
            if (!refs.board) {
                return;
            }

            var agents = state.agents || [];

            if (!agents.length) {
                refs.board.innerHTML = '<div class="cc-empty">' + escapeHtml(label('noAgents', 'No agents are configured.')) + '</div>';

                return;
            }

            refs.board.innerHTML = agents.map(function (agent) {
                var status = agent.presenceStatus || 'Offline';
                var detail = agent.presenceReason || status;

                var actions = agent.activeInteractionId
                    ? '<span class="cc-agent__actions">' +
                        '<button type="button" class="btn btn-sm btn-outline-secondary" data-cc-engage="' + escapeHtml(agent.activeInteractionId) + '" data-cc-mode="Monitor">' + escapeHtml(label('monitor', 'Monitor')) + '</button>' +
                        '<button type="button" class="btn btn-sm btn-outline-secondary" data-cc-engage="' + escapeHtml(agent.activeInteractionId) + '" data-cc-mode="Whisper">' + escapeHtml(label('whisper', 'Whisper')) + '</button>' +
                        '<button type="button" class="btn btn-sm btn-outline-secondary" data-cc-engage="' + escapeHtml(agent.activeInteractionId) + '" data-cc-mode="Barge">' + escapeHtml(label('barge', 'Barge')) + '</button>' +
                        '<button type="button" class="btn btn-sm btn-outline-danger" data-cc-engage="' + escapeHtml(agent.activeInteractionId) + '" data-cc-mode="TakeOver">' + escapeHtml(label('takeOver', 'Take over')) + '</button>' +
                    '</span>'
                    : '';

                return '<div class="cc-agent">' +
                    '<span class="cc-presence__dot is-' + status.toLowerCase() + '"></span>' +
                    '<span class="cc-agent__body">' +
                        '<span class="cc-agent__name">' + escapeHtml(agent.displayName || agent.userId) + '</span>' +
                        '<span class="cc-agent__state badge ta-badge text-bg-secondary">' + escapeHtml(detail) + '</span>' +
                    '</span>' +
                    '<span class="cc-badge-count" title="' + escapeHtml(label('activeInteractions', 'Active interactions')) + '">' + agent.activeInteractions + '</span>' +
                    actions +
                '</div>';
            }).join('');

            refs.board.querySelectorAll('[data-cc-engage]').forEach(function (button) {
                button.addEventListener('click', function () {
                    engage(button.getAttribute('data-cc-engage'), button.getAttribute('data-cc-mode'), button);
                });
            });
        }

        function watchQueues(state) {
            if (!realtime) {
                return;
            }

            (state.queues || []).forEach(function (queue) {
                if (!watched[queue.id]) {
                    watched[queue.id] = true;
                    realtime.watchQueue(queue.id).catch(function () { });
                }
            });
        }

        function render(state) {
            renderSummary(state);
            renderTiles(state);
            renderBoard(state);
            watchQueues(state);
        }

        function refresh() {
            return fetch(config.stateUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (state) {
                    if (state) {
                        render(state);
                    }
                })
                .catch(function () { });
        }

        function engage(interactionId, mode, button) {
            if (!config.engageUrl || !interactionId || !mode) {
                return;
            }

            button.disabled = true;

            post(config.engageUrl, config.antiForgeryToken, {
                interactionId: interactionId,
                mode: mode
            })
                .then(function (response) { return response.ok ? response.json() : { succeeded: false }; })
                .then(function (result) {
                    if (!result || !result.succeeded) {
                        window.alert((result && result.errorMessage) || label('engagementFailed', 'The supervisor action could not be started.'));
                    }
                })
                .finally(function () {
                    button.disabled = false;
                });
        }

        if (window.contactCenterRealTime && config.hubUrl) {
            realtime = window.contactCenterRealTime.connect({
                hubUrl: config.hubUrl,
                onSnapshot: refresh,
                onPresenceChanged: refresh,
                onOfferReceived: refresh,
                onOfferRevoked: refresh,
                onQueueStatsChanged: refresh
            });
        }

        refresh();
        window.setInterval(refresh, REFRESH_INTERVAL_MS);
    }

    function boot() {
        var roots = document.querySelectorAll('[data-cc-dashboard]');
        Array.prototype.forEach.call(roots, init);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }

    window.contactCenterSupervisorDashboard = { init: init };
})(window, document);
