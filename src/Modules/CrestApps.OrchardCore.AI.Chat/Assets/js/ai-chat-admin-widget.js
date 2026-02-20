window.aiChatAdminWidget = (function () {
    'use strict';

    var POSITION_STORAGE_KEY_SUFFIX = '-ai-admin-widget-pos';
    var TOGGLE_POSITION_STORAGE_KEY_SUFFIX = '-ai-admin-widget-toggle-pos';
    var STATE_STORAGE_KEY_SUFFIX = '-ai-admin-widget-state';
    var SESSION_STORAGE_KEY_SUFFIX = '-ai-admin-widget-session';
    var RESIZED_KEY_SUFFIX = '-ai-admin-widget-resized';

    function initialize(config) {
        if (!config || !config.containerSelector || !config.toggleSelector) {
            console.error('aiChatAdminWidget: containerSelector and toggleSelector are required.');
            return;
        }

        var container = document.querySelector(config.containerSelector);
        var toggleBtn = document.querySelector(config.toggleSelector);

        if (!container || !toggleBtn) {
            return;
        }

        var header = container.querySelector('.ai-admin-widget-header');
        var resizeHandle = container.querySelector('.ai-admin-widget-resize-handle');
        var restoreBtn = container.querySelector(config.restoreSizeSelector);
        var promptEl = container.querySelector(config.promptSelector);
        var storagePrefix = config.storagePrefix || 'default';
        var positionKey = storagePrefix + POSITION_STORAGE_KEY_SUFFIX;
        var togglePosKey = storagePrefix + TOGGLE_POSITION_STORAGE_KEY_SUFFIX;
        var stateKey = storagePrefix + STATE_STORAGE_KEY_SUFFIX;
        var sessionKey = storagePrefix + SESSION_STORAGE_KEY_SUFFIX;
        var resizedKey = storagePrefix + RESIZED_KEY_SUFFIX;

        // Restore saved state.
        restorePosition(container, positionKey);
        restoreTogglePosition(toggleBtn, togglePosKey);

        var savedState = localStorage.getItem(stateKey);
        if (savedState === 'open') {
            container.classList.add('is-open');
            toggleBtn.style.display = 'none';
        }

        // Toggle open/close.
        toggleBtn.addEventListener('click', function (e) {
            if (toggleBtn._wasDragged) {
                toggleBtn._wasDragged = false;
                return;
            }
            container.classList.add('is-open');
            toggleBtn.style.display = 'none';
            localStorage.setItem(stateKey, 'open');
        });

        // Close button.
        var closeBtn = container.querySelector(config.closeButtonSelector);
        if (closeBtn) {
            closeBtn.addEventListener('click', function () {
                container.classList.remove('is-open');
                toggleBtn.style.display = '';
                localStorage.setItem(stateKey, 'closed');
            });
        }

        // Make widget draggable.
        if (header) {
            makeDraggable(container, header, positionKey);
        }

        // Make toggle button draggable.
        makeDraggableToggle(toggleBtn, togglePosKey);

        // Make resizable (also handles restore button).
        if (resizeHandle) {
            makeResizable(container, resizeHandle, restoreBtn, positionKey, resizedKey);
        }

        // Auto-grow textarea.
        if (promptEl) {
            setupAutoGrow(promptEl);
        }

        // Initialize chat using existing ai-chat.js.
        if (config.chatConfig && window.openAIChatManager) {
            config.chatConfig.widget = {
                showChatButton: config.toggleSelector,
                newChatButton: config.newChatButtonSelector,
                chatWidgetContainer: config.containerSelector,
                chatHistorySection: config.historyPanelSelector,
                closeHistoryButton: config.closeHistorySelector,
                closeChatButton: config.closeButtonSelector,
                showHistoryButton: config.showHistorySelector,
                chatWidgetStateName: sessionKey,
            };
            window.openAIChatManager.initialize(config.chatConfig);
        }
    }

    function setupAutoGrow(textarea) {
        var lineHeight = parseFloat(getComputedStyle(textarea).lineHeight) || 20;
        var maxLines = 5;
        var maxHeight = lineHeight * maxLines;

        function adjust() {
            textarea.style.height = 'auto';
            var scrollH = textarea.scrollHeight;
            textarea.style.height = Math.min(scrollH, maxHeight) + 'px';
            textarea.style.overflowY = scrollH > maxHeight ? 'auto' : 'hidden';
        }

        textarea.addEventListener('input', adjust);
        adjust();
    }

    function makeDraggable(container, handle, storageKey) {
        var isDragging = false;
        var startX, startY, initialLeft, initialTop;

        function onStart(e) {
            if (e.target.closest('button') || e.target.closest('a')) {
                return;
            }

            isDragging = true;
            var point = getEventPoint(e);
            var rect = container.getBoundingClientRect();
            startX = point.x;
            startY = point.y;
            initialLeft = rect.left;
            initialTop = rect.top;
            container.style.transition = 'none';
            e.preventDefault();
        }

        function onMove(e) {
            if (!isDragging) {
                return;
            }

            var point = getEventPoint(e);
            var dx = point.x - startX;
            var dy = point.y - startY;
            var newLeft = initialLeft + dx;
            var newTop = initialTop + dy;

            var maxLeft = window.innerWidth - container.offsetWidth;
            var maxTop = window.innerHeight - container.offsetHeight;
            newLeft = Math.max(0, Math.min(newLeft, maxLeft));
            newTop = Math.max(0, Math.min(newTop, maxTop));

            container.style.left = newLeft + 'px';
            container.style.top = newTop + 'px';
            container.style.right = 'auto';
            container.style.bottom = 'auto';
        }

        function onEnd() {
            if (!isDragging) {
                return;
            }

            isDragging = false;
            container.style.transition = '';
            savePosition(container, storageKey);
        }

        handle.addEventListener('mousedown', onStart);
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onEnd);

        handle.addEventListener('touchstart', onStart, { passive: false });
        document.addEventListener('touchmove', onMove, { passive: false });
        document.addEventListener('touchend', onEnd);
    }

    function makeDraggableToggle(btn, storageKey) {
        var isDragging = false;
        var hasMoved = false;
        var startX, startY, initialLeft, initialTop;
        var dragThreshold = 5;

        function onStart(e) {
            isDragging = true;
            hasMoved = false;
            btn._wasDragged = false;
            var point = getEventPoint(e);
            var rect = btn.getBoundingClientRect();
            startX = point.x;
            startY = point.y;
            initialLeft = rect.left;
            initialTop = rect.top;
            btn.style.transition = 'none';
            e.preventDefault();
        }

        function onMove(e) {
            if (!isDragging) {
                return;
            }

            var point = getEventPoint(e);
            var dx = point.x - startX;
            var dy = point.y - startY;

            if (!hasMoved && Math.abs(dx) < dragThreshold && Math.abs(dy) < dragThreshold) {
                return;
            }

            hasMoved = true;
            var newLeft = initialLeft + dx;
            var newTop = initialTop + dy;

            var maxLeft = window.innerWidth - btn.offsetWidth;
            var maxTop = window.innerHeight - btn.offsetHeight;
            newLeft = Math.max(0, Math.min(newLeft, maxLeft));
            newTop = Math.max(0, Math.min(newTop, maxTop));

            btn.style.left = newLeft + 'px';
            btn.style.top = newTop + 'px';
            btn.style.right = 'auto';
            btn.style.bottom = 'auto';
            btn.style.position = 'fixed';
        }

        function onEnd() {
            if (!isDragging) {
                return;
            }

            isDragging = false;
            btn.style.transition = '';

            if (hasMoved) {
                btn._wasDragged = true;
                saveTogglePosition(btn, storageKey);
            }
        }

        btn.addEventListener('mousedown', onStart);
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onEnd);

        btn.addEventListener('touchstart', onStart, { passive: false });
        document.addEventListener('touchmove', onMove, { passive: false });
        document.addEventListener('touchend', onEnd);
    }

    function makeResizable(container, resizeHandle, restoreBtn, storageKey, resizedKey) {
        var isResizing = false;
        var startX, startY, startWidth, startHeight, startLeft, startTop;

        // Use localStorage as the source of truth for resized state.
        if (localStorage.getItem(resizedKey) === 'true') {
            container.classList.add('is-resized');
        }

        function onStart(e) {
            isResizing = true;
            var point = getEventPoint(e);
            var rect = container.getBoundingClientRect();
            startX = point.x;
            startY = point.y;
            startWidth = container.offsetWidth;
            startHeight = container.offsetHeight;
            startLeft = rect.left;
            startTop = rect.top;
            container.style.transition = 'none';
            e.preventDefault();
            e.stopPropagation();
        }

        function onMove(e) {
            if (!isResizing) {
                return;
            }

            var point = getEventPoint(e);
            var dx = point.x - startX;
            var dy = point.y - startY;

            // Top-left handle: drag left = wider, drag up = taller.
            var newWidth = startWidth - dx;
            var newHeight = startHeight - dy;

            newWidth = Math.max(288, newWidth);
            newHeight = Math.max(320, newHeight);

            // Adjust position so the right/bottom edges stay fixed.
            var newLeft = startLeft + (startWidth - newWidth);
            var newTop = startTop + (startHeight - newHeight);

            newLeft = Math.max(0, newLeft);
            newTop = Math.max(0, newTop);

            container.style.width = newWidth + 'px';
            container.style.height = newHeight + 'px';
            container.style.left = newLeft + 'px';
            container.style.top = newTop + 'px';
            container.style.right = 'auto';
            container.style.bottom = 'auto';
        }

        function onEnd() {
            if (!isResizing) {
                return;
            }

            isResizing = false;
            container.style.transition = '';
            container.classList.add('is-resized');
            localStorage.setItem(resizedKey, 'true');
            savePosition(container, storageKey);
        }

        if (restoreBtn) {
            restoreBtn.addEventListener('click', function () {
                container.style.width = '';
                container.style.height = '';
                container.classList.remove('is-resized');
                localStorage.removeItem(resizedKey);
                savePosition(container, storageKey);
            });
        }

        resizeHandle.addEventListener('mousedown', onStart);
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onEnd);

        resizeHandle.addEventListener('touchstart', onStart, { passive: false });
        document.addEventListener('touchmove', onMove, { passive: false });
        document.addEventListener('touchend', onEnd);
    }

    function savePosition(container, storageKey) {
        try {
            var rect = container.getBoundingClientRect();
            var data = {
                left: rect.left,
                top: rect.top,
                width: container.offsetWidth,
                height: container.offsetHeight,
            };
            localStorage.setItem(storageKey, JSON.stringify(data));
        } catch (e) {
            // Ignore storage errors.
        }
    }

    function restorePosition(container, storageKey) {
        try {
            var saved = localStorage.getItem(storageKey);
            if (!saved) {
                return;
            }

            var data = JSON.parse(saved);
            var vw = window.innerWidth;
            var vh = window.innerHeight;

            if (data.left >= 0 && data.top >= 0 && data.left < vw && data.top < vh) {
                container.style.left = Math.min(data.left, vw - 100) + 'px';
                container.style.top = Math.min(data.top, vh - 100) + 'px';
                container.style.right = 'auto';
                container.style.bottom = 'auto';
            }

            if (data.width && data.width >= 288) {
                container.style.width = Math.min(data.width, vw - 16) + 'px';
            }

            if (data.height && data.height >= 320) {
                container.style.height = Math.min(data.height, vh - 16) + 'px';
            }
        } catch (e) {
            // Ignore parse errors.
        }
    }

    function saveTogglePosition(btn, storageKey) {
        try {
            var rect = btn.getBoundingClientRect();
            var data = { left: rect.left, top: rect.top };
            localStorage.setItem(storageKey, JSON.stringify(data));
        } catch (e) {
            // Ignore storage errors.
        }
    }

    function restoreTogglePosition(btn, storageKey) {
        try {
            var saved = localStorage.getItem(storageKey);
            if (!saved) {
                return;
            }

            var data = JSON.parse(saved);
            var vw = window.innerWidth;
            var vh = window.innerHeight;

            if (data.left >= 0 && data.top >= 0 && data.left < vw && data.top < vh) {
                btn.style.left = Math.min(data.left, vw - btn.offsetWidth) + 'px';
                btn.style.top = Math.min(data.top, vh - btn.offsetHeight) + 'px';
                btn.style.right = 'auto';
                btn.style.bottom = 'auto';
            }
        } catch (e) {
            // Ignore parse errors.
        }
    }

    function getEventPoint(e) {
        if (e.touches && e.touches.length > 0) {
            return { x: e.touches[0].clientX, y: e.touches[0].clientY };
        }
        return { x: e.clientX, y: e.clientY };
    }

    return {
        initialize: initialize,
    };
})();
