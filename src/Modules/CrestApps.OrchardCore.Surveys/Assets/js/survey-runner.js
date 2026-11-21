// @ts-check

// The survey runner: it shows and hides questions and choices as answers change (plain rules in the browser, the rest
// from the server), saves the page as the respondent works, shows the time left, checks answers with immediate
// feedback, and keeps a page from being submitted twice. The server decides everything again when the page is posted,
// and the page works without this script.

import { debounce, evaluateRules, formatTimeLeft, readAnswers } from './survey-runner-logic.js';

const autosaveDelay = 10000;
const evaluateDelay = 300;

/**
 * @param {HTMLFormElement} form
 * @returns {string}
 */
function antiforgery(form) {
    return /** @type {HTMLInputElement | null} */ (form.querySelector('input[name="__RequestVerificationToken"]'))?.value ?? '';
}

/**
 * @param {HTMLFormElement} form
 * @param {string} url
 * @returns {Promise<any>}
 */
async function post(form, url) {
    const body = new FormData(form);
    body.delete('direction');

    const response = await fetch(url, {
        method: 'POST',
        body,
        credentials: 'same-origin',
        headers: { RequestVerificationToken: antiforgery(form) },
    });

    return response.ok ? response.json() : null;
}

/**
 * @param {HTMLFormElement} form
 * @param {{ questions: Set<string>, choices: Map<string, Set<string>> }} hidden
 */
function applyVisibility(form, hidden) {
    for (const question of form.querySelectorAll('[data-question-id]')) {
        const element = /** @type {HTMLElement} */ (question);
        const id = element.dataset.questionId ?? '';

        element.hidden = hidden.questions.has(id);

        const hiddenChoices = hidden.choices.get(id) ?? new Set();

        for (const input of element.querySelectorAll('input[type="radio"], input[type="checkbox"], option')) {
            const choice = /** @type {HTMLInputElement | HTMLOptionElement} */ (input);

            if (!choice.value) {
                continue;
            }

            const wrapper = /** @type {HTMLElement} */ (choice.closest('.form-check') ?? choice);
            wrapper.hidden = hiddenChoices.has(choice.value);
        }
    }
}

/**
 * @param {HTMLFormElement} form
 */
function initForm(form) {
    const rulesElement = form.querySelector('script[data-survey-rules]');
    const rules = rulesElement ? JSON.parse(rulesElement.textContent || '{}') : {};
    const serverRules = (form.dataset.serverRules ?? '').split(' ').filter((key) => key);
    const questionIds = new Set(Array.from(form.querySelectorAll('[data-question-id]')).map((element) => /** @type {HTMLElement} */ (element).dataset.questionId ?? ''));
    const live = document.querySelector('[data-survey-live]');
    let dirty = false;
    let submitting = false;

    const announce = (/** @type {string} */ text) => {
        if (live) {
            live.textContent = text;
        }
    };

    const evaluateLocal = () => {
        if (Object.keys(rules).length > 0) {
            applyVisibility(form, evaluateRules(rules, readAnswers(new FormData(form).entries()), questionIds));
        }
    };

    const evaluateServer = debounce(async () => {
        if (serverRules.length === 0 || !form.dataset.evaluateUrl) {
            return;
        }

        const result = await post(form, form.dataset.evaluateUrl);

        if (!result?.ok) {
            return;
        }

        const choices = new Map(Object.entries(result.hiddenChoiceIds ?? {}).map(([key, value]) => [key, new Set(/** @type {string[]} */ (value))]));
        applyVisibility(form, { questions: new Set(result.hiddenQuestionIds ?? []), choices });

        for (const [key, text] of Object.entries(result.texts ?? {})) {
            const target = form.querySelector(`[data-survey-text="${CSS.escape(key)}"]`);

            if (target) {
                target.textContent = /** @type {string} */ (text);
            }
        }
    }, evaluateDelay);

    const save = async () => {
        if (!dirty || submitting || !form.dataset.saveUrl) {
            return;
        }

        dirty = false;
        await post(form, form.dataset.saveUrl);
    };

    const autosave = debounce(save, autosaveDelay);

    const changed = () => {
        dirty = true;
        evaluateLocal();
        evaluateServer();
        autosave();
    };

    form.addEventListener('input', changed);
    form.addEventListener('change', changed);
    form.addEventListener('survey:change', changed);

    document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'hidden') {
            save();
        }
    });

    form.addEventListener('submit', (event) => {
        const submitter = /** @type {HTMLButtonElement | null} */ (/** @type {SubmitEvent} */ (event).submitter);
        const confirmMessage = form.dataset.confirmMessage;

        if (submitting) {
            event.preventDefault();

            return;
        }

        if (submitter?.value === 'finish' && confirmMessage && !window.confirm(confirmMessage)) {
            event.preventDefault();

            return;
        }

        submitting = true;
        form.querySelectorAll('button[type="submit"]').forEach((button) => button.setAttribute('aria-disabled', 'true'));
    });

    for (const button of form.querySelectorAll('[data-survey-check]')) {
        button.addEventListener('click', async () => {
            const questionId = /** @type {HTMLElement} */ (button).dataset.surveyCheck ?? '';
            const result = await post(form, (form.dataset.checkUrl ?? '') + encodeURIComponent(questionId));
            const output = form.querySelector(`[data-survey-check-result="${CSS.escape(questionId)}"]`);

            if (!output || !result) {
                return;
            }

            const parts = [];

            if (!result.ok) {
                parts.push(...(result.errors?.length ? result.errors : [result.message]));
            } else {
                if (result.feedback?.correct === true) {
                    parts.push('✓');
                } else if (result.feedback?.correct === false) {
                    parts.push('✗');
                }

                if (result.feedback?.rightAnswer) {
                    parts.push(result.feedback.rightAnswer);
                }

                if (result.feedback?.feedback) {
                    parts.push(result.feedback.feedback);
                }

                button.remove();
                form.querySelector(`[data-question-id="${CSS.escape(questionId)}"]`)?.querySelectorAll('input, select, textarea').forEach((input) => input.setAttribute('readonly', 'readonly'));
            }

            output.textContent = parts.filter((part) => part).join(' ');
            announce(output.textContent ?? '');
        });
    }

    const message = /** @type {HTMLElement | null} */ (document.querySelector('[data-survey-message]'));
    const firstError = /** @type {HTMLElement | null} */ (form.querySelector('.has-errors input, .has-errors select, .has-errors textarea'));

    if (message) {
        message.focus();
    }

    firstError?.focus();
    evaluateLocal();

    initTimer(form, announce);
}

/**
 * @param {HTMLFormElement} form
 * @param {(text: string) => void} announce
 */
function initTimer(form, announce) {
    const timer = /** @type {HTMLElement | null} */ (document.querySelector('[data-survey-timer]'));
    const output = timer?.querySelector('[data-survey-timer-value]');
    const deadline = timer?.dataset.deadlineUtc ? Date.parse(timer.dataset.deadlineUtc) : NaN;

    if (!timer || !output || Number.isNaN(deadline)) {
        return;
    }

    const warned = new Set();

    const tick = () => {
        const left = deadline - Date.now();

        output.textContent = formatTimeLeft(left);

        for (const minutes of [5, 1]) {
            if (left <= minutes * 60000 && left > 0 && !warned.has(minutes)) {
                warned.add(minutes);
                announce((form.dataset.timeWarning ?? '{0}').replace('{0}', String(minutes)));
            }
        }

        if (left <= 0) {
            announce(form.dataset.timeUp ?? '');
            const finish = /** @type {HTMLButtonElement | null} */ (form.querySelector('button[value="finish"], button[value="next"]'));

            if (finish) {
                finish.click();
            } else {
                form.submit();
            }

            return;
        }

        setTimeout(tick, 1000);
    };

    tick();
}

document.querySelectorAll('form[data-survey-form]').forEach((form) => initForm(/** @type {HTMLFormElement} */ (form)));
