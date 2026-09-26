/*
 * What a supervisor can do about an agent's call, and how it reads: the live dashboard's per-agent actions (Listen,
 * Whisper, Barge, Take over, and a More menu with End call, Transfer, Record, the agent's state and a message), and the
 * engagement banner the supervisor's own soft phone shows while they are on a call (who they are listening to, a mode
 * switcher, and Stop -- or Hang up, once they have taken the call over).
 *
 * The server says what is possible (the modes the call's provider supports, the interventions the supervisor's
 * permission allows, whether they are engaged and connected); these helpers only decide how it looks. Every string is
 * the page's localized one, with an English fallback.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundles and under the unit tests.
 */
(function (root) {
    'use strict';

    var contactCenter = root.CrestAppsContactCenter = root.CrestAppsContactCenter || {};

    // The engagement modes, in the order they are offered.
    var MONITOR_MODES = ['Monitor', 'Whisper', 'Barge'];

    // Where an engagement is, from the supervisor's click to its end.
    var PHASES = {
        requested: 'requested',
        connecting: 'connecting',
        connected: 'connected',
        tookOver: 'tookOver',
        ended: 'ended'
    };

    function text(labels, key, fallback) {
        return (labels && labels[key]) || fallback;
    }

    function format(template) {
        var args = Array.prototype.slice.call(arguments, 1);

        return String(template).replace(/\{(\d+)\}/g, function (match, index) {
            return args[index] !== undefined ? args[index] : match;
        });
    }

    function escapeHtml(value) {
        return String(value === undefined || value === null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function modeLabel(mode, labels) {
        switch (mode) {
            case 'Whisper':
                return text(labels, 'whisper', 'Whisper');
            case 'Barge':
                return text(labels, 'barge', 'Barge');
            default:
                return text(labels, 'listen', 'Listen');
        }
    }

    // What the supervisor hears and who hears them, per mode.
    function modeHint(mode, labels) {
        switch (mode) {
            case 'Whisper':
                return text(labels, 'whisperHint', 'Only the agent hears you.');
            case 'Barge':
                return text(labels, 'bargeHint', 'The agent and the customer hear you.');
            default:
                return text(labels, 'listenHint', 'Nobody hears you.');
        }
    }

    function has(list, value) {
        return Array.isArray(list) && list.indexOf(value) >= 0;
    }

    // The dashboard's actions for one agent row.
    //   agent: a row of the dashboard state; state: { canIntervene, canMessage }; labels: localized strings.
    // Returns { modes: [{ mode, label, pressed, action }], stop, takeOver: { disabled, title } | null,
    //           menu: [{ action, label, danger }], engagedLabel, unavailable }.
    function agentActions(agent, state, labels) {
        var actions = {
            modes: [],
            stop: false,
            takeOver: null,
            menu: [],
            engagedLabel: '',
            unavailable: (agent && agent.monitoringUnavailableReason) || ''
        };

        if (!agent) {
            return actions;
        }

        var interactionId = agent.activeInteractionId;
        var engaged = !!(interactionId && agent.monitorMode);
        var interventions = agent.availableInterventions || [];

        if (interactionId) {
            MONITOR_MODES.forEach(function (mode) {
                if (!has(agent.availableMonitoringModes, mode)) {
                    return;
                }

                actions.modes.push({
                    mode: mode,
                    label: modeLabel(mode, labels),
                    pressed: engaged && agent.monitorMode === mode,
                    // Engaged, a mode button changes the mode on the same leg; otherwise it rings the supervisor.
                    action: engaged ? 'switch' : 'engage'
                });
            });

            if (engaged) {
                actions.stop = true;
                actions.engagedLabel = agent.monitorConnected
                    ? format(text(labels, 'engagedAs', 'You: {0}'), modeLabel(agent.monitorMode, labels))
                    : text(labels, 'connecting', 'Connecting your phone…');
            }

            if (has(interventions, 'TakeOver')) {
                var ready = engaged && agent.monitorConnected;

                // Not on the call yet: it barges in first, and takes the call over once the supervisor's phone is on it.
                actions.takeOver = {
                    disabled: engaged && !agent.monitorConnected,
                    joinFirst: !engaged,
                    title: ready
                        ? text(labels, 'takeOverHint', 'You take the call; the agent is released.')
                        : text(labels, 'takeOverJoin', 'Joins the call on your phone, then takes it; the agent is released.')
                };
            }

            if (has(interventions, 'EndCall')) {
                actions.menu.push({ action: 'end-call', label: text(labels, 'endCall', 'End call…'), danger: true });
            }

            if (has(interventions, 'Transfer')) {
                actions.menu.push({ action: 'transfer', label: text(labels, 'transfer', 'Transfer…') });
            }

            if (has(interventions, 'Record')) {
                var recording = agent.recordingState === 'Recording';

                actions.menu.push({
                    action: recording ? 'record-off' : 'record-on',
                    label: recording ? text(labels, 'recordOff', 'Stop recording') : text(labels, 'recordOn', 'Start recording')
                });
            }
        }

        if (state && state.canIntervene) {
            actions.menu.push({ action: 'state:Available', label: text(labels, 'setAvailable', 'Set Available') });
            actions.menu.push({ action: 'state:Away', label: text(labels, 'setNotReady', 'Set Not ready') });
            actions.menu.push({ action: 'state:Break', label: text(labels, 'setBreak', 'Set Break') });
            actions.menu.push({ action: 'state:SignOut', label: text(labels, 'signOut', 'Sign out of queues'), danger: true });
        }

        if (state && state.canMessage) {
            actions.menu.push({ action: 'message', label: text(labels, 'message', 'Message…') });
        }

        return actions;
    }

    // The HTML of one agent row's call actions: the mode switcher, Stop and Take over (the More menu is agentMenuHtml).
    function agentActionsHtml(agent, state, labels) {
        var actions = agentActions(agent, state, labels);
        var interactionId = escapeHtml(agent.activeInteractionId || '');
        var html = '';

        if (actions.engagedLabel) {
            html += '<span class="badge text-bg-info cc-agent__monitor" data-cc-monitor-state>' + escapeHtml(actions.engagedLabel) + '</span>';
        }

        if (actions.modes.length) {
            html += '<span class="btn-group btn-group-sm" role="group" aria-label="' + escapeHtml(text(labels, 'modes', 'Monitoring mode')) + '">' +
                actions.modes.map(function (mode) {
                    return '<button type="button" class="btn btn-sm ' + (mode.pressed ? 'btn-primary' : 'btn-outline-secondary') + '"' +
                        ' data-cc-' + mode.action + '="' + interactionId + '" data-cc-mode="' + escapeHtml(mode.mode) + '"' +
                        ' aria-pressed="' + (mode.pressed ? 'true' : 'false') + '"' +
                        ' title="' + escapeHtml(modeHint(mode.mode, labels)) + '">' + escapeHtml(mode.label) + '</button>';
                }).join('') +
                '</span>';
        }

        if (actions.stop) {
            html += '<button type="button" class="btn btn-sm btn-outline-danger" data-cc-stop="' + interactionId + '">' +
                escapeHtml(text(labels, 'stop', 'Stop')) + '</button>';
        }

        if (actions.takeOver) {
            html += '<button type="button" class="btn btn-sm btn-outline-warning" data-cc-takeover="' + interactionId + '"' +
                (actions.takeOver.joinFirst ? ' data-cc-join-first="true"' : '') +
                (actions.takeOver.disabled ? ' disabled aria-disabled="true"' : '') +
                ' title="' + escapeHtml(actions.takeOver.title) + '">' + escapeHtml(text(labels, 'takeOver', 'Take over')) + '</button>';
        }

        if (actions.unavailable) {
            html += '<span class="cc-agent__unavailable text-muted small" role="note" title="' + escapeHtml(actions.unavailable) + '">' +
                '<i class="fa-solid fa-circle-info" aria-hidden="true"></i> ' +
                escapeHtml(text(labels, 'cannotMonitor', 'Cannot be monitored')) +
                '<span class="visually-hidden">: ' + escapeHtml(actions.unavailable) + '</span></span>';
        }

        return html ? '<span class="cc-agent__actions">' + html + '</span>' : '';
    }

    // The kebab button that opens the agent's More menu, and the menu: '' when the supervisor has nothing to do there. It
    // sits at the far right of the agent's name row, apart from the call actions under it.
    function agentMenuHtml(agent, state, labels, openMenu) {
        var actions = agentActions(agent, state, labels);

        if (!actions.menu.length) {
            return '';
        }

        var id = escapeHtml(agent.agentId);
        var interactionId = escapeHtml(agent.activeInteractionId || '');
        var menuId = 'cc-agent-menu-' + id;
        var moreLabel = format(text(labels, 'moreActionsFor', 'More actions for {0}'), agent.displayName || agent.userId || '');

        return '<span class="cc-agent__more">' +
            '<button type="button" class="btn btn-sm btn-outline-secondary cc-agent__kebab" data-cc-more="' + id + '" aria-haspopup="menu"' +
            ' aria-expanded="' + (openMenu ? 'true' : 'false') + '" aria-controls="' + menuId + '"' +
            ' aria-label="' + escapeHtml(moreLabel) + '" title="' + escapeHtml(moreLabel) + '">' +
            '<i class="fa-solid fa-ellipsis-vertical" aria-hidden="true"></i></button>' +
            '<span class="cc-agent__menu dropdown-menu' + (openMenu ? ' show' : '') + '" role="menu" id="' + menuId + '"' +
            ' data-cc-menu="' + id + '"' + (openMenu ? '' : ' hidden') + '>' +
            actions.menu.map(function (item) {
                return '<button type="button" role="menuitem" class="dropdown-item' + (item.danger ? ' text-danger' : '') + '"' +
                    ' data-cc-action="' + escapeHtml(item.action) + '" data-cc-agent="' + id + '" data-cc-interaction="' + interactionId + '">' +
                    escapeHtml(item.label) + '</button>';
            }).join('') +
            '</span></span>';
    }

    // The supervisor's phone's view of their engagement after an event.
    //   current: the engagement, or null; event: { source: 'hub'|'phone', state|type, ... }.
    function nextEngagement(current, event) {
        if (!event) {
            return current;
        }

        if (event.source === 'hub') {
            var notification = event.notification || {};

            switch (notification.state) {
                case 'Requested':
                    return {
                        interactionId: notification.interactionId,
                        token: notification.monitorToken,
                        agentName: notification.agentName || '',
                        mode: notification.mode || 'Monitor',
                        phase: PHASES.requested,
                        reason: ''
                    };
                case 'Connected':
                    return current && current.interactionId === notification.interactionId && current.phase !== PHASES.ended && current.phase !== PHASES.tookOver
                        ? Object.assign({}, current, { phase: PHASES.connected })
                        : current;
                case 'ModeChanged':
                    return current && current.interactionId === notification.interactionId
                        ? Object.assign({}, current, { mode: notification.mode || current.mode })
                        : current;
                case 'TookOver':
                    return current && current.interactionId === notification.interactionId
                        ? Object.assign({}, current, { phase: PHASES.tookOver, mode: 'Barge' })
                        : current;
                case 'Ended':
                    return current && current.interactionId === notification.interactionId
                        ? Object.assign({}, current, { phase: PHASES.ended, reason: notification.reason || '' })
                        : current;
                default:
                    return current;
            }
        }

        if (!current || (event.token && current.token && event.token !== current.token)) {
            return current;
        }

        switch (event.type) {
            case 'answering':
                return current.phase === PHASES.requested ? Object.assign({}, current, { phase: PHASES.connecting }) : current;
            case 'connected':
                return current.phase === PHASES.requested || current.phase === PHASES.connecting
                    ? Object.assign({}, current, { phase: PHASES.connected })
                    : current;
            case 'ended':
            case 'refused':
                return Object.assign({}, current, { phase: PHASES.ended });
            default:
                return current;
        }
    }

    // The HTML of the supervisor's engagement banner in their soft phone, or '' when there is none to show.
    function monitorBannerHtml(engagement, labels) {
        if (!engagement || engagement.phase === PHASES.ended) {
            return '';
        }

        var name = engagement.agentName || text(labels, 'anAgent', 'an agent');
        var tookOver = engagement.phase === PHASES.tookOver;
        var title;

        if (tookOver) {
            title = format(text(labels, 'tookOver', 'You took over {0}\'s call'), name);
        } else if (engagement.phase === PHASES.connected) {
            title = format(text(labels, 'monitoring', 'Monitoring {0}'), name);
        } else {
            title = format(text(labels, 'connectingTo', 'Connecting to {0}\'s call…'), name);
        }

        var html = '<div class="telephony-soft-phone__monitor-title d-flex align-items-center gap-1 mb-1">' +
            '<i class="fa-solid fa-headset" aria-hidden="true"></i>' +
            '<strong data-cc-monitor-title>' + escapeHtml(title) + '</strong></div>';

        if (!tookOver) {
            html += '<div class="telephony-soft-phone__monitor-hint small" data-cc-monitor-hint>' + escapeHtml(modeHint(engagement.mode, labels)) + '</div>' +
                '<div class="btn-group btn-group-sm w-100" role="radiogroup" aria-label="' + escapeHtml(text(labels, 'modes', 'Monitoring mode')) + '">' +
                MONITOR_MODES.map(function (mode) {
                    var checked = engagement.mode === mode;

                    return '<button type="button" role="radio" class="btn btn-sm ' + (checked ? 'btn-primary' : 'btn-outline-primary') + '"' +
                        ' aria-checked="' + (checked ? 'true' : 'false') + '" data-cc-monitor-mode="' + mode + '"' +
                        (engagement.phase === PHASES.connected ? '' : ' disabled') + '>' + escapeHtml(modeLabel(mode, labels)) + '</button>';
                }).join('') +
                '</div>';
        }

        html += '<button type="button" class="btn btn-sm btn-outline-danger w-100 mt-1" data-cc-monitor-stop>' +
            escapeHtml(tookOver ? text(labels, 'hangUp', 'Hang up') : text(labels, 'stop', 'Stop')) + '</button>';

        return html;
    }

    // The HTML of a supervisor's message on the agent's soft phone.
    function supervisorMessageHtml(notification, labels) {
        if (!notification || !notification.text) {
            return '';
        }

        var from = notification.fromName
            ? format(text(labels, 'messageFrom', 'Message from {0}'), notification.fromName)
            : text(labels, 'messageFromSupervisor', 'Message from your supervisor');

        return '<div class="alert alert-info alert-dismissible py-2 small mb-2" role="alert" data-cc-supervisor-message="' + escapeHtml(notification.messageId || '') + '">' +
            '<strong>' + escapeHtml(from) + '</strong><div>' + escapeHtml(notification.text) + '</div>' +
            '<button type="button" class="btn-close" data-cc-dismiss-message aria-label="' + escapeHtml(text(labels, 'dismiss', 'Dismiss')) + '"></button>' +
            '</div>';
    }

    contactCenter.supervisorMessageHtml = supervisorMessageHtml;
    contactCenter.MONITOR_MODES = MONITOR_MODES;
    // An agent id quoted into an attribute selector ([data-x="…"]). The browser's CSS.escape is used where it has one;
    // otherwise backslashes are escaped before quotes, so neither can end the string early or change what it matches.
    function attributeSelectorValue(value, css) {
        if (css && typeof css.escape === 'function') {
            return css.escape(value);
        }

        return String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    }

    contactCenter.attributeSelectorValue = attributeSelectorValue;

    // Where an agent's More menu goes, in viewport pixels, so it is always fully on screen: under its button with its
    // right edge on the button's, moved left or right as far as it must to stay inside the viewport, and flipped above the
    // button when there is no room below it (or pinned to the bottom when there is room nowhere).
    //   anchor: the button's rect { left, right, top, bottom }; menu: { width, height }; viewport: { width, height }.
    function menuPlacement(anchor, menu, viewport, margin, gap) {
        var edge = typeof margin === 'number' ? margin : 8;
        var offset = typeof gap === 'number' ? gap : 4;
        var maxLeft = Math.max(edge, viewport.width - menu.width - edge);
        var left = Math.min(Math.max(anchor.right - menu.width, edge), maxLeft);
        var below = anchor.bottom + offset;
        var above = anchor.top - offset - menu.height;
        var fitsBelow = below + menu.height <= viewport.height - edge;
        var flipped = !fitsBelow && above >= edge;
        var top = fitsBelow || !flipped
            ? Math.max(edge, Math.min(below, viewport.height - menu.height - edge))
            : above;

        return { left: Math.round(left), top: Math.round(top), flipped: flipped };
    }

    // What an agent on a phone call of their own (a number dialed from the keypad, an extension call) is doing, for their
    // row: "On a call · +17025550100 · 1:05". Empty when the agent is on no such call.
    //   phoneCall: { direction: 'Outbound'|'Inbound', party, isExtension, startedUtc }; nowMs: the server's clock, in ms.
    function phoneCallSummary(phoneCall, nowMs, labels) {
        if (!phoneCall) {
            return '';
        }

        var parts = [text(labels, 'onACall', 'On a call')];
        var started = Date.parse(phoneCall.startedUtc || '');

        if (phoneCall.party) {
            parts.push((phoneCall.direction === 'Inbound' ? '← ' : '→ ') + phoneCall.party);
        }

        if (isFinite(started) && isFinite(nowMs)) {
            var seconds = Math.max(0, Math.floor((nowMs - started) / 1000));
            var minutes = Math.floor(seconds / 60);
            var rest = seconds % 60;

            parts.push(minutes + ':' + (rest < 10 ? '0' : '') + rest);
        }

        return parts.join(' · ');
    }

    contactCenter.supervisorMenuPlacement = menuPlacement;
    contactCenter.supervisorPhoneCallSummary = phoneCallSummary;
    contactCenter.MONITOR_PHASES = PHASES;
    contactCenter.supervisorAgentActions = agentActions;
    contactCenter.supervisorAgentActionsHtml = agentActionsHtml;
    contactCenter.supervisorAgentMenuHtml = agentMenuHtml;
    contactCenter.nextMonitorEngagement = nextEngagement;
    contactCenter.monitorBannerHtml = monitorBannerHtml;
    contactCenter.monitorModeLabel = modeLabel;
    contactCenter.escapeSupervisorHtml = escapeHtml;
}(typeof globalThis !== 'undefined' ? globalThis : window));
