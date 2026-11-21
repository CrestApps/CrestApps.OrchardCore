// @ts-check

/**
 * The browser mirror of the server's Conditions evaluator (CrestApps.OrchardCore.Conditions.ConditionEvaluator and
 * ConditionalVisibility). Scripts that show and hide questions or fields import it so the browser and the server decide
 * alike; the server still decides when the form is posted. Both run the shared vectors in
 * tests/fixtures/conditions/condition-vectors.json.
 *
 * Bundles import it:  import { isRuleMet, getHidden } from '../../../CrestApps.OrchardCore.Resources/Assets/js/conditions.js';
 * Pages without a bundle load the `crestapps-conditions` resource, which sets window.CrestAppsConditions.
 */

/**
 * @typedef {{ Source: string, Operator: string, Values?: string[] }} Condition
 * @typedef {{ Match?: string, Conditions?: Condition[], Groups?: Rule[] }} Rule
 * @typedef {{ values: string[], ancestors?: string[], withheld?: boolean }} Value
 */

const isoDate = /^\d{4}-\d{2}-\d{2}(T\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:\d{2})?)?$/;

/** A source that can't answer, such as a hidden question: every condition reading it fails. */
export const withheld = Object.freeze({ values: [], ancestors: [], withheld: true });

/** @param {unknown} left @param {unknown} right */
export function same(left, right) {
    return String(left ?? '').trim().toLowerCase() === String(right ?? '').trim().toLowerCase();
}

/** @param {unknown} value @returns {number} the number, or NaN when the text is not a plain invariant number */
export function toNumber(value) {
    const text = String(value ?? '').trim();

    return /^[-+]?(\d+(\.\d*)?|\.\d+)$/.test(text) ? Number(text) : Number.NaN;
}

/**
 * Reads an ISO 8601 date or date and time; a value without an offset is read as UTC, as the server reads it.
 * @param {unknown} value
 * @returns {number} milliseconds since the epoch, or NaN
 */
export function toDate(value) {
    const text = String(value ?? '').trim();

    if (!isoDate.test(text)) {
        return Number.NaN;
    }

    const hasTime = text.includes('T');
    const hasOffset = /(Z|[+-]\d{2}:\d{2})$/.test(text);

    return Date.parse(hasTime && !hasOffset ? text + 'Z' : text);
}

/**
 * Reads a pair of comparable values: numbers when both are numbers, else ISO dates when both are dates.
 * @param {unknown} left
 * @param {unknown} right
 * @returns {number | null} the sign of left - right, or null when they don't compare
 */
function compareValues(left, right) {
    const a = toNumber(left);
    const b = toNumber(right);

    if (!Number.isNaN(b)) {
        return Number.isNaN(a) ? null : Math.sign(a - b);
    }

    const da = toDate(left);
    const db = toDate(right);

    if (Number.isNaN(db) || Number.isNaN(da)) {
        return null;
    }

    return Math.sign(da - db);
}

/** @param {string} value */
function isBlank(value) {
    return String(value ?? '').trim() === '';
}

/** @param {unknown} value @returns {number} */
function toCount(value) {
    const text = String(value ?? '').trim();

    return /^[-+]?\d+$/.test(text) ? Number(text) : Number.NaN;
}

/**
 * Tells whether one condition is met by a source's value; the same rules as the server's ConditionEvaluator.
 * @param {Condition} condition
 * @param {Value} value
 * @returns {boolean}
 */
export function isConditionMet(condition, value) {
    if (!value || value.withheld) {
        return false;
    }

    const values = value.values ?? [];
    const ancestors = value.ancestors ?? [];
    const targets = condition.Values ?? [];
    const expected = targets.length > 0 ? targets[0] : null;
    const has = (/** @type {string} */ target) => values.some((item) => same(item, target));
    const compare = (/** @type {(c: number) => boolean} */ test) => {
        // Numbers first; a target that is not a number compares as a date.
        if (!Number.isNaN(toNumber(expected))) {
            return values.some((item) => {
                const sign = Number.isNaN(toNumber(item)) ? null : compareValues(item, expected);

                return sign !== null && test(sign);
            });
        }

        if (!Number.isNaN(toDate(expected))) {
            return values.some((item) => {
                const sign = compareValues(item, expected);

                return sign !== null && test(sign);
            });
        }

        return false;
    };
    const lower = (/** @type {string | null} */ text) => String(text ?? '').toLowerCase();

    switch (condition.Operator) {
        case 'Is': return expected !== null && has(expected);
        case 'IsNot': return expected === null || !has(expected);
        case 'IsOneOf': return targets.some(has);
        case 'IsNoneOf': return !targets.some(has);
        case 'IsEmpty': return !values.some((item) => !isBlank(item));
        case 'IsNotEmpty': return values.some((item) => !isBlank(item));
        case 'Contains': return !!expected && values.some((item) => lower(item).includes(lower(expected)));
        case 'GreaterThan': return compare((c) => c > 0);
        case 'LessThan': return compare((c) => c < 0);
        case 'IsOrUnder': return expected !== null && (has(expected) || ancestors.some((item) => same(item, expected)));
        case 'NotContains': return !expected || !values.some((item) => lower(item).includes(lower(expected)));
        case 'StartsWith': return !isBlank(expected ?? '') && values.some((item) => lower(item).trim().startsWith(lower(expected).trim()));
        case 'EndsWith': return !isBlank(expected ?? '') && values.some((item) => lower(item).trim().endsWith(lower(expected).trim()));
        case 'GreaterThanOrEqual': return compare((c) => c >= 0);
        case 'LessThanOrEqual': return compare((c) => c <= 0);
        case 'Between': return targets.length >= 2 && isBetween(values, targets[0], targets[1]);
        case 'CountAtLeast': {
            const least = toCount(expected);

            return !Number.isNaN(least) && values.filter((item) => !isBlank(item)).length >= least;
        }
        case 'CountAtMost': {
            const most = toCount(expected);

            return !Number.isNaN(most) && values.filter((item) => !isBlank(item)).length <= most;
        }
        default: return false;
    }
}

/**
 * Both bounds included; they may come in either order.
 * @param {string[]} values
 * @param {string} first
 * @param {string} second
 */
function isBetween(values, first, second) {
    let low = toNumber(first);
    let high = toNumber(second);

    if (!Number.isNaN(low) && !Number.isNaN(high)) {
        if (low > high) {
            [low, high] = [high, low];
        }

        return values.some((item) => {
            const number = toNumber(item);

            return !Number.isNaN(number) && number >= low && number <= high;
        });
    }

    let from = toDate(first);
    let to = toDate(second);

    if (!Number.isNaN(from) && !Number.isNaN(to)) {
        if (from > to) {
            [from, to] = [to, from];
        }

        return values.some((item) => {
            const date = toDate(item);

            return !Number.isNaN(date) && date >= from && date <= to;
        });
    }

    return false;
}

/** @param {Rule | null | undefined} rule @returns {boolean} */
export function isRuleEmpty(rule) {
    if (!rule) {
        return true;
    }

    if ((rule.Conditions ?? []).some((condition) => condition && !isBlank(condition.Source))) {
        return false;
    }

    return (rule.Groups ?? []).every(isRuleEmpty);
}

/**
 * Judges a rule: null when nothing in it can be judged, so a parent rule leaves it out.
 * @param {Rule | null | undefined} rule
 * @param {(source: string) => Value | null | undefined} lookup
 * @returns {boolean | null}
 */
function judge(rule, lookup) {
    if (!rule || isRuleEmpty(rule)) {
        return null;
    }

    const any = rule.Match === 'Any';
    let judged = 0;
    let met = 0;

    for (const condition of rule.Conditions ?? []) {
        if (!condition || isBlank(condition.Source)) {
            continue;
        }

        const value = lookup(condition.Source);

        if (!value) {
            continue;
        }

        judged++;

        if (isConditionMet(condition, value)) {
            met++;

            if (any) {
                return true;
            }
        } else if (!any) {
            return false;
        }
    }

    for (const group of rule.Groups ?? []) {
        const result = judge(group, lookup);

        if (result === null) {
            continue;
        }

        judged++;

        if (result) {
            met++;

            if (any) {
                return true;
            }
        } else if (!any) {
            return false;
        }
    }

    if (judged === 0) {
        return null;
    }

    return any ? met > 0 : met === judged;
}

/**
 * Tells whether a rule is met. A condition on a source the lookup doesn't know (it returns null) is left out, and a
 * rule with nothing left to judge applies. Match (All or Any) applies over the conditions and the nested groups.
 * @param {Rule | null | undefined} rule
 * @param {(source: string) => Value | null | undefined} lookup
 * @returns {boolean}
 */
export function isRuleMet(rule, lookup) {
    const result = judge(rule, lookup);

    return result === null ? true : result;
}

/**
 * The sources a rule reads, in its conditions and its nested groups, without repeats.
 * @param {Rule | null | undefined} rule
 * @returns {string[]}
 */
export function ruleSources(rule) {
    /** @type {string[]} */
    const found = [];
    const walk = (/** @type {Rule | null | undefined} */ current) => {
        if (!current) {
            return;
        }

        for (const condition of current.Conditions ?? []) {
            if (condition && !isBlank(condition.Source) && !found.includes(condition.Source)) {
                found.push(condition.Source);
            }
        }

        for (const group of current.Groups ?? []) {
            walk(group);
        }
    };

    walk(rule);

    return found;
}

/**
 * The items that can reach themselves through the sources of their rules.
 * @param {Record<string, Rule>} rules
 * @returns {Set<string>}
 */
export function getCyclic(rules) {
    const has = (/** @type {string} */ key) => Object.prototype.hasOwnProperty.call(rules, key);
    const sources = (/** @type {string} */ key) => ruleSources(rules[key]).filter(has);
    const cyclic = new Set();

    for (const start of Object.keys(rules)) {
        const seen = new Set();
        const pending = sources(start);

        while (pending.length > 0) {
            const current = /** @type {string} */ (pending.pop());

            if (current === start) {
                cyclic.add(start);
                break;
            }

            if (!seen.has(current)) {
                seen.add(current);
                pending.push(...sources(current));
            }
        }
    }

    return cyclic;
}

/**
 * Works out the hidden items: an item shows when its rule is met, and a condition reading a hidden item fails, so a
 * chain closes when an early answer changes. Items caught in a cycle show.
 * @param {Record<string, Rule>} rules the rule of each item, by the key conditions use as source
 * @param {(source: string) => Value | null | undefined} lookup
 * @returns {Set<string>}
 */
export function getHidden(rules, lookup) {
    /** @type {Map<string, boolean>} */
    const shown = new Map();
    const visiting = new Set();
    const cyclic = getCyclic(rules);

    /** @param {string} key @returns {boolean} */
    const shows = (key) => {
        if (shown.has(key)) {
            return /** @type {boolean} */ (shown.get(key));
        }

        const rule = rules[key];

        if (!rule || isRuleEmpty(rule) || cyclic.has(key) || visiting.has(key)) {
            return true;
        }

        visiting.add(key);

        const result = isRuleMet(rule, (source) => {
            const value = lookup(source);

            if (!value) {
                return null;
            }

            return Object.prototype.hasOwnProperty.call(rules, source) && !shows(source) ? withheld : value;
        });

        visiting.delete(key);
        shown.set(key, result);

        return result;
    };

    const hidden = new Set();

    for (const key of Object.keys(rules)) {
        if (!shows(key)) {
            hidden.add(key);
        }
    }

    return hidden;
}
