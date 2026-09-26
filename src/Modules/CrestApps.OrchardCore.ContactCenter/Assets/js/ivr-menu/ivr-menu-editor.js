/*
 * The entry point's visual IVR menu editor.
 *
 * The menu used to be typed as raw JSON. This builds it with forms instead: menus with a prompt and a row per key,
 * each key choosing what it does and where it goes from lists of the tenant's queues, agents, approved external
 * destinations and the menus defined here. The JSON textarea is still the field the form posts, kept in step with
 * every edit, and stays one switch away for anyone who prefers to type it; JSON that cannot be read keeps the
 * editor in that mode, with the reason, rather than throwing an edit away.
 *
 * The menus are drawn the way a caller walks them: the first menu at the top, and each submenu nested under the key
 * that opens it, so nobody names menus or picks them by name. A key that goes back to a menu already drawn is a jump,
 * labelled by the keys that lead there ("Main menu > 2"). The flow is still stored as a flat list of named menus; the
 * editor names new ones itself.
 *
 * The model, the JSON round trip, the checks and the menu tree live in ivr-flow-model.js, which has no DOM and is unit
 * tested.
 */
(function (window, document) {
    'use strict';

    var ivr = window.CrestAppsIvrMenu;
    var NEW_MENU = '__new__';

    // A small element builder: h('div', { className: 'x', 'data-a': 'b', text: 'hi' }, [children]).
    function h(tag, attributes, children) {
        var element = document.createElement(tag);

        Object.keys(attributes || {}).forEach(function (name) {
            var value = attributes[name];

            if (value === null || value === undefined || value === false) {
                return;
            }

            if (name === 'text') {
                element.textContent = value;
            } else if (name === 'className') {
                element.className = value;
            } else if (name === 'value') {
                element.value = value;
            } else if (value === true) {
                element.setAttribute(name, '');
            } else {
                element.setAttribute(name, value);
            }
        });

        (children || []).forEach(function (child) {
            if (child !== null && child !== undefined && child !== false) {
                element.appendChild(typeof child === 'string' ? document.createTextNode(child) : child);
            }
        });

        return element;
    }

    function icon(name) {
        return h('i', { className: name, 'aria-hidden': 'true' });
    }

    function init(root) {
        var config = {};

        try {
            config = JSON.parse(root.getAttribute('data-config') || '{}');
        } catch (error) {
            config = {};
        }

        var strings = config.strings || {};
        var kindLabels = config.kindLabels || {};
        var catalog = {
            queue: config.queues || [],
            agent: config.agents || [],
            external: config.externals || [],
            media: config.media || []
        };
        var textarea = root.querySelector('[data-ivr-json]');
        var visual = root.querySelector('[data-ivr-visual]');
        var jsonPanel = root.querySelector('[data-ivr-json-panel]');
        var toggle = root.querySelector('[data-ivr-advanced-toggle]');
        var message = root.querySelector('[data-ivr-message]');
        var model = ivr.createModel();
        var tree = ivr.buildMenuTree(model);
        var transientIssues = {};
        var idPrefix = 'ivr' + Math.random().toString(36).slice(2, 8);

        if (!textarea || !visual || !jsonPanel || !toggle) {
            return;
        }

        function t(key, fallback, params) {
            var value = strings[key] || fallback;

            Object.keys(params || {}).forEach(function (name) {
                value = value.split('{' + name + '}').join(params[name] === undefined || params[name] === null ? '' : String(params[name]));
            });

            return value;
        }

        function kindLabel(kind) {
            return kindLabels[kind] || kind;
        }

        function showMessage(text) {
            if (!message) {
                return;
            }

            message.textContent = text || '';
            message.hidden = !text;
        }

        function sync() {
            textarea.value = ivr.toJson(model);
        }

        // Where a model field lives: "flow:rootNodeId", "fallback:kind", "node:0:prompt", "option:0:2:target".
        function parseField(field) {
            var parts = String(field || '').split(':');

            switch (parts[0]) {
                case 'flow':
                    return { scope: 'flow', name: parts[1] };
                case 'fallback':
                    return { scope: 'fallback', name: parts[1] };
                case 'node':
                    return { scope: 'node', node: Number(parts[1]), name: parts[2] };
                case 'option':
                    return { scope: 'option', node: Number(parts[1]), option: Number(parts[2]), name: parts[3] };
                default:
                    return null;
            }
        }

        function scopeOf(path) {
            if (path.fallback) {
                return 'fallback';
            }

            if (path.option !== undefined) {
                return 'option:' + path.node + ':' + path.option;
            }

            if (path.node !== undefined) {
                return 'node:' + path.node;
            }

            return 'flow';
        }

        function fieldOf(path) {
            return scopeOf(path) + ':' + (path.field || '');
        }

        function describe(entry) {
            var params = entry.params || {};
            var fallbacks = {
                maxRetriesInvalid: 'Callers need at least one try. Set the retries to 1 or more.',
                rootMissing: 'Choose the menu callers hear first.',
                rootNotFound: 'The first menu, "{nodeId}", does not exist.',
                nodeIdMissing: 'Give this menu a name.',
                nodeIdDuplicate: 'Another menu is already named "{nodeId}".',
                promptMissing: 'Enter what callers hear, or a recorded prompt, so this menu is not silent.',
                noOptions: 'Add at least one key, or callers can never leave this menu.',
                digitMissing: 'Choose a key.',
                digitInvalid: '"{digit}" is not a telephone key (0-9, * or #).',
                digitDuplicate: 'Key {digit} is already used on this menu.',
                actionMissing: 'Choose what this key does.',
                kindUnknown: '"{kind}" is not an action this editor knows.',
                targetMissing: 'Choose where this goes.',
                subMenuNotFound: 'The menu "{targetId}" does not exist.',
                targetUnknown: '"{targetId}" is not in the list. It may have been deleted or disabled.',
                unreachable: 'No key or fallback leads here, so callers never hear this menu.',
                renameRefused: 'Menu names must be unique and cannot be empty.'
            };

            return t('issue_' + entry.code, fallbacks[entry.code] || entry.code, params);
        }

        // Shows every problem beside the field that has it, and a count above the editor.
        function applyIssues() {
            var issues = ivr.validate(model, catalog);

            Object.keys(transientIssues).forEach(function (scope) {
                issues.push(transientIssues[scope]);
            });

            visual.querySelectorAll('[data-ivr-issues]').forEach(function (container) {
                container.replaceChildren();
                container.hidden = true;
            });

            visual.querySelectorAll('[data-ivr-field]').forEach(function (control) {
                control.classList.remove('is-invalid', 'border-warning');
                control.removeAttribute('aria-invalid');
            });

            var errors = 0;
            var warnings = 0;

            issues.forEach(function (entry) {
                var scope = scopeOf(entry.path);
                var container = visual.querySelector('[data-ivr-issues="' + scope + '"]');
                var control = visual.querySelector('[data-ivr-field="' + fieldOf(entry.path) + '"]');
                var isError = entry.severity === 'error';

                if (isError) {
                    errors++;
                } else {
                    warnings++;
                }

                if (control) {
                    control.classList.add(isError ? 'is-invalid' : 'border-warning');

                    if (isError) {
                        control.setAttribute('aria-invalid', 'true');
                    }
                }

                if (container) {
                    container.hidden = false;
                    container.appendChild(h('div', { className: isError ? 'text-danger' : 'text-warning-emphasis' }, [
                        icon(isError ? 'fa-solid fa-circle-exclamation me-1' : 'fa-solid fa-triangle-exclamation me-1'),
                        describe(entry)
                    ]));
                }
            });

            var summary = visual.querySelector('[data-ivr-summary]');

            if (summary) {
                summary.replaceChildren();
                summary.hidden = errors === 0 && warnings === 0;
                summary.className = 'alert py-2 mb-3 ' + (errors > 0 ? 'alert-danger' : 'alert-warning');

                if (errors > 0) {
                    summary.appendChild(h('div', { text: t('summaryErrors', '{count} problem(s) to fix. The entry point will not save until they are fixed.', { count: errors }) }));
                }

                if (warnings > 0) {
                    summary.appendChild(h('div', { text: t('summaryWarnings', '{count} warning(s) to review.', { count: warnings }) }));
                }
            }
        }

        function select(field, options, value, attributes) {
            var control = h('select', Object.assign({ className: 'form-select', 'data-ivr-field': field }, attributes || {}), options.map(function (option) {
                return h('option', { value: option.value, text: option.text, disabled: option.disabled });
            }));

            control.value = value;

            return control;
        }

        function withUnknown(options, value, unknownLabel) {
            if (value && !options.some(function (option) { return String(option.value) === value; })) {
                return options.concat([{ value: value, text: t(unknownLabel, 'Not found: {id}', { id: value }) }]);
            }

            return options;
        }

        // A menu as the caller reaches it: "Main menu", "Main menu > 2 > 1", or, for one no key opens, its stored name.
        function menuLabel(index) {
            var path = tree.paths[index];
            var main = t('mainMenu', 'Main menu');

            if (path) {
                return path.length ? [main].concat(path).join(' > ') : main;
            }

            return t('unusedMenu', 'Unused menu ({id})', { id: String((model.nodes[index] && model.nodes[index].nodeId) || '').trim() });
        }

        // The menus a key or the fallback can jump to, by where they are. The menu drawn under this very key is offered
        // first, as its own submenu.
        function menuOptions(ownChild) {
            var options = [];

            if (ownChild !== undefined && model.nodes[ownChild]) {
                options.push({ value: String(model.nodes[ownChild].nodeId || '').trim(), text: t('ownSubmenu', 'Its submenu, below') });
            }

            model.nodes.forEach(function (node, index) {
                var id = String(node.nodeId || '').trim();

                if (id && index !== ownChild) {
                    options.push({ value: id, text: t('goTo', 'Go to: {menu}', { menu: menuLabel(index) }) });
                }
            });

            return options;
        }

        // The target control for a kind: a list of queues, agents, destinations or menus, or nothing at all.
        function targetControl(field, action, id, ownChild) {
            var kind = action ? action.kind : '';
            var type = action ? ivr.targetTypeOf(kind) : null;
            var value = action ? String(action.targetId || '').trim() : '';

            if (!action) {
                return h('div', { className: 'form-text' });
            }

            if (!ivr.isKnownKind(kind)) {
                return h('input', { className: 'form-control', 'data-ivr-field': field, 'data-ivr-text': 'target', value: action.targetId || '', id: id, 'aria-label': t('target', 'Target') });
            }

            if (!type) {
                return h('div', { className: 'form-text pt-2', text: t('noTarget', 'No destination needed.') });
            }

            if (type === 'menu') {
                // A key can always be given a new submenu of its own; the fallback only jumps to a menu that exists.
                var isOption = String(field).indexOf('option:') === 0;

                return select(field, [{ value: '', text: t('chooseMenu', 'Choose a menu') }]
                    .concat(withUnknown(menuOptions(ownChild), value, 'unknownMenu'))
                    .concat(isOption ? [{ value: NEW_MENU, text: t('newSubmenu', '+ New submenu') }] : []), value, { id: id, 'aria-label': t('targetMenu', 'Menu to open') });
            }

            var list = catalog[type];
            var chooseLabels = {
                queue: t('chooseQueue', 'Choose a queue'),
                agent: t('chooseAgent', 'Choose an agent'),
                external: t('chooseExternal', 'Choose an approved destination')
            };

            // With nothing to pick from, the identifier can still be typed, and the checks warn that it is unknown.
            if (!list.length) {
                return h('input', {
                    className: 'form-control',
                    'data-ivr-field': field,
                    'data-ivr-text': 'target',
                    value: action.targetId || '',
                    id: id,
                    placeholder: t('typeTarget', 'Identifier'),
                    'aria-label': chooseLabels[type]
                });
            }

            return select(field, [{ value: '', text: chooseLabels[type] }].concat(withUnknown(list, value, 'unknownTarget')), value, { id: id, 'aria-label': chooseLabels[type] });
        }

        function kindOptions(current, emptyLabel) {
            var options = [{ value: '', text: emptyLabel }].concat(ivr.ACTION_KINDS.map(function (kind) {
                return { value: kind, text: kindLabel(kind) };
            }));

            if (current && !ivr.isKnownKind(current)) {
                options.push({ value: current, text: t('unknownKind', 'Unknown: {id}', { id: current }) });
            }

            return options;
        }

        function renderFlowSettings() {
            var retriesId = idPrefix + '-retries';
            var fallbackKindId = idPrefix + '-fallback-kind';
            var fallbackTargetId = idPrefix + '-fallback-target';
            var fallback = model.fallback;

            return h('div', { className: 'card mb-3' }, [
                h('div', { className: 'card-body' }, [
                    h('div', { className: 'row g-3' }, [
                        h('div', { className: 'col-md-3' }, [
                            h('label', { className: 'form-label', for: retriesId, text: t('maxRetries', 'Tries') }),
                            h('input', {
                                type: 'number',
                                min: '1',
                                max: '10',
                                step: '1',
                                className: 'form-control',
                                id: retriesId,
                                'data-ivr-field': 'flow:maxRetries',
                                'data-ivr-text': 'maxRetries',
                                value: model.maxRetries === null || model.maxRetries === undefined || (typeof model.maxRetries === 'number' && isNaN(model.maxRetries)) ? '' : String(model.maxRetries)
                            }),
                            h('div', { className: 'form-text', text: t('maxRetriesHint', 'Wrong or missing keys allowed before the fallback.') })
                        ]),
                        h('div', { className: 'col-md-9' }, [
                            h('label', { className: 'form-label', for: fallbackKindId, text: t('fallback', 'When the tries run out') }),
                            h('div', { className: 'row g-2' }, [
                                h('div', { className: 'col-sm-6' }, [
                                    select('fallback:kind', kindOptions(fallback && fallback.kind, t('noFallback', 'Route to the entry point target')), fallback ? fallback.kind : '', { id: fallbackKindId })
                                ]),
                                h('div', { className: 'col-sm-6' }, [targetControl('fallback:target', fallback, fallbackTargetId)])
                            ]),
                            h('div', { className: 'form-text', text: t('fallbackHint', 'With no fallback, the caller is routed the way this entry point routes calls with no menu.') })
                        ])
                    ]),
                    h('div', { className: 'small mt-2', 'data-ivr-issues': 'flow', hidden: true }),
                    h('div', { className: 'small mt-2', 'data-ivr-issues': 'fallback', hidden: true })
                ])
            ]);
        }

        function renderOption(node, nodeIndex, option, optionIndex) {
            var prefix = 'option:' + nodeIndex + ':' + optionIndex;
            var digitId = idPrefix + '-d-' + nodeIndex + '-' + optionIndex;
            var kindId = idPrefix + '-k-' + nodeIndex + '-' + optionIndex;
            var targetId = idPrefix + '-t-' + nodeIndex + '-' + optionIndex;
            var digit = String(option.digit || '').trim();
            var ownChild = tree.childOf(nodeIndex, optionIndex);
            var digits = ivr.availableDigits(node, optionIndex).map(function (key) {
                return { value: key, text: key };
            });

            if (digit && ivr.TELEPHONE_KEYS.indexOf(digit) === -1) {
                digits.push({ value: digit, text: digit });
            }

            if (digit && !digits.some(function (entry) { return entry.value === digit; })) {
                // A key another option also claims: keep it shown so the duplicate can be seen and fixed.
                digits.push({ value: digit, text: digit });
            }

            // The border and padding live on a wrapper: on the grid row itself, the gutter's negative top margin pulled
            // the controls up against the separator line.
            return h('div', { className: 'border-top py-2 ivr-option', 'data-ivr-option': prefix }, [h('div', { className: 'row g-2 align-items-center' }, [
                h('div', { className: 'col-4 col-md-2' }, [
                    h('label', { className: 'visually-hidden', for: digitId, text: t('key', 'Key') }),
                    select(prefix + ':digit', [{ value: '', text: '-' }].concat(digits), digit, { id: digitId, className: 'form-select font-monospace' })
                ]),
                h('div', { className: 'col-8 col-md-4' }, [
                    h('label', { className: 'visually-hidden', for: kindId, text: t('action', 'Action') }),
                    select(prefix + ':kind', kindOptions(option.action && option.action.kind, t('chooseAction', 'Choose an action')), option.action ? option.action.kind : '', { id: kindId })
                ]),
                h('div', { className: 'col-10 col-md-5' }, [
                    h('label', { className: 'visually-hidden', for: targetId, text: t('target', 'Target') }),
                    targetControl(prefix + ':target', option.action, targetId, ownChild)
                ]),
                h('div', { className: 'col-2 col-md-1 text-end' }, [
                    h('button', {
                        type: 'button',
                        className: 'btn btn-outline-danger btn-sm',
                        'data-ivr-command': 'remove-option',
                        'data-ivr-node': String(nodeIndex),
                        'data-ivr-option-index': String(optionIndex),
                        title: t('removeKey', 'Remove key {digit}', { digit: digit }),
                        'aria-label': t('removeKey', 'Remove key {digit}', { digit: digit })
                    }, [icon('fa-solid fa-trash')])
                ])
            ]), h('div', { className: 'small mt-1', 'data-ivr-issues': prefix, hidden: true }),
                ownChild === undefined ? null : h('div', { className: 'ivr-submenu ms-2 ms-md-4 ps-2 ps-md-3 border-start border-2 border-primary-subtle mt-2' }, [renderNode(model.nodes[ownChild], ownChild)])]);
        }

        function renderNode(node, nodeIndex) {
            var nodeId = String(node.nodeId || '').trim();
            var promptId = idPrefix + '-p-' + nodeIndex;
            var mediaId = idPrefix + '-m-' + nodeIndex;
            var canAddKey = ivr.availableDigits(node, -1).length > 0;

            var isUnused = tree.unused.indexOf(nodeIndex) >= 0;

            return h('div', { className: 'card mb-3', 'data-ivr-node-card': String(nodeIndex) }, [
                h('div', { className: 'card-header d-flex flex-wrap align-items-center gap-2' }, [
                    h('span', { className: 'fw-semibold', text: menuLabel(nodeIndex) }),
                    nodeIndex === tree.rootIndex
                        ? h('span', { className: 'badge text-bg-primary', title: t('rootBadgeHint', 'Callers hear this menu first.') }, [icon('fa-solid fa-play me-1'), t('rootBadge', 'First menu')])
                        : h('span', { className: 'badge ' + (isUnused ? 'text-bg-warning' : 'text-bg-secondary'), text: isUnused ? t('unusedBadge', 'Not used') : t('submenuBadge', 'Submenu') }),
                    nodeIndex === tree.rootIndex ? null : h('button', {
                        type: 'button',
                        className: 'btn btn-outline-danger btn-sm ms-auto',
                        'data-ivr-command': 'remove-menu',
                        'data-ivr-node': String(nodeIndex)
                    }, [icon('fa-solid fa-trash me-1'), t('removeSubmenu', 'Remove submenu')])
                ]),
                h('div', { className: 'card-body' }, [
                    h('div', { className: 'small mb-2', 'data-ivr-issues': 'node:' + nodeIndex, hidden: true }),
                    h('div', { className: 'mb-3' }, [
                        h('label', { className: 'form-label', for: promptId, text: t('prompt', 'What callers hear') }),
                        h('textarea', {
                            className: 'form-control',
                            rows: '2',
                            id: promptId,
                            'data-ivr-field': 'node:' + nodeIndex + ':prompt',
                            'data-ivr-text': 'prompt',
                            placeholder: t('promptPlaceholder', 'Press 1 for sales, 2 for support.')
                        }, [node.prompt || '']),
                        h('div', { className: 'form-text', text: t('promptHint', 'Spoken to the caller. Mention every key below.') })
                    ]),
                    h('div', { className: 'mb-3' }, [
                        h('label', { className: 'form-label', for: mediaId, text: t('promptMedia', 'Recorded prompt (optional)') }),
                        select('node:' + nodeIndex + ':promptMediaId', [{ value: '', text: t('speakPrompt', 'None: speak the text above') }]
                            .concat(withUnknown(catalog.media, String(node.promptMediaId || '').trim(), 'unknownTarget')), String(node.promptMediaId || '').trim(), { id: mediaId }),
                        h('div', { className: 'form-text', text: catalog.media.length
                            ? t('promptMediaHint', 'A voice media recording played instead of speaking the text above.')
                            : t('noMedia', 'No recordings yet. Upload one under Voice media to play it here.') })
                    ]),
                    h('div', { className: 'row g-2 small fw-semibold text-body-secondary d-none d-md-flex pb-1', 'aria-hidden': 'true' }, [
                        h('div', { className: 'col-md-2', text: t('key', 'Key') }),
                        h('div', { className: 'col-md-4', text: t('action', 'Action') }),
                        h('div', { className: 'col-md-5', text: t('target', 'Target') })
                    ]),
                    h('div', { 'data-ivr-field': 'node:' + nodeIndex + ':options' }, node.options.map(function (option, optionIndex) {
                        return renderOption(node, nodeIndex, option, optionIndex);
                    })),
                    h('button', {
                        type: 'button',
                        className: 'btn btn-outline-secondary btn-sm mt-2',
                        'data-ivr-command': 'add-option',
                        'data-ivr-node': String(nodeIndex),
                        disabled: !canAddKey,
                        title: canAddKey ? null : t('allKeysUsed', 'Every key on the keypad is already used.')
                    }, [icon('fa-solid fa-plus me-1'), t('addKey', 'Add key')])
                ])
            ]);
        }

        function renderEmpty() {
            return h('div', { className: 'card' }, [
                h('div', { className: 'card-body text-center py-4' }, [
                    h('div', { className: 'fs-3 text-body-secondary mb-2', 'aria-hidden': 'true' }, [icon('fa-solid fa-sitemap')]),
                    h('div', { className: 'fw-semibold', text: t('emptyTitle', 'No IVR menu') }),
                    h('div', { className: 'text-body-secondary small mb-3', text: t('emptyHint', 'Callers are routed straight to the target selected above.') }),
                    h('button', { type: 'button', className: 'btn btn-outline-primary btn-sm', 'data-ivr-command': 'build' }, [icon('fa-solid fa-plus me-1'), t('build', 'Build an IVR menu')])
                ])
            ]);
        }

        function captureFocus() {
            var active = document.activeElement;

            if (!active || !visual.contains(active)) {
                return null;
            }

            return {
                field: active.getAttribute('data-ivr-field'),
                command: active.getAttribute('data-ivr-command'),
                node: active.getAttribute('data-ivr-node')
            };
        }

        function restoreFocus(focus) {
            if (!focus) {
                return;
            }

            var target = null;

            if (focus.field) {
                target = visual.querySelector('[data-ivr-field="' + focus.field + '"]');
            } else if (focus.command) {
                target = visual.querySelector('[data-ivr-command="' + focus.command + '"]' + (focus.node !== null ? '[data-ivr-node="' + focus.node + '"]' : ''));
            }

            if (target && typeof target.focus === 'function') {
                target.focus();
            }
        }

        function render(focusField) {
            var focus = focusField ? { field: focusField } : captureFocus();
            var children = [];

            tree = ivr.buildMenuTree(model);

            if (model.nodes.length === 0) {
                children.push(renderEmpty());
            } else {
                children.push(h('div', { 'data-ivr-summary': true, role: 'status', 'aria-live': 'polite', hidden: true }));
                children.push(renderFlowSettings());

                if (tree.rootIndex !== undefined) {
                    children.push(renderNode(model.nodes[tree.rootIndex], tree.rootIndex));
                }

                if (tree.unused.length) {
                    children.push(h('div', { className: 'mt-4 mb-2' }, [
                        h('div', { className: 'fw-semibold', text: t('unusedMenus', 'Menus no key opens') }),
                        h('div', { className: 'form-text mt-0', text: t('unusedMenusHint', 'Callers never hear these. Point a key at one, or remove it.') })
                    ]));
                    tree.unused.forEach(function (index) {
                        children.push(renderNode(model.nodes[index], index));
                    });
                }

                children.push(h('div', { className: 'd-flex flex-wrap gap-2' }, [
                    h('button', { type: 'button', className: 'btn btn-outline-danger btn-sm ms-auto', 'data-ivr-command': 'clear' }, [icon('fa-solid fa-xmark me-1'), t('clear', 'Remove the IVR menu')])
                ]));
            }

            visual.replaceChildren.apply(visual, children);
            sync();
            applyIssues();
            restoreFocus(focus);
        }

        function actionAt(location) {
            if (location.scope === 'fallback') {
                return model.fallback;
            }

            var option = model.nodes[location.node] && model.nodes[location.node].options[location.option];

            return option ? option.action : null;
        }

        function setActionAt(location, action) {
            if (location.scope === 'fallback') {
                model.fallback = action;

                return;
            }

            var option = model.nodes[location.node] && model.nodes[location.node].options[location.option];

            if (option) {
                option.action = action;
            }
        }

        // Typing: the model and the posted JSON follow every keystroke, without redrawing under the cursor.
        function onInput(event) {
            var control = event.target;
            var location = parseField(control.getAttribute('data-ivr-field'));
            var kind = control.getAttribute('data-ivr-text');

            if (!location || !kind) {
                return;
            }

            if (kind === 'maxRetries') {
                model.maxRetries = control.value === '' ? '' : Number(control.value);
            } else if (kind === 'target') {
                var action = actionAt(location);

                if (action) {
                    action.targetId = control.value;
                }
            } else if (location.scope === 'node' && model.nodes[location.node]) {
                model.nodes[location.node][kind] = control.value;
            }

            sync();
            applyIssues();
        }

        function onChange(event) {
            var control = event.target;
            var field = control.getAttribute('data-ivr-field');
            var location = parseField(field);

            if (!location) {
                return;
            }

            if (control.getAttribute('data-ivr-text')) {
                // Already applied on input; a change only settles it.
                return;
            }

            if (location.scope === 'node' && location.name === 'nodeId') {
                delete transientIssues['node:' + location.node];

                if (!ivr.renameNode(model, location.node, control.value)) {
                    control.value = model.nodes[location.node].nodeId;
                    transientIssues['node:' + location.node] = { code: 'renameRefused', severity: 'error', path: { node: location.node, field: 'nodeId' }, params: {} };
                }

                render(field);

                return;
            }

            if (location.scope === 'option' && ((location.name === 'kind' && control.value === 'SubMenu') ||
                (location.name === 'target' && control.value === NEW_MENU))) {
                ivr.addSubMenu(model, location.node, location.option);
                render('node:' + (model.nodes.length - 1) + ':prompt');

                return;
            }

            if (location.scope === 'node' && location.name === 'promptMediaId') {
                model.nodes[location.node].promptMediaId = control.value;
            } else if (location.scope === 'flow' && location.name === 'rootNodeId') {
                model.rootNodeId = control.value;
            } else if (location.name === 'digit') {
                model.nodes[location.node].options[location.option].digit = control.value;
            } else if (location.name === 'kind') {
                var current = actionAt(location);

                if (!control.value) {
                    setActionAt(location, null);
                } else if (current) {
                    ivr.setActionKind(current, control.value);
                } else {
                    setActionAt(location, ivr.createAction(control.value));
                }
            } else if (location.name === 'target') {
                var action = actionAt(location);

                if (action) {
                    action.targetId = control.value;
                }
            }

            render(field);
        }

        function onClick(event) {
            var button = event.target.closest('[data-ivr-command]');

            if (!button || !visual.contains(button)) {
                return;
            }

            var command = button.getAttribute('data-ivr-command');
            var nodeIndex = Number(button.getAttribute('data-ivr-node'));

            switch (command) {
                case 'build':
                    model = ivr.createModel();
                    ivr.addNode(model);
                    render('node:0:prompt');
                    break;
                case 'remove-menu':
                    transientIssues = {};
                    ivr.removeSubMenu(model, nodeIndex);
                    render();
                    break;
                case 'add-option':
                    if (ivr.addOption(model.nodes[nodeIndex])) {
                        render('option:' + nodeIndex + ':' + (model.nodes[nodeIndex].options.length - 1) + ':kind');
                    }
                    break;
                case 'remove-option':
                    model.nodes[nodeIndex].options.splice(Number(button.getAttribute('data-ivr-option-index')), 1);
                    render();
                    break;
                case 'clear':
                    transientIssues = {};
                    model = ivr.createModel();
                    render();
                    break;
                default:
                    break;
            }
        }

        // The switch between the forms and the JSON. Leaving the JSON needs JSON the forms can show.
        function setAdvanced(advanced) {
            if (!advanced) {
                var result = ivr.parseJson(textarea.value);

                if (!result.ok) {
                    toggle.checked = true;
                    showMessage(t('jsonInvalid', 'The JSON cannot be shown in the visual editor until it is fixed or cleared: {error}', {
                        error: result.error === 'notAnObject' ? t('jsonNotAnObject', 'the menu must be a JSON object.') : result.error
                    }));
                    textarea.focus();

                    return;
                }

                model = result.model;
                transientIssues = {};
                showMessage('');
                jsonPanel.hidden = true;
                visual.hidden = false;
                render();

                return;
            }

            sync();
            showMessage('');
            visual.hidden = true;
            jsonPanel.hidden = false;
        }

        visual.addEventListener('input', onInput);
        visual.addEventListener('change', onChange);
        visual.addEventListener('click', onClick);
        toggle.addEventListener('change', function () {
            setAdvanced(toggle.checked);
        });

        // Start in the forms, unless the field holds JSON they cannot show (a rejected edit shown back as typed).
        var initial = ivr.parseJson(textarea.value);

        if (initial.ok) {
            model = initial.model;
            toggle.checked = false;
            jsonPanel.hidden = true;
            visual.hidden = false;

            // Leave the posted text exactly as the server sent it until the operator changes something.
            var original = textarea.value;
            render();
            textarea.value = original;
        } else {
            toggle.checked = true;
            visual.hidden = true;
            jsonPanel.hidden = false;
            showMessage(t('jsonInvalid', 'The JSON cannot be shown in the visual editor until it is fixed or cleared: {error}', { error: initial.error }));
        }

        root.removeAttribute('data-ivr-loading');
    }

    function boot() {
        document.querySelectorAll('[data-ivr-menu-editor]').forEach(init);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
}(window, document));
