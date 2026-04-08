(function (window, document) {
    'use strict';

    function assignFiles(fileInput, files) {
        if (!fileInput || !files) {
            return;
        }

        var dataTransfer = new DataTransfer();

        Array.prototype.forEach.call(files, function (file) {
            dataTransfer.items.add(file);
        });

        fileInput.files = dataTransfer.files;
        fileInput.dispatchEvent(new Event('change', { bubbles: true }));
    }

    window.initDocumentDropZone = function initDocumentDropZone(options) {
        if (!options) {
            return;
        }

        var dropZone = typeof options.dropZone === 'string'
            ? document.querySelector(options.dropZone)
            : options.dropZone;
        var fileInput = typeof options.fileInput === 'string'
            ? document.querySelector(options.fileInput)
            : options.fileInput;
        var browseButton = typeof options.browseButton === 'string'
            ? document.querySelector(options.browseButton)
            : options.browseButton;

        if (!dropZone || !fileInput) {
            return;
        }

        function openPicker() {
            fileInput.click();
        }

        function preventDefaults(event) {
            event.preventDefault();
            event.stopPropagation();
        }

        function setDragState(isActive) {
            dropZone.classList.toggle('document-drop-zone--dragover', isActive);
        }

        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(function (eventName) {
            dropZone.addEventListener(eventName, preventDefaults);
        });

        dropZone.addEventListener('dragenter', function () { setDragState(true); });
        dropZone.addEventListener('dragover', function () { setDragState(true); });
        dropZone.addEventListener('dragleave', function (event) {
            if (!dropZone.contains(event.relatedTarget)) {
                setDragState(false);
            }
        });
        dropZone.addEventListener('drop', function (event) {
            setDragState(false);
            var files = event.dataTransfer ? event.dataTransfer.files : null;
            if (files && files.length > 0) {
                assignFiles(fileInput, files);
            }
        });

        if (browseButton) {
            browseButton.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                openPicker();
            });
        }

        dropZone.addEventListener('click', function (event) {
            if (event.target.closest('[data-drop-zone-browse]')) {
                return;
            }

            openPicker();
        });
    };
})(window, document);
