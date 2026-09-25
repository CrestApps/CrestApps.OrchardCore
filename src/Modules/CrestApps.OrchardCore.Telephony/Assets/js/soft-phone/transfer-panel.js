/*
 * The soft phone's own transfer panel.
 *
 * Transferring a call used to open the browser's "Transfer to number" prompt whenever the provider had no directory.
 * The prompt was headed with the site's address, blocked the page, and was cut off inside the desktop app's narrow
 * window. The panel replaces it inside the phone: the agent searches the directory or types a number, picks blind or
 * warm when the call offers both, and goes back to the keypad without leaving the phone. A warm transfer that the
 * phone's transfer service carries out becomes a consult the panel follows until the agent completes or cancels it.
 * The decisions it makes live in soft-phone/transfer-target.js, soft-phone/transfer-service.js and
 * soft-phone/consult-state.js; this file only draws them and wires them to the call.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    var KIND_ICONS = { agent: 'fa-user', queue: 'fa-users', external: 'fa-phone' };

    function label(strings, key, fallback) {
        return strings && typeof strings[key] === 'string' && strings[key] ? strings[key] : fallback;
    }

    function format(template, value) {
        return String(template).replace('{0}', value);
    }

    // Creates the panel inside `container`.
    //   options.strings        - the phone's localized labels.
    //   options.escapeHtml     - escapes text for markup.
    //   options.formatNumber   - formats a number for display.
    //   options.modes()        - the supported modes (see transferModes).
    //   options.ownNumbers()   - the tenant's own outbound caller ids.
    //   options.loadDirectory() - a promise of directory entries, or null when there is none.
    //   options.allowNumbers() - whether a typed number is offered (defaults to true).
    //   options.resolveTarget(state) - resolves the target itself ({ refused, ... }), or null to use the provider rules.
    //   options.transfer(target, modeValue, modeName) - performs the transfer; resolves to the command result. A result
    //                            with a live `consult` opens the consult view.
    //   options.getConsult(consult), options.completeConsult(consult), options.cancelConsult(consult)
    //                          - follow and finish a consult; each resolves to a result with the consult's state.
    //   options.onChange()     - called when the panel opens or closes, so the phone re-renders around it.
    function createTransferPanel(container, options) {
        options = options || {};

        var strings = options.strings || {};
        var escapeHtml = options.escapeHtml || function (value) { return String(value == null ? '' : value); };
        var formatNumber = options.formatNumber || function (value) { return value; };
        var state = { open: false, query: '', selected: null, mode: 'blind', entries: [], loading: false, error: '', consult: null };
        var parts = {};
        var pollTimer = null;

        function modes() {
            return typeof options.modes === 'function' ? options.modes() : ['blind'];
        }

        function allowNumbers() {
            return typeof options.allowNumbers === 'function' ? options.allowNumbers() !== false : true;
        }

        function changed() {
            if (typeof options.onChange === 'function') {
                options.onChange();
            }
        }

        function isOpen() {
            return state.open;
        }

        function open(context) {
            if (!container) {
                return;
            }

            var supported = modes();

            stopPolling();
            state = {
                open: true,
                query: '',
                selected: null,
                mode: supported.length ? supported[0] : 'blind',
                entries: [],
                loading: false,
                error: '',
                consult: null
            };

            renderShell(context || {});
            loadDirectory();
            changed();

            if (parts.input) {
                parts.input.focus();
            }
        }

        function close() {
            if (!state.open) {
                return;
            }

            stopPolling();
            state.open = false;
            state.entries = [];
            state.consult = null;

            if (container) {
                container.innerHTML = '';
            }

            parts = {};
            changed();
        }

        function loadDirectory() {
            var pending = typeof options.loadDirectory === 'function' ? options.loadDirectory() : null;

            if (!pending || typeof pending.then !== 'function') {
                renderResults();

                return;
            }

            state.loading = true;
            renderResults();

            pending.then(function (entries) {
                if (!state.open) {
                    return;
                }

                state.entries = Array.isArray(entries) ? entries : [];
            }).catch(function () {
                state.entries = [];
            }).then(function () {
                state.loading = false;

                if (state.open && !state.consult) {
                    // What the call offers can depend on what the directory said (a warm transfer needs a provider
                    // that can hold the caller), so the choice is drawn again now it is known.
                    renderModes();
                    renderResults();
                }
            });
        }

        function renderShell(context) {
            var title = context.callLabel
                ? format(label(strings, 'transferCallOf', 'Transfer {0}'), context.callLabel)
                : label(strings, 'transferTitle', 'Transfer call');

            container.innerHTML =
                '<div class="telephony-soft-phone__transfer-header">' +
                '<button type="button" class="telephony-soft-phone__settings-back" data-telephony-transfer-back title="' +
                escapeHtml(label(strings, 'back', 'Back')) + '" aria-label="' + escapeHtml(label(strings, 'back', 'Back')) + '">' +
                '<i class="fa-solid fa-arrow-left" aria-hidden="true"></i></button>' +
                '<span class="telephony-soft-phone__transfer-title" data-telephony-transfer-title>' + escapeHtml(title) + '</span>' +
                '</div>' +
                '<div data-telephony-transfer-modes-slot></div>' +
                '<input type="text" class="telephony-soft-phone__transfer-input" data-telephony-transfer-input autocomplete="off" inputmode="text" ' +
                'placeholder="' + escapeHtml(label(strings, 'transferSearchPlaceholder', 'Search a name, or enter a number')) + '" ' +
                'aria-label="' + escapeHtml(label(strings, 'transferDestination', 'Transfer destination')) + '" />' +
                '<div class="telephony-soft-phone__transfer-error" data-telephony-transfer-error role="alert" hidden></div>' +
                '<div class="telephony-soft-phone__transfer-results" data-telephony-transfer-results></div>' +
                '<div class="telephony-soft-phone__transfer-actions" data-telephony-transfer-actions>' +
                '<button type="button" class="btn btn-sm btn-outline-secondary" data-telephony-transfer-cancel>' +
                escapeHtml(label(strings, 'cancel', 'Cancel')) + '</button>' +
                '<button type="button" class="btn btn-sm btn-primary" data-telephony-transfer-confirm>' +
                escapeHtml(label(strings, 'transfer', 'Transfer')) + '</button>' +
                '</div>';

            parts = {
                back: container.querySelector('[data-telephony-transfer-back]'),
                modesSlot: container.querySelector('[data-telephony-transfer-modes-slot]'),
                input: container.querySelector('[data-telephony-transfer-input]'),
                error: container.querySelector('[data-telephony-transfer-error]'),
                results: container.querySelector('[data-telephony-transfer-results]'),
                actions: container.querySelector('[data-telephony-transfer-actions]'),
                confirm: container.querySelector('[data-telephony-transfer-confirm]'),
                hint: null,
                modes: []
            };

            parts.back.addEventListener('click', close);
            container.querySelector('[data-telephony-transfer-cancel]').addEventListener('click', close);
            parts.confirm.addEventListener('click', submit);
            parts.input.addEventListener('input', function () {
                state.query = parts.input.value;
                state.selected = null;
                setError('');
                renderResults();
            });
            parts.input.addEventListener('keydown', function (event) {
                if (event.key === 'Enter') {
                    event.preventDefault();
                    submit();
                } else if (event.key === 'Escape') {
                    event.preventDefault();
                    close();
                }
            });
            parts.results.addEventListener('click', function (event) {
                var target = event.target && event.target.closest ? event.target.closest('[data-telephony-transfer-option]') : null;

                if (!target) {
                    return;
                }

                var destination = target.getAttribute('data-telephony-directory-destination');

                if (destination !== null) {
                    var entry = softPhone.filterTransferTargets(state.entries, '').filter(function (candidate) {
                        return candidate.destination === destination;
                    })[0];

                    state.selected = state.selected && entry && state.selected.destination === entry.destination ? null : entry || null;
                } else {
                    state.selected = null;
                }

                setError('');
                renderResults();

                if (target.hasAttribute('data-telephony-transfer-number')) {
                    submit();
                }
            });

            renderModes();
        }

        function renderModes() {
            if (!parts.modesSlot) {
                return;
            }

            var supported = modes();

            if (supported.indexOf(state.mode) === -1) {
                state.mode = supported.length ? supported[0] : 'blind';
            }

            parts.modesSlot.innerHTML = supported.length > 1
                ? '<div class="telephony-soft-phone__transfer-modes" role="radiogroup" aria-label="' +
                    escapeHtml(label(strings, 'transferType', 'Transfer type')) + '">' +
                    supported.map(function (mode) {
                        var text = mode === 'warm'
                            ? label(strings, 'transferWarm', 'Warm')
                            : label(strings, 'transferBlind', 'Blind');

                        return '<button type="button" role="radio" class="telephony-soft-phone__transfer-mode" data-telephony-transfer-mode="' +
                            mode + '" aria-checked="false">' + escapeHtml(text) + '</button>';
                    }).join('') +
                    '</div>' +
                    '<div class="telephony-soft-phone__transfer-hint" data-telephony-transfer-mode-hint></div>'
                : '';

            parts.hint = parts.modesSlot.querySelector('[data-telephony-transfer-mode-hint]');
            parts.modes = Array.prototype.slice.call(parts.modesSlot.querySelectorAll('[data-telephony-transfer-mode]'));
            parts.modes.forEach(function (button) {
                button.addEventListener('click', function () {
                    state.mode = button.getAttribute('data-telephony-transfer-mode');
                    setError('');
                    renderMode();
                });
            });

            renderMode();
        }

        function renderMode() {
            (parts.modes || []).forEach(function (button) {
                var selected = button.getAttribute('data-telephony-transfer-mode') === state.mode;

                button.classList.toggle('is-active', selected);
                button.setAttribute('aria-checked', selected ? 'true' : 'false');
            });

            if (parts.hint) {
                parts.hint.textContent = state.mode === 'warm'
                    ? label(strings, 'transferWarmHint', 'You speak to them before the call is handed over.')
                    : label(strings, 'transferBlindHint', 'The call is sent straight to them.');
            }
        }

        function entryHtml(entry) {
            var selected = !!(state.selected && state.selected.destination === entry.destination);
            var icon = KIND_ICONS[entry.kind] || 'fa-user';
            var presence = entry.presence
                ? ' data-telephony-presence="' + escapeHtml(entry.presence) + '"'
                : '';
            var status = entry.status
                ? '<span class="telephony-soft-phone__directory-presence telephony-soft-phone__directory-presence--' +
                    escapeHtml(String(entry.presence || '').toLowerCase()) + '">' + escapeHtml(entry.status) + '</span>'
                : '';

            return '<button type="button" class="telephony-soft-phone__directory-entry telephony-soft-phone__transfer-option' +
                (selected ? ' is-selected' : '') + (entry.disabled ? ' is-unavailable' : '') +
                '" data-telephony-transfer-option data-telephony-directory-destination="' +
                escapeHtml(entry.destination) + '"' + presence + ' aria-pressed="' + (selected ? 'true' : 'false') + '"' +
                (entry.disabled ? ' aria-disabled="true"' : '') + '>' +
                '<i class="fa-solid ' + icon + '" aria-hidden="true"></i>' +
                '<span class="telephony-soft-phone__directory-name">' + escapeHtml(entry.name) + '</span>' +
                status +
                '<span class="telephony-soft-phone__directory-destination">' + escapeHtml(entry.detail) + '</span></button>';
        }

        function renderResults() {
            if (!parts.results || state.consult) {
                return;
            }

            var matches = softPhone.filterTransferTargets(state.entries, state.query);
            var html = '';
            var query = String(state.query || '').trim();

            if (query && softPhone.isNumberLike(query) && allowNumbers()) {
                html += '<button type="button" class="telephony-soft-phone__transfer-option telephony-soft-phone__transfer-option--number" ' +
                    'data-telephony-transfer-option data-telephony-transfer-number>' +
                    '<i class="fa-solid fa-phone" aria-hidden="true"></i>' +
                    '<span class="telephony-soft-phone__directory-name">' +
                    escapeHtml(format(label(strings, 'transferToNumber', 'Transfer to {0}'), formatNumber(query) || query)) +
                    '</span></button>';
            }

            if (state.loading) {
                html += '<div class="telephony-soft-phone__directory-empty">' +
                    escapeHtml(label(strings, 'directoryLoading', 'Loading the directory...')) + '</div>';
            } else if (matches.length) {
                var group = null;

                matches.forEach(function (entry) {
                    var entryGroup = entry.group || label(strings, 'directory', 'Directory');

                    if (entryGroup !== group) {
                        group = entryGroup;
                        html += '<div class="telephony-soft-phone__transfer-group" role="presentation">' + escapeHtml(group) + '</div>';
                    }

                    html += entryHtml(entry);
                });
            } else if (state.entries.length && query) {
                html += '<div class="telephony-soft-phone__directory-empty">' +
                    escapeHtml(label(strings, 'directoryNoMatch', 'Nobody in the directory matches.')) + '</div>';
            } else if (!query) {
                html += '<div class="telephony-soft-phone__directory-empty">' +
                    escapeHtml(label(strings, state.entries.length ? 'directoryEmpty' : 'transferEnterNumber',
                        state.entries.length ? 'No directory entries are available.' : 'Enter the number or extension to transfer to.')) +
                    '</div>';
            }

            parts.results.innerHTML = html;

            if (parts.confirm) {
                var text = state.selected
                    ? format(label(strings, 'transferToNumber', 'Transfer to {0}'), state.selected.name)
                    : label(strings, 'transfer', 'Transfer');

                parts.confirm.textContent = text;
            }
        }

        function setError(message) {
            state.error = message || '';

            if (parts.error) {
                parts.error.textContent = state.error;
                parts.error.hidden = !state.error;
            }
        }

        function refusalMessage(refused) {
            switch (refused) {
                case 'own-number':
                    return label(strings, 'transferOwnNumber', 'That is this phone system\'s own number. Choose who to transfer the call to.');
                case 'invalid-number':
                    return label(strings, 'transferInvalidNumber', 'Enter a complete phone number or extension.');
                case 'unavailable':
                    return label(strings, 'transferAgentUnavailable', 'That agent cannot take a call right now. Choose someone who is available.');
                case 'warm-queue':
                    return label(strings, 'transferWarmQueue', 'A queue cannot be consulted. Choose an agent or a number, or send the call to the queue with a blind transfer.');
                case 'external-not-allowed':
                    return label(strings, 'transferNumberNotAllowed', 'Transfers to numbers that are not on the approved list are turned off. Choose from the list.');
                default:
                    return label(strings, 'transferTargetRequired', 'Choose who to transfer the call to, or enter a number.');
            }
        }

        function resolveTarget() {
            var resolved = typeof options.resolveTarget === 'function' ? options.resolveTarget(state) : null;

            if (resolved) {
                return { target: resolved, refused: resolved.refused };
            }

            var target = softPhone.resolveTransferTarget({
                query: state.query,
                selected: state.selected,
                ownNumbers: typeof options.ownNumbers === 'function' ? options.ownNumbers() : []
            });

            return { target: target.destination, refused: target.refused };
        }

        function submit() {
            if (state.consult) {
                return;
            }

            var resolved = resolveTarget();

            if (resolved.refused) {
                setError(refusalMessage(resolved.refused));

                if (parts.input) {
                    parts.input.focus();
                }

                return;
            }

            setError('');

            var targetName = state.selected ? state.selected.name : (resolved.target && resolved.target.label) || '';
            var pending = typeof options.transfer === 'function'
                ? options.transfer(resolved.target, softPhone.transferModeValue(state.mode), state.mode)
                : null;

            // A refused hub transfer is reported by the phone's own error line, as every other command is; a refused
            // service transfer carries its reason, which is shown here. Either way the panel stays open so the agent
            // can pick someone else.
            Promise.resolve(pending).then(function (result) {
                if (!state.open) {
                    return;
                }

                if (result && result.consult && result.consult.live) {
                    showConsult(result.consult, targetName);

                    return;
                }

                if (result && result.succeeded !== false) {
                    close();
                } else if (result && result.error) {
                    setError(result.error);
                }
            }).catch(function () { });
        }

        function stopPolling() {
            if (pollTimer) {
                root.clearTimeout(pollTimer);
                pollTimer = null;
            }
        }

        function schedulePoll() {
            stopPolling();

            if (!state.consult || !state.consult.view.live || typeof options.getConsult !== 'function') {
                return;
            }

            pollTimer = root.setTimeout(function () {
                pollTimer = null;

                if (!state.open || !state.consult) {
                    return;
                }

                Promise.resolve(options.getConsult(state.consult.consult)).then(function (result) {
                    if (state.open && state.consult && result && result.consult) {
                        updateConsult(result.consult, false);
                    }
                }).catch(function () { }).then(schedulePoll);
            }, softPhone.CONSULT_POLL_INTERVAL_MS || 1500);
        }

        function showConsult(consult, targetName) {
            state.consult = { consult: consult, name: targetName, view: null, message: '' };

            if (parts.modesSlot) {
                parts.modesSlot.innerHTML = '';
            }

            if (parts.input) {
                parts.input.hidden = true;
            }

            // Leaving the panel mid-consult would leave the caller on hold with nobody deciding what happens to them,
            // so while it runs the only ways out are the consult's own Complete and Cancel.
            if (parts.back) {
                parts.back.hidden = true;
            }

            parts.results.innerHTML =
                '<div class="telephony-soft-phone__consult" data-telephony-consult>' +
                '<i class="fa-solid fa-user-clock" aria-hidden="true"></i>' +
                '<span class="telephony-soft-phone__consult-status" data-telephony-consult-status role="status" aria-live="polite"></span>' +
                '</div>';
            parts.actions.innerHTML =
                '<button type="button" class="btn btn-sm btn-outline-danger" data-telephony-consult-cancel>' +
                escapeHtml(label(strings, 'consultCancel', 'Cancel transfer')) + '</button>' +
                '<button type="button" class="btn btn-sm btn-primary" data-telephony-consult-complete>' +
                escapeHtml(label(strings, 'consultComplete', 'Complete transfer')) + '</button>' +
                '<button type="button" class="btn btn-sm btn-primary" data-telephony-consult-done hidden>' +
                escapeHtml(label(strings, 'consultDone', 'Back to the call')) + '</button>';

            parts.consultStatus = parts.results.querySelector('[data-telephony-consult-status]');
            parts.consultCancel = parts.actions.querySelector('[data-telephony-consult-cancel]');
            parts.consultComplete = parts.actions.querySelector('[data-telephony-consult-complete]');
            parts.consultDone = parts.actions.querySelector('[data-telephony-consult-done]');

            parts.consultComplete.addEventListener('click', function () { finishConsult(options.completeConsult); });
            parts.consultCancel.addEventListener('click', function () { finishConsult(options.cancelConsult); });
            parts.consultDone.addEventListener('click', close);

            updateConsult(consult, true);
        }

        function updateConsult(consult, byAgent) {
            var previous = state.consult.view ? state.consult.view.status : '';
            var view = softPhone.consultView(consult, state.consult.name, strings);

            state.consult.consult = consult;
            state.consult.view = view;

            var message = view.message;

            if (!view.live && !byAgent && previous) {
                message = softPhone.consultEndedMessage(previous, consult, state.consult.name, strings, {}) || message;
            }

            parts.consultStatus.textContent = message;
            parts.consultComplete.disabled = !view.canComplete;
            parts.consultCancel.disabled = !view.canCancel;
            parts.consultComplete.hidden = !view.live;
            parts.consultCancel.hidden = !view.live;
            parts.consultDone.hidden = view.live;

            if (view.live) {
                schedulePoll();
            } else {
                stopPolling();
            }
        }

        function finishConsult(command) {
            if (!state.consult || typeof command !== 'function') {
                return;
            }

            parts.consultComplete.disabled = true;
            parts.consultCancel.disabled = true;
            stopPolling();

            Promise.resolve(command(state.consult.consult)).then(function (result) {
                if (!state.open || !state.consult) {
                    return;
                }

                if (result && result.succeeded !== false) {
                    close();

                    return;
                }

                // Refused -- most often completing before the destination answered. The consult goes on as it was.
                setError(result && result.error ? result.error : label(strings, 'failed', 'Call failed'));
                updateConsult(state.consult.consult, true);
            }).catch(function () {
                if (state.open && state.consult) {
                    updateConsult(state.consult.consult, true);
                }
            });
        }

        return {
            open: open,
            close: close,
            isOpen: isOpen,
            submit: submit
        };
    }

    softPhone.createTransferPanel = createTransferPanel;
}(typeof globalThis !== 'undefined' ? globalThis : window));
