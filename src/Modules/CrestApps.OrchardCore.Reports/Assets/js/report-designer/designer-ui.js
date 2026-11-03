/*
 * DOM and server helpers of the report builder: element building that never interprets text as HTML, the
 * antiforgery-aware JSON client, debouncing, and Bootstrap modal handling.
 */
(function (root) {
    'use strict';

    var designer = root.CrestAppsReportDesigner = root.CrestAppsReportDesigner || {};
    var ui = designer.ui = {};

    // Builds an element. Attributes starting with "on" become listeners; "dataset" and "style" take objects; text
    // children become text nodes, so user data is never parsed as HTML.
    ui.h = function (tag, attrs) {
        var element = root.document.createElement(tag);
        var children = Array.prototype.slice.call(arguments, 2);

        Object.keys(attrs || {}).forEach(function (name) {
            var value = attrs[name];

            if (value === null || value === undefined || value === false) {
                return;
            }

            if (name.indexOf('on') === 0 && typeof value === 'function') {
                element.addEventListener(name.slice(2).toLowerCase(), value);
            } else if (name === 'dataset') {
                Object.keys(value).forEach(function (key) {
                    element.dataset[key] = value[key];
                });
            } else if (name === 'style' && typeof value === 'object') {
                Object.keys(value).forEach(function (key) {
                    element.style[key] = value[key];
                });
            } else if (name === 'className') {
                element.className = value;
            } else if (name === 'value') {
                element.value = value;
            } else if (name === 'checked' || name === 'selected' || name === 'disabled' || name === 'multiple') {
                element[name] = !!value;
            } else if (value === true) {
                element.setAttribute(name, '');
            } else {
                element.setAttribute(name, String(value));
            }
        });

        ui.append(element, children);

        return element;
    };

    // Appends children: elements as they are, and anything else (text, numbers) as a text node, so text is never parsed
    // as HTML. Text and numbers are handled first, so only objects (the nodes this script built) reach appendChild.
    ui.append = function (element, children) {
        (children || []).forEach(function (child) {
            if (child === null || child === undefined || child === false) {
                return;
            }

            if (typeof child !== 'object') {
                element.appendChild(root.document.createTextNode(String(child)));

                return;
            }

            if (Array.isArray(child)) {
                ui.append(element, child);
            } else if (child instanceof root.Node) {
                element.appendChild(child);
            } else {
                element.appendChild(root.document.createTextNode(String(child)));
            }
        });

        return element;
    };

    // Keeps a URL on this site. The builder only navigates to the root-relative paths the server gives it; anything
    // else, such as another site's address or a script URL, becomes a harmless path on this site, and characters a URL
    // cannot hold as they are (such as markup) are encoded. The server's paths need no encoding, so they are unchanged.
    ui.localUrl = function (url) {
        if (!url) {
            return '';
        }

        var path = String(url).replace(/^[\s\\/]+/, '');

        return encodeURI('/' + path);
    };

    // Turns the drop-down lists of server-rendered HTML the builder inserted, such as a preview's filters, into
    // searchable pickers. Pages initialise their own lists on load; inserted HTML needs this.
    ui.initPickers = function (container) {
        if (!container || !root.Selectpicker || typeof root.Selectpicker.getOrCreateInstance !== 'function') {
            return;
        }

        container.querySelectorAll('select.selectpicker').forEach(function (select) {
            root.Selectpicker.getOrCreateInstance(select);
        });
    };

    ui.clear = function (element) {
        while (element && element.firstChild) {
            element.removeChild(element.firstChild);
        }

        return element;
    };

    ui.select = function (options, value, attrs) {
        var select = ui.h('select', Object.assign({ className: 'form-select form-select-sm' }, attrs || {}));

        options.forEach(function (option) {
            var item = typeof option === 'string' ? { value: option, text: option } : option;
            var element = ui.h('option', { value: item.value == null ? '' : item.value }, item.text);

            if (String(item.value == null ? '' : item.value) === String(value == null ? '' : value)) {
                element.selected = true;
            }

            select.appendChild(element);
        });

        return select;
    };

    ui.icon = function (name) {
        return ui.h('i', { className: 'fa-solid ' + name, 'aria-hidden': 'true' });
    };

    ui.debounce = function (action, delay) {
        var timer = null;

        var debounced = function () {
            var args = arguments;

            root.clearTimeout(timer);
            timer = root.setTimeout(function () {
                action.apply(null, args);
            }, delay);
        };

        debounced.cancel = function () {
            root.clearTimeout(timer);
        };

        return debounced;
    };

    function token() {
        var input = root.document.querySelector('input[name="__RequestVerificationToken"]');

        return input ? input.value : '';
    }

    ui.request = function (url, options) {
        var settings = options || {};
        var headers = { 'Accept': settings.html ? 'text/html' : 'application/json' };
        var init = {
            method: settings.method || 'GET',
            headers: headers,
            credentials: 'same-origin'
        };

        // A request sent while the page unloads must outlive it.
        if (settings.keepalive) {
            init.keepalive = true;
        }

        if (settings.body !== undefined) {
            init.method = settings.method || 'POST';
            init.body = JSON.stringify(settings.body);
            headers['Content-Type'] = 'application/json';
        }

        if (init.method !== 'GET') {
            headers['RequestVerificationToken'] = token();
        }

        return root.fetch(url, init).then(function (response) {
            if (!response.ok) {
                var error = new Error('Request failed with status ' + response.status + '.');

                error.status = response.status;

                // Keep a JSON answer, such as who changed a report on a 409, for the caller.
                return response.text().then(function (text) {
                    try {
                        error.body = text ? JSON.parse(text) : null;
                    } catch (e) {
                        error.body = null;
                    }

                    throw error;
                }, function () {
                    throw error;
                });
            }

            if (response.status === 204) {
                return null;
            }

            return settings.html ? response.text() : response.json();
        });
    };

    var openMenu = null;

    ui.closeMenu = function () {
        if (openMenu) {
            openMenu();
            openMenu = null;
        }
    };

    // Moves a flyout that is showing so it stays in the window: up when it would pass the bottom, and to the left of its
    // item when it would pass the right edge.
    ui.keepSubmenuOnScreen = function (submenu) {
        submenu.style.top = '';
        submenu.classList.remove('opens-left');

        var box = submenu.getBoundingClientRect();

        if (!box.height) {
            return;
        }

        if (box.bottom > root.innerHeight - 4) {
            submenu.style.top = (submenu.offsetTop - (box.bottom - root.innerHeight + 4)) + 'px';
        }

        if (box.right > root.innerWidth - 4) {
            submenu.classList.add('opens-left');
        }
    };

    // Opens a context menu at a point of the page, such as where a field was right-clicked. Each item has a label and an
    // action, or items of its own that open beside it; { divider: true } draws a line and { header: text } a heading.
    // A checked item shows a check. The menu closes on a click elsewhere, on Escape, and when an action runs.
    ui.menu = function (items, x, y) {
        var build = function (entries, submenu) {
            var list = ui.h('div', { className: 'dropdown-menu rd-menu shadow-sm' + (submenu ? ' rd-submenu' : ' show'), role: 'menu' });

            entries.forEach(function (entry) {
                if (!entry) {
                    return;
                }

                if (entry.divider) {
                    list.appendChild(ui.h('div', { className: 'dropdown-divider', role: 'separator' }));

                    return;
                }

                if (entry.header) {
                    list.appendChild(ui.h('div', { className: 'dropdown-header text-truncate' }, entry.header));

                    return;
                }

                var hasItems = entry.items && entry.items.length;
                var button = ui.h('button', {
                    type: 'button',
                    className: 'dropdown-item d-flex align-items-center gap-2' + (hasItems ? ' rd-menu-parent' : ''),
                    role: 'menuitem',
                    disabled: !!entry.disabled,
                    'aria-haspopup': hasItems ? 'true' : null,
                    'aria-expanded': hasItems ? 'false' : null,
                    onclick: function (event) {
                        event.stopPropagation();

                        if (hasItems) {
                            var open = item.classList.toggle('is-open');

                            button.setAttribute('aria-expanded', open ? 'true' : 'false');
                            ui.keepSubmenuOnScreen(submenu);

                            return;
                        }

                        ui.closeMenu();
                        entry.action();
                    }
                },
                ui.h('span', { className: 'rd-menu-check' }, entry.checked ? ui.icon('fa-check') : (entry.icon ? ui.icon(entry.icon) : null)),
                ui.h('span', { className: 'flex-grow-1' }, entry.label),
                hasItems ? ui.icon('fa-caret-right small') : null);
                var submenu = hasItems ? build(entry.items, true) : null;
                var item = ui.h('div', {
                    className: 'rd-menu-item',
                    // A flyout that would leave the window opens upward or to the left instead.
                    onmouseenter: submenu ? function () {
                        ui.keepSubmenuOnScreen(submenu);
                    } : null,
                    onfocusin: submenu ? function () {
                        ui.keepSubmenuOnScreen(submenu);
                    } : null
                }, button, submenu);

                list.appendChild(item);
            });

            return list;
        };

        return ui.popup(build(items, false), x, y);
    };

    // Shows an element, such as a menu or a color palette, floating at a point of the page, kept on screen. It closes on
    // a click elsewhere, on Escape, when the window is resized, and when another popup opens (ui.closeMenu).
    ui.popup = function (element, x, y) {
        ui.closeMenu();

        element.style.position = 'fixed';
        element.style.left = x + 'px';
        element.style.top = y + 'px';
        element.style.zIndex = '1080';
        root.document.body.appendChild(element);

        var box = element.getBoundingClientRect();

        if (box.right > root.innerWidth) {
            element.style.left = Math.max(4, root.innerWidth - box.width - 4) + 'px';
        }

        if (box.bottom > root.innerHeight) {
            element.style.top = Math.max(4, root.innerHeight - box.height - 4) + 'px';
        }

        var onPointer = function (event) {
            if (!element.contains(event.target)) {
                ui.closeMenu();
            }
        };
        var onKey = function (event) {
            if (event.key === 'Escape') {
                ui.closeMenu();
            }
        };

        root.setTimeout(function () {
            root.document.addEventListener('pointerdown', onPointer, true);
        }, 0);
        root.document.addEventListener('keydown', onKey);
        root.addEventListener('resize', ui.closeMenu);

        openMenu = function () {
            root.document.removeEventListener('pointerdown', onPointer, true);
            root.document.removeEventListener('keydown', onKey);
            root.removeEventListener('resize', ui.closeMenu);
            element.remove();
        };

        var first = element.querySelector('button:not([disabled]), input');

        if (first) {
            first.focus();
        }

        return element;
    };

    ui.modal = function (title, body, footer, size) {
        var dialog = ui.h('div', { className: 'modal fade', tabindex: '-1', role: 'dialog', 'aria-modal': 'true' },
            ui.h('div', { className: 'modal-dialog modal-dialog-scrollable ' + (size || 'modal-lg') },
                ui.h('div', { className: 'modal-content' },
                    ui.h('div', { className: 'modal-header' },
                        ui.h('h5', { className: 'modal-title' }, title),
                        ui.h('button', { type: 'button', className: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Close' })),
                    ui.h('div', { className: 'modal-body' }, body),
                    footer ? ui.h('div', { className: 'modal-footer' }, footer) : null)));

        root.document.body.appendChild(dialog);

        var instance = root.bootstrap && root.bootstrap.Modal
            ? root.bootstrap.Modal.getOrCreateInstance(dialog)
            : null;

        dialog.addEventListener('hidden.bs.modal', function () {
            if (instance) {
                instance.dispose();
            }

            dialog.remove();
        });

        if (instance) {
            instance.show();
        } else {
            dialog.classList.add('show');
            dialog.style.display = 'block';
        }

        return {
            element: dialog,
            close: function () {
                if (instance) {
                    instance.hide();
                } else {
                    dialog.remove();
                }
            }
        };
    };
})(typeof window !== 'undefined' ? window : globalThis);
