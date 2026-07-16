(function () {
    var selector = '[data-subject-action-assignment-toggle]';

    function initializeToggle(assignmentTypeSelect) {
        var targetId = assignmentTypeSelect.dataset.assignmentTargetId;
        var specificOwnerValue = assignmentTypeSelect.dataset.assignmentSpecificOwnerValue;

        if (!targetId ||
            !specificOwnerValue ||
            assignmentTypeSelect.dataset.assignmentToggleInitialized === 'true') {
            return;
        }

        var specificOwnerContainer = document.getElementById(targetId);

        if (!specificOwnerContainer) {
            return;
        }

        assignmentTypeSelect.dataset.assignmentToggleInitialized = 'true';

        function toggle() {
            var isSpecificOwner = assignmentTypeSelect.value === specificOwnerValue;

            specificOwnerContainer.classList.toggle('d-none', !isSpecificOwner);
            assignmentTypeSelect.setAttribute('aria-expanded', isSpecificOwner.toString());
        }

        assignmentTypeSelect.addEventListener('change', toggle);
        toggle();
    }

    function initialize() {
        document.querySelectorAll(selector).forEach(initializeToggle);
    }

    function observeDynamicAdditions() {
        var observer = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var addedNodes = mutations[i].addedNodes;

                for (var j = 0; j < addedNodes.length; j++) {
                    var node = addedNodes[j];

                    if (node.nodeType !== Node.ELEMENT_NODE) {
                        continue;
                    }

                    if (node.matches(selector)) {
                        initializeToggle(node);
                    }
                    else {
                        var toggles = node.querySelectorAll(selector);
                        toggles.forEach(initializeToggle);
                    }
                }
            }
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            initialize();
            observeDynamicAdditions();
        }, { once: true });

        return;
    }

    initialize();
    observeDynamicAdditions();
})();
