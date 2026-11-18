// Asks before a submit button marked data-confirm-submit posts its form, using the button's data-title, data-message,
// data-ok-text, data-cancel-text, data-ok-class and data-cancel-class in the admin theme's confirmDialog.
// The admin theme's own data-url-af="RemoveUrl" confirmation submits the closest form without the button, so it loses
// the button's formaction and its form="..." attribute and skips the form's validation. requestSubmit(button) keeps all three.
(function () {
    'use strict';

    document.addEventListener('click', (event) => {
        const button = event.target.closest('button[data-confirm-submit]');

        if (!button || !button.form || button.disabled || button.dataset.confirmed === 'true' || typeof window.confirmDialog !== 'function') {
            return;
        }

        event.preventDefault();

        window.confirmDialog({
            ...button.dataset,
            callback: (confirmed) => {
                if (!confirmed) {
                    return;
                }

                button.dataset.confirmed = 'true';

                try {
                    button.form.requestSubmit(button);
                } finally {
                    delete button.dataset.confirmed;
                }
            },
        });
    });
})();
