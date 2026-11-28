// @ts-check
/*
 * Live contact search for the messaging composers (new conversation and broadcast).
 *
 * A contact can only be messaged at an address on the channel being sent on, so the search is scoped to the channel of
 * the endpoint picked in the "send from" list. Markup:
 *
 *   <select multiple data-messaging-contact-picker
 *           data-search-url="..."                 the contact search endpoint
 *           data-endpoint-select="#endpoint-id"   the "send from" select
 *           data-endpoint-channels='{"id":"SMS"}'  endpoint id -> channel name
 *           data-new-address-text="..."          subtext for a typed address that is not in the contacts
 *           data-phone-channels='["SMS"]'        the channels whose addresses are phone numbers
 *           data-phone-hint="..."                shown when a typed number is not in international format
 *           data-number-entry="#entry">          optional: the number input with a country picker, see below
 *
 * The CrestApps bootstrap-select fork is a vanilla-JS plugin (no jQuery); it is driven through its native
 * window.Selectpicker API, and matches are appended as real <option>s so selections submit with the form.
 *
 * When the search term is itself a valid address on the channel, the search returns it too, without a contact id, so an
 * address that is not in the contacts can still be picked and messaged. It is offered only when it is exactly what was
 * typed and, on a phone channel, in E.164: a number is never guessed into a country.
 *
 * On a phone channel the number entry lets the agent pick the country from a flag (intl-tel-input) and type the number
 * the way they would say it. A valid number is added to the To line in E.164. Its markup:
 *
 *   <button id="toggle" hidden>                   shown only on a phone channel; opens the entry
 *   <div id="entry" hidden data-number-toggle="#toggle" data-invalid-text="...">
 *       <input type="tel" data-number-input>
 *       <button data-number-add>
 *       <div data-number-error hidden>
 */
import { acceptsTypedAddress, isE164, shouldShowPhoneHint } from './shared/messaging-recipient-address.js';

(function (root) {
    'use strict';

    function init(select) {
        var Selectpicker = root.Selectpicker;
        var url = select.getAttribute('data-search-url');

        if (!Selectpicker || !url) { return; }

        var endpointSelect = document.querySelector(select.getAttribute('data-endpoint-select'));
        var endpointChannels = {};

        try {
            endpointChannels = JSON.parse(select.getAttribute('data-endpoint-channels') || '{}');
        } catch (e) {
            endpointChannels = {};
        }

        var picker = Selectpicker.getOrCreateInstance(select);

        if (!picker || !picker.searchbox) { return; }

        var newAddressText = select.getAttribute('data-new-address-text') || '';
        var phoneHint = select.getAttribute('data-phone-hint') || '';
        var phoneChannels = [];

        try {
            phoneChannels = JSON.parse(select.getAttribute('data-phone-channels') || '[]');
        } catch (e) {
            phoneChannels = [];
        }

        /** @type {HTMLElement | null} */
        var hint = null;
        var timer = null;
        // Guards the synthetic input event dispatched to re-apply the filter, so it never re-issues a request.
        var suppress = false;

        function currentChannel() {
            return endpointSelect ? (endpointChannels[endpointSelect.value] || '') : '';
        }

        function isPhoneChannel() {
            return phoneChannels.indexOf(currentChannel()) >= 0;
        }

        // The hint sits under the search box, inside the menu, where the agent is looking while typing.
        function showHint(show) {
            if (!phoneHint) { return; }

            if (!hint && show) {
                hint = document.createElement('div');
                hint.className = 'small text-body-secondary px-2 pt-1';
                hint.setAttribute('role', 'status');
                hint.textContent = phoneHint;
                picker.searchbox.insertAdjacentElement('afterend', hint);
            }

            if (hint) { hint.hidden = !show; }
        }

        initNumberEntry(select, picker, isPhoneChannel, endpointSelect, newAddressText);

        picker.searchbox.addEventListener('input', function () {
            if (suppress) { return; }

            var q = picker.searchbox.value.trim();
            var channel = currentChannel();

            clearTimeout(timer);

            if (q.length < 2 || !channel) {
                showHint(false);

                return;
            }

            timer = setTimeout(function () {
                fetch(url + '?q=' + encodeURIComponent(q) + '&channel=' + encodeURIComponent(channel), { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
                    .then(function (response) { return response.ok ? response.json() : []; })
                    .then(function (items) {
                        var changed = false;
                        var phone = isPhoneChannel();
                        var offeredTypedAddress = false;

                        // A typed address left unpicked by an earlier search is dropped, so the list holds only the
                        // address being typed now.
                        Array.prototype.forEach.call(select.querySelectorAll('option[data-messaging-new-address]'), function (option) {
                            if (!option.selected) {
                                option.remove();
                                changed = true;
                            }
                        });

                        (items || []).forEach(function (item) {
                            if (!item.address || (!item.id && !acceptsTypedAddress(item, q, phone))) {
                                return;
                            }

                            if (!item.id) { offeredTypedAddress = true; }

                            if (select.querySelector('option[value="' + CSS.escape(item.address) + '"]')) {
                                return;
                            }

                            var option = document.createElement('option');
                            option.value = item.address;
                            option.textContent = item.name || item.displayAddress || item.address;

                            if (item.id) {
                                option.setAttribute('data-subtext', item.displayAddress || item.address);
                            } else {
                                option.setAttribute('data-messaging-new-address', '');

                                if (newAddressText) { option.setAttribute('data-subtext', newAddressText); }
                            }

                            // The live filter matches the typed text, which can differ from the formatted address
                            // shown (a phone number typed without punctuation, for one).
                            option.setAttribute('data-tokens', item.address + ' ' + q);
                            select.appendChild(option);
                            changed = true;
                        });

                        if (changed) { picker.refresh(); }

                        showHint(shouldShowPhoneHint(q, phone, offeredTypedAddress));

                        // refresh() rebuilds the list but drops the active live-search filter, so re-apply it to the
                        // freshly added options. The suppress guard keeps this from fetching again.
                        suppress = true;
                        picker.searchbox.dispatchEvent(new Event('input', { bubbles: true }));
                        suppress = false;
                    })
                    .catch(function () { });
            }, 250);
        });
    }

    /*
     * A subject line belongs only to channels whose messages carry one, so it follows the "send from" pick:
     *
     *   <div data-messaging-subject-line
     *        data-endpoint-select="#endpoint-id"     the "send from" select
     *        data-endpoint-channels='{"id":"Email"}' endpoint id -> channel name
     *        data-subject-channels='["Email"]'>      the channels with subjects
     *
     * A hidden line's inputs are disabled, so a subject typed for one channel is not posted for another.
     */
    function initSubjectLine(line) {
        var endpointSelect = document.querySelector(line.getAttribute('data-endpoint-select'));
        var endpointChannels = {};
        var subjectChannels = [];

        try {
            endpointChannels = JSON.parse(line.getAttribute('data-endpoint-channels') || '{}');
            subjectChannels = JSON.parse(line.getAttribute('data-subject-channels') || '[]');
        } catch (e) {
            endpointChannels = {};
            subjectChannels = [];
        }

        function update() {
            var channel = endpointSelect ? (endpointChannels[endpointSelect.value] || '') : '';
            var visible = subjectChannels.indexOf(channel) !== -1;

            line.classList.toggle('d-none', !visible);

            Array.prototype.forEach.call(line.querySelectorAll('input, textarea'), function (input) {
                input.disabled = !visible;
            });
        }

        if (endpointSelect) {
            endpointSelect.addEventListener('change', update);
        }

        update();
    }

    // The number entry: a phone input with a country picker beside the To line. A valid number is added to the To line in
    // E.164 as a selected recipient, so it is sent exactly as validated here.
    function initNumberEntry(select, picker, isPhoneChannel, endpointSelect, newAddressText) {
        var entrySelector = select.getAttribute('data-number-entry');
        /** @type {HTMLElement | null} */
        var entry = entrySelector ? document.querySelector(entrySelector) : null;
        var toggleSelector = entry ? entry.getAttribute('data-number-toggle') : null;
        /** @type {HTMLElement | null} */
        var toggle = toggleSelector ? document.querySelector(toggleSelector) : null;
        /** @type {HTMLInputElement | null} */
        var input = entry ? entry.querySelector('[data-number-input]') : null;
        var addButton = entry ? entry.querySelector('[data-number-add]') : null;
        /** @type {HTMLElement | null} */
        var error = entry ? entry.querySelector('[data-number-error]') : null;
        var intlTelInput = root.intlTelInput;

        if (!entry || !toggle || !input || !addButton || typeof intlTelInput !== 'function') { return; }

        var entryElement = entry;
        var toggleElement = toggle;
        var inputElement = input;
        var invalidText = entry.getAttribute('data-invalid-text') || '';
        var telephoneInput = null;

        function ensureInput() {
            if (telephoneInput) { return telephoneInput; }

            /** @type {Record<string, unknown>} */
            var options = {
                containerClass: 'w-100',
                dropdownParent: document.body,
                strictMode: true,
            };

            // Start on the agent's own country, read from the browser's language, as the likeliest one to message.
            var region = (navigator.language || '').split('-')[1];

            if (region && region.length === 2) {
                options.initialCountry = region.toLowerCase();
            }

            telephoneInput = intlTelInput(inputElement, options);

            return telephoneInput;
        }

        function setOpen(open) {
            entryElement.hidden = !open;
            toggleElement.setAttribute('aria-expanded', open ? 'true' : 'false');
        }

        function setError(message) {
            inputElement.classList.toggle('is-invalid', !!message);

            if (error) {
                error.textContent = message || '';
                error.hidden = !message;
            }
        }

        function syncToggle() {
            var phone = isPhoneChannel();

            toggleElement.hidden = !phone;

            if (!phone) { setOpen(false); }
        }

        function add() {
            var iti = ensureInput();
            var number = inputElement.value.trim() ? iti.getNumber() : '';

            if (!number || !iti.isValidNumber() || !isE164(number)) {
                setError(invalidText);

                return;
            }

            var option = select.querySelector('option[value="' + CSS.escape(number) + '"]');

            if (!option) {
                option = document.createElement('option');
                option.value = number;
                // Shown in E.164, exactly as it will be sent.
                option.textContent = number;
                option.setAttribute('data-messaging-new-address', '');

                if (newAddressText) { option.setAttribute('data-subtext', newAddressText); }

                select.appendChild(option);
            }

            option.selected = true;
            picker.refresh();
            select.dispatchEvent(new Event('change', { bubbles: true }));

            iti.setNumber('');
            setError('');
            setOpen(false);
            toggleElement.focus();
        }

        toggleElement.addEventListener('click', function () {
            var open = entryElement.hidden;

            setOpen(open);

            if (open) {
                ensureInput();
                inputElement.focus();
            }
        });

        addButton.addEventListener('click', add);

        inputElement.addEventListener('keydown', function (e) {
            // Enter adds the number rather than sending the half-written message.
            if (e.key === 'Enter') {
                e.preventDefault();
                add();
            }
        });

        inputElement.addEventListener('input', function () { setError(''); });

        if (endpointSelect) { endpointSelect.addEventListener('change', syncToggle); }

        syncToggle();
    }

    function initAll() {
        Array.prototype.forEach.call(document.querySelectorAll('[data-messaging-contact-picker]'), init);
        Array.prototype.forEach.call(document.querySelectorAll('[data-messaging-subject-line]'), initSubjectLine);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }
}(typeof globalThis !== 'undefined' ? globalThis : window));
