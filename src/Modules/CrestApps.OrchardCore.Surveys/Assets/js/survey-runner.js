// @ts-check

// The survey runner: it shows and hides questions and choices as answers change (plain rules in the browser, the rest
// from the server), saves the page as the respondent works, shows the time left, checks answers with immediate
// feedback, and keeps a page from being submitted twice. The server decides everything again when the page is posted,
// and the page works without this script.

import { debounce, evaluateRules, formatTimeLeft, isQuestionHidden, isRetryable, lockAttribute, readAnswers, retryDelay } from './survey-runner-logic.js';

const autosaveDelay = 10000;
const evaluateDelay = 300;
const maxAttempts = 5;

/**
 * @param {number} milliseconds
 * @returns {Promise<void>}
 */
function sleep(milliseconds) {
    return new Promise((resolve) => setTimeout(resolve, milliseconds));
}

/**
 * Sends a request, and sends it again while the server answers that it is too busy (429) or unavailable (503): such a
 * request was not handled, so sending it again is safe. Returns the last response, or null when the network failed.
 * @param {string} url
 * @param {RequestInit} init
 * @param {(waiting: boolean) => void} [onRetry] told when a retry is waiting and when it is over
 * @returns {Promise<Response | null>}
 */
async function send(url, init, onRetry) {
    for (let attempt = 1; ; attempt++) {
        /** @type {Response} */
        let response;

        try {
            response = await fetch(url, init);
        } catch {
            return null;
        }

        if (!isRetryable(response.status) || attempt >= maxAttempts) {
            onRetry?.(false);

            return response;
        }

        onRetry?.(true);
        await sleep(retryDelay(response.headers.get('Retry-After'), attempt));
    }
}

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

    // Files and drawn signatures are sent only with the page post, not with every autosave or evaluation: the server
    // keeps them only when the page is posted forward.
    for (const [name, value] of Array.from(body.entries())) {
        if (value instanceof File) {
            body.delete(name);
        }
    }

    for (const input of form.querySelectorAll('[data-survey-signature] input[type="hidden"]')) {
        body.delete(/** @type {HTMLInputElement} */ (input).name);
    }

    const response = await send(url, {
        method: 'POST',
        body,
        credentials: 'same-origin',
        headers: { RequestVerificationToken: antiforgery(form) },
    });

    return response?.ok ? response.json() : null;
}

/**
 * Puts markup the server sanitized into an element: parsed as an inert document (no script runs), then moved in.
 * @param {Element} target
 * @param {string} html
 */
function setMarkup(target, html) {
    const parsed = new DOMParser().parseFromString(html, 'text/html');

    target.replaceChildren(...Array.from(parsed.body.childNodes));
}

/**
 * Updates a piped text of the page: a title and a description are sanitized HTML, as the page first showed them; other
 * texts are plain.
 * @param {Element} target
 * @param {string} key
 * @param {string} text
 */
function setText(target, key, text) {
    if (key.startsWith('title:') || key.startsWith('description:')) {
        setMarkup(target, text);
    } else {
        target.textContent = text;
    }
}

/**
 * @param {HTMLFormElement} form
 * @param {{ questions: Set<string>, choices: Map<string, Set<string>> }} hidden
 */
function applyVisibility(form, hidden) {
    for (const question of form.querySelectorAll('[data-question-id]')) {
        const element = /** @type {HTMLElement} */ (question);
        const id = element.dataset.questionId ?? '';

        const locked = element.hasAttribute('data-survey-locked');

        element.hidden = isQuestionHidden(id, locked, element.hidden, hidden.questions);

        // The choices of a locked answer stay as the server showed them too.
        if (locked) {
            continue;
        }

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

    // Shows, on each answer that would do it, why a change can't be saved: it would skip or hide an answer already
    // checked. The server refuses such a save or page post and keeps the earlier answers.
    const showLockErrors = (/** @type {Record<string, string[]> | undefined} */ errors) => {
        let first = '';

        for (const output of form.querySelectorAll('[data-survey-lock-error]')) {
            const element = /** @type {HTMLElement} */ (output);
            const messages = errors?.[element.dataset.surveyLockError ?? ''] ?? [];
            const text = messages.join(' ');

            if (text && text !== element.textContent) {
                first ||= text;
            }

            element.textContent = text;
            element.hidden = !text;
        }

        if (first) {
            announce(first);
        }
    };

    const hasLocks = () => form.querySelector('[data-survey-locked]') !== null;

    const evaluateServer = debounce(async () => {
        // With a checked answer on the response, the server also says whether a change would skip or hide it.
        if ((serverRules.length === 0 && !hasLocks()) || !form.dataset.evaluateUrl) {
            return;
        }

        const result = await post(form, form.dataset.evaluateUrl);

        if (!result?.ok) {
            return;
        }

        showLockErrors(result.errors);

        const choices = new Map(Object.entries(result.hiddenChoiceIds ?? {}).map(([key, value]) => [key, new Set(/** @type {string[]} */ (value))]));
        applyVisibility(form, { questions: new Set(result.hiddenQuestionIds ?? []), choices });

        for (const [key, text] of Object.entries(result.texts ?? {})) {
            const target = form.querySelector(`[data-survey-text="${CSS.escape(key)}"]`);

            if (target) {
                setText(target, key, /** @type {string} */ (text ?? ''));
            }
        }
    }, evaluateDelay);

    const save = async () => {
        if (!dirty || submitting || !form.dataset.saveUrl) {
            return;
        }

        dirty = false;
        const result = await post(form, form.dataset.saveUrl);

        if (!result) {
            // The save didn't reach the server (the network, or still too busy after retries): the answers are still
            // on the page, and the next change or leaving the page saves them again.
            dirty = true;

            return;
        }

        // A refused save kept nothing: say why on the answers that caused it.
        if (!result.ok && result.errors && Object.keys(result.errors).length > 0) {
            showLockErrors(result.errors);
        }
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

        // The page is posted from here, so a post the server refuses as too busy (many respondents behind one network)
        // is sent again and the answers stay on the page; without this script the form posts as usual.
        event.preventDefault();
        submitPage(submitter);
    });

    const retryStatus = /** @type {HTMLElement | null} */ (form.querySelector('[data-survey-retry]'));

    const showRetry = (/** @type {string} */ text) => {
        if (retryStatus) {
            retryStatus.textContent = text;
            retryStatus.hidden = !text;
        }

        if (text) {
            announce(text);
        }
    };

    const submitPage = async (/** @type {HTMLButtonElement | null} */ submitter) => {
        const body = new FormData(form);

        if (submitter?.name) {
            body.set(submitter.name, submitter.value);
        }

        const response = await send(form.action, {
            method: 'POST',
            body,
            credentials: 'same-origin',
            headers: { RequestVerificationToken: antiforgery(form), 'X-Survey-Fetch': '1' },
        }, (waiting) => showRetry(waiting ? form.dataset.retryMessage ?? '' : ''));

        if (response && !isRetryable(response.status)) {
            const result = response.ok && (response.headers.get('Content-Type') ?? '').includes('json')
                ? await response.json().catch(() => null)
                : null;

            // The server says where to go: the next page, this page with its errors, or the ending. Anything else
            // (the response is gone, a form token that expired) reloads the page as the server has it now.
            window.location.assign(result?.redirect ?? form.action);

            return;
        }

        // Not sent: the answers are still on the page, and the respondent can send them again.
        submitting = false;
        form.querySelectorAll('button[type="submit"]').forEach((button) => button.removeAttribute('aria-disabled'));
        showRetry(form.dataset.retryFailed ?? '');
    };

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

                button.remove();

                const question = form.querySelector(`[data-question-id="${CSS.escape(questionId)}"]`);

                // The answer is locked now: rules no longer show or hide it here, as on the server.
                question?.setAttribute('data-survey-locked', 'true');
                // readonly does nothing on a radio, a checkbox or a drop-down, so those are disabled, as the server
                // renders a locked answer; a locked answer isn't read from the post anyway.
                question?.querySelectorAll('input, select, textarea').forEach((input) => {
                    const attribute = lockAttribute(input.tagName, input instanceof HTMLInputElement ? input.type : '');

                    if (attribute) {
                        input.setAttribute(attribute, attribute);
                    }
                });
            }

            // The feedback is the author's HTML, sanitized by the server; the rest is plain text.
            output.textContent = parts.filter((part) => part).join(' ');

            for (const html of result.ok ? [result.feedback?.feedback, result.feedback?.generalFeedback] : []) {
                if (html) {
                    const block = document.createElement('div');
                    setMarkup(block, html);
                    output.append(block);
                }
            }

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

    // Controls with scripts of their own initialize on this event, for this page and any content inserted later.
    form.dispatchEvent(new CustomEvent('survey:page-loaded', { bubbles: true, detail: { pageId: form.dataset.pageId } }));

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

/**
 * A kiosk response page left without input goes back to the landing page, which forgets the response, so the next
 * person at the device never sees the previous person's answers.
 * @param {HTMLElement} container
 */
function initKioskIdle(container) {
    const url = container.dataset.surveyKioskIdleUrl;
    const seconds = Number.parseInt(container.dataset.surveyKioskIdleSeconds ?? '', 10);

    if (!url || !Number.isFinite(seconds) || seconds <= 0) {
        return;
    }

    /** @type {ReturnType<typeof setTimeout> | undefined} */
    let timer;

    const reset = () => {
        clearTimeout(timer);
        timer = setTimeout(() => window.location.assign(url), seconds * 1000);
    };

    for (const type of ['input', 'change', 'keydown', 'pointerdown', 'scroll']) {
        document.addEventListener(type, reset, { passive: true, capture: true });
    }

    reset();
}

document.querySelectorAll('form[data-survey-form]').forEach((form) => initForm(/** @type {HTMLFormElement} */ (form)));
document.querySelectorAll('[data-survey-kiosk-idle-url]').forEach((container) => initKioskIdle(/** @type {HTMLElement} */ (container)));
