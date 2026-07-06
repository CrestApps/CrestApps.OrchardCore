window.listManagementUI = (function () {
    'use strict';

    const defaultOptions = {
        clientSideSearch: true,
        selectedLabel: '',
        selectionEnabled: true,
        filterSubmit: false,
        itemInputName: 'itemIds',
        bulkActionInputName: 'Options.BulkAction',
        submitBulkActionName: 'submit.BulkAction',
        searchBoxSelector: '#search-box',
        searchResultSelector: '[data-filter-value]',
        searchDomSelector: '',
        searchAlertSelector: '#list-alert',
        emptyAlertSelector: '',
        submitFilterSelector: '#submitFilter',
        filterSubmitSelector: '.filter select, .filter input, .filter-options select, .filter-options input, .selectpicker[data-filter-submit], [data-list-filter-submit]',
        actionsSelector: '#actions',
        itemsSelector: '#items',
        filtersSelector: '.filter',
        selectedItemsSelector: '#selected-items',
        selectAllSelector: '#select-all',
        bulkActionSelector: '.dropdown-menu .dropdown-item[data-action]',
        singleResultActionSelector: '',
        searchFirstElementClasses: '',
        searchLastElementClasses: ''
    };

    const initializedAttribute = 'data-list-management-initialized';

    const ready = (callback) => {
        if (document.readyState !== 'loading') {
            callback();
            return;
        }

        document.addEventListener('DOMContentLoaded', callback, { once: true });
    };

    const parseBoolean = (value, fallback) => {
        if (value === undefined || value === null || value === '') {
            return fallback;
        }

        if (typeof value === 'boolean') {
            return value;
        }

        switch (String(value).toLowerCase()) {
            case 'true':
            case '1':
            case 'yes':
            case 'on':
                return true;
            case 'false':
            case '0':
            case 'no':
            case 'off':
                return false;
            default:
                return fallback;
        }
    };

    const splitClasses = (value) => (value || '')
        .split(/\s+/)
        .map((entry) => entry.trim())
        .filter(Boolean);

    const normalizeSearchText = (value) => {
        if (value === undefined || value === null) {
            return '';
        }

        return String(value)
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .toLowerCase()
            .trim();
    };

    const getElements = (root, selector) => {
        if (!selector) {
            return [];
        }

        try {
            return Array.from(root.querySelectorAll(selector));
        } catch (error) {
            console.warn('Invalid list management selector.', selector, error);
            return [];
        }
    };

    const getElement = (root, selector) => getElements(root, selector)[0] || null;

    const getScopedRoot = (root) => root && typeof root.querySelector === 'function'
        ? root
        : document;

    const getItemCheckboxes = (root, options) =>
        getElements(root, `input[type='checkbox'][name='${options.itemInputName}']`);

    const getCheckedItemCheckboxes = (root, options) =>
        getElements(root, `input[type='checkbox'][name='${options.itemInputName}']:checked`);

    const getSearchResults = (root, options) => getElements(root, options.searchResultSelector);

    const readOptionsFromElement = (root) => {
        if (!root || !root.dataset) {
            return {};
        }

        const clientSideSearch = parseBoolean(root.dataset.clientSideSearch, defaultOptions.clientSideSearch);

        return {
            selectedLabel: root.dataset.selectedLabel ?? defaultOptions.selectedLabel,
            clientSideSearch,
            selectionEnabled: parseBoolean(root.dataset.selectionEnabled, defaultOptions.selectionEnabled),
            filterSubmit: parseBoolean(root.dataset.filterSubmit, !clientSideSearch),
            itemInputName: root.dataset.itemInputName ?? defaultOptions.itemInputName,
            bulkActionInputName: root.dataset.bulkActionInputName ?? defaultOptions.bulkActionInputName,
            submitBulkActionName: root.dataset.submitBulkActionName ?? defaultOptions.submitBulkActionName,
            searchBoxSelector: root.dataset.searchBoxSelector ?? defaultOptions.searchBoxSelector,
            searchResultSelector: root.dataset.searchResultSelector ?? defaultOptions.searchResultSelector,
            searchDomSelector: root.dataset.searchDomSelector ?? root.dataset.searchTextSelector ?? defaultOptions.searchDomSelector,
            searchAlertSelector: root.dataset.searchAlertSelector ?? defaultOptions.searchAlertSelector,
            emptyAlertSelector: root.dataset.emptyAlertSelector ?? defaultOptions.emptyAlertSelector,
            submitFilterSelector: root.dataset.submitFilterSelector ?? defaultOptions.submitFilterSelector,
            filterSubmitSelector: root.dataset.filterSubmitSelector ?? defaultOptions.filterSubmitSelector,
            actionsSelector: root.dataset.actionsSelector ?? defaultOptions.actionsSelector,
            itemsSelector: root.dataset.itemsSelector ?? defaultOptions.itemsSelector,
            filtersSelector: root.dataset.filtersSelector ?? defaultOptions.filtersSelector,
            selectedItemsSelector: root.dataset.selectedItemsSelector ?? defaultOptions.selectedItemsSelector,
            selectAllSelector: root.dataset.selectAllSelector ?? defaultOptions.selectAllSelector,
            bulkActionSelector: root.dataset.bulkActionSelector ?? defaultOptions.bulkActionSelector,
            singleResultActionSelector: root.dataset.singleResultActionSelector ?? defaultOptions.singleResultActionSelector,
            searchFirstElementClasses: root.dataset.searchFirstElementClasses ?? defaultOptions.searchFirstElementClasses,
            searchLastElementClasses: root.dataset.searchLastElementClasses ?? defaultOptions.searchLastElementClasses
        };
    };

    const getFilterText = (element, options) => {
        let text = element.getAttribute('data-filter-value');

        if (options.searchDomSelector) {
            const searchTextNodes = getElements(element, options.searchDomSelector);

            if (searchTextNodes.length > 0) {
                text = searchTextNodes
                    .map((node) => node.textContent || '')
                    .join(' ');
            }
        }

        if (!text) {
            text = element.textContent || '';
        }

        return normalizeSearchText(text);
    };

    const getBoundaryElements = (root, options) => {
        const ignoredElements = getElements(root, '.ignore-elements');
        const searchResults = getSearchResults(root, options);

        return [...new Set([...ignoredElements, ...searchResults])];
    };

    const clearVisibleBoundaryClasses = (root, options) => {
        const elements = getBoundaryElements(root, options);
        const classesToRemove = [
            'first-child-visible',
            'last-child-visible',
            ...splitClasses(options.searchFirstElementClasses),
            ...splitClasses(options.searchLastElementClasses)
        ];

        elements.forEach((element) => {
            element.classList.remove(...classesToRemove);
        });
    };

    const hasVisibleIgnoredSibling = (element, siblingProperty) => {
        let sibling = element?.[siblingProperty] ?? null;

        while (sibling) {
            if (!sibling.classList.contains('d-none')) {
                return sibling.classList.contains('ignore-elements');
            }

            sibling = sibling[siblingProperty];
        }

        return false;
    };

    const applyVisibleBoundaryClasses = (root, visibleElements, options) => {
        clearVisibleBoundaryClasses(root, options);

        const firstElementClasses = splitClasses(options.searchFirstElementClasses);
        const lastElementClasses = splitClasses(options.searchLastElementClasses);

        if (visibleElements.length === 0) {
            const visibleIgnoredElements = getBoundaryElements(root, options)
                .filter((element) => element.classList.contains('ignore-elements') && !element.classList.contains('d-none'));

            if (visibleIgnoredElements.length > 0) {
                visibleIgnoredElements[0].classList.add('first-child-visible', ...firstElementClasses);
                visibleIgnoredElements[visibleIgnoredElements.length - 1].classList.add('last-child-visible', ...lastElementClasses);
            }

            return;
        }

        if (!hasVisibleIgnoredSibling(visibleElements[0], 'previousElementSibling')) {
            visibleElements[0].classList.add('first-child-visible', ...firstElementClasses);
        }

        if (!hasVisibleIgnoredSibling(visibleElements[visibleElements.length - 1], 'nextElementSibling')) {
            visibleElements[visibleElements.length - 1].classList.add('last-child-visible', ...lastElementClasses);
        }
    };

    const toggleSearchAlerts = (root, options, hasSearch, visibleCount) => {
        const searchAlert = getElement(root, options.searchAlertSelector);

        if (searchAlert) {
            searchAlert.classList.toggle('d-none', !hasSearch || visibleCount > 0);
        }

        const emptyAlert = getElement(root, options.emptyAlertSelector);

        if (emptyAlert && hasSearch) {
            emptyAlert.classList.add('d-none');
        }
    };

    const filterClientSideResults = (root, options, rawSearch) => {
        const search = normalizeSearchText(rawSearch);
        const results = getSearchResults(root, options);
        const visibleElements = [];

        clearVisibleBoundaryClasses(root, options);

        results.forEach((element) => {
            const isMatch = search === '' || getFilterText(element, options).includes(search);

            element.classList.toggle('d-none', !isMatch);

            if (isMatch) {
                visibleElements.push(element);
            }
        });

        applyVisibleBoundaryClasses(root, visibleElements, options);
        toggleSearchAlerts(root, options, search !== '', visibleElements.length);

        return visibleElements;
    };

    const getVisibleResults = (root, options) =>
        getSearchResults(root, options).filter((element) => !element.classList.contains('d-none'));

    const submitFilter = (root, options) => {
        const submitFilterButton = getElement(root, options.submitFilterSelector);

        if (submitFilterButton) {
            submitFilterButton.click();
        }
    };

    const initializeSearch = (root, options) => {
        const searchBox = getElement(root, options.searchBoxSelector);

        if (!searchBox) {
            return;
        }

        searchBox.addEventListener('keydown', (event) => {
            if (event.key === 'Escape') {
                searchBox.value = '';

                if (options.clientSideSearch) {
                    filterClientSideResults(root, options, '');
                }

                event.preventDefault();
                return;
            }

            if (event.key !== 'Enter') {
                return;
            }

            event.preventDefault();

            if (!options.clientSideSearch) {
                submitFilter(root, options);
                return;
            }

            const visibleResults = getVisibleResults(root, options);

            if (visibleResults.length === 1 && options.singleResultActionSelector) {
                const actionElement = getElement(visibleResults[0], options.singleResultActionSelector);

                if (actionElement) {
                    actionElement.click();
                }
            }
        });

        if (options.clientSideSearch) {
            const applySearch = () => {
                filterClientSideResults(root, options, searchBox.value);
            };

            searchBox.addEventListener('input', applySearch);
            applySearch();
        }
    };

    const initializeSelection = (root, options) => {
        if (!options.selectionEnabled) {
            return;
        }

        const actions = getElement(root, options.actionsSelector);
        const items = getElement(root, options.itemsSelector);
        const filters = getElements(root, options.filtersSelector);
        const selectedItems = getElement(root, options.selectedItemsSelector);
        const selectAllCtrl = getElement(root, options.selectAllSelector);
        const itemsCheckboxes = getItemCheckboxes(root, options);

        const updateSelectedItemsText = () => {
            if (!selectedItems) {
                return;
            }

            const selectedCount = getCheckedItemCheckboxes(root, options).length;
            const label = options.selectedLabel ? ` ${options.selectedLabel}` : '';

            selectedItems.textContent = `${selectedCount}${label}`;
        };

        const displayActionsOrFilters = () => {
            const checkedCount = getCheckedItemCheckboxes(root, options).length;
            const showActions = checkedCount > 1;

            if (actions) {
                actions.classList.toggle('d-none', !showActions);
            }

            filters.forEach((filterElement) => {
                filterElement.classList.toggle('d-none', showActions);
            });

            if (selectedItems) {
                selectedItems.classList.toggle('d-none', !showActions);
            }

            if (items) {
                items.classList.toggle('d-none', showActions);
            }
        };

        getElements(root, options.bulkActionSelector).forEach((item) => {
            item.addEventListener('click', () => {
                const checkedCheckboxes = getCheckedItemCheckboxes(root, options);

                if (checkedCheckboxes.length <= 1) {
                    return;
                }

                const actionData = Object.assign({}, item.dataset);

                confirmDialog({
                    ...actionData,
                    callback: (result) => {
                        if (!result) {
                            return;
                        }

                        const bulkActionInput = getElement(root, `[name='${options.bulkActionInputName}']`);
                        const submitBulkAction = getElement(root, `[name='${options.submitBulkActionName}']`);

                        if (bulkActionInput) {
                            bulkActionInput.value = actionData.action;
                        }

                        if (submitBulkAction) {
                            submitBulkAction.click();
                        }
                    }
                });
            });
        });

        if (selectAllCtrl) {
            selectAllCtrl.addEventListener('change', () => {
                itemsCheckboxes.forEach((checkbox) => {
                    checkbox.checked = selectAllCtrl.checked;
                });

                updateSelectedItemsText();
                displayActionsOrFilters();
            });
        }

        itemsCheckboxes.forEach((checkbox) => {
            checkbox.addEventListener('change', () => {
                const itemsCount = itemsCheckboxes.length;
                const selectedItemsCount = getCheckedItemCheckboxes(root, options).length;

                if (selectAllCtrl) {
                    selectAllCtrl.checked = selectedItemsCount === itemsCount;
                    selectAllCtrl.indeterminate = selectedItemsCount > 0 && selectedItemsCount < itemsCount;
                }

                updateSelectedItemsText();
                displayActionsOrFilters();
            });
        });

        updateSelectedItemsText();
        displayActionsOrFilters();
    };

    const initializeFilterSubmission = (root, options) => {
        if (!options.filterSubmit) {
            return;
        }

        getElements(root, options.filterSubmitSelector).forEach((element) => {
            element.addEventListener('change', () => submitFilter(root, options));
            element.addEventListener('changed.bs.select', () => submitFilter(root, options));
        });
    };

    const initializeRoot = (root, options) => {
        const scopedRoot = getScopedRoot(root);

        if (scopedRoot !== document && scopedRoot.hasAttribute(initializedAttribute)) {
            return scopedRoot;
        }

        const config = Object.assign({}, defaultOptions, readOptionsFromElement(scopedRoot), options);

        initializeSearch(scopedRoot, config);
        initializeSelection(scopedRoot, config);
        initializeFilterSubmission(scopedRoot, config);

        if (scopedRoot !== document) {
            scopedRoot.setAttribute(initializedAttribute, 'true');
        }

        return scopedRoot;
    };

    const initializeAll = (root) => {
        getElements(getScopedRoot(root), '[data-list-management]').forEach((element) => {
            initializeRoot(element);
        });
    };

    const initialize = (rootOrSelectedLabel, options) => {
        if (rootOrSelectedLabel && rootOrSelectedLabel.nodeType === 1) {
            return initializeRoot(rootOrSelectedLabel, options);
        }

        const overrides = typeof rootOrSelectedLabel === 'string'
            ? Object.assign({}, options, { selectedLabel: rootOrSelectedLabel })
            : options;

        return initializeRoot(document, overrides);
    };

    ready(() => initializeAll(document));

    return {
        initialize: initialize,
        initializeAll: initializeAll
    };
})();
