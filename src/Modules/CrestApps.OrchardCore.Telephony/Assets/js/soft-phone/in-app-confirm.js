/*
 * A confirmation asked inside the soft phone instead of through the browser.
 *
 * The browser's own confirm dialog is headed with the site's address, blocks every call event while it is open, and
 * inside the desktop app's narrow window it is cut off. The phone asks its questions in its own panel instead.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Asks `options.message` in a bar inserted into `host` ahead of `options.before` (or at its end). Resolves true when
    // the agent confirms and false when they cancel or press Escape. Asking again replaces a question still open.
    //   options - { message, confirmLabel, cancelLabel, before }
    function showInAppConfirm(host, options) {
        options = options || {};

        if (!host || !host.ownerDocument) {
            return Promise.resolve(false);
        }

        var existing = host.querySelector('[data-telephony-confirm]');

        if (existing && typeof existing.__telephonyCancel === 'function') {
            existing.__telephonyCancel();
        }

        var doc = host.ownerDocument;
        var bar = doc.createElement('div');
        var message = doc.createElement('p');
        var actions = doc.createElement('div');
        var cancel = doc.createElement('button');
        var confirm = doc.createElement('button');
        var messageId = 'telephony-confirm-' + Date.now();

        bar.className = 'telephony-soft-phone__confirm';
        bar.setAttribute('data-telephony-confirm', '');
        bar.setAttribute('role', 'alertdialog');
        bar.setAttribute('aria-labelledby', messageId);
        message.className = 'telephony-soft-phone__confirm-text';
        message.id = messageId;
        message.textContent = options.message || '';
        actions.className = 'telephony-soft-phone__confirm-actions';
        cancel.type = 'button';
        cancel.className = 'btn btn-sm btn-outline-secondary';
        cancel.setAttribute('data-telephony-confirm-cancel', '');
        cancel.textContent = options.cancelLabel || 'Cancel';
        confirm.type = 'button';
        confirm.className = 'btn btn-sm btn-danger';
        confirm.setAttribute('data-telephony-confirm-accept', '');
        confirm.textContent = options.confirmLabel || 'OK';
        actions.appendChild(cancel);
        actions.appendChild(confirm);
        bar.appendChild(message);
        bar.appendChild(actions);

        return new Promise(function (resolve) {
            function finish(answer) {
                if (bar.parentNode) {
                    bar.parentNode.removeChild(bar);
                }

                resolve(answer);
            }

            bar.__telephonyCancel = function () {
                finish(false);
            };

            cancel.addEventListener('click', function () {
                finish(false);
            });
            confirm.addEventListener('click', function () {
                finish(true);
            });
            bar.addEventListener('keydown', function (event) {
                if (event.key === 'Escape') {
                    event.preventDefault();
                    finish(false);
                }
            });

            if (options.before && options.before.parentNode === host) {
                host.insertBefore(bar, options.before);
            } else {
                host.appendChild(bar);
            }

            confirm.focus();
        });
    }

    softPhone.showInAppConfirm = showInAppConfirm;
}(typeof globalThis !== 'undefined' ? globalThis : window));
