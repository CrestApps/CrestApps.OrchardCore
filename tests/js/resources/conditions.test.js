import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import {
    getCyclic,
    getHidden,
    isConditionMet,
    isRuleEmpty,
    isRuleMet,
    ruleSources,
    withheld,
} from '../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/conditions.js';

// The same vectors the server's ConditionVectorTests run, so the browser and the server agree.
const vectors = JSON.parse(readFileSync('tests/fixtures/conditions/condition-vectors.json', 'utf8'));

const lookupFor = (sources) => (source) => {
    const value = sources?.[source];

    if (!value) {
        return null;
    }

    return value.withheld ? withheld : { values: value.values ?? [], ancestors: value.ancestors ?? [] };
};

describe('condition vectors', () => {
    it.each(vectors.rules.map((vector) => [vector.name, vector]))('%s', (_, vector) => {
        expect(isRuleMet(vector.rule, lookupFor(vector.sources))).toBe(vector.expected);
    });

    it.each(vectors.hidden.map((vector) => [vector.name, vector]))('%s', (_, vector) => {
        const hidden = [...getHidden(vector.rules, lookupFor(vector.sources))].sort();

        expect(hidden).toEqual([...vector.hidden].sort());
    });
});

describe('isConditionMet', () => {
    it('fails every operator on a withheld source', () => {
        for (const operator of ['Is', 'IsNot', 'IsEmpty', 'NotContains', 'CountAtMost', 'Between']) {
            expect(isConditionMet({ Source: 'q', Operator: operator, Values: ['1', '2'] }, withheld)).toBe(false);
        }
    });

    it('fails an unknown operator', () => {
        expect(isConditionMet({ Source: 'q', Operator: 'Nope', Values: ['x'] }, { values: ['x'] })).toBe(false);
    });
});

describe('rules', () => {
    it('treats a rule with only empty groups as empty', () => {
        expect(isRuleEmpty({ Groups: [{}, { Conditions: [{ Source: ' ', Operator: 'Is' }] }] })).toBe(true);
        expect(isRuleEmpty({ Groups: [{ Conditions: [{ Source: 'q', Operator: 'IsNotEmpty' }] }] })).toBe(false);
    });

    it('lists the sources of nested groups once', () => {
        const rule = {
            Conditions: [{ Source: 'a', Operator: 'IsNotEmpty' }],
            Groups: [{ Conditions: [{ Source: 'b', Operator: 'Is' }], Groups: [{ Conditions: [{ Source: 'c', Operator: 'Is' }, { Source: 'a', Operator: 'Is' }] }] }],
        };

        expect(ruleSources(rule)).toEqual(['a', 'b', 'c']);
    });

    it('finds a cycle through a nested group', () => {
        const rules = {
            a: { Groups: [{ Conditions: [{ Source: 'b', Operator: 'Is', Values: ['x'] }] }] },
            b: { Conditions: [{ Source: 'a', Operator: 'Is', Values: ['x'] }] },
        };

        expect([...getCyclic(rules)].sort()).toEqual(['a', 'b']);
    });

    it('covers every operator the vectors name', () => {
        const operators = new Set();
        const walk = (rule) => {
            (rule.Conditions ?? []).forEach((condition) => operators.add(condition.Operator));
            (rule.Groups ?? []).forEach(walk);
        };

        vectors.rules.forEach((vector) => walk(vector.rule));

        for (const operator of operators) {
            expect(isConditionMet({ Source: 'q', Operator: operator, Values: ['1', '2'] }, { values: ['1'] })).toBeTypeOf('boolean');
        }

        expect(operators.size).toBe(18);
    });
});
