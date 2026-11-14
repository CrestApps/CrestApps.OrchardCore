import { escapeHtml } from './html.js';

function hostWindow() {
    return typeof window !== 'undefined' ? window : {};
}

function hasValue(value) {
    return value !== undefined && value !== null && value !== '';
}

// The admin dialog merges what it is given over the theme's own defaults ({ ...defaults, ...options }), so an option
// that is passed as undefined replaces the default and the dialog reads "undefined". Only what has a value is passed.
function dialogOptions(title, message, options, callback) {
    const dialog = { callback };

    if (hasValue(title)) {
        dialog.title = escapeHtml(title);
    }

    if (hasValue(message)) {
        dialog.message = escapeHtml(message);
    }

    if (hasValue(options.okText)) {
        dialog.okText = options.okText;
    }

    if (hasValue(options.cancelText)) {
        dialog.cancelText = options.cancelText;
    }

    return dialog;
}

// Asks a question before something that cannot be taken back. In the admin the question is Orchard Core's own dialog
// (the confirmDialog of TheAdmin); elsewhere it is the browser's confirm. Resolves with true when the person agrees.
// The admin dialog writes its title and message into the page as markup, so they are escaped first: a question that
// names a record (a subscription's title) shows that name as text.
export function confirmAction(title, message, options = {}) {
    const host = hostWindow();

    if (typeof host.confirmDialog === 'function') {
        return new Promise(resolve => {
            host.confirmDialog(dialogOptions(title, message, options, result => resolve(Boolean(result))));
        });
    }

    return Promise.resolve(typeof host.confirm === 'function' ? Boolean(host.confirm(message || title || '')) : true);
}

// The admin theme asks before a [data-url-af~="RemoveUrl"] link or button, with the element's data-title and
// data-message, and writes both into its dialog as markup. A question that names a record (a child tenant's name) is
// written in data-confirm instead, with data-confirm-title when the title names one too, and is handed to the theme's
// dialog escaped, just before the theme's own click listener reads it, so the name shows as text. Without this script
// the theme asks its default question, which names nothing.
export function bindRemoveLinks(root) {
    root.addEventListener('click', event => {
        const element = event.target && typeof event.target.closest === 'function'
            ? event.target.closest('[data-url-af~="RemoveUrl"][data-confirm]')
            : null;

        if (!element) {
            return;
        }

        element.dataset.message = escapeHtml(element.dataset.confirm);

        if (hasValue(element.dataset.confirmTitle)) {
            element.dataset.title = escapeHtml(element.dataset.confirmTitle);
        }
    }, true);
}

const submitButtons = 'button[type="submit"], input[type="submit"], button:not([type])';

// The form whose next submit was already agreed to. Set just before the submit this script asks the browser for and
// cleared as soon as that call returns, whether or not the submit happened (an invalid field stops it before the submit
// event), so an agreement is never left behind for a later click.
let agreedForm = null;

// The element that carries the question: the submit button when it has its own (a button with a formaction, or one that
// submits another form through form="..."), otherwise the form.
function questionOf(form, submitter) {
    if (submitter && typeof submitter.matches === 'function' && submitter.matches('[data-confirm]')) {
        return submitter;
    }

    return form.matches('form[data-confirm]') ? form : null;
}

function submitAgreed(form, submitter) {
    agreedForm = form;

    try {
        if (typeof form.requestSubmit === 'function') {
            form.requestSubmit(submitter || undefined);
        } else {
            form.submit();
        }
    } finally {
        agreedForm = null;
    }
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

        if (asking && agreedForm !== form) {
            const host = hostWindow();

            if (typeof host.confirmDialog === 'function') {
                // The admin dialog answers later, after this submit is over, so the submit is held back and asked for
                // again once the person agrees. Held back from the other submit listeners too: the admin theme's
                // no-multisubmit would mark the form as submitting now and then drop the agreed submit that follows.
                event.preventDefault();
                event.stopPropagation();

                confirmAction(asking.dataset.confirmTitle, asking.dataset.confirm, {
                    okText: asking.dataset.confirmOk,
                    cancelText: asking.dataset.confirmCancel,
                }).then(agreed => {
                    if (agreed) {
                        submitAgreed(form, submitter);
                    }
                });

                return;
            }

            // The browser's confirm answers now, so this submit is decided here and, once agreed, simply goes on. It
            // cannot be held back and asked for again: a submit asked for while the browser is still sending this one
            // is ignored, which left the form unsent after a "yes" and let the next click through without a question.
            const question = asking.dataset.confirm || asking.dataset.confirmTitle || '';

            if (typeof host.confirm === 'function' && !host.confirm(question)) {
                event.preventDefault();
                event.stopPropagation();

                return;
            }
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
