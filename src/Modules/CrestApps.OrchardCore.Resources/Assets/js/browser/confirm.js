import { escapeHtml } from './html.js';

// Asks a question before something that cannot be taken back. In the admin the question is Orchard Core's own dialog
// (the confirmDialog of TheAdmin); elsewhere it is the browser's confirm. Resolves with true when the person agrees.
// The admin dialog writes its title and message into the page as markup, so they are escaped first: a question that
// names a record (a subscription's title) shows that name as text.
export function confirmAction(title, message, options = {}) {
    const host = typeof window !== 'undefined' ? window : {};

    if (typeof host.confirmDialog === 'function') {
        return new Promise(resolve => {
            host.confirmDialog({
                title: escapeHtml(title),
                message: escapeHtml(message),
                okText: options.okText,
                cancelText: options.cancelText,
                callback: result => resolve(Boolean(result)),
            });
        });
    }

    return Promise.resolve(typeof host.confirm === 'function' ? Boolean(host.confirm(message || title || '')) : true);
}

const submitButtons = 'button[type="submit"], input[type="submit"], button:not([type])';
const confirmed = new WeakSet();

// The element that carries the question: the submit button when it has its own (a button with a formaction, or one that
// submits another form through form="..."), otherwise the form.
function questionOf(form, submitter) {
    if (submitter && typeof submitter.matches === 'function' && submitter.matches('[data-confirm]')) {
        return submitter;
    }

    return form.matches('form[data-confirm]') ? form : null;
}

// One delegated listener for every form on the page:
// - <form data-confirm="Question?">, or <button type="submit" data-confirm="Question?">, asks first (data-confirm-title,
//   data-confirm-ok and data-confirm-cancel are optional) and submits with the same button once the person agrees;
// - <form data-once> disables its submit buttons after the first submit, so a double click sends it once.
export function bindForms(root) {
    root.addEventListener('submit', event => {
        const form = event.target;

        if (!form || typeof form.matches !== 'function') {
            return;
        }

        const submitter = event.submitter;
        const asking = questionOf(form, submitter);

        if (asking) {
            if (!confirmed.has(form)) {
                // Held back from the other submit listeners too: the admin theme's no-multisubmit would mark the form
                // as submitting now and then drop the confirmed submit that follows.
                event.preventDefault();
                event.stopPropagation();

                confirmAction(asking.dataset.confirmTitle, asking.dataset.confirm, {
                    okText: asking.dataset.confirmOk,
                    cancelText: asking.dataset.confirmCancel,
                }).then(agreed => {
                    if (!agreed) {
                        return;
                    }

                    confirmed.add(form);

                    if (typeof form.requestSubmit === 'function') {
                        form.requestSubmit(submitter || undefined);
                    } else {
                        form.submit();
                    }
                });

                return;
            }

            confirmed.delete(form);
        }

        if (form.matches('form[data-once]')) {
            // After the submit event: a button disabled during it would leave its name and value out of the post.
            setTimeout(() => {
                if (event.defaultPrevented) {
                    return;
                }

                form.querySelectorAll(submitButtons).forEach(button => {
                    button.disabled = true;
                });
            }, 0);
        }
    }, true);
}
