/*
 * What an offer's buttons show while the agent's accept is under way, on the agent workspace and the docked agent bar.
 *
 * Accepting an offer gave no sign anything was happening until the round trip finished, so the agent clicked again.
 * From the first click the accept button reads "Answering..." (or "Dialing..." for a preview dial) with a spinner, and
 * it and its siblings (Decline, Skip) are disabled until the call is on screen. An accept that fails brings the buttons
 * back with an error; one that never answers is given up on after a while so the buttons are never stuck.
 *
 * Concatenated ahead of the scripts that use it by the module asset pipeline. It attaches to a shared namespace
 * rather than exporting, so the same file runs in the browser bundles and under the unit tests.
 */
(function (root) {
    'use strict';

    var contactCenter = root.CrestAppsContactCenter = root.CrestAppsContactCenter || {};

    // How long an accept may go unanswered before the buttons come back.
    var OFFER_ACCEPT_TIMEOUT_MS = 20000;

    function text(labels, key, fallback) {
        return (labels && labels[key]) || fallback;
    }

    // The accept button's look for an offer: { disabled, spinner, label }.
    function offerAcceptView(state, labels) {
        var pending = !!(state && state.pending);
        var preview = !!(state && state.preview);

        if (pending) {
            return {
                disabled: true,
                spinner: true,
                label: preview ? text(labels, 'dialing', 'Dialing…') : text(labels, 'answering', 'Answering…')
            };
        }

        return {
            disabled: false,
            spinner: false,
            label: preview ? text(labels, 'dial', 'Dial') : text(labels, 'accept', 'Accept')
        };
    }

    // Applies offerAcceptView to an offer's buttons: all of them disabled while pending, and the accept button
    // ([data-cc-accept]) swapped to the spinner and label, then back to exactly what it showed before.
    function showOfferAccepting(container, state, labels) {
        if (!container || typeof container.querySelectorAll !== 'function') {
            return;
        }

        var view = offerAcceptView(state, labels);

        Array.prototype.forEach.call(container.querySelectorAll('button'), function (button) {
            button.disabled = view.disabled;
        });

        var acceptButton = container.querySelector('[data-cc-accept]');

        if (!acceptButton) {
            return;
        }

        if (view.spinner) {
            if (acceptButton.__restingHtml === undefined) {
                acceptButton.__restingHtml = acceptButton.innerHTML;
            }

            var spinner = acceptButton.ownerDocument.createElement('span');
            spinner.className = 'spinner-border spinner-border-sm me-1';
            spinner.setAttribute('role', 'status');
            spinner.setAttribute('aria-hidden', 'true');
            acceptButton.textContent = '';
            acceptButton.appendChild(spinner);
            acceptButton.appendChild(acceptButton.ownerDocument.createTextNode(view.label));
        } else if (acceptButton.__restingHtml !== undefined) {
            acceptButton.innerHTML = acceptButton.__restingHtml;
            acceptButton.__restingHtml = undefined;
        }
    }

    // Settles like `promise`, or rejects with an error flagged `offerAcceptTimedOut` once the accept has gone
    // unanswered for OFFER_ACCEPT_TIMEOUT_MS.
    function withOfferAcceptTimeout(promise) {
        return new Promise(function (resolve, reject) {
            var timer = root.setTimeout(function () {
                var error = new Error('The accept timed out.');
                error.offerAcceptTimedOut = true;
                reject(error);
            }, OFFER_ACCEPT_TIMEOUT_MS);

            Promise.resolve(promise).then(function (value) {
                root.clearTimeout(timer);
                resolve(value);
            }, function (error) {
                root.clearTimeout(timer);
                reject(error);
            });
        });
    }

    contactCenter.OFFER_ACCEPT_TIMEOUT_MS = OFFER_ACCEPT_TIMEOUT_MS;
    contactCenter.offerAcceptView = offerAcceptView;
    contactCenter.showOfferAccepting = showOfferAccepting;
    contactCenter.withOfferAcceptTimeout = withOfferAcceptTimeout;
}(typeof globalThis !== 'undefined' ? globalThis : window));
