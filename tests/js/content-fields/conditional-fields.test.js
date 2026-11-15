import { beforeAll, describe, expect, it } from 'vitest';

// The script starts the editors on the page when it loads; a page without any is enough here.
globalThis.document = globalThis.document ?? { readyState: 'complete', querySelectorAll: () => [], addEventListener: () => {} };

let rules;

beforeAll(async () => {
    await import('../../../src/Modules/CrestApps.OrchardCore.ContentFields/Assets/js/conditional-fields.js');
    rules = globalThis.CrestAppsConditionalFields;
});

const value = (values, ancestors = []) => ({ values, ancestors });
const when = (source, operator, values = []) => ({ Conditions: [{ Source: source, Operator: operator, Values: values }] });

// The same cases as the server's ConditionEvaluatorTests, so the browser and the server agree.
describe('conditionMet', () => {
    it.each([
        ['Is', ['Yes'], ['yes'], true],
        ['Is', ['No'], ['Yes'], false],
        ['IsNot', [], ['Yes'], true],
        ['IsOneOf', ['Printer'], ['Laptop', 'Printer'], true],
        ['IsNoneOf', ['Phone'], ['Laptop', 'Printer'], true],
        ['IsEmpty', [' '], [], true],
        ['IsNotEmpty', ['x'], [], true],
        ['Contains', ['The screen is black'], ['SCREEN'], true],
        ['GreaterThan', ['12.5'], ['10'], true],
        ['GreaterThan', ['many'], ['10'], false],
        ['LessThan', ['9'], ['10'], true],
    ])('%s %j against %j is %s', (operator, actual, expected, met) => {
        expect(rules.conditionMet({ Source: 'q', Operator: operator, Values: expected }, value(actual))).toBe(met);
    });

    it('matches a sub-category under its category', () => {
        const laptop = value(['laptop'], ['hardware']);

        expect(rules.conditionMet({ Source: 'c', Operator: 'IsOrUnder', Values: ['hardware'] }, laptop)).toBe(true);
        expect(rules.conditionMet({ Source: 'c', Operator: 'Is', Values: ['hardware'] }, laptop)).toBe(false);
    });

    it('fails every operator on a hidden source', () => {
        expect(rules.conditionMet({ Source: 'q', Operator: 'IsEmpty', Values: [] }, { values: [], ancestors: [], withheld: true })).toBe(false);
    });
});

describe('ruleMet', () => {
    it('leaves out conditions on unknown sources', () => {
        const rule = { Conditions: [{ Source: 'removed', Operator: 'Is', Values: ['x'] }, { Source: 'outage', Operator: 'Is', Values: ['Yes'] }] };

        expect(rules.ruleMet(rule, (source) => (source === 'outage' ? value(['Yes']) : null))).toBe(true);
        expect(rules.ruleMet({ Conditions: [rule.Conditions[0]] }, () => null)).toBe(true);
    });

    it('needs any one condition when the rule says Any', () => {
        const rule = { Match: 'Any', Conditions: [{ Source: 'a', Operator: 'Is', Values: ['1'] }, { Source: 'b', Operator: 'Is', Values: ['1'] }] };

        expect(rules.ruleMet(rule, (source) => value([source === 'b' ? '1' : '0']))).toBe(true);
    });
});

describe('hiddenKeys', () => {
    it('closes a chain when an early answer changes', () => {
        const answers = { outage: ['No'], affected: ['Many'] };
        const hidden = rules.hiddenKeys(
            { affected: when('outage', 'Is', ['Yes']), count: when('affected', 'Is', ['Many']) },
            (source) => (source in answers || source === 'count' ? value(answers[source] ?? []) : null));

        expect([...hidden].sort()).toEqual(['affected', 'count']);
    });

    it('shows fields caught in a cycle', () => {
        const hidden = rules.hiddenKeys(
            { a: when('b', 'Is', ['x']), b: when('a', 'Is', ['x']), c: when('d', 'Is', ['x']) },
            () => value([]));

        expect([...hidden]).toEqual(['c']);
    });
});
