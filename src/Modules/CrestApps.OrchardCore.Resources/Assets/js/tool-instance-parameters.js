// Drives the tool instance parameter editor: add, remove, and reindex rows, show only the fields that
// apply to the selected fill mode, keep placements the source forbids out of reach, and preview the
// request the configured parameters will produce.
(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        const list = document.getElementById('parametersList');
        const template = document.getElementById('parameterRowTemplate');

        if (!list || !template) {
            return;
        }

        const emptyState = document.getElementById('parametersEmpty');
        const addButton = document.getElementById('addParameter');
        const previewRequest = document.getElementById('parameterPreviewRequest');
        const previewNotes = document.getElementById('parameterPreviewNotes');
        const sourceSelect = document.getElementById('sourceSelect');

        let reservedMap = {};

        try {
            reservedMap = JSON.parse(list.dataset.reservedMap || '{}');
        } catch (error) {
            reservedMap = {};
        }

        function selectedSource() {
            return sourceSelect ? sourceSelect.value : '';
        }

        function reservedNames() {
            const names = reservedMap[selectedSource()] || [];

            return names.map(function (name) {
                return name.toLowerCase();
            });
        }

        const presets = {
            query: { name: 'search', type: 'String', fill: 'Model', binding: 'Query', description: 'The value to filter by.' },
            path: { name: 'id', type: 'String', fill: 'Model', binding: 'Path', required: true, description: 'The identifier to look up.' },
            user: { name: 'userId', type: 'String', fill: 'Context', binding: 'Query', context: 'user.id' }
        };

        function rows() {
            return Array.from(list.querySelectorAll('.parameter-row'));
        }

        function reindex() {
            rows().forEach(function (row, index) {
                row.querySelectorAll('[name]').forEach(function (field) {
                    field.name = field.name.replace(/Parameters\[[^\]]*\]/, 'Parameters[' + index + ']');
                });
            });

            if (emptyState) {
                emptyState.style.display = rows().length === 0 ? '' : 'none';
            }
        }

        // Only the fields that belong to the selected fill mode stay in the DOM flow, so a row never asks
        // for a description the model will not see or a value the server will not read.
        function applyFill(row) {
            const fill = row.querySelector('.param-fill');
            const value = fill ? fill.value : 'Model';

            row.querySelectorAll('.fill-model').forEach(function (element) {
                element.style.display = value === 'Model' ? '' : 'none';
            });
            row.querySelectorAll('.fill-fixed').forEach(function (element) {
                element.style.display = value === 'Fixed' ? '' : 'none';
            });
            row.querySelectorAll('.fill-context').forEach(function (element) {
                element.style.display = value === 'Context' ? '' : 'none';
            });

            // A placement the source refuses for this fill mode is disabled rather than silently rejected
            // on save. The clearest example is a header the model would otherwise be able to set.
            const binding = row.querySelector('.param-binding');
            const source = selectedSource();

            if (binding) {
                Array.from(binding.options).forEach(function (option) {
                    if (!option.value) {
                        return;
                    }

                    // Options belonging to another source are hidden outright; the remaining ones are
                    // disabled when the source refuses this fill mode.
                    const belongsToSource = !option.dataset.source || option.dataset.source === source;

                    option.hidden = !belongsToSource;

                    if (!belongsToSource) {
                        option.disabled = true;

                        if (binding.value === option.value) {
                            binding.value = '';
                        }

                        return;
                    }

                    const fills = (option.dataset.fills || '').split(',').filter(Boolean);
                    const allowed = fills.length === 0 || fills.indexOf(value) >= 0;

                    option.disabled = !allowed;
                    option.title = allowed
                        ? (option.dataset.hint || option.title)
                        : 'Not available when this parameter is filled by ' + value.toLowerCase() + '.';

                    if (!allowed && binding.value === option.value) {
                        binding.value = '';
                    }
                });
            }

            const secret = row.querySelector('.param-secret');
            const fixedValue = row.querySelector('.param-fixed-value');

            if (secret && fixedValue) {
                fixedValue.type = secret.checked ? 'password' : 'text';
            }
        }

        function validateRow(row) {
            const nameField = row.querySelector('.param-name');

            if (!nameField) {
                return;
            }

            const name = (nameField.value || '').trim();
            let message = '';

            if (name && !/^[A-Za-z_][A-Za-z0-9_]{0,63}$/.test(name)) {
                message = 'Use letters, digits, and underscores, starting with a letter or underscore.';
            } else if (name && reservedNames().indexOf(name.toLowerCase()) >= 0) {
                message = "'" + name + "' is reserved by this source.";
            } else if (name && rows().filter(function (other) {
                const otherName = other.querySelector('.param-name');

                return otherName && otherName !== nameField && (otherName.value || '').trim().toLowerCase() === name.toLowerCase();
            }).length > 0) {
                message = 'Parameter names must be unique.';
            }

            nameField.classList.toggle('is-invalid', message !== '');
            nameField.setCustomValidity(message);
            nameField.title = message;
        }

        function describeValue(row) {
            const fill = row.querySelector('.param-fill');
            const name = (row.querySelector('.param-name') || {}).value || 'value';

            if (!fill || fill.value === 'Model') {
                return '{' + name + '}';
            }

            if (fill.value === 'Fixed') {
                const secret = row.querySelector('.param-secret');

                if (secret && secret.checked) {
                    return '••••';
                }

                return ((row.querySelector('.param-fixed-value') || {}).value || '').trim() || '{' + name + '}';
            }

            const context = row.querySelector('.param-context');

            return '<' + (context && context.value ? context.value : 'context') + '>';
        }

        function updatePreview() {
            if (!previewRequest) {
                return;
            }

            const method = (document.getElementById('HttpMethod') || {}).value || 'GET';
            const baseUrl = ((document.getElementById('BaseUrl') || {}).value || 'https://api.example.com').replace(/\/+$/, '');
            let path = ((document.getElementById('PathTemplate') || {}).value || '').trim();

            const queryParts = [];
            const headerParts = [];
            const bodyParts = [];
            const modelSees = [];

            rows().forEach(function (row) {
                const name = ((row.querySelector('.param-name') || {}).value || '').trim();

                if (!name) {
                    return;
                }

                const binding = row.querySelector('.param-binding');
                const target = binding ? binding.value : '';
                const bindingName = ((row.querySelector('.param-binding-name') || {}).value || '').trim() || name;
                const value = describeValue(row);
                const fill = row.querySelector('.param-fill');

                if (!fill || fill.value === 'Model') {
                    const required = row.querySelector('.param-required');
                    modelSees.push(name + (required && required.checked ? ' (required)' : ''));
                }

                if (target === 'Query') {
                    queryParts.push(bindingName + '=' + value);
                } else if (target === 'Header') {
                    headerParts.push(bindingName + ': ' + value);
                } else if (target === 'Body') {
                    bodyParts.push(bindingName + '=' + value);
                } else if (target === 'Path') {
                    path = path.replace('{' + bindingName + '}', value);
                }
            });

            let url = baseUrl + (path ? '/' + path.replace(/^\/+/, '') : '');

            if (queryParts.length > 0) {
                url += '?' + queryParts.join('&');
            }

            previewRequest.textContent = method + ' ' + url;

            const notes = [];

            if (headerParts.length > 0) {
                notes.push('Headers: ' + headerParts.join(', '));
            }

            if (bodyParts.length > 0) {
                notes.push('Body: ' + bodyParts.join(', '));
            }

            notes.push(modelSees.length > 0
                ? 'The model fills: ' + modelSees.join(', ')
                : 'The model fills no declared parameters.');

            if (previewNotes) {
                previewNotes.textContent = notes.join(' · ');
            }
        }

        function refresh() {
            rows().forEach(function (row) {
                applyFill(row);
                validateRow(row);
            });

            updatePreview();
        }

        function addRow(preset) {
            const fragment = template.content.cloneNode(true);
            const row = fragment.querySelector('.parameter-row');

            list.appendChild(fragment);
            reindex();

            if (preset) {
                const set = function (selector, value) {
                    const field = row.querySelector(selector);

                    if (field && value !== undefined) {
                        field.value = value;
                    }
                };

                set('.param-name', preset.name);
                set('.param-type', preset.type);
                set('.param-fill', preset.fill);
                set('.param-binding', preset.binding);
                set('.param-context', preset.context);
                set('input[name$="].Description"]', preset.description);

                const required = row.querySelector('.param-required');

                if (required && preset.required) {
                    required.checked = true;
                }
            }

            refresh();

            const nameField = row.querySelector('.param-name');

            if (nameField) {
                nameField.focus();
            }
        }

        if (addButton) {
            addButton.addEventListener('click', function () {
                addRow(null);
            });
        }

        document.querySelectorAll('.preset-parameter').forEach(function (button) {
            button.addEventListener('click', function () {
                addRow(presets[button.dataset.preset]);
            });
        });

        list.addEventListener('click', function (event) {
            const remove = event.target.closest('.remove-parameter');

            if (remove) {
                const row = remove.closest('.parameter-row');

                if (row) {
                    row.remove();
                    reindex();
                    refresh();
                }

                return;
            }

            const advanced = event.target.closest('.toggle-advanced');

            if (advanced) {
                event.preventDefault();

                const fields = advanced.parentElement.querySelector('.advanced-fields');

                if (fields) {
                    fields.style.display = fields.style.display === 'none' ? '' : 'none';
                }
            }
        });

        list.addEventListener('change', refresh);
        list.addEventListener('input', refresh);

        if (sourceSelect) {
            sourceSelect.addEventListener('change', refresh);
        }

        ['BaseUrl', 'PathTemplate', 'HttpMethod'].forEach(function (id) {
            const field = document.getElementById(id);

            if (field) {
                field.addEventListener('input', updatePreview);
                field.addEventListener('change', updatePreview);
            }
        });

        reindex();
        refresh();
    });
})();
