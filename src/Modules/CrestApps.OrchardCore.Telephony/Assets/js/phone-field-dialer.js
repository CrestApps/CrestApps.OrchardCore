/*
 * Enhances Phone Field editors and displays with a "dial" button when the telephony soft phone is
 * available on the page. The button reads the phone number and asks the soft phone to place the call,
 * so this script never talks to a provider directly.
 */
(function () {
    'use strict';

    function getNumber(placeholder) {
        if (placeholder.hasAttribute('data-phone-number')) {
            return (placeholder.getAttribute('data-phone-number') || '').trim();
        }

        var field = placeholder.closest('[data-phone-field]');

        if (field) {
            var e164 = field.querySelector('[data-phone-e164]');

            if (e164 && e164.value) {
                return e164.value.trim();
            }

            var tel = field.querySelector('input[type="tel"]');

            if (tel && tel.value) {
                return tel.value.trim();
            }
        }

        return '';
    }

    function createButton() {
        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-sm btn-outline-success telephony-phone-dial-btn';
        button.title = 'Call with the soft phone';
        button.innerHTML = '<i class="fa-solid fa-phone"></i>';

        return button;
    }

    function enhance(placeholder) {
        if (placeholder.__telephonyEnhanced) {
            return;
        }

        placeholder.__telephonyEnhanced = true;

        var button = createButton();

        button.addEventListener('click', function () {
            var number = getNumber(placeholder);

            if (number && window.telephonySoftPhone && typeof window.telephonySoftPhone.dial === 'function') {
                window.telephonySoftPhone.dial(number);
            }
        });

        placeholder.appendChild(button);
    }

    function enhanceAll() {
        var placeholders = document.querySelectorAll('[data-phone-dial]');

        Array.prototype.forEach.call(placeholders, enhance);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', enhanceAll);
    } else {
        enhanceAll();
    }
})();
