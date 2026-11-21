// Before an attempt is submitted, the quiz player asks in the site's own modal, not the browser's confirm(), and says
// how many questions are still unanswered.

// Whether a question's fieldset holds an answer: a ticked choice, a typed text or number, or a chosen option.
export function isAnswered(fields) {
    return fields.some(field => {
        if (field.type === 'radio' || field.type === 'checkbox') {
            return field.checked;
        }

        if (field.type === 'hidden' || field.type === 'button' || field.type === 'submit') {
            return false;
        }

        return typeof field.value === 'string' && field.value.trim() !== '';
    });
}

// The sentence of the modal, from the localized texts: "1 question is not answered.", "{0} questions are not
// answered." or "You answered every question.".
export function unansweredText(count, texts) {
    if (count <= 0) {
        return texts.allAnswered;
    }

    return count === 1 ? texts.one : texts.many.replace('{0}', String(count));
}

// The questions of the page that can take an answer and have none yet. A question checked with immediate feedback is
// locked and answered.
export function countUnanswered(form) {
    let count = 0;

    form.querySelectorAll('[data-question]').forEach(fieldset => {
        if (fieldset.dataset.locked === 'true') {
            return;
        }

        const fields = [...fieldset.querySelectorAll('input, select, textarea')].filter(field => field.name);

        if (fields.length > 0 && !isAnswered(fields)) {
            count++;
        }
    });

    return count;
}

export function initSubmitConfirm(form) {
    const button = form.querySelector('[data-learning-submit]');
    const dialog = document.querySelector('[data-learning-submit-confirm]');

    if (!button || !dialog || !window.bootstrap?.Modal) {
        return;
    }

    const modal = window.bootstrap.Modal.getOrCreateInstance(dialog);
    let confirmed = false;

    button.addEventListener('click', event => {
        if (confirmed) {
            return;
        }

        event.preventDefault();

        const count = countUnanswered(form) + Number(button.dataset.unansweredElsewhere || 0);

        dialog.querySelector('[data-learning-unanswered-text]').textContent = unansweredText(count, {
            one: dialog.dataset.unansweredOne,
            many: dialog.dataset.unansweredMany,
            allAnswered: dialog.dataset.allAnswered,
        });
        dialog.querySelector('[data-learning-unanswered-text]').classList.toggle('text-danger', count > 0);
        modal.show();
    });

    dialog.querySelector('[data-learning-submit-confirmed]')?.addEventListener('click', () => {
        confirmed = true;
        modal.hide();
        form.requestSubmit(button);
    });
}
