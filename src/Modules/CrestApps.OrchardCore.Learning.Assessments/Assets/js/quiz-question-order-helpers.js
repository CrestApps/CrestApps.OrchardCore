// @ts-check
// The order of a quiz's fixed questions: the order the author set for the questions still chosen, then the questions
// picked since, in the order they were picked.

/**
 * Keeps the order set for the questions still chosen and adds the new ones last.
 * @param {string[] | null | undefined} order The order set so far.
 * @param {string[] | null | undefined} chosen The questions chosen now, in the picker's order.
 * @returns {string[]}
 */
export function mergeOrder(order, chosen) {
    const selected = new Set(chosen ?? []);
    const kept = (order ?? []).filter(id => selected.has(id));
    const known = new Set(kept);

    return [...kept, ...(chosen ?? []).filter(id => !known.has(id))];
}

/**
 * Moves a question up (delta -1) or down (delta 1); a move past either end changes nothing.
 * @param {string[]} order The order.
 * @param {number} index The position of the question.
 * @param {number} delta The move.
 * @returns {string[]}
 */
export function move(order, index, delta) {
    const target = index + delta;
    const result = [...order];

    if (index < 0 || index >= result.length || target < 0 || target >= result.length) {
        return result;
    }

    [result[index], result[target]] = [result[target], result[index]];

    return result;
}
