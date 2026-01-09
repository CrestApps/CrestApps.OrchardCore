/*
** Smart Text Field - AI-powered autocomplete for TextField
*/
window.smartTextFieldManager = (function () {
    'use strict';

    var instances = {};
    var debounceTimers = {};
    var minChars = 3;
    var debounceDelay = 500;

    function initialize(inputId, profileId, wrapperId) {
        var input = document.getElementById(inputId);

        if (!input || instances[inputId]) {
            return;
        }

        var suggestionsContainer = document.getElementById(wrapperId + '-suggestions');
        var spinnerContainer = document.getElementById(wrapperId + '-spinner');

        if (!suggestionsContainer) {
            console.warn('Suggestions container not found for:', wrapperId);
            return;
        }

        instances[inputId] = {
            input: input,
            profileId: profileId,
            suggestionsContainer: suggestionsContainer,
            spinnerContainer: spinnerContainer,
            selectedIndex: -1
        };

        input.addEventListener('input', function (e) {
            handleInput(inputId, e.target.value);
        });

        input.addEventListener('keydown', function (e) {
            handleKeyDown(inputId, e);
        });

        input.addEventListener('blur', function () {
            setTimeout(function () {
                hideSuggestions(inputId);
            }, 200);
        });

        document.addEventListener('click', function (e) {
            if (!input.contains(e.target) && !suggestionsContainer.contains(e.target)) {
                hideSuggestions(inputId);
            }
        });
    }

    function handleInput(inputId, value) {
        var instance = instances[inputId];

        if (!instance) {
            return;
        }

        if (debounceTimers[inputId]) {
            clearTimeout(debounceTimers[inputId]);
        }

        if (!value || value.length < minChars) {
            hideSuggestions(inputId);
            return;
        }

        debounceTimers[inputId] = setTimeout(function () {
            fetchSuggestions(inputId, value);
        }, debounceDelay);
    }

    function handleKeyDown(inputId, e) {
        var instance = instances[inputId];

        if (!instance) {
            return;
        }

        var suggestions = instance.suggestionsContainer.querySelectorAll('.dropdown-item');

        if (suggestions.length === 0) {
            return;
        }

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                instance.selectedIndex = Math.min(instance.selectedIndex + 1, suggestions.length - 1);
                updateSelection(inputId, suggestions);
                break;
            case 'ArrowUp':
                e.preventDefault();
                instance.selectedIndex = Math.max(instance.selectedIndex - 1, 0);
                updateSelection(inputId, suggestions);
                break;
            case 'Enter':
                if (instance.selectedIndex >= 0 && suggestions[instance.selectedIndex]) {
                    e.preventDefault();
                    selectSuggestion(inputId, suggestions[instance.selectedIndex].textContent);
                }
                break;
            case 'Escape':
                hideSuggestions(inputId);
                break;
        }
    }

    function updateSelection(inputId, suggestions) {
        var instance = instances[inputId];

        suggestions.forEach(function (suggestion, index) {
            if (index === instance.selectedIndex) {
                suggestion.classList.add('active');
            } else {
                suggestion.classList.remove('active');
            }
        });
    }

    function fetchSuggestions(inputId, value) {
        var instance = instances[inputId];

        if (!instance) {
            return;
        }

        showSpinner(inputId);

        var requestBody = {
            profileId: instance.profileId,
            prompt: value
        };

        fetch('/api/ai/completion/utility', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(requestBody)
        })
        .then(function (response) {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(function (data) {
            hideSpinner(inputId);
            if (data && data.success && data.message && data.message.content) {
                displaySuggestions(inputId, parseSuggestions(data.message.content));
            } else {
                hideSuggestions(inputId);
            }
        })
        .catch(function (error) {
            console.error('Error fetching suggestions:', error);
            hideSpinner(inputId);
            hideSuggestions(inputId);
        });
    }

    function parseSuggestions(content) {
        // Try to parse as JSON first
        try {
            var parsed = JSON.parse(content);
            if (Array.isArray(parsed)) {
                return parsed.filter(function (item) {
                    return typeof item === 'string' && item.trim().length > 0;
                });
            }
            if (parsed.suggestions && Array.isArray(parsed.suggestions)) {
                return parsed.suggestions;
            }
        } catch (e) {
            // Not JSON, try other formats
        }

        // Try to parse as newline-separated or comma-separated list
        var lines = content.split(/[\n,]/)
            .map(function (line) {
                // Remove list markers like "1.", "-", "*"
                return line.replace(/^[\d\.\-\*\s]+/, '').trim();
            })
            .filter(function (line) {
                return line.length > 0 && line.length < 200;
            });

        return lines.slice(0, 10);
    }

    function displaySuggestions(inputId, suggestions) {
        var instance = instances[inputId];

        if (!instance || !suggestions || suggestions.length === 0) {
            hideSuggestions(inputId);
            return;
        }

        instance.suggestionsContainer.innerHTML = '';
        instance.selectedIndex = -1;

        suggestions.forEach(function (suggestion) {
            var item = document.createElement('a');
            item.className = 'dropdown-item';
            item.href = '#';
            item.textContent = suggestion;
            item.addEventListener('click', function (e) {
                e.preventDefault();
                selectSuggestion(inputId, suggestion);
            });
            item.addEventListener('mouseenter', function () {
                var items = instance.suggestionsContainer.querySelectorAll('.dropdown-item');
                items.forEach(function (el) { el.classList.remove('active'); });
                item.classList.add('active');
                instance.selectedIndex = Array.from(items).indexOf(item);
            });
            instance.suggestionsContainer.appendChild(item);
        });

        instance.suggestionsContainer.style.display = 'block';
    }

    function selectSuggestion(inputId, suggestion) {
        var instance = instances[inputId];

        if (!instance) {
            return;
        }

        instance.input.value = suggestion;
        hideSuggestions(inputId);

        // Trigger input event for any form validation
        var event = new Event('input', { bubbles: true });
        instance.input.dispatchEvent(event);
    }

    function hideSuggestions(inputId) {
        var instance = instances[inputId];

        if (instance && instance.suggestionsContainer) {
            instance.suggestionsContainer.style.display = 'none';
            instance.suggestionsContainer.innerHTML = '';
            instance.selectedIndex = -1;
        }
    }

    function showSpinner(inputId) {
        var instance = instances[inputId];

        if (instance && instance.spinnerContainer) {
            instance.spinnerContainer.style.display = 'block';
        }
    }

    function hideSpinner(inputId) {
        var instance = instances[inputId];

        if (instance && instance.spinnerContainer) {
            instance.spinnerContainer.style.display = 'none';
        }
    }

    return {
        initialize: initialize
    };
})();
