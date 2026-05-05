window.activityQueryBuilder = (function () {
    'use strict';

    const defaultConfig = {
        containerId: 'query-builder',
        addFilterButtonId: 'add-filter-btn',
        filterFormId: 'filter-form',
        groups: {}
    };

    let config = {};
    let activeFilters = [];
    let filterCounter = 0;

    function initialize(options) {
        config = Object.assign({}, defaultConfig, options);
        activeFilters = [];
        filterCounter = 0;

        buildDropdownMenu();
        restoreActiveFilters();
    }

    function buildDropdownMenu() {
        const btn = document.getElementById(config.addFilterButtonId);

        if (!btn) {
            return;
        }

        const dropdownMenu = btn.nextElementSibling;

        if (!dropdownMenu) {
            return;
        }

        dropdownMenu.innerHTML = '';

        for (const [groupKey, group] of Object.entries(config.groups)) {
            const header = document.createElement('li');
            header.innerHTML = '<h6 class="dropdown-header">' + escapeHtml(group.label) + '</h6>';
            dropdownMenu.appendChild(header);

            for (const [fieldKey, field] of Object.entries(group.fields)) {
                const item = document.createElement('li');
                const link = document.createElement('a');
                link.className = 'dropdown-item';
                link.href = 'javascript:void(0)';
                link.textContent = field.label;
                link.dataset.group = groupKey;
                link.dataset.field = fieldKey;

                link.addEventListener('click', function () {
                    addFilter(groupKey, fieldKey);
                });

                item.appendChild(link);
                dropdownMenu.appendChild(item);
            }
        }
    }

    function restoreActiveFilters() {
        const container = document.getElementById(config.containerId);

        if (!container) {
            return;
        }

        // Check for existing values in hidden inputs and show appropriate filters.
        for (const [groupKey, group] of Object.entries(config.groups)) {
            for (const [fieldKey, field] of Object.entries(group.fields)) {
                const existingInput = document.querySelector('[name="' + field.inputName + '"]');

                if (existingInput && existingInput.value) {
                    addFilter(groupKey, fieldKey, existingInput.value);
                } else if (field.type === 'multiselect') {
                    const existingInputs = document.querySelectorAll('[name="' + field.inputName + '"]');
                    const values = [];

                    existingInputs.forEach(function (input) {
                        if (input.value) {
                            values.push(input.value);
                        }
                    });

                    if (values.length > 0) {
                        addFilter(groupKey, fieldKey, values);
                    }
                }
            }
        }
    }

    function addFilter(groupKey, fieldKey, presetValue) {
        const group = config.groups[groupKey];

        if (!group) {
            return;
        }

        const field = group.fields[fieldKey];

        if (!field) {
            return;
        }

        const container = document.getElementById(config.containerId);

        if (!container) {
            return;
        }

        const filterId = 'filter-' + (filterCounter++);

        const filterRow = document.createElement('div');
        filterRow.className = 'query-builder-rule d-flex align-items-center gap-2 mb-2 p-2 border rounded bg-light';
        filterRow.id = filterId;
        filterRow.dataset.group = groupKey;
        filterRow.dataset.field = fieldKey;

        const badge = document.createElement('span');
        badge.className = 'badge text-bg-secondary';
        badge.textContent = group.label;
        filterRow.appendChild(badge);

        const label = document.createElement('span');
        label.className = 'fw-medium';
        label.textContent = field.label;
        filterRow.appendChild(label);

        const inputContainer = document.createElement('div');
        inputContainer.className = 'flex-grow-1';

        const input = createFieldInput(field, presetValue);
        inputContainer.appendChild(input);
        filterRow.appendChild(inputContainer);

        const removeBtn = document.createElement('button');
        removeBtn.type = 'button';
        removeBtn.className = 'btn btn-sm btn-outline-danger';
        removeBtn.innerHTML = '<i class="fa-solid fa-times"></i>';
        removeBtn.title = 'Remove filter';
        removeBtn.addEventListener('click', function () {
            removeFilter(filterId, field);
        });

        filterRow.appendChild(removeBtn);
        container.appendChild(filterRow);

        activeFilters.push({ id: filterId, group: groupKey, field: fieldKey });
    }

    function createFieldInput(field, presetValue) {
        switch (field.type) {
            case 'select':
                return createSelectInput(field, presetValue);
            case 'multiselect':
                return createMultiSelectInput(field, presetValue);
            case 'datetime':
                return createDateTimeInput(field, presetValue);
            case 'text':
                return createTextInput(field, presetValue);
            default:
                return createTextInput(field, presetValue);
        }
    }

    function createSelectInput(field, presetValue) {
        const select = document.createElement('select');
        select.className = 'form-select form-select-sm';
        select.name = field.inputName;

        if (field.options) {
            for (const opt of field.options) {
                const option = document.createElement('option');
                option.value = opt.value;
                option.textContent = opt.label;

                if (presetValue && opt.value === presetValue) {
                    option.selected = true;
                }

                select.appendChild(option);
            }
        }

        return select;
    }

    function createMultiSelectInput(field, presetValues) {
        const wrapper = document.createElement('div');
        wrapper.className = 'd-flex flex-wrap gap-1';

        if (field.options) {
            for (const opt of field.options) {
                const checkDiv = document.createElement('div');
                checkDiv.className = 'form-check form-check-inline';

                const checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'form-check-input';
                checkbox.name = field.inputName;
                checkbox.value = opt.value;
                checkbox.id = field.inputName.replace(/[\[\]\.]/g, '_') + '_' + opt.value;

                if (presetValues && Array.isArray(presetValues) && presetValues.includes(opt.value)) {
                    checkbox.checked = true;
                }

                const label = document.createElement('label');
                label.className = 'form-check-label';
                label.htmlFor = checkbox.id;
                label.textContent = opt.label;

                checkDiv.appendChild(checkbox);
                checkDiv.appendChild(label);
                wrapper.appendChild(checkDiv);
            }
        }

        return wrapper;
    }

    function createDateTimeInput(field, presetValue) {
        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-control form-control-sm';
        input.name = field.inputName;
        input.placeholder = field.placeholder || 'Select date...';

        if (presetValue) {
            input.value = presetValue;
        }

        // Initialize flatpickr if available.
        setTimeout(function () {
            if (typeof flatpickr !== 'undefined') {
                flatpickr(input, {
                    dateFormat: field.dateFormat || 'Y-m-d',
                    allowInput: true
                });
            }
        }, 0);

        return input;
    }

    function createTextInput(field, presetValue) {
        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'form-control form-control-sm';
        input.name = field.inputName;
        input.placeholder = field.placeholder || '';

        if (presetValue) {
            input.value = presetValue;
        }

        return input;
    }

    function removeFilter(filterId, field) {
        const filterRow = document.getElementById(filterId);

        if (filterRow) {
            filterRow.remove();
        }

        activeFilters = activeFilters.filter(function (f) { return f.id !== filterId; });

        // Clear hidden inputs for this field.
        const inputs = document.querySelectorAll('[name="' + field.inputName + '"]');

        inputs.forEach(function (input) {
            if (input.closest('#' + config.containerId) === null) {
                input.value = '';
            }
        });
    }

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.appendChild(document.createTextNode(text));

        return div.innerHTML;
    }

    return {
        initialize: initialize
    };
})();
