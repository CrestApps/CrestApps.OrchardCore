/*
 * Contact Center supervisor dashboard client.
 *
 * Binds the supervisor dashboard page to the real-time Contact Center hub and the dashboard state
 * endpoint. It renders live summary metrics, per-queue tiles with service-level health, and an agent
 * presence board. It reads its configuration from the root element's data-config attribute and depends
 * on the shared "contact-center-realtime" helper for the hub connection.
 */
import {
    filterSupervisorAgents,
    normalizeAgentBoardFilters,
} from './shared/agent-board-filters.js';
import { presenceLabel } from './shared/agent-presence.js';
import { supervisorPhoneCallSummary, supervisorStaleWorkSummary } from './shared/supervisor-actions.js';
import { createSupervisorInterventions } from './supervisor-interventions.js';

(function (window, document) {
    'use strict';

    var REFRESH_INTERVAL_MS = 10000;

    // The dispatch breakdown reads far more than the state poll does, so it is asked for less often.
    var DISPATCH_REFRESH_INTERVAL_MS = 30000;

    // Where a supervisor's agent board filters are kept between visits, in their own browser only.
    var FILTERS_STORAGE_KEY = 'crestapps.contactCenter.dashboard.agentFilters';

    function readStoredFilters() {
        try {
            var raw = window.localStorage.getItem(FILTERS_STORAGE_KEY);

            return raw ? JSON.parse(raw) : null;
        } catch (error) {
            return null;
        }
    }

    function storeFilters(filters) {
        try {
            window.localStorage.setItem(FILTERS_STORAGE_KEY, JSON.stringify(filters));
        } catch (error) {
            // A private window, or storage turned off: the filters just are not remembered.
        }
    }

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

    // How long an agent has been idle: the seconds, and hours once there are any.
    function formatIdle(totalSeconds) {
        if (!isFinite(totalSeconds) || totalSeconds < 0) {
            totalSeconds = 0;
        }

        var hours = Math.floor(totalSeconds / 3600);
        var minutes = Math.floor((totalSeconds % 3600) / 60);
        var seconds = Math.floor(totalSeconds % 60);

        if (hours > 0) {
            return hours + 'h ' + minutes + 'm';
        }

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
            boardCount: root.querySelector('[data-cc-board-count]'),
            filters: root.querySelector('[data-cc-board-filters]'),
            connection: root.querySelector('[data-cc-connection]'),
            error: root.querySelector('[data-cc-error]'),
            qualityAlerts: root.querySelector('[data-cc-quality-alerts]'),
            dispatch: root.querySelector('[data-cc-dispatch]'),
            dispatchPanel: root.querySelector('[data-cc-dispatch-panel]')
        };

        // How far the server's clock is ahead of this browser's, so an idle timer is not off by the difference.
        var clockOffsetMs = 0;


        // The agent board's filters (see shared/agent-board-filters.js), remembered from the supervisor's last visit.
        var filters = readStoredFilters() || {};

        function filterControl(name) {
            return refs.filters ? refs.filters.querySelector('[data-cc-filter="' + name + '"]') : null;
        }

        function filterOption(text, value) {
            var option = document.createElement('option');

            option.textContent = text;
            option.value = value;

            return option;
        }

        // The queue and campaign options follow the state: rebuilt only when the list itself changed, so a poll never
        // resets a select the supervisor is using.
        function syncFilterOptions(select, items, cacheKey) {
            if (!select) {
                return;
            }

            var signature = (items || []).map(function (item) {
                return item.id + '\u0001' + (item.name || item.id);
            }).join('\u0002');

            if (lastHtml[cacheKey] === signature) {
                return;
            }

            lastHtml[cacheKey] = signature;

            var allOption = select.options.length ? select.options[0] : null;

            while (select.options.length > 1) {
                select.remove(1);
            }

            (items || []).forEach(function (item) {
                select.add(filterOption(item.name || item.id, item.id));
            });

            if (!allOption) {
                select.add(filterOption('', ''), 0);
            }
        }

        function syncFilterControls(state) {
            if (!refs.filters) {
                return;
            }

            syncFilterOptions(filterControl('queueId'), state.queues, 'filterQueues');
            syncFilterOptions(filterControl('campaignId'), state.campaigns, 'filterCampaigns');

            // The campaign filter only means something when the tenant has campaigns.
            var campaign = filterControl('campaignId');

            if (campaign && campaign.parentElement) {
                campaign.parentElement.hidden = !(state.campaigns || []).length;
            }

            filters = typeof normalizeAgentBoardFilters === 'function'
                ? normalizeAgentBoardFilters(filters, state)
                : filters;

            ['search', 'queueId', 'campaignId', 'status'].forEach(function (name) {
                var control = filterControl(name);

                if (control && control !== document.activeElement && control.value !== (filters[name] || '')) {
                    control.value = filters[name] || '';
                }
            });
        }

        function bindFilters() {
            if (!refs.filters) {
                return;
            }

            function applyFrom(control) {
                filters[control.getAttribute('data-cc-filter')] = control.value;
                storeFilters(filters);

                if (lastState) {
                    syncFilterControls(lastState);
                    renderBoard(lastState);
                }
            }

            refs.filters.addEventListener('input', function (event) {
                if (event.target && event.target.matches('[data-cc-filter="search"]')) {
                    applyFrom(event.target);
                }
            });

            refs.filters.addEventListener('change', function (event) {
                if (event.target && event.target.matches('select[data-cc-filter]')) {
                    applyFrom(event.target);
                }
            });
        }

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

            var allAgents = state.agents || [];
            var agents = typeof filterSupervisorAgents === 'function'
                ? filterSupervisorAgents(allAgents, filters)
                : allAgents;

            renderBoardCount(agents.length, allAgents.length);

            if (!allAgents.length) {
                setRegionHtml(refs.board, 'board', '<div class="cc-empty">' + escapeHtml(label('noAgents', 'No agents are configured.')) + '</div>');

                return;
            }

            if (!agents.length) {
                setRegionHtml(refs.board, 'board', '<div class="cc-empty">' + escapeHtml(label('noMatchingAgents', 'No agents match these filters.')) + '</div>');

                return;
            }

            var serverNow = Date.parse(state.serverTimeUtc || '');
            var boardHtml = agents.map(function (agent) {
                var status = agent.presenceStatus || 'Offline';

                // An agent on a phone call of their own says so, whatever their presence: what they dialed, and for how long.
                var phoneCall = supervisorPhoneCallSummary(agent.phoneCall, isFinite(serverNow) ? serverNow : Date.now(), strings);
                // Otherwise their state, named as the agent's own screens name it ("On a call", "Wrap-up"), or the
                // reason they gave for a not-ready one.
                var detail = phoneCall || presenceLabel({ status: status, reason: agent.presenceReason }, config.presenceLabels);

                // Listen / Whisper / Barge, Stop and Take over under the name, and the More menu's kebab at the far right
                // of the name row (see supervisor-interventions.js).
                var actions = interventions ? interventions.actionsHtml(agent, state) : '';
                var menu = interventions ? interventions.menuHtml(agent, state) : '';

                return '<div class="cc-agent">' +
                    '<span class="cc-presence__dot is-' + status.toLowerCase() + '"></span>' +
                    '<span class="cc-agent__body">' +
                        '<span class="cc-agent__name">' + escapeHtml(agent.displayName || agent.userId) + '</span>' +
                        '<span class="cc-agent__state badge ta-badge text-bg-secondary">' + escapeHtml(detail) + '</span>' +
                        idleHtml(agent) +
                        staleHtml(agent) +
                        signedInHtml(agent, state, status) +
                    '</span>' +
                    '<span class="cc-badge-count" title="' + escapeHtml(label('activeInteractions', 'Active interactions')) + '">' + agent.activeInteractions + '</span>' +
                    menu +
                    actions +
                '</div>';
            }).join('');

            // The actions are delegated from the board (see supervisor-interventions.js), so a redraw needs no rebinding.
            setRegionHtml(refs.board, 'board', boardHtml);
            updateIdleTimes();
        }

        // A warning for an agent holding work that nothing backs -- a call that is over but still on their line. The
        // supervisor's Free agent action in the More menu clears it.
        function staleHtml(agent) {
            var summary = supervisorStaleWorkSummary(agent, strings);

            if (!summary) {
                return '';
            }

            return '<span class="cc-agent__stale badge ta-badge text-bg-warning" role="note" data-cc-stale title="' + escapeHtml(label('staleHint', 'This agent holds work nothing backs. Use Free agent in the More menu to release it.')) + '">' +
                '<i class="fa-solid fa-triangle-exclamation" aria-hidden="true"></i> ' + escapeHtml(summary) + '</span>';
        }

        // How long an Available agent has been waiting for work. The text is filled in by updateIdleTimes, so the card's
        // markup does not change every second and a poll does not redraw the board for it.
        function idleHtml(agent) {
            if (!agent.idleSinceUtc) {
                return '';
            }

            return '<span class="cc-agent__meta">' + escapeHtml(label('idleFor', 'Idle')) + ' ' +
                '<span class="cc-agent__idle" data-cc-idle-since="' + escapeHtml(agent.idleSinceUtc) + '"></span></span>';
        }

        function nameOf(items, id) {
            var item = (items || []).find(function (candidate) {
                return candidate.id === id;
            });

            return item && item.name ? item.name : id;
        }

        // The queues and campaigns the agent is signed in to; an agent who is at work but in none of them is why nothing
        // reaches them, so it is said.
        function signedInHtml(agent, state, status) {
            var names = (agent.queueIds || []).map(function (id) {
                return nameOf(state.queues, id);
            }).concat((agent.campaignIds || []).map(function (id) {
                return nameOf(state.campaigns, id);
            }));

            if (!names.length) {
                return status === 'Offline'
                    ? ''
                    : '<span class="cc-agent__meta">' + escapeHtml(label('notSignedIn', 'Not signed in to any queue or campaign')) + '</span>';
            }

            return '<span class="cc-agent__meta">' + escapeHtml(label('signedInTo', 'Signed in to')) + ': ' + escapeHtml(names.join(', ')) + '</span>';
        }

        function updateIdleTimes() {
            var now = Date.now() + clockOffsetMs;

            Array.prototype.forEach.call(root.querySelectorAll('[data-cc-idle-since]'), function (element) {
                var since = Date.parse(element.getAttribute('data-cc-idle-since'));
                var text = isFinite(since) ? formatIdle((now - since) / 1000) : '';

                if (element.textContent !== text) {
                    element.textContent = text;
                }
            });
        }

        function renderBoardCount(shown, total) {
            if (!refs.boardCount) {
                return;
            }

            var text = shown === total
                ? format(label('agentCount', '{0} agents'), total)
                : format(label('filteredAgentCount', '{0} of {1} agents'), shown, total);

            if (refs.boardCount.textContent !== text) {
                refs.boardCount.textContent = text;
            }
        }

        // The breakdown of what holds each queue's and campaign's pending work back, and what is left to dispatch.
        function renderDispatch(state) {
            if (!refs.dispatch || !state) {
                return;
            }

            var queues = state.queues || [];
            var serverNow = Date.parse(state.serverTimeUtc || '');
            var reference = isFinite(serverNow) ? serverNow : Date.now();

            if (!queues.length) {
                setRegionHtml(refs.dispatch, 'dispatch', '<div class="cc-empty">' + escapeHtml(label('noPendingWork', 'No work is waiting to be dispatched.')) + '</div>');

                return;
            }

            setRegionHtml(refs.dispatch, 'dispatch', queues.map(function (queue) {
                return dispatchCard(queue, reference);
            }).join(''));
        }

        function dispatchCard(queue, reference) {
            var cls = 'cc-dispatch__card';
            var pacingStuck = queue.pacing === 'ProfileDisabled' || queue.pacing === 'ProfileMissing' ||
                !!queue.pacingLockNotAcquiredSinceUtc ||
                queue.pacingStalledMinutes != null;
            var stalled = !!queue.queueBlocker ||
                !!queue.routing ||
                pacingStuck ||
                queue.profileMismatchCount > 0 ||
                (queue.dialableNowCount > 0 && queue.availableAgentCount === 0);

            if (stalled) {
                cls += ' is-stalled';
            } else if (queue.heldBackCount > 0 && queue.dialableNowCount === 0) {
                cls += ' is-held';
            }

            var headline = format(
                label('dispatchHeadline', 'Pending {0}: {1} dialable now, {2} held back'),
                queue.pendingCount,
                queue.dialableNowCount,
                queue.heldBackCount);

            if (queue.evaluatedCount < queue.pendingCount) {
                headline += ' ' + format(label('dispatchPartial', '(explaining the first {0})'), queue.evaluatedCount);
            }

            var items = (queue.buckets || []).map(function (bucket) {
                var text = label('blocker.' + bucket.blocker, bucket.blocker);

                if (bucket.nextDueUtc) {
                    var due = Date.parse(bucket.nextDueUtc);

                    if (isFinite(due)) {
                        text += ' (' + format(label('dispatchNextDue', 'next due in {0}'), formatWait(Math.max(0, (due - reference) / 1000))) + ')';
                    }
                }

                return '<li><span>' + escapeHtml(text) + '</span><span class="cc-strong">' + bucket.count + '</span></li>';
            }).join('');

            var notes = [];

            // The dialer cannot pace the campaign at all: said first, since it is why dialable records are not dialed.
            if (queue.pacingLockNotAcquiredSinceUtc) {
                var lockSince = Date.parse(queue.pacingLockNotAcquiredSinceUtc);

                if (isFinite(lockSince)) {
                    notes.push(format(
                        label('dispatchLockNotAcquired', 'Not being paced: its pacing lock has not been acquired since {0} ({1} ago), so every pacing cycle skips itself. Check the distributed lock.'),
                        new Date(lockSince).toLocaleString(),
                        formatWait(Math.max(0, (reference - lockSince) / 1000))));
                }
            } else if (queue.pacingStalledMinutes != null) {
                notes.push(format(label('dispatchPacingStalled', 'Pacing stalled: no pacing cycle has completed for {0} minutes.'), queue.pacingStalledMinutes));
            }

            if (queue.queueBlocker) {
                notes.push(label('blocker.' + queue.queueBlocker, queue.queueBlocker));
            }

            notes.push(queue.signedInAgentCount === 0
                ? label('dispatchNoAgents', 'No agents are signed in.')
                : format(label('dispatchAgents', '{0} of {1} signed-in agents are available.'), queue.availableAgentCount, queue.signedInAgentCount));

            if (queue.routing) {
                var routing = queue.routing;
                var ago = formatWait(Math.max(0, (reference - Date.parse(routing.occurredUtc)) / 1000));

                notes.push(format(label('dispatchRouting', 'Routing found no agent {0} ago: {1}'), ago, routing.reason || label('dispatchNoReason', 'no reason recorded')));

                (routing.candidateReasons || []).slice(0, 4).forEach(function (reason) {
                    notes.push(format(label('dispatchCandidate', '{0} agents: {1}'), reason.agentCount, reason.reason));
                });
            } else if (!queue.queueBlocker && !pacingStuck && queue.dialableNowCount > 0 && queue.availableAgentCount > 0) {
                notes.push(label('dispatchWaiting', 'Work and available agents are both here; it is waiting for the next dispatch pass.'));
            } else if (!queue.queueBlocker && queue.dialableNowCount > 0 && queue.availableAgentCount === 0) {
                notes.push(label('dispatchNoAvailable', 'Dialable work is waiting for an available agent.'));
            }

            if (queue.staleAgentCount > 0) {
                notes.push(format(label('dispatchStaleAgents', '{0} signed-in agents look available or busy only because of stale work. Free them from the agent list.'), queue.staleAgentCount));
            }

            var loaded = loadedLines(queue);

            return '<div class="' + cls + '">' +
                '<div class="cc-dispatch__name">' + escapeHtml(queue.name) + '</div>' +
                '<div class="cc-dispatch__headline">' + escapeHtml(headline) + '</div>' +
                '<ul class="cc-dispatch__list">' + items + '</ul>' +
                (loaded.length ? '<div class="cc-dispatch__note">' + loaded.map(escapeHtml).join('<br />') + '</div>' : '') +
                '<div class="cc-dispatch__note">' + notes.map(escapeHtml).join('<br />') + '</div>' +
                '</div>';
        }

        // What is loaded in a campaign's queue: its items by state, how it is paced, the dialer profiles its items are stamped
        // with, what kind of work is pending, and where the stamp and the activity disagree.
        function loadedLines(queue) {
            if (!queue.campaignId) {
                return [];
            }

            var lines = [format(
                label('loadedItems', 'Queue items: {0} waiting, {1} reserved, {2} assigned'),
                queue.waitingItemCount,
                queue.reservedItemCount,
                queue.assignedItemCount)];

            if (queue.pacing && queue.pacing !== 'NothingWaiting') {
                lines.push(label('pacing.' + queue.pacing, queue.pacing));
            }

            (queue.profileStamps || []).forEach(function (stamp) {
                var text;

                if (!stamp.profileId) {
                    text = label('stampNone', 'No dialer profile stamped');
                } else if (stamp.name) {
                    text = format(
                        label('stampProfile', '{0} ({1}, {2})'),
                        stamp.name,
                        stamp.mode,
                        stamp.enabled ? label('profileOn', 'turned on') : label('profileOff', 'turned off'));
                } else {
                    text = label('stampUnreadable', 'A dialer profile that cannot be read');
                }

                lines.push(format(label('stampLine', 'Stamped: {0}: {1}'), text, stamp.itemCount));
            });

            (queue.activityMix || []).slice(0, 4).forEach(function (group) {
                lines.push(format(
                    label('mixLine', 'Pending {0} / {1} / {2}: {3}'),
                    group.source || '-',
                    group.interactionType || '-',
                    group.channel || '-',
                    group.count));
            });

            if (queue.profileMismatchCount > 0) {
                lines.push(format(label('mismatch', '{0} queue items are stamped with a different dialer profile than their activity'), queue.profileMismatchCount));
            }

            if (queue.activitiesWithoutProfileCount > 0) {
                lines.push(format(label('noOwnProfile', '{0} pending activities carry no dialer profile of their own'), queue.activitiesWithoutProfileCount));
            }

            return lines;
        }

        function refreshDispatch() {
            if (!config.dispatchUrl || document.hidden) {
                return Promise.resolve();
            }

            return fetch(config.dispatchUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
                .then(function (response) { return response.ok ? response.json() : null; })
                .then(renderDispatch)
                .catch(function () { });
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
            syncFilterControls(state);
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

                        var serverTime = Date.parse(state.serverTimeUtc || '');

                        if (isFinite(serverTime)) {
                            clockOffsetMs = serverTime - Date.now();
                        }

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
        var interventions = createSupervisorInterventions({
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
        });

        if (interventions) {
            interventions.bind(refs.board);
        }

        bindFilters();

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

        if (refs.dispatchPanel && !config.dispatchUrl) {
            refs.dispatchPanel.hidden = true;
        }

        refreshDispatch();
        window.setInterval(refreshDispatch, DISPATCH_REFRESH_INTERVAL_MS);
        window.setInterval(updateIdleTimes, 1000);
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
