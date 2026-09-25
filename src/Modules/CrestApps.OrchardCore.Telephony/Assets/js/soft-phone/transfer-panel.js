/*
 * The soft phone's own transfer panel.
 *
 * Transferring a call used to open the browser's "Transfer to number" prompt whenever the provider had no directory.
 * The prompt was headed with the site's address, blocked the page, and was cut off inside the desktop app's narrow
 * window. The panel replaces it inside the phone: the agent searches the directory or types a number, picks blind or
 * warm when the provider offers both, and goes back to the keypad without leaving the phone. The decisions it makes
 * live in soft-phone/transfer-target.js; this file only draws them and wires them to the call.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    function label(strings, key, fallback) {
        return strings && typeof strings[key] === 'string' && strings[key] ? strings[key] : fallback;
    }

    function format(template, value) {
        return String(template).replace('{0}', value);
    }

    // Creates the panel inside `container`.
    //   options.strings       - the phone's localized labels.
    //   options.escapeHtml    - escapes text for markup.
    //   options.formatNumber  - formats a number for display.
    //   options.modes()       - the supported modes (see transferModes).
    //   options.ownNumbers()  - the tenant's own outbound caller ids.
    //   options.loadDirectory() - a promise of the provider's directory entries, or null when it has none.
    //   options.transfer(destination, modeValue) - performs the transfer; resolves to the hub result.
    //   options.onChange()    - called when the panel opens or closes, so the phone re-renders around it.
    function createTransferPanel(container, options) {
        options = options || {};

        var strings = options.strings || {};
        var escapeHtml = options.escapeHtml || function (value) { return String(value == null ? '' : value); };
        var formatNumber = options.formatNumber || function (value) { return value; };
        var state = { open: false, query: '', selected: null, mode: 'blind', entries: [], loading: false, error: '' };
        var parts = {};

        function modes() {
            return typeof options.modes === 'function' ? options.modes() : ['blind'];
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

            state = {
                open: true,
                query: '',
                selected: null,
                mode: supported.length ? supported[0] : 'blind',
                entries: [],
                loading: false,
                error: ''
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

            state.open = false;
            state.entries = [];

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

                if (state.open) {
                    renderResults();
                }
            });
        }

        function renderShell(context) {
            var supported = modes();
            var title = context.callLabel
                ? format(label(strings, 'transferCallOf', 'Transfer {0}'), context.callLabel)
                : label(strings, 'transferTitle', 'Transfer call');
            var modeHtml = '';

            if (supported.length > 1) {
                modeHtml = '<div class="telephony-soft-phone__transfer-modes" role="radiogroup" aria-label="' +
                    escapeHtml(label(strings, 'transferType', 'Transfer type')) + '">' +
                    supported.map(function (mode) {
                        var text = mode === 'warm'
                            ? label(strings, 'transferWarm', 'Warm')
                            : label(strings, 'transferBlind', 'Blind');

                        return '<button type="button" role="radio" class="telephony-soft-phone__transfer-mode" data-telephony-transfer-mode="' +
                            mode + '" aria-checked="false">' + escapeHtml(text) + '</button>';
                    }).join('') +
                    '</div>' +
                    '<div class="telephony-soft-phone__transfer-hint" data-telephony-transfer-mode-hint></div>';
            }

            container.innerHTML =
                '<div class="telephony-soft-phone__transfer-header">' +
                '<button type="button" class="telephony-soft-phone__settings-back" data-telephony-transfer-back title="' +
                escapeHtml(label(strings, 'back', 'Back')) + '" aria-label="' + escapeHtml(label(strings, 'back', 'Back')) + '">' +
                '<i class="fa-solid fa-arrow-left" aria-hidden="true"></i></button>' +
                '<span class="telephony-soft-phone__transfer-title" data-telephony-transfer-title>' + escapeHtml(title) + '</span>' +
                '</div>' +
                modeHtml +
                '<input type="text" class="telephony-soft-phone__transfer-input" data-telephony-transfer-input autocomplete="off" inputmode="text" ' +
                'placeholder="' + escapeHtml(label(strings, 'transferSearchPlaceholder', 'Search a name, or enter a number')) + '" ' +
                'aria-label="' + escapeHtml(label(strings, 'transferDestination', 'Transfer destination')) + '" />' +
                '<div class="telephony-soft-phone__transfer-error" data-telephony-transfer-error role="alert" hidden></div>' +
                '<div class="telephony-soft-phone__transfer-results" data-telephony-transfer-results></div>' +
                '<div class="telephony-soft-phone__transfer-actions">' +
                '<button type="button" class="btn btn-sm btn-outline-secondary" data-telephony-transfer-cancel>' +
                escapeHtml(label(strings, 'cancel', 'Cancel')) + '</button>' +
                '<button type="button" class="btn btn-sm btn-primary" data-telephony-transfer-confirm>' +
                escapeHtml(label(strings, 'transfer', 'Transfer')) + '</button>' +
                '</div>';

            parts = {
                input: container.querySelector('[data-telephony-transfer-input]'),
                error: container.querySelector('[data-telephony-transfer-error]'),
                results: container.querySelector('[data-telephony-transfer-results]'),
                confirm: container.querySelector('[data-telephony-transfer-confirm]'),
                hint: container.querySelector('[data-telephony-transfer-mode-hint]'),
                modes: Array.prototype.slice.call(container.querySelectorAll('[data-telephony-transfer-mode]'))
            };

            container.querySelector('[data-telephony-transfer-back]').addEventListener('click', close);
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
            parts.modes.forEach(function (button) {
                button.addEventListener('click', function () {
                    state.mode = button.getAttribute('data-telephony-transfer-mode');
                    renderMode();
                });
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

        function renderResults() {
            if (!parts.results) {
                return;
            }

            var matches = softPhone.filterTransferTargets(state.entries, state.query);
            var html = '';
            var query = String(state.query || '').trim();

            if (query && softPhone.isNumberLike(query)) {
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
                html += '<div class="telephony-soft-phone__transfer-group" role="presentation">' +
                    escapeHtml(label(strings, 'directory', 'Directory')) + '</div>' +
                    matches.map(function (entry) {
                        var selected = !!(state.selected && state.selected.destination === entry.destination);

                        return '<button type="button" class="telephony-soft-phone__directory-entry telephony-soft-phone__transfer-option' +
                            (selected ? ' is-selected' : '') + '" data-telephony-transfer-option data-telephony-directory-destination="' +
                            escapeHtml(entry.destination) + '" aria-pressed="' + (selected ? 'true' : 'false') + '">' +
                            '<i class="fa-solid fa-user" aria-hidden="true"></i>' +
                            '<span class="telephony-soft-phone__directory-name">' + escapeHtml(entry.name) + '</span>' +
                            '<span class="telephony-soft-phone__directory-destination">' + escapeHtml(entry.detail) + '</span></button>';
                    }).join('');
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
            if (refused === 'own-number') {
                return label(strings, 'transferOwnNumber', 'That is this phone system\'s own number. Choose who to transfer the call to.');
            }

            if (refused === 'invalid-number') {
                return label(strings, 'transferInvalidNumber', 'Enter a complete phone number or extension.');
            }

            return label(strings, 'transferTargetRequired', 'Choose who to transfer the call to, or enter a number.');
        }

        function submit() {
            var target = softPhone.resolveTransferTarget({
                query: state.query,
                selected: state.selected,
                ownNumbers: typeof options.ownNumbers === 'function' ? options.ownNumbers() : []
            });

            if (target.refused) {
                setError(refusalMessage(target.refused));

                if (parts.input) {
                    parts.input.focus();
                }

                return;
            }

            setError('');

            var pending = typeof options.transfer === 'function'
                ? options.transfer(target.destination, softPhone.transferModeValue(state.mode))
                : null;

            // A refused transfer is reported by the phone's own error line, as every other command is; the panel stays
            // open so the agent can pick someone else.
            Promise.resolve(pending).then(function (result) {
                if (result && result.succeeded !== false) {
                    close();
                }
            }).catch(function () { });
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
