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
