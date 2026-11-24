// @ts-check
// What a question bank can supply to a quiz's "Draw at random" rules: the bank's tags, and how many of its questions
// a rule with a tag and a difficulty can draw. The server sends one summary per bank, { questions: [{ tags,
// difficulty }] }, and the editor works the rest out here. A tag matches whatever its case, as when an attempt draws.

/**
 * @typedef {{ tags?: string[], difficulty?: number }} QuestionSummary
 */

/**
 * The bank's tags, each once whatever its case (the first spelling wins), in alphabetical order.
 * @param {QuestionSummary[]} questions
 * @returns {string[]}
 */
export function bankTags(questions) {
    const tags = new Map();

    for (const question of questions ?? []) {
        for (const tag of question?.tags ?? []) {
            const text = (tag ?? '').trim();
            const key = text.toLocaleLowerCase();

            if (text && !tags.has(key)) {
                tags.set(key, text);
            }
        }
    }

    return [...tags.values()].sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
}

/**
 * How many questions of the bank a rule can draw: those with the tag (any when empty) and the difficulty (any when
 * empty).
 * @param {QuestionSummary[]} questions
 * @param {string | null | undefined} tag
 * @param {string | number | null | undefined} difficulty
 * @returns {number}
 */
export function availableCount(questions, tag, difficulty) {
    const wantedTag = (tag ?? '').trim().toLocaleLowerCase();
    const wantedDifficulty = difficulty === '' || difficulty === null || difficulty === undefined ? null : Number(difficulty);

    return (questions ?? []).filter(question =>
        (!wantedTag || (question?.tags ?? []).some(candidate => (candidate ?? '').trim().toLocaleLowerCase() === wantedTag))
        && (wantedDifficulty === null || question?.difficulty === wantedDifficulty)).length;
}

/**
 * Renames the fields of the rule rows so their indexes run from 0 without a gap, which model binding needs after a row
 * is added or removed. A name is "<prefix>[<index>].<field>"; other names are left alone.
 * @param {Element[]} rows
 * @param {string} prefix
 */
export function renumber(rows, prefix) {
    rows.forEach((row, index) => {
        row.querySelectorAll('[name]').forEach(field => {
            const name = field.getAttribute('name') ?? '';

            if (name.startsWith(prefix + '[')) {
                field.setAttribute('name', name.replace(/^([^[]+)\[[^\]]*\]/, `$1[${index}]`));
            }
        });
    });
}
