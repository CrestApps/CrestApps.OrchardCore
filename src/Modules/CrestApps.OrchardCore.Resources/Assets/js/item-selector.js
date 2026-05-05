window.itemSelector = (() => {
    const initializedAttribute = 'data-item-selector-initialized';

    const escapeHtml = (value) => {
        return String(value ?? '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    };

    const debounce = (callback, delay) => {
        let timeoutId;

        return (...args) => {
            window.clearTimeout(timeoutId);
            timeoutId = window.setTimeout(() => callback(...args), delay);
        };
    };

    const parseConfiguration = (root) => {
        const configurationNode = root.querySelector('.item-selector-config');

        if (!configurationNode) {
            throw new Error('Missing item selector configuration.');
        }

        return JSON.parse(configurationNode.textContent);
    };

    const buildUrl = (endpoint, query) => {
        const url = new URL(endpoint, window.location.origin);

        if (query) {
            url.searchParams.set('query', query);
        }
        else {
            url.searchParams.delete('query');
        }

        return url.toString();
    };

    const normalizeItem = (item) => {
        if (!item || !item.value) {
            return null;
        }

        return {
            value: item.value,
            text: item.text ?? item.value,
            secondaryText: item.secondaryText ?? '',
            isEnabled: item.isEnabled !== false,
            selected: item.selected === true
        };
    };

    const formatText = (template, value) => {
        return String(template ?? '').replace('{0}', value);
    };

    const initialize = (root) => {
        if (!root || root.getAttribute(initializedAttribute) === 'true') {
            return;
        }

        const configuration = parseConfiguration(root);
        const hiddenInputs = root.querySelector('.item-selector-hidden-inputs');
        const selectedContainer = root.querySelector('.item-selector-selected');
        const selectedItemsContainer = root.querySelector('.item-selector-selected-items');
        const selectionSummary = root.querySelector('.item-selector-selection-summary');
        const selectionSummaryLabel = selectionSummary?.querySelector('.item-selector-section-title');
        const menuTitle = root.querySelector('.item-selector-menu-title');
        const toggleButton = root.querySelector('.item-selector-toggle');
        const closeButton = root.querySelector('.item-selector-close');
        const menu = root.querySelector('.item-selector-menu');
        const searchContainer = root.querySelector('.item-selector-search');
        const searchInput = root.querySelector('.item-selector-search-input');
        const searchButton = root.querySelector('.item-selector-search-button');
        const actions = root.querySelector('.item-selector-actions');
        const selectAllButton = root.querySelector('.item-selector-select-all');
        const deselectAllButton = root.querySelector('.item-selector-deselect-all');
        const availableItemsLabel = root.querySelector('.item-selector-available-label');
        const status = root.querySelector('.item-selector-status');
        const options = root.querySelector('.item-selector-options');

        const knownItems = new Map();
        const selectedValues = [];
        let initialLoadCompleted = false;
        let isLoading = false;
        let lastRequestedQuery = null;
        let activeRequestId = 0;

        const isSelected = (value) => {
            return selectedValues.includes(value);
        };

        const cacheItems = (items) => {
            items
                .map(normalizeItem)
                .filter(Boolean)
                .forEach((item) => {
                    knownItems.set(item.value, item);
                });
        };

        const setStatus = (message, isVisible = true) => {
            if (!status) {
                return;
            }

            status.textContent = message ?? '';
            status.classList.toggle('d-none', !isVisible || !message);
        };

        const showMenu = () => {
            menu.classList.add('show');
            toggleButton.setAttribute('aria-expanded', 'true');
        };

        const hideMenu = () => {
            menu.classList.remove('show');
            toggleButton.setAttribute('aria-expanded', 'false');
        };

        const updateToggleText = () => {
            const selectedCount = selectedValues.length;

            if (selectedCount === 0) {
                toggleButton.textContent = configuration.buttonText;
                return;
            }

            if (!configuration.multiple) {
                const item = knownItems.get(selectedValues[0]);
                toggleButton.textContent = item?.text ?? configuration.buttonText;
                return;
            }

            toggleButton.textContent = `${configuration.buttonText} (${selectedCount})`;
        };

        const renderHiddenInputs = () => {
            hiddenInputs.innerHTML = '';

            selectedValues.forEach((value) => {
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = configuration.inputName;
                input.value = value;
                hiddenInputs.appendChild(input);
            });
        };

        const createChipHtml = (item) => {
            return `
<span class="badge text-bg-secondary item-selector-chip">
    <span class="item-selector-chip-label">${escapeHtml(item.text)}</span>
    <button type="button" class="btn-close btn-close-white ms-2 item-selector-remove" aria-label="Remove" data-value="${escapeHtml(item.value)}"></button>
</span>`;
        };

        const wireRemoveButtons = (container) => {
            container.querySelectorAll('.item-selector-remove').forEach((button) => {
                button.addEventListener('click', (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    removeValue(button.dataset.value);
                });
            });
        };

        const renderSelected = () => {
            const selectedItems = selectedValues
                .map((value) => knownItems.get(value))
                .filter(Boolean);

            const selectedHtml = selectedItems.length === 0
                ? `<span class="text-muted small item-selector-placeholder">${escapeHtml(configuration.noSelectionText)}</span>`
                : selectedItems.map(createChipHtml).join('');

            selectedItemsContainer.innerHTML = selectedHtml;
            wireRemoveButtons(selectedItemsContainer);

            if (!configuration.showSelectedItems) {
                selectedContainer.classList.add('d-none');
                return;
            }

            selectedContainer.classList.remove('d-none');
            selectedContainer.innerHTML = selectedHtml;
            wireRemoveButtons(selectedContainer);
        };

        const getSortedItems = () => {
            return Array.from(knownItems.values())
                .sort((left, right) => {
                    const leftSelected = isSelected(left.value);
                    const rightSelected = isSelected(right.value);

                    if (leftSelected && !rightSelected) {
                        return -1;
                    }

                    if (!leftSelected && rightSelected) {
                        return 1;
                    }

                    return left.text.localeCompare(right.text);
                });
        };

        const renderOptions = () => {
            const items = getSortedItems();

            if (items.length === 0) {
                options.innerHTML = `<div class="list-group-item text-muted small item-selector-empty">${escapeHtml(configuration.emptyResultsText)}</div>`;
                return;
            }

            options.innerHTML = items.map((item) => {
                const type = configuration.multiple ? 'checkbox' : 'radio';
                const checked = isSelected(item.value) ? ' checked' : '';
                const disabled = item.isEnabled ? '' : ' disabled';
                const activeClass = isSelected(item.value) ? ' active' : '';
                const secondaryText = item.secondaryText
                    ? `<span class="small text-body-secondary item-selector-option-secondary">${escapeHtml(item.secondaryText)}</span>`
                    : '';

                return `
<button type="button" class="list-group-item list-group-item-action${activeClass}" data-item-selector-option data-value="${escapeHtml(item.value)}"${disabled}>
    <span class="item-selector-option">
        <input class="form-check-input item-selector-option-indicator" type="${type}" tabindex="-1"${checked}${disabled}>
        <span class="item-selector-option-text">
            <span class="item-selector-option-primary">${escapeHtml(item.text)}</span>
            ${secondaryText}
        </span>
    </span>
</button>`;
            }).join('');

            options.querySelectorAll('[data-item-selector-option]').forEach((button) => {
                button.addEventListener('click', (event) => {
                    event.preventDefault();
                    toggleSelection(button.dataset.value);
                });
            });
        };

        const syncUi = () => {
            renderHiddenInputs();
            renderSelected();
            renderOptions();
            updateToggleText();
        };

        const addValue = (value) => {
            if (!value || isSelected(value)) {
                return;
            }

            if (!configuration.multiple) {
                selectedValues.splice(0, selectedValues.length);
            }

            selectedValues.push(value);
            syncUi();

            if (!configuration.multiple || configuration.closeOnSelect) {
                hideMenu();
            }
        };

        const removeValue = (value) => {
            const index = selectedValues.indexOf(value);

            if (index === -1) {
                return;
            }

            selectedValues.splice(index, 1);
            syncUi();
        };

        const toggleSelection = (value) => {
            if (isSelected(value)) {
                removeValue(value);
                return;
            }

            addValue(value);
        };

        const selectAll = () => {
            if (!configuration.multiple) {
                return;
            }

            getSortedItems()
                .filter((item) => item.isEnabled)
                .forEach((item) => {
                    if (!isSelected(item.value)) {
                        selectedValues.push(item.value);
                    }
                });

            syncUi();
        };

        const deselectAll = () => {
            selectedValues.splice(0, selectedValues.length);
            syncUi();
        };

        const loadItems = async (query) => {
            const normalizedQuery = query?.trim() ?? '';
            const requestId = ++activeRequestId;

            isLoading = true;
            setStatus(configuration.loadingText);

            try {
                const response = await fetch(buildUrl(configuration.endpoint, normalizedQuery), {
                    credentials: 'same-origin',
                    headers: {
                        Accept: 'application/json'
                    }
                });

                if (!response.ok) {
                    throw new Error(`Request failed with status ${response.status}.`);
                }

                const items = await response.json();

                if (requestId !== activeRequestId) {
                    return;
                }

                cacheItems(items);
                initialLoadCompleted = true;
                lastRequestedQuery = normalizedQuery;
                setStatus(
                    normalizedQuery
                        ? formatText(configuration.resultsTextFormat, items.length)
                        : null,
                    !!normalizedQuery);
                syncUi();
            }
            catch (error) {
                if (requestId !== activeRequestId) {
                    return;
                }

                console.error(error);
                setStatus(configuration.loadErrorText);
            }
            finally {
                if (requestId === activeRequestId) {
                    isLoading = false;
                }
            }
        };

        const requestSearch = async (force = false) => {
            const query = searchInput.value.trim();

            if (!force && query === lastRequestedQuery) {
                return;
            }

            if (isLoading && !force) {
                return;
            }

            await loadItems(query);
        };

        const debouncedSearch = debounce(() => {
            requestSearch();
        }, configuration.searchDelay);

        cacheItems(configuration.initialItems ?? []);

        (configuration.initialItems ?? []).forEach((item) => {
            const normalized = normalizeItem(item);

            if (normalized && normalized.selected !== false && !isSelected(normalized.value)) {
                selectedValues.push(normalized.value);
            }
        });

        toggleButton.textContent = configuration.buttonText;
        menuTitle.textContent = configuration.buttonText;
        searchInput.placeholder = configuration.searchPlaceholder;
        searchButton.textContent = configuration.searchButtonText;
        selectionSummaryLabel.textContent = configuration.selectedItemsLabel;
        availableItemsLabel.textContent = configuration.availableItemsLabel;
        searchButton.classList.toggle('d-none', !configuration.enableSearchButton);

        if (!configuration.enableSearch) {
            searchContainer.classList.add('d-none');
        }

        if (configuration.multiple && (configuration.enableSelectAll || configuration.enableDeselectAll)) {
            actions.classList.remove('d-none');
            selectAllButton.classList.toggle('d-none', !configuration.enableSelectAll);
            deselectAllButton.classList.toggle('d-none', !configuration.enableDeselectAll);
            selectAllButton.textContent = configuration.selectAllText;
            deselectAllButton.textContent = configuration.deselectAllText;
        }

        toggleButton.addEventListener('click', async (event) => {
            event.preventDefault();
            event.stopPropagation();

            const isOpen = menu.classList.contains('show');

            if (isOpen) {
                hideMenu();
                return;
            }

            showMenu();

            if (!initialLoadCompleted) {
                await loadItems('');
            }

            if (configuration.enableSearch) {
                searchInput.focus();
            }
        });

        searchButton.addEventListener('click', async (event) => {
            event.preventDefault();
            event.stopPropagation();
            await requestSearch(true);
        });

        closeButton.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            hideMenu();
        });

        searchInput.addEventListener('click', (event) => {
            event.stopPropagation();
        });

        searchInput.addEventListener('input', () => {
            if (!configuration.enableSearch) {
                return;
            }

            debouncedSearch();
        });

        searchInput.addEventListener('keydown', async (event) => {
            if (event.key === 'Escape') {
                event.preventDefault();
                hideMenu();
                return;
            }

            if (event.key !== 'Enter') {
                return;
            }

            event.preventDefault();
            await requestSearch(true);
        });

        selectAllButton.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            selectAll();
        });

        deselectAllButton.addEventListener('click', (event) => {
            event.preventDefault();
            event.stopPropagation();
            deselectAll();
        });

        menu.addEventListener('click', (event) => {
            event.stopPropagation();
        });

        document.addEventListener('click', (event) => {
            if (root.contains(event.target)) {
                return;
            }

            hideMenu();
        });

        root.setAttribute(initializedAttribute, 'true');
        syncUi();
    };

    const initializeAll = (selector = '[data-item-selector]') => {
        document.querySelectorAll(selector).forEach((root) => initialize(root));
    };

    document.addEventListener('DOMContentLoaded', () => initializeAll());

    return {
        initialize,
        initializeAll
    };
})();
