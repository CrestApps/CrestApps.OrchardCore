// @ts-check
/*
 * Small behaviors of the Surveys admin pages: list filters that apply as soon as one changes, and buttons that copy
 * a link to the clipboard.
 */

/**
 * Submits the form a filter select belongs to through its filter button, so the list keeps its other values.
 * @param {HTMLSelectElement} select The filter.
 */
function submitFilter(select) {
    const form = select.form;

    if (!form) {
        return;
    }

    const submit = /** @type {HTMLElement | null} */ (form.querySelector('[name="submit.Filter"]'));

    if (submit) {
        submit.click();
    } else {
        form.submit();
    }
}

/**
 * Copies a text and shows that it was copied on the button.
 * @param {HTMLElement} button The button.
 */
async function copy(button) {
    const text = button.dataset.surveyCopy ?? '';

    try {
        await navigator.clipboard.writeText(text);
        button.classList.add('btn-success');
        setTimeout(() => button.classList.remove('btn-success'), 1500);
    } catch {
        window.prompt('', text);
    }
}

document.addEventListener('change', event => {
    const target = /** @type {HTMLElement} */ (event.target);

    if (target instanceof HTMLSelectElement && target.matches('[data-surveys-list-filter]')) {
        submitFilter(target);
    }
});

document.addEventListener('click', event => {
    const button = /** @type {HTMLElement | null} */ (/** @type {HTMLElement} */ (event.target).closest('[data-survey-copy]'));

    if (button) {
        event.preventDefault();
        copy(button);
    }
});
