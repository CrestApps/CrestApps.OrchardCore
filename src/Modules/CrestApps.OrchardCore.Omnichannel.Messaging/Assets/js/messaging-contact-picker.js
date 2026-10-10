/*
 * Live contact search for the messaging composers (new conversation and broadcast).
 *
 * A contact can only be messaged at an address on the channel being sent on, so the search is scoped to the channel of
 * the endpoint picked in the "send from" list. Markup:
 *
 *   <select multiple data-messaging-contact-picker
 *           data-search-url="..."                 the contact search endpoint
 *           data-endpoint-select="#endpoint-id"   the "send from" select
 *           data-endpoint-channels='{"id":"SMS"}'> endpoint id -> channel name
 *
 * The CrestApps bootstrap-select fork is a vanilla-JS plugin (no jQuery); it is driven through its native
 * window.Selectpicker API, and matches are appended as real <option>s so selections submit with the form.
 */
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

        var timer = null;
        // Guards the synthetic input event dispatched to re-apply the filter, so it never re-issues a request.
        var suppress = false;

        function currentChannel() {
            return endpointSelect ? (endpointChannels[endpointSelect.value] || '') : '';
        }

        picker.searchbox.addEventListener('input', function () {
            if (suppress) { return; }

            var q = picker.searchbox.value.trim();
            var channel = currentChannel();

            clearTimeout(timer);

            if (q.length < 2 || !channel) { return; }

            timer = setTimeout(function () {
                fetch(url + '?q=' + encodeURIComponent(q) + '&channel=' + encodeURIComponent(channel), { headers: { 'Accept': 'application/json' }, credentials: 'same-origin' })
                    .then(function (response) { return response.ok ? response.json() : []; })
                    .then(function (items) {
                        var added = false;

                        (items || []).forEach(function (item) {
                            if (!item.address || select.querySelector('option[value="' + CSS.escape(item.address) + '"]')) {
                                return;
                            }

                            var option = document.createElement('option');
                            option.value = item.address;
                            option.textContent = item.name || item.displayAddress || item.address;
                            option.setAttribute('data-subtext', item.displayAddress || item.address);
                            select.appendChild(option);
                            added = true;
                        });

                        if (added) { picker.refresh(); }

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
