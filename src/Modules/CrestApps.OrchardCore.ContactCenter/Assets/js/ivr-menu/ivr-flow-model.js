/*
 * The inbound entry point's IVR menu, as the visual editor holds it, and the JSON the entry point stores.
 *
 * The editor still posts the same "IvrFlowJson" field the raw textarea did, so the server's binder and the entry
 * point's validator stay the authority on what is saved. This file only turns that JSON into something a form can
 * edit and back, and checks it the way the server will, so mistakes show beside the field that caused them.
 *
 * No DOM here: the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var ivr = root.CrestAppsIvrMenu = root.CrestAppsIvrMenu || {};

    // The order the editor offers the kinds in.
    var ACTION_KINDS = ['RouteToQueue', 'RouteToAgent', 'SubMenu', 'Voicemail', 'ExternalTransfer', 'Repeat'];

    // The server's IvrActionKind order, for a flow whose kinds were written as numbers.
    var ENUM_ORDER = ['RouteToQueue', 'RouteToAgent', 'Voicemail', 'ExternalTransfer', 'Repeat', 'SubMenu'];

    // The keys on a telephone keypad, in keypad order.
    var TELEPHONE_KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'];

    // What each kind's target is, for the kinds that need one.
    var TARGET_TYPES = {
        RouteToQueue: 'queue',
        RouteToAgent: 'agent',
        SubMenu: 'menu',
        ExternalTransfer: 'external'
    };

    // The server's default when a flow does not say.
    var DEFAULT_MAX_RETRIES = 3;

    function isObject(value) {
        return value !== null && typeof value === 'object' && !Array.isArray(value);
    }

    // The server binds property names without regard to case, so the editor reads them the same way.
    function read(source, name) {
        if (!isObject(source)) {
            return undefined;
        }

        if (Object.prototype.hasOwnProperty.call(source, name)) {
            return source[name];
        }

        var lower = name.toLowerCase();
        var keys = Object.keys(source);

        for (var index = 0; index < keys.length; index++) {
            if (keys[index].toLowerCase() === lower) {
                return source[keys[index]];
            }
        }

        return undefined;
    }

    function text(value) {
        return value === null || value === undefined ? '' : String(value);
    }

    function trimmedOrNull(value) {
        var trimmed = text(value).trim();

        return trimmed ? trimmed : null;
    }

    // Spoken text keeps its own spacing; only a prompt with nothing to say is empty.
    function promptOrNull(value) {
        var raw = text(value);

        return raw.trim() ? raw : null;
    }

    function isKnownKind(kind) {
        return ACTION_KINDS.indexOf(kind) !== -1;
    }

    function targetTypeOf(kind) {
        return TARGET_TYPES[kind] || null;
    }

    function needsTarget(kind) {
        return targetTypeOf(kind) !== null;
    }

    // A kind as the editor names it. An action with no kind is the server's default, RouteToQueue; a kind the
    // editor does not know is kept as written so the server is the one to refuse it.
    function normalizeKind(value) {
        if (value === null || value === undefined || value === '') {
            return ENUM_ORDER[0];
        }

        var raw = String(value).trim();

        if (/^\d+$/.test(raw)) {
            return ENUM_ORDER[Number(raw)] || raw;
        }

        for (var index = 0; index < ACTION_KINDS.length; index++) {
            if (ACTION_KINDS[index].toLowerCase() === raw.toLowerCase()) {
                return ACTION_KINDS[index];
            }
        }

        return raw;
    }

    function createAction(kind) {
        return { kind: kind || ACTION_KINDS[0], targetId: '' };
    }

    function createOption(digit) {
        return { digit: digit || '', action: createAction() };
    }

    function createNode(nodeId) {
        return { nodeId: nodeId || '', prompt: '', promptMediaId: '', options: [] };
    }

    function createModel() {
        return { rootNodeId: '', maxRetries: DEFAULT_MAX_RETRIES, fallback: null, nodes: [] };
    }

    function readAction(source) {
        if (!isObject(source)) {
            return null;
        }

        return {
            kind: normalizeKind(read(source, 'Kind')),
            targetId: text(read(source, 'TargetId'))
        };
    }

    function readOption(source) {
        return {
            digit: text(read(source, 'Digit')),
            action: readAction(read(source, 'Action'))
        };
    }

    function readNode(source) {
        var options = read(source, 'Options');

        return {
            nodeId: text(read(source, 'NodeId')),
            prompt: text(read(source, 'Prompt')),
            promptMediaId: text(read(source, 'PromptMediaId')),
            // The server skips a null entry, so the editor does too.
            options: Array.isArray(options) ? options.filter(isObject).map(readOption) : []
        };
    }

    // A stored flow (parsed JSON) as the editor's model.
    function fromFlow(flow) {
        var model = createModel();

        if (!isObject(flow)) {
            return model;
        }

        var retries = read(flow, 'MaxRetries');
        var nodes = read(flow, 'Nodes');

        model.rootNodeId = text(read(flow, 'RootNodeId'));
        model.maxRetries = retries === null || retries === undefined || retries === '' ? DEFAULT_MAX_RETRIES : Number(retries);
        model.fallback = readAction(read(flow, 'FallbackAction'));
        model.nodes = Array.isArray(nodes) ? nodes.filter(isObject).map(readNode) : [];

        return model;
    }

    function writeAction(action) {
        if (!action || !action.kind) {
            return null;
        }

        return {
            Kind: action.kind,
            TargetId: trimmedOrNull(action.targetId)
        };
    }

    // The editor's model as the flow the entry point stores, or null when there are no menus: an entry point with
    // no menu routes straight to its target, and the server refuses a flow object that has no menus in it.
    function toFlow(model) {
        if (!model || !Array.isArray(model.nodes) || model.nodes.length === 0) {
            return null;
        }

        var retries = Number(model.maxRetries);

        return {
            RootNodeId: trimmedOrNull(model.rootNodeId),
            MaxRetries: isFinite(retries) && model.maxRetries !== '' && model.maxRetries !== null ? Math.trunc(retries) : 0,
            FallbackAction: writeAction(model.fallback),
            Nodes: model.nodes.map(function (node) {
                return {
                    NodeId: trimmedOrNull(node.nodeId),
                    Prompt: promptOrNull(node.prompt),
                    PromptMediaId: trimmedOrNull(node.promptMediaId),
                    Options: (node.options || []).map(function (option) {
                        return {
                            Digit: trimmedOrNull(option.digit),
                            Action: writeAction(option.action)
                        };
                    })
                };
            })
        };
    }

    // The text the form posts: indented JSON, or empty for "no menu".
    function toJson(model) {
        var flow = toFlow(model);

        return flow ? JSON.stringify(flow, null, 2) : '';
    }

    // Reads the textarea. Empty is a valid "no menu"; anything else must be a JSON object.
    function parseJson(value) {
        var raw = text(value).trim();

        if (!raw) {
            return { ok: true, model: createModel() };
        }

        var parsed;

        try {
            parsed = JSON.parse(raw);
        } catch (error) {
            return { ok: false, error: error && error.message ? error.message : String(error) };
        }

        if (!isObject(parsed)) {
            return { ok: false, error: 'notAnObject' };
        }

        return { ok: true, model: fromFlow(parsed) };
    }

    function forEachAction(model, callback) {
        (model.nodes || []).forEach(function (node, nodeIndex) {
            (node.options || []).forEach(function (option, optionIndex) {
                callback(option.action, { node: nodeIndex, option: optionIndex }, option);
            });
        });

        if (model.fallback) {
            callback(model.fallback, { fallback: true }, null);
        }
    }

    // The menus a caller can reach from the root, through sub-menu keys and the fallback.
    function reachableNodeIds(model) {
        var byId = {};
        var reached = {};

        (model.nodes || []).forEach(function (node) {
            var id = text(node.nodeId).trim();

            if (id && !byId[id]) {
                byId[id] = node;
            }
        });

        function visit(id) {
            if (!id || reached[id] || !byId[id]) {
                return;
            }

            reached[id] = true;

            (byId[id].options || []).forEach(function (option) {
                if (option.action && option.action.kind === 'SubMenu') {
                    visit(text(option.action.targetId).trim());
                }
            });

            // A caller who runs out of retries on any reachable menu is sent to the fallback.
            if (model.fallback && model.fallback.kind === 'SubMenu') {
                visit(text(model.fallback.targetId).trim());
            }
        }

        visit(text(model.rootNodeId).trim());

        return Object.keys(reached);
    }

    function issue(code, severity, path, params) {
        return { code: code, severity: severity, path: path || {}, params: params || {} };
    }

    function catalogHas(catalog, type, id) {
        var list = catalog && catalog[type];

        if (!Array.isArray(list)) {
            return true;
        }

        return list.some(function (entry) {
            return entry && String(entry.value) === id;
        });
    }

    // Checks the model the way IvrFlowValidator does, plus what only an editor can say (a menu nobody can reach, a
    // target that is not in the lists). Each issue carries a path to the field it belongs to:
    // { node, option } for a key, { node, field } for a menu, { fallback: true } or { field } for the flow.
    // The catalog ({ queue: [], agent: [], external: [] } of { value, text }) is optional.
    function validate(model, catalog) {
        var issues = [];

        if (!model || !Array.isArray(model.nodes) || model.nodes.length === 0) {
            return issues;
        }

        var retries = Number(model.maxRetries);

        if (model.maxRetries === '' || model.maxRetries === null || !isFinite(retries) || Math.trunc(retries) < 1) {
            issues.push(issue('maxRetriesInvalid', 'error', { field: 'maxRetries' }));
        }

        var ids = {};

        model.nodes.forEach(function (node, nodeIndex) {
            var id = text(node.nodeId).trim();

            if (!id) {
                issues.push(issue('nodeIdMissing', 'error', { node: nodeIndex, field: 'nodeId' }));
            } else if (ids[id]) {
                issues.push(issue('nodeIdDuplicate', 'error', { node: nodeIndex, field: 'nodeId' }, { nodeId: id }));
            } else {
                ids[id] = true;
            }
        });

        var rootId = text(model.rootNodeId).trim();

        if (!rootId) {
            issues.push(issue('rootMissing', 'error', { field: 'rootNodeId' }));
        } else if (!ids[rootId]) {
            issues.push(issue('rootNotFound', 'error', { field: 'rootNodeId' }, { nodeId: rootId }));
        }

        model.nodes.forEach(function (node, nodeIndex) {
            var nodeId = text(node.nodeId).trim();

            if (!text(node.prompt).trim() && !text(node.promptMediaId).trim()) {
                issues.push(issue('promptMissing', 'error', { node: nodeIndex, field: 'prompt' }, { nodeId: nodeId }));
            }

            if (!node.options || node.options.length === 0) {
                issues.push(issue('noOptions', 'error', { node: nodeIndex, field: 'options' }, { nodeId: nodeId }));
            }

            var digits = {};

            (node.options || []).forEach(function (option, optionIndex) {
                var digit = text(option.digit).trim();
                var path = { node: nodeIndex, option: optionIndex, field: 'digit' };

                if (!digit) {
                    issues.push(issue('digitMissing', 'error', path, { nodeId: nodeId }));
                } else if (TELEPHONE_KEYS.indexOf(digit) === -1) {
                    issues.push(issue('digitInvalid', 'error', path, { nodeId: nodeId, digit: digit }));
                } else if (digits[digit]) {
                    issues.push(issue('digitDuplicate', 'error', path, { nodeId: nodeId, digit: digit }));
                } else {
                    digits[digit] = true;
                }

                if (!option.action) {
                    issues.push(issue('actionMissing', 'error', { node: nodeIndex, option: optionIndex, field: 'kind' }, { nodeId: nodeId, digit: digit }));
                }
            });
        });

        forEachAction(model, function (action, path, option) {
            if (!action) {
                return;
            }

            var params = {
                nodeId: path.fallback ? '' : text(model.nodes[path.node].nodeId).trim(),
                digit: option ? text(option.digit).trim() : ''
            };

            if (!isKnownKind(action.kind)) {
                issues.push(issue('kindUnknown', 'error', withField(path, 'kind'), withParam(params, 'kind', action.kind)));

                return;
            }

            var type = targetTypeOf(action.kind);
            var targetId = text(action.targetId).trim();

            if (!type) {
                return;
            }

            if (!targetId) {
                issues.push(issue('targetMissing', 'error', withField(path, 'target'), withParam(params, 'kind', action.kind)));
            } else if (type === 'menu') {
                if (!ids[targetId]) {
                    issues.push(issue('subMenuNotFound', 'error', withField(path, 'target'), withParam(params, 'targetId', targetId)));
                }
            } else if (!catalogHas(catalog, type, targetId)) {
                issues.push(issue('targetUnknown', 'warning', withField(path, 'target'), withParam(withParam(params, 'targetId', targetId), 'kind', action.kind)));
            }
        });

        // Only worth saying once the root is sound: with no root, nothing is reachable and the root error says so.
        if (rootId && ids[rootId]) {
            var reached = reachableNodeIds(model);

            model.nodes.forEach(function (node, nodeIndex) {
                var id = text(node.nodeId).trim();

                if (id && reached.indexOf(id) === -1) {
                    issues.push(issue('unreachable', 'warning', { node: nodeIndex, field: 'nodeId' }, { nodeId: id }));
                }
            });
        }

        return issues;
    }

    function copyOf(source) {
        var copy = {};

        Object.keys(source).forEach(function (name) {
            copy[name] = source[name];
        });

        return copy;
    }

    function withField(path, field) {
        var copy = copyOf(path);

        copy.field = field;

        return copy;
    }

    function withParam(params, key, value) {
        var copy = copyOf(params);

        copy[key] = value;

        return copy;
    }

    function hasErrors(issues) {
        return (issues || []).some(function (entry) {
            return entry.severity === 'error';
        });
    }

    // A menu name nobody has used yet: "main" for the first menu, then "menu-2", "menu-3", ...
    function suggestNodeId(model) {
        var used = {};

        (model.nodes || []).forEach(function (node) {
            used[text(node.nodeId).trim()] = true;
        });

        if (!used.main) {
            return 'main';
        }

        var number = (model.nodes || []).length + 1;

        while (used['menu-' + number]) {
            number++;
        }

        return 'menu-' + number;
    }

    // Keys this option may take: the ones no other option on the menu already answers, plus its own.
    function availableDigits(node, optionIndex) {
        var taken = {};

        (node.options || []).forEach(function (option, index) {
            if (index !== optionIndex) {
                taken[text(option.digit).trim()] = true;
            }
        });

        return TELEPHONE_KEYS.filter(function (key) {
            return !taken[key];
        });
    }

    function nextFreeDigit(node) {
        return availableDigits(node, -1)[0] || '';
    }

    function addNode(model, nodeId) {
        var node = createNode(nodeId || suggestNodeId(model));

        node.options.push(createOption('1'));
        model.nodes.push(node);

        if (!text(model.rootNodeId).trim()) {
            model.rootNodeId = node.nodeId;
        }

        return node;
    }

    function addOption(node) {
        var digit = nextFreeDigit(node);

        if (!digit) {
            return null;
        }

        var option = createOption(digit);

        node.options.push(option);

        return option;
    }

    // How many keys (and the fallback) open the menu, and whether it is the root.
    function countReferences(model, nodeId) {
        var id = text(nodeId).trim();
        var count = 0;

        if (!id) {
            return 0;
        }

        forEachAction(model, function (action) {
            if (action && action.kind === 'SubMenu' && text(action.targetId).trim() === id) {
                count++;
            }
        });

        return count;
    }

    // Renames a menu and everything that points at it. Refuses an empty name or one another menu already has.
    function renameNode(model, nodeIndex, newId) {
        var node = model.nodes[nodeIndex];
        var target = text(newId).trim();

        if (!node || !target) {
            return false;
        }

        var clash = model.nodes.some(function (other, index) {
            return index !== nodeIndex && text(other.nodeId).trim() === target;
        });

        if (clash) {
            return false;
        }

        var oldId = text(node.nodeId).trim();

        node.nodeId = target;

        if (!oldId || oldId === target) {
            return true;
        }

        if (text(model.rootNodeId).trim() === oldId) {
            model.rootNodeId = target;
        }

        forEachAction(model, function (action) {
            if (action && action.kind === 'SubMenu' && text(action.targetId).trim() === oldId) {
                action.targetId = target;
            }
        });

        return true;
    }

    // Removes a menu. A root that goes away is replaced by the first menu left; keys that opened the removed menu
    // are left pointing at it so the editor shows them as broken rather than silently changing what they do.
    function removeNode(model, nodeIndex) {
        var node = model.nodes[nodeIndex];

        if (!node) {
            return;
        }

        model.nodes.splice(nodeIndex, 1);

        if (text(model.rootNodeId).trim() === text(node.nodeId).trim()) {
            model.rootNodeId = model.nodes.length ? model.nodes[0].nodeId : '';
        }
    }

    // Changes what an action does. A new kind starts with no target, since a queue id is not a menu name.
    function setActionKind(action, kind) {
        if (action.kind !== kind) {
            action.kind = kind;
            action.targetId = '';
        }

        return action;
    }

    ivr.ACTION_KINDS = ACTION_KINDS;
    ivr.ENUM_ORDER = ENUM_ORDER;
    ivr.TELEPHONE_KEYS = TELEPHONE_KEYS;
    ivr.DEFAULT_MAX_RETRIES = DEFAULT_MAX_RETRIES;
    ivr.isKnownKind = isKnownKind;
    ivr.targetTypeOf = targetTypeOf;
    ivr.needsTarget = needsTarget;
    ivr.normalizeKind = normalizeKind;
    ivr.createModel = createModel;
    ivr.createNode = createNode;
    ivr.createOption = createOption;
    ivr.createAction = createAction;
    ivr.fromFlow = fromFlow;
    ivr.toFlow = toFlow;
    ivr.toJson = toJson;
    ivr.parseJson = parseJson;
    ivr.validate = validate;
    ivr.hasErrors = hasErrors;
    ivr.reachableNodeIds = reachableNodeIds;
    ivr.suggestNodeId = suggestNodeId;
    ivr.availableDigits = availableDigits;
    ivr.nextFreeDigit = nextFreeDigit;
    ivr.addNode = addNode;
    ivr.addOption = addOption;
    ivr.countReferences = countReferences;
    ivr.renameNode = renameNode;
    ivr.removeNode = removeNode;
    ivr.setActionKind = setActionKind;
}(typeof globalThis !== 'undefined' ? globalThis : window));
