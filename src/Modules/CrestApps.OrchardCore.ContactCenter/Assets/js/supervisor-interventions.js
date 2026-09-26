/*
 * The live dashboard's supervisor interventions: Listen / Whisper / Barge, Stop, Take over, and the More menu (End call,
 * Transfer, Record, the agent's state, Message), with the confirm and form panels they open.
 *
 * The dashboard script owns the page and its polling; this owns what the per-agent actions do. The rows are redrawn
 * whenever the state changes, so everything here is delegated from the board and the panel, and what is open (a menu,
 * a panel) is remembered across redraws. How the actions read lives in shared/supervisor-actions.js.
 *
 * Concatenated after shared/supervisor-actions.js and ahead of supervisor-dashboard.js.
 */
(function (window, document) {
    'use strict';

    var contactCenter = window.CrestAppsContactCenter = window.CrestAppsContactCenter || {};

    // How long a Take over that had to barge in first waits for the supervisor's phone to connect.
    var TAKEOVER_JOIN_TIMEOUT_MS = 30000;

    // Each panel's title: the localized string's key and its fallback.
    var PANEL_TITLES = {
        'end-call': ['endCallTitle', 'End call'],
        'sign-out': ['signOutTitle', 'Sign out'],
        transfer: ['transferTitle', 'Transfer call'],
        message: ['messageTitle', 'Message']
    };

    function createSupervisorInterventions(options) {
        var root = options.root;
        var config = options.config || {};
        var strings = config.strings || {};
        var urls = config.interventionUrls || {};
        var escape = contactCenter.escapeSupervisorHtml;
        var panel = root.querySelector('[data-cc-intervention-panel]');
        var status = root.querySelector('[data-cc-intervention-status]');
        var openMenuAgentId = null;
        var openMenuAnchor = null;
        var openPanel = null;
        var pendingTakeovers = {};
        var lastState = { agents: [], queues: [] };

        function label(key, fallback) {
            return strings[key] || fallback;
        }

        function format(template) {
            var args = Array.prototype.slice.call(arguments, 1);

            return String(template).replace(/\{(\d+)\}/g, function (match, index) {
                return args[index] !== undefined ? args[index] : match;
            });
        }

        function findAgent(agentId) {
            return (lastState.agents || []).find(function (agent) {
                return agent.agentId === agentId;
            });
        }

        function findAgentByInteraction(interactionId) {
            return (lastState.agents || []).find(function (agent) {
                return agent.activeInteractionId === interactionId;
            });
        }

        function announce(message, isError) {
            if (isError) {
                options.showError(message);

                return;
            }

            options.clearError();

            if (status) {
                status.textContent = message || '';
            }
        }

        function post(name, payload) {
            var url = name === 'engage' ? config.engageUrl : urls[name];

            if (!url) {
                return Promise.resolve({ succeeded: false, errorMessage: label('unavailableAction', 'This action is not available.') });
            }

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
                    'RequestVerificationToken': config.antiForgeryToken || ''
                },
                body: body.toString()
            })
                .then(function (response) {
                    return response.ok ? response.json() : { succeeded: false };
                })
                .catch(function () {
                    return { succeeded: false };
                });
        }

        // Runs an action, reports how it went, and refreshes the board.
        function act(name, payload, button, done) {
            if (button) {
                button.disabled = true;
            }

            return post(name, payload).then(function (result) {
                if (!result || !result.succeeded) {
                    announce((result && result.errorMessage) || label('engagementFailed', 'The supervisor action could not be started.'), true);
                } else {
                    announce(result.message || label('actionDone', 'Done.'));

                    if (typeof done === 'function') {
                        done(result);
                    }
                }

                if (button) {
                    button.disabled = false;
                }

                options.refresh();

                return result;
            });
        }

        function closeMenu(restoreFocus) {
            var agentId = openMenuAgentId;

            if (!agentId) {
                return;
            }

            openMenuAgentId = null;
            openMenuAnchor = null;
            options.redraw();

            if (restoreFocus) {
                var trigger = root.querySelector('[data-cc-more="' + cssEscape(agentId) + '"]');

                if (trigger) {
                    trigger.focus();
                }
            }
        }

        function cssEscape(value) {
            return contactCenter.attributeSelectorValue(value, window.CSS);
        }

        function toggleMenu(agentId) {
            openMenuAgentId = openMenuAgentId === agentId ? null : agentId;
            options.redraw();

            if (openMenuAgentId) {
                positionMenu();

                var first = root.querySelector('[data-cc-menu="' + cssEscape(agentId) + '"] [role="menuitem"]');

                if (first) {
                    first.focus();
                }
            }
        }

        // Puts the open menu next to its button, fully inside the viewport (see shared/supervisor-actions.js).
        function positionMenu() {
            if (!openMenuAgentId) {
                return;
            }

            var trigger = root.querySelector('[data-cc-more="' + cssEscape(openMenuAgentId) + '"]');
            var menu = root.querySelector('[data-cc-menu="' + cssEscape(openMenuAgentId) + '"]');

            if (!trigger || !menu) {
                return;
            }

            var anchor = trigger.getBoundingClientRect();

            openMenuAnchor = { left: anchor.left, top: anchor.top };

            var placed = contactCenter.supervisorMenuPlacement(
                { left: anchor.left, right: anchor.right, top: anchor.top, bottom: anchor.bottom },
                { width: menu.offsetWidth, height: menu.offsetHeight },
                { width: document.documentElement.clientWidth || window.innerWidth, height: window.innerHeight });

            menu.style.left = placed.left + 'px';
            menu.style.top = placed.top + 'px';
            menu.setAttribute('data-cc-menu-placement', placed.flipped ? 'above' : 'below');
        }

        // Whether the open menu's button has moved since the menu was placed next to it.
        function menuButtonMoved() {
            var trigger = openMenuAgentId ? root.querySelector('[data-cc-more="' + cssEscape(openMenuAgentId) + '"]') : null;

            if (!trigger || !openMenuAnchor) {
                return true;
            }

            var anchor = trigger.getBoundingClientRect();

            return Math.abs(anchor.left - openMenuAnchor.left) > 1 || Math.abs(anchor.top - openMenuAnchor.top) > 1;
        }

        function showPanel(kind, agent) {
            openPanel = { kind: kind, agentId: agent.agentId, interactionId: agent.activeInteractionId };
            renderPanel();

            var focusable = panel && panel.querySelector('select, textarea, input, button');

            if (focusable) {
                focusable.focus();
            }
        }

        function closePanel() {
            openPanel = null;
            renderPanel();
        }

        function transferOptions() {
            var queues = (lastState.queues || []).map(function (queue) {
                return '<option value="Queue:' + escape(queue.id) + '">' + escape(format(label('toQueue', 'Queue: {0}'), queue.name)) + '</option>';
            }).join('');
            var agents = (lastState.agents || [])
                .filter(function (agent) {
                    return agent.presenceStatus === 'Available' && (!openPanel || agent.agentId !== openPanel.agentId);
                })
                .map(function (agent) {
                    return '<option value="Agent:' + escape(agent.agentId) + '">' + escape(format(label('toAgent', 'Agent: {0}'), agent.displayName || agent.userId)) + '</option>';
                }).join('');

            return (queues ? '<optgroup label="' + escape(label('queues', 'Queues')) + '">' + queues + '</optgroup>' : '') +
                (agents ? '<optgroup label="' + escape(label('availableAgents', 'Available agents')) + '">' + agents + '</optgroup>' : '') +
                '<option value="External:">' + escape(label('toNumber', 'A phone number')) + '</option>';
        }

        function renderPanel() {
            if (!panel) {
                return;
            }

            var agent = openPanel ? findAgent(openPanel.agentId) : null;

            if (!openPanel || !agent) {
                openPanel = null;
                panel.innerHTML = '';
                panel.hidden = true;

                return;
            }

            var name = agent.displayName || agent.userId;
            var body;
            var confirm;

            switch (openPanel.kind) {
                case 'end-call':
                    body = '<p class="mb-2">' + escape(format(label('endCallConfirm', 'End {0}\'s call for everyone on it?'), name)) + '</p>';
                    confirm = label('endCallButton', 'End call');
                    break;
                case 'sign-out':
                    body = '<p class="mb-2">' + escape(format(label('signOutConfirm', 'Sign {0} out of their queues?'), name)) + '</p>';
                    confirm = label('signOutButton', 'Sign out');
                    break;
                case 'transfer':
                    body = '<label class="form-label" for="ccTransferTarget">' + escape(label('transferTo', 'Transfer to')) + '</label>' +
                        '<select class="form-select form-select-sm mb-2" id="ccTransferTarget" data-cc-transfer-target>' + transferOptions() + '</select>' +
                        '<label class="form-label" for="ccTransferNumber">' + escape(label('transferNumber', 'Number (for a phone number)')) + '</label>' +
                        '<input type="tel" class="form-control form-control-sm mb-2" id="ccTransferNumber" data-cc-transfer-number autocomplete="off" />';
                    confirm = label('transferButton', 'Transfer');
                    break;
                case 'message':
                    body = '<label class="form-label" for="ccAgentMessage">' + escape(format(label('messageTo', 'Message to {0}'), name)) + '</label>' +
                        '<textarea class="form-control form-control-sm mb-2" id="ccAgentMessage" rows="2" maxlength="500" data-cc-message-text></textarea>';
                    confirm = label('sendButton', 'Send');
                    break;
                default:
                    openPanel = null;
                    panel.hidden = true;

                    return;
            }

            panel.innerHTML = '<div class="cc-panel mb-3" role="dialog" aria-modal="false" aria-labelledby="ccInterventionTitle">' +
                '<div class="cc-panel__header" id="ccInterventionTitle">' + escape(format(label('panelTitle', '{0}: {1}'), name, label(PANEL_TITLES[openPanel.kind][0], PANEL_TITLES[openPanel.kind][1]))) + '</div>' +
                '<div class="cc-panel__body">' + body +
                '<div class="d-flex gap-2">' +
                '<button type="button" class="btn btn-sm ' + (openPanel.kind === 'end-call' || openPanel.kind === 'sign-out' ? 'btn-danger' : 'btn-primary') + '" data-cc-panel-confirm>' + escape(confirm) + '</button>' +
                '<button type="button" class="btn btn-sm btn-outline-secondary" data-cc-panel-cancel>' + escape(label('cancel', 'Cancel')) + '</button>' +
                '</div></div></div>';
            panel.hidden = false;
        }

        function confirmPanel(button) {
            var current = openPanel;

            if (!current) {
                return;
            }

            switch (current.kind) {
                case 'end-call':
                    act('endCall', { interactionId: current.interactionId }, button, closePanel);
                    break;
                case 'sign-out':
                    act('agentState', { agentId: current.agentId, status: 'SignOut' }, button, closePanel);
                    break;
                case 'transfer':
                    var choice = (panel.querySelector('[data-cc-transfer-target]') || {}).value || '';
                    var separator = choice.indexOf(':');
                    var targetType = separator > 0 ? choice.substring(0, separator) : '';
                    var targetId = separator > 0 ? choice.substring(separator + 1) : '';

                    if (targetType === 'External') {
                        targetId = ((panel.querySelector('[data-cc-transfer-number]') || {}).value || '').trim();
                    }

                    if (!targetType || !targetId) {
                        announce(label('transferTargetRequired', 'Choose where to transfer the call.'), true);

                        return;
                    }

                    act('transfer', { interactionId: current.interactionId, targetType: targetType, targetId: targetId }, button, closePanel);
                    break;
                case 'message':
                    var message = ((panel.querySelector('[data-cc-message-text]') || {}).value || '').trim();

                    if (!message) {
                        announce(label('messageRequired', 'Type a message to send.'), true);

                        return;
                    }

                    act('message', { agentId: current.agentId, text: message }, button, closePanel);
                    break;
            }
        }

        function runMenuAction(action, agentId, interactionId, button) {
            var agent = findAgent(agentId);

            closeMenu(false);

            if (!agent) {
                return;
            }

            if (action === 'end-call' || action === 'transfer' || action === 'message') {
                showPanel(action, agent);

                return;
            }

            if (action === 'record-on' || action === 'record-off') {
                act('recording', { interactionId: interactionId, record: action === 'record-on' ? 'true' : 'false' }, button);

                return;
            }

            if (action.indexOf('state:') === 0) {
                var state = action.substring('state:'.length);

                if (state === 'SignOut') {
                    showPanel('sign-out', agent);

                    return;
                }

                act('agentState', { agentId: agentId, status: state }, button);
            }
        }

        function takeOver(button) {
            var interactionId = button.getAttribute('data-cc-takeover');

            if (button.getAttribute('data-cc-join-first') === 'true') {
                // Not on the call yet: barge in, and take the call over once the supervisor's phone is on it.
                act('engage', { interactionId: interactionId, mode: 'Barge' }, button, function () {
                    pendingTakeovers[interactionId] = Date.now();
                    announce(label('takeOverJoining', 'Joining the call on your phone to take it over…'));
                });

                return;
            }

            act('takeover', { interactionId: interactionId }, button);
        }

        function onBoardClick(event) {
            var target = event.target.closest('button');

            if (!target || !root.contains(target) || target.disabled) {
                return;
            }

            if (target.hasAttribute('data-cc-engage')) {
                act('engage', { interactionId: target.getAttribute('data-cc-engage'), mode: target.getAttribute('data-cc-mode') }, target);
            } else if (target.hasAttribute('data-cc-switch')) {
                act('switch', { interactionId: target.getAttribute('data-cc-switch'), mode: target.getAttribute('data-cc-mode') }, target);
            } else if (target.hasAttribute('data-cc-stop')) {
                act('stop', { interactionId: target.getAttribute('data-cc-stop') }, target);
            } else if (target.hasAttribute('data-cc-takeover')) {
                takeOver(target);
            } else if (target.hasAttribute('data-cc-more')) {
                toggleMenu(target.getAttribute('data-cc-more'));
            } else if (target.hasAttribute('data-cc-action')) {
                runMenuAction(target.getAttribute('data-cc-action'), target.getAttribute('data-cc-agent'), target.getAttribute('data-cc-interaction'), target);
            }
        }

        function onBoardKeydown(event) {
            var menu = event.target.closest('[role="menu"]');

            if (event.key === 'Escape' && openMenuAgentId) {
                event.preventDefault();
                closeMenu(true);

                return;
            }

            if (!menu || (event.key !== 'ArrowDown' && event.key !== 'ArrowUp')) {
                return;
            }

            var items = Array.prototype.slice.call(menu.querySelectorAll('[role="menuitem"]'));
            var index = items.indexOf(document.activeElement);
            var next = event.key === 'ArrowDown' ? index + 1 : index - 1;

            event.preventDefault();

            if (items.length) {
                items[(next + items.length) % items.length].focus();
            }
        }

        function bind(board) {
            if (board) {
                board.addEventListener('click', onBoardClick);
                board.addEventListener('keydown', onBoardKeydown);
            }

            if (panel) {
                panel.addEventListener('click', function (event) {
                    var target = event.target.closest('button');

                    if (!target) {
                        return;
                    }

                    if (target.hasAttribute('data-cc-panel-confirm')) {
                        confirmPanel(target);
                    } else if (target.hasAttribute('data-cc-panel-cancel')) {
                        closePanel();
                    }
                });

                panel.addEventListener('keydown', function (event) {
                    if (event.key === 'Escape') {
                        closePanel();
                    }
                });
            }

            document.addEventListener('click', function (event) {
                if (openMenuAgentId && !event.target.closest('.cc-agent__more')) {
                    closeMenu(false);
                }
            });

            // A menu placed in the viewport no longer follows its button once the page moves, so it closes instead. A
            // scroll that left the button where it was (one that finished just as the menu opened) is no reason to.
            window.addEventListener('scroll', function (event) {
                if (openMenuAgentId && !(event.target && event.target.closest && event.target.closest('.cc-agent__menu')) && menuButtonMoved()) {
                    closeMenu(false);
                }
            }, true);

            window.addEventListener('resize', function () {
                positionMenu();
            });

            document.addEventListener('keydown', function (event) {
                if (event.key === 'Escape' && openMenuAgentId) {
                    closeMenu(true);
                }
            });
        }

        // The new state: a Take over that had to barge in first goes ahead once the supervisor's phone is on the call.
        function onState(state) {
            lastState = state || lastState;

            Object.keys(pendingTakeovers).forEach(function (interactionId) {
                var agent = findAgentByInteraction(interactionId);

                if (!agent || Date.now() - pendingTakeovers[interactionId] > TAKEOVER_JOIN_TIMEOUT_MS) {
                    delete pendingTakeovers[interactionId];

                    return;
                }

                if (agent.monitorMode && agent.monitorConnected) {
                    delete pendingTakeovers[interactionId];
                    act('takeover', { interactionId: interactionId }, null);
                }
            });

            // A panel stays as the supervisor left it -- a half-typed message is theirs -- unless its agent is gone.
            if (openPanel && !findAgent(openPanel.agentId)) {
                closePanel();
            }
        }

        return {
            actionsHtml: function (agent, state) {
                return contactCenter.supervisorAgentActionsHtml(agent, state, strings);
            },
            menuHtml: function (agent, state) {
                return contactCenter.supervisorAgentMenuHtml(agent, state, strings, openMenuAgentId === agent.agentId);
            },
            bind: bind,
            onState: onState,
            // Whether a redraw now would pull the rows out from under the supervisor.
            isMenuOpen: function () {
                return !!openMenuAgentId;
            }
        };
    }

    contactCenter.createSupervisorInterventions = createSupervisorInterventions;
})(window, document);
