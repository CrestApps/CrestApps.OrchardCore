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
