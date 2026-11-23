// @ts-check

// The pure parts of the survey runner: reading a form's answers by question, evaluating the plain rules the server sent
// with the shared Conditions evaluator, and formatting the time left. survey-runner.js wires them to the page.

import { getHidden } from '../../../CrestApps.OrchardCore.Resources/Assets/js/conditions.js';

/**
 * @typedef {import('../../../CrestApps.OrchardCore.Resources/Assets/js/conditions.js').Rule} Rule
 */

const prefix = 'q.';

/**
 * Groups the values of a form by question id and row: `q.{id}` gives the question, `q.{id}.{row}` a row of it.
 * @param {Iterable<[string, FormDataEntryValue]>} entries the form entries
 * @returns {Map<string, string[]>} the values by source key (`{id}` or `{id}.{row}`)
 */
export function readAnswers(entries) {
    /** @type {Map<string, string[]>} */
    const answers = new Map();

    for (const [name, value] of entries) {
        if (!name.startsWith(prefix) || typeof value !== 'string') {
            continue;
        }

        const key = name.substring(prefix.length);

        if (key.endsWith('.other')) {
            continue;
        }

        const trimmed = value.trim();
        const values = answers.get(key) ?? [];

        if (trimmed !== '') {
            values.push(trimmed);
        }

        answers.set(key, values);
    }

    return answers;
}

/**
 * Works out which items the plain rules hide, with the answers of the page.
 * @param {Record<string, Rule>} rules the rules by key (`q:{id}`, `c:{questionId}:{choiceId}`)
 * @param {Map<string, string[]>} answers the answers by source key
 * @param {Set<string>} questionIds the ids of the questions on the page
 * @returns {{ questions: Set<string>, choices: Map<string, Set<string>> }} the hidden questions and choices
 */
export function evaluateRules(rules, answers, questionIds) {
    // The rule of a question is keyed by the question id, the source conditions use, so the shared evaluator withholds
    // the answer of a hidden question and a chain of rules closes.
    /** @type {Record<string, Rule>} */
    const keyed = {};

    for (const [key, rule] of Object.entries(rules ?? {})) {
        keyed[key.startsWith('q:') ? key.substring(2) : key] = rule;
    }

    const hiddenKeys = getHidden(keyed, (source) => {
        const questionId = source.split('.')[0];

        return questionIds.has(questionId) ? { values: answers.get(source) ?? [] } : null;
    });

    /** @type {Set<string>} */
    const questions = new Set();
    /** @type {Map<string, Set<string>>} */
    const choices = new Map();

    for (const key of hiddenKeys) {
        if (key.startsWith('c:')) {
            const [, questionId, choiceId] = key.split(':');
            const set = choices.get(questionId) ?? new Set();

            set.add(choiceId);
            choices.set(questionId, set);
        } else {
            questions.add(key);
        }
    }

    return { questions, choices };
}

/**
 * Works out whether a question shows after the rules ran. A checked (locked) answer keeps the visibility the server gave
 * it: the server refuses a change that would skip or hide it, so the page must not look as if the change had hidden it.
 * @param {string} questionId the question
 * @param {boolean} locked whether its answer is checked and locked
 * @param {boolean} hiddenNow whether it is hidden now
 * @param {Set<string>} hiddenQuestions the questions the rules hide
 * @returns {boolean} whether it is hidden
 */
export function isQuestionHidden(questionId, locked, hiddenNow, hiddenQuestions) {
    return locked ? hiddenNow : hiddenQuestions.has(questionId);
}

/**
 * Works out how the runner locks a form control once its answer is checked. `readonly` does nothing on a radio, a
 * checkbox or a drop-down, so those are disabled, as the server renders a locked answer (the server never reads a locked
 * answer from a post anyway); text-like controls become read-only, and hidden inputs stay as they are.
 * @param {string} tagName the element's tag name
 * @param {string} type the input's type, or an empty string
 * @returns {'disabled' | 'readonly' | null} the attribute to set, or null
 */
export function lockAttribute(tagName, type) {
    const tag = tagName.toUpperCase();
    const kind = (type ?? '').toLowerCase();

    if (tag === 'SELECT' || (tag === 'INPUT' && (kind === 'radio' || kind === 'checkbox'))) {
        return 'disabled';
    }

    return tag === 'INPUT' && kind === 'hidden' ? null : 'readonly';
}

/**
 * Formats the time left as m:ss, or h:mm:ss past an hour.
 * @param {number} milliseconds the time left
 * @returns {string} the text
 */
export function formatTimeLeft(milliseconds) {
    const total = Math.max(0, Math.floor(milliseconds / 1000));
    const hours = Math.floor(total / 3600);
    const minutes = Math.floor((total % 3600) / 60);
    const seconds = total % 60;
    const pad = (/** @type {number} */ value) => String(value).padStart(2, '0');

    return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${minutes}:${pad(seconds)}`;
}

/**
 * Works out how long the runner waits before it sends a request again after the server refused it as too many
 * (status 429) or unavailable (503): the server's Retry-After seconds when it sends them, else a delay that doubles
 * with each attempt, never under one second nor over thirty.
 * @param {string | null} retryAfter the Retry-After header, or null
 * @param {number} attempt the attempt that was refused, from 1
 * @returns {number} the delay in milliseconds
 */
export function retryDelay(retryAfter, attempt) {
    const seconds = Number.parseInt(retryAfter ?? '', 10);
    const delay = Number.isFinite(seconds) && seconds >= 0 ? seconds : 2 ** Math.max(attempt, 1);

    return Math.min(Math.max(delay, 1), 30) * 1000;
}

/**
 * Gets whether a response status means the request was not handled and may be sent again as it is: too many requests
 * (429) or the server is unavailable (503).
 * @param {number} status the status
 * @returns {boolean} whether to retry
 */
export function isRetryable(status) {
    return status === 429 || status === 503;
}

/**
 * Returns a debounced function that runs the last call after a quiet time.
 * @template {(...args: any[]) => void} T
 * @param {T} work the function
 * @param {number} delay the quiet time in milliseconds
 * @returns {(...args: Parameters<T>) => void} the debounced function
 */
export function debounce(work, delay) {
    /** @type {ReturnType<typeof setTimeout> | undefined} */
    let timer;

    return (...args) => {
        clearTimeout(timer);
        timer = setTimeout(() => work(...args), delay);
    };
}
