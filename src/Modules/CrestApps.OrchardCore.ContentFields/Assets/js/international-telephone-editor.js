(function () {
    const selector = '[data-intl-tel-input="true"]';

    function initializeInput(input) {
        if (!window.intlTelInput || input.dataset.intlTelInputInitialized === 'true') {
            return;
        }

        input.dataset.intlTelInputInitialized = 'true';

        const telephoneInput = window.intlTelInput(input, {
            containerClass: 'w-100',
            dropdownParent: document.body,
            numberDisplayFormat: 'INTERNATIONAL',
            strictMode: true
        });

        if (input.disabled) {
            telephoneInput.setDisabled(true);
        }
        else if (input.readOnly) {
            telephoneInput.setReadonly(true);
        }

        if (input.form) {
            input.form.addEventListener('submit', function () {
                if (!input.value) {
                    return;
                }

                const normalizedNumber = telephoneInput.getNumber();

                if (normalizedNumber) {
                    input.value = normalizedNumber;
                }
            });
        }
    }

    function initialize() {
        document.querySelectorAll(selector).forEach(initializeInput);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initialize, { once: true });
        return;
    }

    initialize();
})();
