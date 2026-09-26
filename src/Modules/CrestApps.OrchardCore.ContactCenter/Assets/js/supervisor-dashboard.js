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

    var escapeHtml = window.telephonyClient.escapeHtml;

    function formatWait(totalSeconds) {
        if (!isFinite(totalSeconds) || totalSeconds <= 0) {
            return '0s';
        }

        var minutes = Math.floor(totalSeconds / 60);
        var seconds = Math.floor(totalSeconds % 60);

        return minutes > 0 ? minutes + 'm ' + seconds + 's' : seconds + 's';
    }

    function init(root) {
        var config = parseConfig(root);
        var strings = config.strings;
        var watched = {};
        var lastHtml = {};
        var connectionStatusKey = null;

        var refs = {
            summary: root.querySelector('[data-cc-summary]'),
            tiles: root.querySelector('[data-cc-tiles]'),
            board: root.querySelector('[data-cc-board]'),
            connection: root.querySelector('[data-cc-connection]'),
            error: root.querySelector('[data-cc-error]'),
            qualityAlerts: root.querySelector('[data-cc-quality-alerts]')
        };

        // The latest call-quality alert for each agent, until a supervisor dismisses it.
        var qualityAlerts = {};
        var lastAgents = [];

        var realtime = null;

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

        function setRegionHtml(el, cacheKey, html) {
            if (!el || lastHtml[cacheKey] === html) {
                return false;
            }

            lastHtml[cacheKey] = html;
            el.innerHTML = html;

            return true;
        }

        function metric(value, text) {
            return '<div class="cc-metric"><div class="cc-metric__value">' + value + '</div><div class="cc-metric__label">' + escapeHtml(text) + '</div></div>';
        }

        function renderSummary(state) {
            if (!refs.summary) {
                return;
            }

            setRegionHtml(refs.summary, 'summary',
                metric(state.totalWaiting, label('waiting', 'Waiting')) +
                metric(state.availableAgents, label('available', 'Available agents')) +
                metric((state.agents || []).length, label('agents', 'Agents')) +
                metric((state.queues || []).length, label('queues', 'Queues')));
        }

        function renderTiles(state) {
            if (!refs.tiles) {
                return;
            }

            var queues = state.queues || [];

            if (!queues.length) {
                setRegionHtml(refs.tiles, 'tiles', '<div class="cc-empty">' + escapeHtml(label('noQueues', 'No queues are configured.')) + '</div>');

                return;
            }

            var tilesHtml = queues.map(function (queue) {
                var cls = 'cc-tile';

                if (queue.slaBreachCount > 0) {
                    cls += ' is-breach';
                } else if (queue.slaThresholdSeconds > 0 && queue.longestWaitSeconds > queue.slaThresholdSeconds / 2) {
                    cls += ' is-warn';
                }

                return '<div class="' + cls + '">' +
                    '<div class="cc-tile__name">' + escapeHtml(queue.name) + '</div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('waiting', 'Waiting')) + '</span><span class="cc-strong">' + queue.waitingCount + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('signedIn', 'Signed-in agents')) + '</span><span class="cc-strong">' + queue.signedInAgentCount + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('available', 'Available agents')) + '</span><span class="cc-strong">' + queue.availableAgentCount + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('busy', 'Busy agents')) + '</span><span class="cc-strong">' + queue.busyAgentCount + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('notReady', 'Not-ready agents')) + '</span><span class="cc-strong">' + queue.notReadyAgentCount + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('longestWait', 'Longest wait')) + '</span><span class="cc-strong">' + formatWait(queue.longestWaitSeconds) + '</span></div>' +
                    '<div class="cc-tile__row"><span>' + escapeHtml(label('slaBreaches', 'SLA breaches')) + '</span><span class="cc-strong">' + queue.slaBreachCount + '</span></div>' +
                '</div>';
            }).join('');

            setRegionHtml(refs.tiles, 'tiles', tilesHtml);
        }

        function renderBoard(state) {
            if (!refs.board) {
                return;
            }

            var agents = state.agents || [];

            if (!agents.length) {
                setRegionHtml(refs.board, 'board', '<div class="cc-empty">' + escapeHtml(label('noAgents', 'No agents are configured.')) + '</div>');

                return;
            }

            var serverNow = Date.parse(state.serverTimeUtc || '');
            var boardHtml = agents.map(function (agent) {
                var status = agent.presenceStatus || 'Offline';

                // An agent on a phone call of their own says so, whatever their presence: what they dialed, and for how long.
                var phoneCall = window.CrestAppsContactCenter && typeof window.CrestAppsContactCenter.supervisorPhoneCallSummary === 'function'
                    ? window.CrestAppsContactCenter.supervisorPhoneCallSummary(agent.phoneCall, isFinite(serverNow) ? serverNow : Date.now(), strings)
                    : '';
                var detail = phoneCall || agent.presenceReason || status;

                // Listen / Whisper / Barge, Stop and Take over under the name, and the More menu's kebab at the far right
                // of the name row (see supervisor-interventions.js).
                var actions = interventions ? interventions.actionsHtml(agent, state) : '';
                var menu = interventions ? interventions.menuHtml(agent, state) : '';

                return '<div class="cc-agent">' +
                    '<span class="cc-presence__dot is-' + status.toLowerCase() + '"></span>' +
                    '<span class="cc-agent__body">' +
                        '<span class="cc-agent__name">' + escapeHtml(agent.displayName || agent.userId) + '</span>' +
                        '<span class="cc-agent__state badge ta-badge text-bg-secondary">' + escapeHtml(detail) + '</span>' +
                    '</span>' +
                    '<span class="cc-badge-count" title="' + escapeHtml(label('activeInteractions', 'Active interactions')) + '">' + agent.activeInteractions + '</span>' +
                    menu +
                    actions +
                '</div>';
            }).join('');

            // The actions are delegated from the board (see supervisor-interventions.js), so a redraw needs no rebinding.
            setRegionHtml(refs.board, 'board', boardHtml);
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

        function format(template) {
            var args = Array.prototype.slice.call(arguments, 1);

            return template.replace(/\{(\d+)\}/g, function (match, index) {
                return args[index] !== undefined ? args[index] : match;
            });
        }

        function agentName(alert) {
            var agent = lastAgents.find(function (candidate) {
                return candidate.agentId === alert.agentId || (alert.userId && candidate.userId === alert.userId);
            });

            return agent ? (agent.displayName || agent.userId) : (alert.userId || alert.agentId);
        }

        function renderQualityAlerts() {
            if (!refs.qualityAlerts) {
                return;
            }

            var html = Object.keys(qualityAlerts).map(function (agentId) {
                var alert = qualityAlerts[agentId];
                var cause = label('cause.' + alert.likelyCause, label('cause.Unknown', 'not determined'));
                var text = format(
                    label('qualityAlert', '{0}: {1} of their last {2} calls rated poor. Likely cause: {3}.'),
                    agentName(alert),
                    alert.poorCallCount,
                    alert.recentCallCount,
                    cause);

                return '<div class="alert alert-warning d-flex align-items-center justify-content-between mb-2">' +
                    '<span>' + escapeHtml(text) + '</span>' +
                    '<button type="button" class="btn-close" data-cc-dismiss-quality="' + escapeHtml(agentId) + '" aria-label="' + escapeHtml(label('dismiss', 'Dismiss')) + '"></button>' +
                    '</div>';
            }).join('');

            if (setRegionHtml(refs.qualityAlerts, 'qualityAlerts', html)) {
                refs.qualityAlerts.querySelectorAll('[data-cc-dismiss-quality]').forEach(function (button) {
                    button.addEventListener('click', function () {
                        delete qualityAlerts[button.getAttribute('data-cc-dismiss-quality')];
                        renderQualityAlerts();
                    });
                });
            }
        }

        function onCallQualityAlert(alert) {
            if (!alert || !alert.agentId) {
                return;
            }

            qualityAlerts[alert.agentId] = alert;
            renderQualityAlerts();
        }

        function refresh() {
            return fetch(config.stateUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(function (state) {
                    if (state) {
                        lastAgents = state.agents || [];
                        lastState = state;

                        if (interventions) {
                            interventions.onState(state);
                        }

                        // An open menu is not pulled out from under the supervisor; the board catches up when it closes.
                        if (interventions && interventions.isMenuOpen()) {
                            renderSummary(state);
                            renderTiles(state);
                            watchQueues(state);
                        } else {
                            render(state);
                        }

                        renderQualityAlerts();
                    }
                })
                .catch(function () { });
        }

        var lastState = null;
        var interventions = typeof window.CrestAppsContactCenter === 'object' &&
            typeof window.CrestAppsContactCenter.createSupervisorInterventions === 'function'
            ? window.CrestAppsContactCenter.createSupervisorInterventions({
                root: root,
                config: config,
                refresh: refresh,
                redraw: function () {
                    if (lastState) {
                        renderBoard(lastState);
                    }
                },
                showError: showError,
                clearError: clearError
            })
            : null;

        if (interventions) {
            interventions.bind(refs.board);
        }

        if (window.contactCenterRealTime && config.hubUrl) {
            realtime = window.contactCenterRealTime.connect({
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
                onOfferReceived: refresh,
                onOfferRevoked: refresh,
                onQueueStatsChanged: refresh,
                onRecordingStateChanged: refresh,
                onCallQualityAlert: onCallQualityAlert
            });

            // The supervisor's own engagement moved on (their phone answered, a mode changed, a call ended).
            if (realtime && realtime.connection) {
                realtime.connection.on('SupervisorEngagementChanged', refresh);
            }
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
