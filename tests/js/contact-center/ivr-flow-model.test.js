import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/ivr-menu/ivr-flow-model.js';

const ivr = globalThis.CrestAppsIvrMenu;

// The same fixture the server-side contract test binds (IvrMenuEditorJsonContractTests), so the JSON the editor
// writes is the JSON the server reads.
const fixtureText = readFileSync('tests/fixtures/ivr-menu/all-action-kinds.json', 'utf8');
const fixture = JSON.parse(fixtureText);

function codes(issues) {
    return issues.map(entry => entry.code);
}

function flowWith(options, extra = {}) {
    return {
        RootNodeId: 'main',
        MaxRetries: 3,
        FallbackAction: null,
        Nodes: [{ NodeId: 'main', Prompt: 'Hello', PromptMediaId: null, Options: options }],
        ...extra,
    };
}

describe('JSON and the editor model', () => {
    it('round-trips a flow that uses every action kind', () => {
        const model = ivr.fromFlow(fixture);

        expect(ivr.toFlow(model)).toEqual(fixture);
    });

    it('writes the same indented JSON it reads', () => {
        expect(ivr.toJson(ivr.parseJson(fixtureText).model)).toBe(fixtureText.trim().replace(/\r\n/g, '\n'));
    });

    it('covers every kind the server knows', () => {
        const kinds = new Set();

        fixture.Nodes.forEach(node => node.Options.forEach(option => kinds.add(option.Action.Kind)));

        expect([...kinds].sort()).toEqual([...ivr.ACTION_KINDS].sort());
        expect([...ivr.ENUM_ORDER].sort()).toEqual([...ivr.ACTION_KINDS].sort());
    });

    it.each(ivr.ACTION_KINDS)('round-trips a %s key', kind => {
        const target = ivr.needsTarget(kind) ? (kind === 'SubMenu' ? 'main' : 'target-1') : null;
        const flow = flowWith([{ Digit: '1', Action: { Kind: kind, TargetId: target } }]);

        expect(ivr.toFlow(ivr.fromFlow(flow))).toEqual(flow);
    });

    it.each(ivr.ACTION_KINDS)('round-trips a %s fallback', kind => {
        const target = ivr.needsTarget(kind) ? (kind === 'SubMenu' ? 'main' : 'target-1') : null;
        const flow = flowWith([{ Digit: '1', Action: { Kind: 'Repeat', TargetId: null } }], { FallbackAction: { Kind: kind, TargetId: target } });

        expect(ivr.toFlow(ivr.fromFlow(flow))).toEqual(flow);
    });

    it('reads kinds written as numbers in the server enum order', () => {
        const flow = flowWith(ivr.ENUM_ORDER.map((kind, index) => ({ Digit: String(index + 1), Action: { Kind: index, TargetId: null } })));
        const model = ivr.fromFlow(flow);

        expect(model.nodes[0].options.map(option => option.action.kind)).toEqual(ivr.ENUM_ORDER);
    });

    it('reads property names and kind names without regard to case, as the server does', () => {
        const model = ivr.fromFlow({
            rootNodeId: 'main',
            maxRetries: 2,
            nodes: [{ nodeId: 'main', prompt: 'Hi', options: [{ digit: '1', action: { kind: 'submenu', targetId: 'main' } }] }],
        });

        expect(model.rootNodeId).toBe('main');
        expect(model.maxRetries).toBe(2);
        expect(model.nodes[0].options[0].action).toEqual({ kind: 'SubMenu', targetId: 'main' });
    });

    it('takes the server defaults for what a flow leaves out', () => {
        const model = ivr.fromFlow({ RootNodeId: 'main', Nodes: [{ NodeId: 'main', Prompt: 'Hi', Options: [{ Digit: '1', Action: {} }] }] });

        expect(model.maxRetries).toBe(ivr.DEFAULT_MAX_RETRIES);
        expect(model.fallback).toBeNull();
        expect(model.nodes[0].options[0].action.kind).toBe('RouteToQueue');
    });

    it('keeps an option with no action as one, so the server can refuse it', () => {
        const flow = flowWith([{ Digit: '1', Action: null }]);

        expect(ivr.toFlow(ivr.fromFlow(flow))).toEqual(flow);
    });

    it('keeps a kind it does not know as written', () => {
        const model = ivr.fromFlow(flowWith([{ Digit: '1', Action: { Kind: 'Teleport', TargetId: 'x' } }]));

        expect(ivr.toFlow(model).Nodes[0].Options[0].Action).toEqual({ Kind: 'Teleport', TargetId: 'x' });
    });

    it('skips null menus and options the way the server does', () => {
        const model = ivr.fromFlow({ RootNodeId: 'main', Nodes: [null, { NodeId: 'main', Prompt: 'Hi', Options: [null, { Digit: '1', Action: { Kind: 'Repeat' } }] }] });

        expect(model.nodes).toHaveLength(1);
        expect(model.nodes[0].options).toHaveLength(1);
    });

    it('trims identifiers and keys but keeps the spoken prompt as typed', () => {
        const model = ivr.createModel();
        const node = ivr.addNode(model, ' main ');

        node.prompt = '  Press 1.  ';
        node.options[0].action = { kind: 'RouteToQueue', targetId: ' q-1 ' };
        model.rootNodeId = ' main ';

        const flow = ivr.toFlow(model);

        expect(flow.RootNodeId).toBe('main');
        expect(flow.Nodes[0].NodeId).toBe('main');
        expect(flow.Nodes[0].Prompt).toBe('  Press 1.  ');
        expect(flow.Nodes[0].Options[0].Action.TargetId).toBe('q-1');
    });

    it('writes an empty prompt and media id as null', () => {
        const model = ivr.createModel();
        const node = ivr.addNode(model);

        node.prompt = '   ';

        expect(ivr.toFlow(model).Nodes[0].Prompt).toBeNull();
        expect(ivr.toFlow(model).Nodes[0].PromptMediaId).toBeNull();
    });
});

// Empty must keep meaning "no IVR: route straight to the entry point's target".
describe('no menu', () => {
    it('reads an empty field as a model with no menus', () => {
        for (const value of ['', '   ', null, undefined]) {
            const result = ivr.parseJson(value);

            expect(result.ok).toBe(true);
            expect(result.model.nodes).toEqual([]);
        }
    });

    it('writes a model with no menus as an empty field', () => {
        expect(ivr.toJson(ivr.createModel())).toBe('');
        expect(ivr.toFlow(ivr.createModel())).toBeNull();
    });

    it('writes an empty field once the last menu is removed', () => {
        const model = ivr.fromFlow(flowWith([{ Digit: '1', Action: { Kind: 'Repeat', TargetId: null } }]));

        ivr.removeNode(model, 0);

        expect(ivr.toJson(model)).toBe('');
        expect(model.rootNodeId).toBe('');
    });

    it('has nothing to report', () => {
        expect(ivr.validate(ivr.createModel())).toEqual([]);
    });
});

describe('parseJson', () => {
    it('reports JSON it cannot read', () => {
        const result = ivr.parseJson('{ "RootNodeId": ');

        expect(result.ok).toBe(false);
        expect(result.error).toBeTruthy();
    });

    it('refuses JSON that is not an object', () => {
        expect(ivr.parseJson('[]')).toEqual({ ok: false, error: 'notAnObject' });
        expect(ivr.parseJson('"text"')).toEqual({ ok: false, error: 'notAnObject' });
    });
});

describe('validate', () => {
    const catalog = {
        queue: [{ value: 'queue-sales', text: 'Sales' }, { value: 'queue-support', text: 'Support' }, { value: 'queue-general', text: 'General' }],
        agent: [{ value: 'agent-front-desk', text: 'Front desk' }],
        external: [{ value: 'destination-billing', text: 'Billing' }],
    };

    it('accepts the fixture', () => {
        expect(ivr.validate(ivr.fromFlow(fixture), catalog)).toEqual([]);
    });

    it('needs a root menu that exists', () => {
        const missing = ivr.fromFlow({ ...fixture, RootNodeId: '' });
        const unknown = ivr.fromFlow({ ...fixture, RootNodeId: 'nowhere' });

        expect(codes(ivr.validate(missing))).toContain('rootMissing');
        expect(ivr.validate(unknown).find(entry => entry.code === 'rootNotFound')).toMatchObject({ severity: 'error', path: { field: 'rootNodeId' }, params: { nodeId: 'nowhere' } });
    });

    it('needs at least one retry', () => {
        for (const value of [0, -1, '', null, 'abc']) {
            const model = ivr.fromFlow(fixture);

            model.maxRetries = value;

            expect(codes(ivr.validate(model))).toContain('maxRetriesInvalid');
        }
    });

    it('needs every menu named once', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[1].nodeId = 'main';

        expect(ivr.validate(model).find(entry => entry.code === 'nodeIdDuplicate')).toMatchObject({ path: { node: 1, field: 'nodeId' } });

        model.nodes[1].nodeId = ' ';

        expect(codes(ivr.validate(model))).toContain('nodeIdMissing');
    });

    it('needs something for the caller to hear', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[1].promptMediaId = '';

        expect(ivr.validate(model).find(entry => entry.code === 'promptMissing')).toMatchObject({ path: { node: 1, field: 'prompt' }, params: { nodeId: 'support' } });
    });

    it('accepts a recorded prompt in place of a spoken one', () => {
        const model = ivr.fromFlow(fixture);

        expect(model.nodes[1].prompt).toBe('');
        expect(codes(ivr.validate(model))).not.toContain('promptMissing');
    });

    it('needs at least one key on a menu', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[1].options = [];

        expect(codes(ivr.validate(model))).toContain('noOptions');
    });

    it('refuses a key that is used twice on the same menu', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[0].options[1].digit = '1';

        expect(ivr.validate(model).find(entry => entry.code === 'digitDuplicate')).toMatchObject({ path: { node: 0, option: 1, field: 'digit' }, params: { digit: '1' } });
    });

    it('allows the same key on different menus', () => {
        const model = ivr.fromFlow(fixture);

        expect(model.nodes[0].options[0].digit).toBe(model.nodes[1].options[0].digit);
        expect(codes(ivr.validate(model))).not.toContain('digitDuplicate');
    });

    it('refuses a key that is not on a telephone keypad', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[0].options[0].digit = '12';
        model.nodes[0].options[1].digit = '';

        expect(codes(ivr.validate(model))).toEqual(expect.arrayContaining(['digitInvalid', 'digitMissing']));
    });

    it('refuses a key with no action', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[0].options[0].action = null;

        expect(ivr.validate(model).find(entry => entry.code === 'actionMissing')).toMatchObject({ path: { node: 0, option: 0, field: 'kind' } });
    });

    it.each(['RouteToQueue', 'RouteToAgent', 'SubMenu', 'ExternalTransfer'])('needs a target for %s', kind => {
        const model = ivr.fromFlow(fixture);

        model.nodes[0].options[0].action = { kind, targetId: '' };

        expect(ivr.validate(model).find(entry => entry.code === 'targetMissing')).toMatchObject({ path: { node: 0, option: 0, field: 'target' }, params: { kind } });
    });

    it.each(['Voicemail', 'Repeat'])('needs no target for %s', kind => {
        const model = ivr.fromFlow(fixture);

        model.nodes[0].options[0].action = { kind, targetId: '' };

        expect(codes(ivr.validate(model, catalog))).toEqual([]);
    });

    it('refuses a sub-menu that does not exist', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[0].options[1].action.targetId = 'gone';

        expect(ivr.validate(model).find(entry => entry.code === 'subMenuNotFound')).toMatchObject({ severity: 'error', params: { targetId: 'gone', nodeId: 'main', digit: '2' } });
    });

    it('checks the fallback like any other action', () => {
        const model = ivr.fromFlow(fixture);

        model.fallback = { kind: 'SubMenu', targetId: 'gone' };

        expect(ivr.validate(model).find(entry => entry.code === 'subMenuNotFound')).toMatchObject({ path: { fallback: true, field: 'target' } });

        model.fallback = { kind: 'RouteToAgent', targetId: '' };

        expect(ivr.validate(model).find(entry => entry.code === 'targetMissing')).toMatchObject({ path: { fallback: true } });
    });

    it('refuses a kind it does not know', () => {
        const model = ivr.fromFlow(flowWith([{ Digit: '1', Action: { Kind: 'Teleport', TargetId: null } }]));

        expect(codes(ivr.validate(model))).toContain('kindUnknown');
    });

    it('warns about a menu no caller can reach', () => {
        const model = ivr.fromFlow(fixture);
        const orphan = ivr.addNode(model, 'orphan');

        orphan.prompt = 'Nobody hears this.';

        const unreachable = ivr.validate(model).filter(entry => entry.code === 'unreachable');

        expect(unreachable).toEqual([expect.objectContaining({ severity: 'warning', path: { node: 2, field: 'nodeId' }, params: { nodeId: 'orphan' } })]);
    });

    it('counts a menu the fallback opens as reachable', () => {
        const model = ivr.fromFlow(fixture);

        ivr.addNode(model, 'last-chance').prompt = 'One more try.';
        model.fallback = { kind: 'SubMenu', targetId: 'last-chance' };

        expect(codes(ivr.validate(model))).not.toContain('unreachable');
    });

    it('does not call every menu unreachable when the root itself is broken', () => {
        const model = ivr.fromFlow({ ...fixture, RootNodeId: 'nowhere' });

        expect(codes(ivr.validate(model))).not.toContain('unreachable');
    });

    it('warns about a queue, agent or destination that is not in the lists', () => {
        const model = ivr.fromFlow(fixture);

        model.nodes[0].options[0].action.targetId = 'queue-deleted';

        expect(ivr.validate(model, catalog).find(entry => entry.code === 'targetUnknown')).toMatchObject({ severity: 'warning', params: { targetId: 'queue-deleted', kind: 'RouteToQueue' } });
        expect(codes(ivr.validate(model))).not.toContain('targetUnknown');
    });

    it('separates errors from warnings', () => {
        expect(ivr.hasErrors([{ severity: 'warning' }])).toBe(false);
        expect(ivr.hasErrors([{ severity: 'warning' }, { severity: 'error' }])).toBe(true);
    });
});

describe('editing helpers', () => {
    it('names the first menu "main" and then numbers the rest', () => {
        const model = ivr.createModel();

        expect(ivr.addNode(model).nodeId).toBe('main');
        expect(ivr.addNode(model).nodeId).toBe('menu-2');
        expect(ivr.addNode(model).nodeId).toBe('menu-3');
        expect(model.rootNodeId).toBe('main');
    });

    it('starts a new menu with one key', () => {
        const node = ivr.addNode(ivr.createModel());

        expect(node.options).toEqual([{ digit: '1', action: { kind: 'RouteToQueue', targetId: '' } }]);
    });

    it('offers only the keys no other option on the menu uses', () => {
        const node = ivr.fromFlow(fixture).nodes[1];

        expect(ivr.availableDigits(node, 0)).toEqual(['1', '2', '3', '4', '5', '6', '7', '8', '9', '0']);
        expect(ivr.availableDigits(node, -1)).not.toContain('1');
    });

    it('adds keys until the keypad runs out', () => {
        const node = ivr.addNode(ivr.createModel());

        for (let index = 0; index < 11; index++) {
            expect(ivr.addOption(node)).not.toBeNull();
        }

        expect(node.options.map(option => option.digit).sort()).toEqual([...ivr.TELEPHONE_KEYS].sort());
        expect(ivr.addOption(node)).toBeNull();
    });

    it('renames a menu and everything that opens it', () => {
        const model = ivr.fromFlow(fixture);

        model.fallback = { kind: 'SubMenu', targetId: 'main' };

        expect(ivr.renameNode(model, 0, 'welcome')).toBe(true);
        expect(model.rootNodeId).toBe('welcome');
        expect(model.nodes[1].options[1].action.targetId).toBe('welcome');
        expect(model.fallback.targetId).toBe('welcome');
        expect(ivr.validate(model)).toEqual([]);
    });

    it('refuses to rename a menu to an empty or taken name', () => {
        const model = ivr.fromFlow(fixture);

        expect(ivr.renameNode(model, 0, ' ')).toBe(false);
        expect(ivr.renameNode(model, 0, 'support')).toBe(false);
        expect(model.nodes[0].nodeId).toBe('main');
    });

    it('counts the keys that open a menu', () => {
        const model = ivr.fromFlow(fixture);

        expect(ivr.countReferences(model, 'support')).toBe(1);
        expect(ivr.countReferences(model, 'main')).toBe(1);
        expect(ivr.countReferences(model, 'nowhere')).toBe(0);
    });

    it('leaves keys that opened a removed menu visibly broken', () => {
        const model = ivr.fromFlow(fixture);

        ivr.removeNode(model, 1);

        expect(codes(ivr.validate(model))).toEqual(['subMenuNotFound']);
    });

    it('picks a new root when the root menu is removed', () => {
        const model = ivr.fromFlow(fixture);

        ivr.removeNode(model, 0);

        expect(model.rootNodeId).toBe('support');
    });

    it('clears the target when the kind changes', () => {
        const action = { kind: 'RouteToQueue', targetId: 'queue-sales' };

        ivr.setActionKind(action, 'RouteToQueue');
        expect(action.targetId).toBe('queue-sales');

        ivr.setActionKind(action, 'SubMenu');
        expect(action).toEqual({ kind: 'SubMenu', targetId: '' });
    });

    it('knows what each kind targets', () => {
        expect(ivr.ACTION_KINDS.map(kind => ivr.targetTypeOf(kind))).toEqual(['queue', 'agent', 'menu', null, 'external', null]);
    });
});

// The editor draws the menus the way a caller walks them: a submenu under the key that opens it. The stored flow stays a
// flat list of named menus, so these are views and edits of that list.
describe('the menus as a caller walks them', () => {
    const tree = flow => ivr.buildMenuTree(ivr.fromFlow(flow));
    const node = (id, options, prompt = 'Hi') => ({ NodeId: id, Prompt: prompt, Options: options });
    const key = (digit, kind, target) => ({ Digit: digit, Action: { Kind: kind, TargetId: target ?? null } });

    it('nests each submenu under the first key that opens it, and knows the keys that lead there', () => {
        const result = tree({
            RootNodeId: 'main',
            MaxRetries: 3,
            Nodes: [
                node('main', [key('1', 'RouteToQueue', 'q'), key('2', 'SubMenu', 'billing')]),
                node('billing', [key('1', 'SubMenu', 'refunds'), key('9', 'SubMenu', 'main')]),
                node('refunds', [key('1', 'Voicemail')]),
            ],
        });

        expect(result.rootIndex).toBe(0);
        expect(result.childOf(0, 1)).toBe(1);
        expect(result.childOf(1, 0)).toBe(2);
        expect(result.paths[0]).toEqual([]);
        expect(result.paths[1]).toEqual(['2']);
        expect(result.paths[2]).toEqual(['2', '1']);
        expect(result.unused).toEqual([]);
    });

    // Going back to the main menu, or two keys opening the same menu, is a jump to a menu already drawn, not a copy.
    it('draws a menu once: a key back to an earlier menu is a jump, not another nesting', () => {
        const result = tree({
            RootNodeId: 'main',
            MaxRetries: 3,
            Nodes: [
                node('main', [key('1', 'SubMenu', 'more'), key('2', 'SubMenu', 'more')]),
                node('more', [key('9', 'SubMenu', 'main')]),
            ],
        });

        expect(result.childOf(0, 0)).toBe(1);
        expect(result.childOf(0, 1)).toBeUndefined();
        expect(result.childOf(1, 0)).toBeUndefined();
        expect(result.indexOf('main')).toBe(0);
        expect(result.indexOf('more')).toBe(1);
    });

    it('sets aside the menus no key opens, each with its own submenus', () => {
        const result = tree({
            RootNodeId: 'main',
            MaxRetries: 3,
            Nodes: [
                node('main', [key('1', 'Voicemail')]),
                node('orphan', [key('3', 'SubMenu', 'orphan-child')]),
                node('orphan-child', [key('1', 'Repeat')]),
            ],
        });

        expect(result.unused).toEqual([1]);
        expect(result.childOf(1, 0)).toBe(2);
    });

    it('starts from the first menu when the first menu named is missing', () => {
        const result = tree({ RootNodeId: 'gone', MaxRetries: 3, Nodes: [node('a', [key('1', 'Voicemail')])] });

        expect(result.rootIndex).toBe(0);
    });

    it('adds a submenu under a key, with a name nobody has to choose', () => {
        const model = ivr.fromFlow({ RootNodeId: 'main', MaxRetries: 3, Nodes: [node('main', [key('1', 'RouteToQueue', 'q'), key('2', 'Voicemail')])] });

        const created = ivr.addSubMenu(model, 0, 1);

        expect(created.nodeId).toBeTruthy();
        expect(created.nodeId).not.toBe('main');
        expect(model.nodes[0].options[1].action).toEqual({ kind: 'SubMenu', targetId: created.nodeId });
        expect(ivr.buildMenuTree(model).childOf(0, 1)).toBe(1);
        // What is left is filling in the new menu itself: the key, the name and the link to it are all in place.
        const errors = ivr.validate(model, { queue: [{ value: 'q' }] }).filter(entry => entry.severity === 'error');
        expect(errors.length).toBeGreaterThan(0);
        expect(errors.every(entry => entry.path.node === 1)).toBe(true);
    });

    // Removing a submenu takes its own submenus with it, and a key that jumped to any of them is left to be chosen again.
    it('removes a submenu with the submenus nested under it, and clears the keys that opened them', () => {
        const model = ivr.fromFlow({
            RootNodeId: 'main',
            MaxRetries: 3,
            Nodes: [
                node('main', [key('1', 'SubMenu', 'a'), key('2', 'SubMenu', 'b')]),
                node('a', [key('1', 'SubMenu', 'a1')]),
                node('a1', [key('1', 'Voicemail')]),
                node('b', [key('1', 'SubMenu', 'a1')]),
            ],
        });

        ivr.removeSubMenu(model, 1);

        expect(model.nodes.map(entry => entry.nodeId)).toEqual(['main', 'b']);
        expect(model.nodes[0].options[0].action).toBeNull();
        expect(model.nodes[1].options[0].action).toBeNull();
        expect(model.nodes[0].options[1].action).toEqual({ kind: 'SubMenu', targetId: 'b' });
    });

    it('never removes the main menu as a submenu', () => {
        const model = ivr.fromFlow({ RootNodeId: 'main', MaxRetries: 3, Nodes: [node('main', [key('1', 'Voicemail')])] });

        ivr.removeSubMenu(model, 0);

        expect(model.nodes).toHaveLength(1);
    });
});

// Pasted or typed JSON reaches the visual editor only when it is a menu the entry point could save: it must parse, and
// pass every check. Warnings (a menu no key opens) do not stop it.
describe('JSON pasted into the editor', () => {
    const catalog = { queue: [{ value: 'q' }], agent: [], external: [] };
    const valid = JSON.stringify({
        RootNodeId: 'main',
        MaxRetries: 3,
        Nodes: [
            { NodeId: 'main', Prompt: 'Press 1.', Options: [{ Digit: '1', Action: { Kind: 'RouteToQueue', TargetId: 'q' } }] },
            { NodeId: 'spare', Prompt: 'Unused.', Options: [{ Digit: '1', Action: { Kind: 'Voicemail' } }] },
        ],
    });

    it('is applied when it parses and passes every check, warnings aside', () => {
        const result = ivr.checkJsonForEditor(valid, catalog);

        expect(result.ok).toBe(true);
        expect(result.model.nodes).toHaveLength(2);
        expect(result.errors).toEqual([]);
        expect(result.warnings.map(entry => entry.code)).toContain('unreachable');
    });

    it('is refused when it does not parse, with the reason', () => {
        const result = ivr.checkJsonForEditor('{ "RootNodeId": ', catalog);

        expect(result.ok).toBe(false);
        expect(result.parseError).toBeTruthy();
        expect(result.model).toBeNull();
    });

    it('is refused when it is not a menu', () => {
        const result = ivr.checkJsonForEditor('[1, 2]', catalog);

        expect(result.ok).toBe(false);
        expect(result.parseError).toBe('notAnObject');
    });

    it('is refused when it parses but fails a check, listing every error', () => {
        const broken = JSON.stringify({
            RootNodeId: 'main',
            MaxRetries: 0,
            Nodes: [{ NodeId: 'main', Prompt: '', Options: [{ Digit: '1', Action: { Kind: 'SubMenu', TargetId: 'nowhere' } }] }],
        });

        const result = ivr.checkJsonForEditor(broken, catalog);

        expect(result.ok).toBe(false);
        expect(result.parseError).toBeNull();
        expect(result.errors.map(entry => entry.code)).toEqual(expect.arrayContaining(['maxRetriesInvalid', 'promptMissing', 'subMenuNotFound']));
    });

    // Clearing the JSON is how a menu is removed, so an empty field is a valid, empty menu.
    it('accepts an empty field as no menu at all', () => {
        const result = ivr.checkJsonForEditor('   ', catalog);

        expect(result.ok).toBe(true);
        expect(result.model.nodes).toEqual([]);
    });

    it('formats JSON it can read, and leaves anything else as it was', () => {
        expect(ivr.formatJson('{"a":1}')).toBe('{\n  "a": 1\n}');
        expect(ivr.formatJson('{ nope')).toBe('{ nope');
    });
});
