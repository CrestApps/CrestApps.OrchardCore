import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { parseSync, traverse } from '@babel/core';
import { globSync } from 'glob';
import { describe, expect, it } from 'vitest';

// Every hand-written browser script is a classic script, and a name that is used but never declared does not
// fail when the file loads — it throws a ReferenceError the first time that line runs. Inside a timer or an
// event handler that error is swallowed, and the feature behind it just stops: a merge once left the soft phone's
// outbound-audio watch reading a hold-audio variable another change had renamed, and the "caller may not hear
// you" warning went dark with nothing failing. This parses each script, resolves every identifier against the
// scopes that enclose it, and fails on any name that is neither declared there nor a known global.
//
// Scripts are checked one file at a time. They wrap themselves in an IIFE and share state through namespaces on
// `window`/`globalThis` (read as members, so never bare names here), not through top-level declarations leaking
// between the concatenated files of a bundle; if a script ever needs another file's top-level name, expose it
// on a namespace instead.
const scriptGlob = 'src/Modules/*/Assets/**/*.js';

// ECMAScript's own globals (Object, Promise, Intl, globalThis, NaN, parseInt, ...), taken from a fresh V8
// realm, which carries the language built-ins and none of Node's or a browser's host objects.
const LANGUAGE_GLOBALS = runInNewContext('Object.getOwnPropertyNames(globalThis)');

// Host objects a browser page provides, kept to the ones these scripts use and their obvious siblings. Add a
// name here only when it is a real browser API the script may rely on being present.
const BROWSER_GLOBALS = [
    'window', 'document', 'navigator', 'location', 'history', 'console', 'crypto', 'performance',
    'localStorage', 'sessionStorage',
    'setTimeout', 'clearTimeout', 'setInterval', 'clearInterval', 'requestAnimationFrame', 'cancelAnimationFrame',
    'queueMicrotask',
    'fetch', 'Headers', 'Request', 'Response', 'AbortController', 'XMLHttpRequest', 'WebSocket', 'BroadcastChannel',
    'URL', 'URLSearchParams', 'FormData', 'Blob', 'File', 'FileReader',
    'Event', 'CustomEvent', 'EventTarget',
    'Node', 'Element', 'HTMLElement', 'HTMLFormElement', 'HTMLInputElement', 'HTMLSelectElement', 'HTMLAudioElement',
    'MutationObserver', 'ResizeObserver', 'IntersectionObserver',
    'Audio', 'AudioContext', 'MediaStream', 'MediaStreamTrack', 'MediaRecorder',
    'RTCPeerConnection', 'RTCRtpSender', 'RTCSessionDescription',
];

// Globals a page defines before these scripts run, which the scripts read as bare names.
const PAGE_GLOBALS = [
    'signalR',          // @microsoft/signalr, loaded ahead of the soft phone's hub connection.
    'flatpickr',        // The flatpickr date picker the range picker and query builder decorate.
    'flatpickrCulture', // Created (as a deliberate sloppy-mode global) by Resources' flatpickr-culture.js.
    'confirmDialog',    // Orchard Core's admin theme confirmation dialog, used by the list bulk actions.
];

const KNOWN_GLOBALS = new Set([...LANGUAGE_GLOBALS, ...BROWSER_GLOBALS, ...PAGE_GLOBALS]);

// Returns every use of a name that no enclosing scope declares and no known global provides, in source order.
function findUndeclaredNames(code, filename) {
    const ast = parseSync(code, { babelrc: false, configFile: false, filename, sourceType: 'script' });
    const found = [];

    const report = (path, name, kind) => {
        if (KNOWN_GLOBALS.has(name) || path.scope.hasBinding(name, { noGlobals: true })) {
            return;
        }

        // `arguments` is bound by every enclosing non-arrow function, which Babel does not model as a binding.
        if (name === 'arguments' && path.findParent(parent => parent.isFunction() && !parent.isArrowFunctionExpression())) {
            return;
        }

        found.push({ name, kind, line: path.node.loc.start.line, column: path.node.loc.start.column + 1 });
    };

    traverse(ast, {
        ReferencedIdentifier(path) {
            // `typeof name` is the one read of an undeclared name that does not throw; scripts use it as a feature probe.
            if (path.parentPath.isUnaryExpression({ operator: 'typeof' })) {
                return;
            }

            report(path, path.node.name, 'read');
        },
        AssignmentExpression(path) {
            // Writing an undeclared name throws in strict code and silently creates a global otherwise; both are bugs.
            if (path.node.left.type === 'Identifier') {
                report(path.get('left'), path.node.left.name, 'assigned');
            }
        },
    });

    return found;
}

describe('the undeclared-name check itself', () => {
    it('catches a renamed variable read inside a timer callback', () => {
        // The shape of the bug this guards against: the variable was renamed, one read of the old name survived.
        const code = `(function () {
            'use strict';
            var holdAudioByCall = {};
            function startOutboundWatch() {
                setInterval(function () {
                    return holdAudio && holdAudio.isEngaged();
                }, 1000);
            }
            startOutboundWatch();
        }());`;

        expect(findUndeclaredNames(code, 'sample.js')).toEqual([
            { name: 'holdAudio', kind: 'read', line: 6, column: 28 },
            { name: 'holdAudio', kind: 'read', line: 6, column: 41 },
        ]);
    });

    it('resolves hoisted functions, vars, parameters, catch bindings and named function expressions', () => {
        const code = `(function (root) {
            later();
            function later() { return counter; }
            var counter = 0;
            try { root.go(); } catch (error) { counter = error ? 1 : 0; }
            var tick = function self() { return self; };
            const run = () => { let n = 1; n++; return n + tick(); };
            run.apply(null, (function () { return arguments; }()));
        }(globalThis));`;

        expect(findUndeclaredNames(code, 'sample.js')).toEqual([]);
    });

    it('allows typeof probes but not a bare read of the same name', () => {
        const code = `var start = typeof startMonitor === 'function' ? startMonitor : null;`;

        expect(findUndeclaredNames(code, 'sample.js')).toEqual([
            { name: 'startMonitor', kind: 'read', line: 1, column: 50 },
        ]);
    });

    it('flags writes to names that were never declared', () => {
        const code = `(function () { var total = 0; totl = total + 1; missing++; }());`;

        // `missing++` reads before it writes, so it surfaces as the read that throws first.
        expect(findUndeclaredNames(code, 'sample.js').map(use => `${use.name} ${use.kind}`)).toEqual([
            'totl assigned',
            'missing read',
        ]);
    });

    it('does not mistake property names, object keys or labels for variables', () => {
        const code = `var o = { holdAudio: 1 }; o.holdAudio = o['x']; outer: for (;;) { break outer; }`;

        expect(findUndeclaredNames(code, 'sample.js')).toEqual([]);
    });
});

describe('browser scripts', () => {
    const files = globSync(scriptGlob, { posix: true }).sort();

    it('are found', () => {
        // A moved folder or a changed glob must not quietly turn this into a check of nothing.
        expect(files).toContain('src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone.js');
        expect(files).toContain('src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/agent-workspace.js');
    });

    it.each(files)('%s uses only declared names', file => {
        const undeclared = findUndeclaredNames(readFileSync(file, 'utf8'), file)
            .map(use => `${file}:${use.line}:${use.column} '${use.name}' is ${use.kind} but never declared`);

        expect(undeclared, 'Declare the name, fix the typo, or (for a real browser/page global) add it to the allow-list in this test.').toEqual([]);
    });
});
