import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import {
    ACTION_KINDS,
    DEFAULT_MAX_RETRIES,
    ENUM_ORDER,
    GO_TO_MENU,
    TELEPHONE_KEYS,
    actionForEditorKind,
    addNode,
    addOption,
    addSubMenu,
    availableDigits,
    buildMenuTree,
    checkJsonForEditor,
    countReferences,
    createModel,
    editorKindOf,
    formatJson,
    fromFlow,
    hasErrors,
    isKnownKind,
    needsTarget,
    parseJson,
    removeNode,
    removeSubMenu,
    renameNode,
    setActionKind,
    targetTypeOf,
    toFlow,
    toJson,
    validate,
} from '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/ivr-menu/ivr-flow-model.js';

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
        const model = fromFlow(fixture);

        expect(toFlow(model)).toEqual(fixture);
    });

    it('writes the same indented JSON it reads', () => {
        expect(toJson(parseJson(fixtureText).model)).toBe(fixtureText.trim().replace(/\r\n/g, '\n'));
    });

    it('covers every kind the server knows', () => {
        const kinds = new Set();

        fixture.Nodes.forEach(node => node.Options.forEach(option => kinds.add(option.Action.Kind)));

        expect([...kinds].sort()).toEqual([...ACTION_KINDS].sort());
        expect([...ENUM_ORDER].sort()).toEqual([...ACTION_KINDS].sort());
    });

    it.each(ACTION_KINDS)('round-trips a %s key', kind => {
        const target = needsTarget(kind) ? (kind === 'SubMenu' ? 'main' : 'target-1') : null;
        const flow = flowWith([{ Digit: '1', Action: { Kind: kind, TargetId: target } }]);

        expect(toFlow(fromFlow(flow))).toEqual(flow);
    });

    it.each(ACTION_KINDS)('round-trips a %s fallback', kind => {
        const target = needsTarget(kind) ? (kind === 'SubMenu' ? 'main' : 'target-1') : null;
        const flow = flowWith([{ Digit: '1', Action: { Kind: 'Repeat', TargetId: null } }], { FallbackAction: { Kind: kind, TargetId: target } });

        expect(toFlow(fromFlow(flow))).toEqual(flow);
    });

    it('reads kinds written as numbers in the server enum order', () => {
        const flow = flowWith(ENUM_ORDER.map((kind, index) => ({ Digit: String(index + 1), Action: { Kind: index, TargetId: null } })));
        const model = fromFlow(flow);

        expect(model.nodes[0].options.map(option => option.action.kind)).toEqual(ENUM_ORDER);
    });

    it('reads property names and kind names without regard to case, as the server does', () => {
        const model = fromFlow({
            rootNodeId: 'main',
            maxRetries: 2,
            nodes: [{ nodeId: 'main', prompt: 'Hi', options: [{ digit: '1', action: { kind: 'submenu', targetId: 'main' } }] }],
        });

        expect(model.rootNodeId).toBe('main');
        expect(model.maxRetries).toBe(2);
        expect(model.nodes[0].options[0].action).toEqual({ kind: 'SubMenu', targetId: 'main' });
    });

    it('takes the server defaults for what a flow leaves out', () => {
        const model = fromFlow({ RootNodeId: 'main', Nodes: [{ NodeId: 'main', Prompt: 'Hi', Options: [{ Digit: '1', Action: {} }] }] });

        expect(model.maxRetries).toBe(DEFAULT_MAX_RETRIES);
        expect(model.fallback).toBeNull();
        expect(model.nodes[0].options[0].action.kind).toBe('RouteToQueue');
    });

    it('keeps an option with no action as one, so the server can refuse it', () => {
        const flow = flowWith([{ Digit: '1', Action: null }]);

        expect(toFlow(fromFlow(flow))).toEqual(flow);
    });

    it('keeps a kind it does not know as written', () => {
        const model = fromFlow(flowWith([{ Digit: '1', Action: { Kind: 'Teleport', TargetId: 'x' } }]));

        expect(toFlow(model).Nodes[0].Options[0].Action).toEqual({ Kind: 'Teleport', TargetId: 'x' });
    });

    it('skips null menus and options the way the server does', () => {
        const model = fromFlow({ RootNodeId: 'main', Nodes: [null, { NodeId: 'main', Prompt: 'Hi', Options: [null, { Digit: '1', Action: { Kind: 'Repeat' } }] }] });

        expect(model.nodes).toHaveLength(1);
        expect(model.nodes[0].options).toHaveLength(1);
    });

    it('trims identifiers and keys but keeps the spoken prompt as typed', () => {
        const model = createModel();
        const node = addNode(model, ' main ');

        node.prompt = '  Press 1.  ';
        node.options[0].action = { kind: 'RouteToQueue', targetId: ' q-1 ' };
        model.rootNodeId = ' main ';

        const flow = toFlow(model);

        expect(flow.RootNodeId).toBe('main');
        expect(flow.Nodes[0].NodeId).toBe('main');
        expect(flow.Nodes[0].Prompt).toBe('  Press 1.  ');
        expect(flow.Nodes[0].Options[0].Action.TargetId).toBe('q-1');
    });

    it('writes an empty prompt and media id as null', () => {
        const model = createModel();
        const node = addNode(model);

        node.prompt = '   ';

        expect(toFlow(model).Nodes[0].Prompt).toBeNull();
        expect(toFlow(model).Nodes[0].PromptMediaId).toBeNull();
    });
});

// Empty must keep meaning "no IVR: route straight to the entry point's target".
describe('no menu', () => {
    it('reads an empty field as a model with no menus', () => {
        for (const value of ['', '   ', null, undefined]) {
            const result = parseJson(value);

            expect(result.ok).toBe(true);
            expect(result.model.nodes).toEqual([]);
        }
    });

    it('writes a model with no menus as an empty field', () => {
        expect(toJson(createModel())).toBe('');
        expect(toFlow(createModel())).toBeNull();
    });

    it('writes an empty field once the last menu is removed', () => {
        const model = fromFlow(flowWith([{ Digit: '1', Action: { Kind: 'Repeat', TargetId: null } }]));

        removeNode(model, 0);

        expect(toJson(model)).toBe('');
        expect(model.rootNodeId).toBe('');
    });

    it('has nothing to report', () => {
        expect(validate(createModel())).toEqual([]);
    });
});

describe('parseJson', () => {
    it('reports JSON it cannot read', () => {
        const result = parseJson('{ "RootNodeId": ');

        expect(result.ok).toBe(false);
        expect(result.error).toBeTruthy();
    });

    it('refuses JSON that is not an object', () => {
        expect(parseJson('[]')).toEqual({ ok: false, error: 'notAnObject' });
        expect(parseJson('"text"')).toEqual({ ok: false, error: 'notAnObject' });
    });
});

describe('validate', () => {
    const catalog = {
        queue: [{ value: 'queue-sales', text: 'Sales' }, { value: 'queue-support', text: 'Support' }, { value: 'queue-general', text: 'General' }],
        agent: [{ value: 'agent-front-desk', text: 'Front desk' }],
        external: [{ value: 'destination-billing', text: 'Billing' }],
    };

    it('accepts the fixture', () => {
        expect(validate(fromFlow(fixture), catalog)).toEqual([]);
    });

    it('needs a root menu that exists', () => {
        const missing = fromFlow({ ...fixture, RootNodeId: '' });
        const unknown = fromFlow({ ...fixture, RootNodeId: 'nowhere' });

        expect(codes(validate(missing))).toContain('rootMissing');
        expect(validate(unknown).find(entry => entry.code === 'rootNotFound')).toMatchObject({ severity: 'error', path: { field: 'rootNodeId' }, params: { nodeId: 'nowhere' } });
    });

    it('needs at least one retry', () => {
        for (const value of [0, -1, '', null, 'abc']) {
            const model = fromFlow(fixture);

            model.maxRetries = value;

            expect(codes(validate(model))).toContain('maxRetriesInvalid');
        }
    });

    it('needs every menu named once', () => {
        const model = fromFlow(fixture);

        model.nodes[1].nodeId = 'main';

        expect(validate(model).find(entry => entry.code === 'nodeIdDuplicate')).toMatchObject({ path: { node: 1, field: 'nodeId' } });

        model.nodes[1].nodeId = ' ';

        expect(codes(validate(model))).toContain('nodeIdMissing');
    });

    it('needs something for the caller to hear', () => {
        const model = fromFlow(fixture);

        model.nodes[1].promptMediaId = '';

        expect(validate(model).find(entry => entry.code === 'promptMissing')).toMatchObject({ path: { node: 1, field: 'prompt' }, params: { nodeId: 'support' } });
    });

    it('accepts a recorded prompt in place of a spoken one', () => {
        const model = fromFlow(fixture);

        expect(model.nodes[1].prompt).toBe('');
        expect(codes(validate(model))).not.toContain('promptMissing');
    });

    it('needs at least one key on a menu', () => {
        const model = fromFlow(fixture);

        model.nodes[1].options = [];

        expect(codes(validate(model))).toContain('noOptions');
    });

    it('refuses a key that is used twice on the same menu', () => {
        const model = fromFlow(fixture);

        model.nodes[0].options[1].digit = '1';

        expect(validate(model).find(entry => entry.code === 'digitDuplicate')).toMatchObject({ path: { node: 0, option: 1, field: 'digit' }, params: { digit: '1' } });
    });

    it('allows the same key on different menus', () => {
        const model = fromFlow(fixture);

        expect(model.nodes[0].options[0].digit).toBe(model.nodes[1].options[0].digit);
        expect(codes(validate(model))).not.toContain('digitDuplicate');
    });

    it('refuses a key that is not on a telephone keypad', () => {
        const model = fromFlow(fixture);

        model.nodes[0].options[0].digit = '12';
        model.nodes[0].options[1].digit = '';

        expect(codes(validate(model))).toEqual(expect.arrayContaining(['digitInvalid', 'digitMissing']));
    });

    it('refuses a key with no action', () => {
        const model = fromFlow(fixture);

        model.nodes[0].options[0].action = null;

        expect(validate(model).find(entry => entry.code === 'actionMissing')).toMatchObject({ path: { node: 0, option: 0, field: 'kind' } });
    });

    it.each(['RouteToQueue', 'RouteToAgent', 'SubMenu', 'ExternalTransfer'])('needs a target for %s', kind => {
        const model = fromFlow(fixture);

        model.nodes[0].options[0].action = { kind, targetId: '' };

        expect(validate(model).find(entry => entry.code === 'targetMissing')).toMatchObject({ path: { node: 0, option: 0, field: 'target' }, params: { kind } });
    });

    it.each(['Voicemail', 'Repeat'])('needs no target for %s', kind => {
        const model = fromFlow(fixture);

        model.nodes[0].options[0].action = { kind, targetId: '' };

        expect(codes(validate(model, catalog))).toEqual([]);
    });

    it('refuses a sub-menu that does not exist', () => {
        const model = fromFlow(fixture);

        model.nodes[0].options[1].action.targetId = 'gone';

        expect(validate(model).find(entry => entry.code === 'subMenuNotFound')).toMatchObject({ severity: 'error', params: { targetId: 'gone', nodeId: 'main', digit: '2' } });
    });

    it('checks the fallback like any other action', () => {
        const model = fromFlow(fixture);

        model.fallback = { kind: 'SubMenu', targetId: 'gone' };

        expect(validate(model).find(entry => entry.code === 'subMenuNotFound')).toMatchObject({ path: { fallback: true, field: 'target' } });

        model.fallback = { kind: 'RouteToAgent', targetId: '' };

        expect(validate(model).find(entry => entry.code === 'targetMissing')).toMatchObject({ path: { fallback: true } });
    });

    it('refuses a kind it does not know', () => {
        const model = fromFlow(flowWith([{ Digit: '1', Action: { Kind: 'Teleport', TargetId: null } }]));

        expect(codes(validate(model))).toContain('kindUnknown');
    });

    it('warns about a menu no caller can reach', () => {
        const model = fromFlow(fixture);
        const orphan = addNode(model, 'orphan');

        orphan.prompt = 'Nobody hears this.';

        const unreachable = validate(model).filter(entry => entry.code === 'unreachable');

        expect(unreachable).toEqual([expect.objectContaining({ severity: 'warning', path: { node: 2, field: 'nodeId' }, params: { nodeId: 'orphan' } })]);
    });

    it('counts a menu the fallback opens as reachable', () => {
        const model = fromFlow(fixture);

        addNode(model, 'last-chance').prompt = 'One more try.';
        model.fallback = { kind: 'SubMenu', targetId: 'last-chance' };

        expect(codes(validate(model))).not.toContain('unreachable');
    });

    it('does not call every menu unreachable when the root itself is broken', () => {
        const model = fromFlow({ ...fixture, RootNodeId: 'nowhere' });

        expect(codes(validate(model))).not.toContain('unreachable');
    });

    it('warns about a queue, agent or destination that is not in the lists', () => {
        const model = fromFlow(fixture);

        model.nodes[0].options[0].action.targetId = 'queue-deleted';

        expect(validate(model, catalog).find(entry => entry.code === 'targetUnknown')).toMatchObject({ severity: 'warning', params: { targetId: 'queue-deleted', kind: 'RouteToQueue' } });
        expect(codes(validate(model))).not.toContain('targetUnknown');
    });

    it('separates errors from warnings', () => {
        expect(hasErrors([{ severity: 'warning' }])).toBe(false);
        expect(hasErrors([{ severity: 'warning' }, { severity: 'error' }])).toBe(true);
    });
});

describe('editing helpers', () => {
    it('names the first menu "main" and then numbers the rest', () => {
        const model = createModel();

        expect(addNode(model).nodeId).toBe('main');
        expect(addNode(model).nodeId).toBe('menu-2');
        expect(addNode(model).nodeId).toBe('menu-3');
        expect(model.rootNodeId).toBe('main');
    });

    it('starts a new menu with one key', () => {
        const node = addNode(createModel());

        expect(node.options).toEqual([{ digit: '1', action: { kind: 'RouteToQueue', targetId: '' } }]);
    });

    it('offers only the keys no other option on the menu uses', () => {
        const node = fromFlow(fixture).nodes[1];

        expect(availableDigits(node, 0)).toEqual(['1', '2', '3', '4', '5', '6', '7', '8', '9', '0']);
        expect(availableDigits(node, -1)).not.toContain('1');
    });

    it('adds keys until the keypad runs out', () => {
        const node = addNode(createModel());

        for (let index = 0; index < 11; index++) {
            expect(addOption(node)).not.toBeNull();
        }

        expect(node.options.map(option => option.digit).sort()).toEqual([...TELEPHONE_KEYS].sort());
        expect(addOption(node)).toBeNull();
    });

    it('renames a menu and everything that opens it', () => {
        const model = fromFlow(fixture);

        model.fallback = { kind: 'SubMenu', targetId: 'main' };

        expect(renameNode(model, 0, 'welcome')).toBe(true);
        expect(model.rootNodeId).toBe('welcome');
        expect(model.nodes[1].options[1].action.targetId).toBe('welcome');
        expect(model.fallback.targetId).toBe('welcome');
        expect(validate(model)).toEqual([]);
    });

    it('refuses to rename a menu to an empty or taken name', () => {
        const model = fromFlow(fixture);

        expect(renameNode(model, 0, ' ')).toBe(false);
        expect(renameNode(model, 0, 'support')).toBe(false);
        expect(model.nodes[0].nodeId).toBe('main');
    });

    it('counts the keys that open a menu', () => {
        const model = fromFlow(fixture);

        expect(countReferences(model, 'support')).toBe(1);
        expect(countReferences(model, 'main')).toBe(1);
        expect(countReferences(model, 'nowhere')).toBe(0);
    });

    it('leaves keys that opened a removed menu visibly broken', () => {
        const model = fromFlow(fixture);

        removeNode(model, 1);

        expect(codes(validate(model))).toEqual(['subMenuNotFound']);
    });

    it('picks a new root when the root menu is removed', () => {
        const model = fromFlow(fixture);

        removeNode(model, 0);

        expect(model.rootNodeId).toBe('support');
    });

    it('clears the target when the kind changes', () => {
        const action = { kind: 'RouteToQueue', targetId: 'queue-sales' };

        setActionKind(action, 'RouteToQueue');
        expect(action.targetId).toBe('queue-sales');

        setActionKind(action, 'SubMenu');
        expect(action).toEqual({ kind: 'SubMenu', targetId: '' });
    });

    it('knows what each kind targets', () => {
        expect(ACTION_KINDS.map(kind => targetTypeOf(kind))).toEqual(['queue', 'agent', 'menu', null, 'external', null]);
    });
});

// The editor draws the menus the way a caller walks them: a submenu under the key that opens it. The stored flow stays a
// flat list of named menus, so these are views and edits of that list.
describe('the menus as a caller walks them', () => {
    const tree = flow => buildMenuTree(fromFlow(flow));
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
        const model = fromFlow({ RootNodeId: 'main', MaxRetries: 3, Nodes: [node('main', [key('1', 'RouteToQueue', 'q'), key('2', 'Voicemail')])] });

        const created = addSubMenu(model, 0, 1);

        expect(created.nodeId).toBeTruthy();
        expect(created.nodeId).not.toBe('main');
        expect(model.nodes[0].options[1].action).toEqual({ kind: 'SubMenu', targetId: created.nodeId });
        expect(buildMenuTree(model).childOf(0, 1)).toBe(1);
        // What is left is filling in the new menu itself: the key, the name and the link to it are all in place.
        const errors = validate(model, { queue: [{ value: 'q' }] }).filter(entry => entry.severity === 'error');
        expect(errors.length).toBeGreaterThan(0);
        expect(errors.every(entry => entry.path.node === 1)).toBe(true);
    });

    // Removing a submenu takes its own submenus with it, and a key that jumped to any of them is left to be chosen again.
    it('removes a submenu with the submenus nested under it, and clears the keys that opened them', () => {
        const model = fromFlow({
            RootNodeId: 'main',
            MaxRetries: 3,
            Nodes: [
                node('main', [key('1', 'SubMenu', 'a'), key('2', 'SubMenu', 'b')]),
                node('a', [key('1', 'SubMenu', 'a1')]),
                node('a1', [key('1', 'Voicemail')]),
                node('b', [key('1', 'SubMenu', 'a1')]),
            ],
        });

        removeSubMenu(model, 1);

        expect(model.nodes.map(entry => entry.nodeId)).toEqual(['main', 'b']);
        expect(model.nodes[0].options[0].action).toBeNull();
        expect(model.nodes[1].options[0].action).toBeNull();
        expect(model.nodes[0].options[1].action).toEqual({ kind: 'SubMenu', targetId: 'b' });
    });

    it('never removes the main menu as a submenu', () => {
        const model = fromFlow({ RootNodeId: 'main', MaxRetries: 3, Nodes: [node('main', [key('1', 'Voicemail')])] });

        removeSubMenu(model, 0);

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
        const result = checkJsonForEditor(valid, catalog);

        expect(result.ok).toBe(true);
        expect(result.model.nodes).toHaveLength(2);
        expect(result.errors).toEqual([]);
        expect(result.warnings.map(entry => entry.code)).toContain('unreachable');
    });

    it('is refused when it does not parse, with the reason', () => {
        const result = checkJsonForEditor('{ "RootNodeId": ', catalog);

        expect(result.ok).toBe(false);
        expect(result.parseError).toBeTruthy();
        expect(result.model).toBeNull();
    });

    it('is refused when it is not a menu', () => {
        const result = checkJsonForEditor('[1, 2]', catalog);

        expect(result.ok).toBe(false);
        expect(result.parseError).toBe('notAnObject');
    });

    it('is refused when it parses but fails a check, listing every error', () => {
        const broken = JSON.stringify({
            RootNodeId: 'main',
            MaxRetries: 0,
            Nodes: [{ NodeId: 'main', Prompt: '', Options: [{ Digit: '1', Action: { Kind: 'SubMenu', TargetId: 'nowhere' } }] }],
        });

        const result = checkJsonForEditor(broken, catalog);

        expect(result.ok).toBe(false);
        expect(result.parseError).toBeNull();
        expect(result.errors.map(entry => entry.code)).toEqual(expect.arrayContaining(['maxRetriesInvalid', 'promptMissing', 'subMenuNotFound']));
    });

    // Clearing the JSON is how a menu is removed, so an empty field is a valid, empty menu.
    it('accepts an empty field as no menu at all', () => {
        const result = checkJsonForEditor('   ', catalog);

        expect(result.ok).toBe(true);
        expect(result.model.nodes).toEqual([]);
    });

    it('formats JSON it can read, and leaves anything else as it was', () => {
        expect(formatJson('{"a":1}')).toBe('{\n  "a": 1\n}');
        expect(formatJson('{ nope')).toBe('{ nope');
    });
});

// "Go to another menu" is how the editor shows a key that jumps to a menu drawn somewhere else, such as back to the
// main menu. It is not a kind of its own: it is stored as a SubMenu, so the server reads it like any other.
describe('going to another menu', () => {
    const node = (id, options) => ({ NodeId: id, Prompt: 'Hi', Options: options });
    const key = (digit, kind, target) => ({ Digit: digit, Action: { Kind: kind, TargetId: target ?? null } });

    it('shows a key that opens its own submenu as a submenu', () => {
        expect(editorKindOf({ kind: 'SubMenu', targetId: 'billing' }, true)).toBe('SubMenu');
    });

    it('shows a key that jumps to a menu drawn elsewhere as going to another menu', () => {
        expect(editorKindOf({ kind: 'SubMenu', targetId: 'main' }, false)).toBe(GO_TO_MENU);
    });

    it('shows every other kind as itself, and no action as none', () => {
        expect(editorKindOf({ kind: 'Voicemail', targetId: '' }, false)).toBe('Voicemail');
        expect(editorKindOf(null, false)).toBe('');
    });

    it('starts a key sent to another menu as a submenu waiting for the menu to be chosen', () => {
        expect(actionForEditorKind(GO_TO_MENU)).toEqual({ kind: 'SubMenu', targetId: '' });
        expect(actionForEditorKind('Voicemail')).toEqual({ kind: 'Voicemail', targetId: '' });
    });

    it('is never stored as a kind of its own', () => {
        expect(isKnownKind(GO_TO_MENU)).toBe(false);

        const model = fromFlow({
            RootNodeId: 'main',
            MaxRetries: 3,
            Nodes: [node('main', [key('2', 'SubMenu', 'billing')]), node('billing', [])],
        });

        const jump = actionForEditorKind(GO_TO_MENU);
        jump.targetId = 'main';
        model.nodes[1].options.push({ digit: '9', action: jump });

        const stored = toFlow(model).Nodes[1].Options[0];

        expect(stored.Digit).toBe('9');
        expect(stored.Action).toEqual({ Kind: 'SubMenu', TargetId: 'main' });
    });

    it('is what a key back to the main menu reads as in the menu tree', () => {
        const model = fromFlow({
            RootNodeId: 'main',
            MaxRetries: 3,
            Nodes: [
                node('main', [key('2', 'SubMenu', 'billing')]),
                node('billing', [key('9', 'SubMenu', 'main')]),
            ],
        });
        const tree = buildMenuTree(model);
        const kindOf = (menu, option) => editorKindOf(model.nodes[menu].options[option].action, tree.childOf(menu, option) !== undefined);

        expect(kindOf(0, 0)).toBe('SubMenu');
        expect(kindOf(1, 0)).toBe(GO_TO_MENU);
    });
});
