(function (global) {
    if (!global) {
        return;
    }

    var namespace = global.crestAppsAIChat = global.crestAppsAIChat || {};

    namespace.patchSessionProfileSync = function patchSessionProfileSync(chatApp) {
        if (!chatApp || chatApp.__crestAppsSessionProfilePatched) {
            return chatApp;
        }

        chatApp.__crestAppsSessionProfilePatched = true;

        function getCurrentProfileId() {
            return typeof chatApp.getProfileId === 'function' ? chatApp.getProfileId() : null;
        }

        function clearMismatchedSession() {
            if (typeof chatApp.setSessionId === 'function') {
                chatApp.setSessionId('');
            }

            chatApp.isSessionStarted = false;
            chatApp.sessionProfileId = getCurrentProfileId();

            if (chatApp.widgetIsInitialized && chatApp.chatWidgetStateSession) {
                localStorage.removeItem(chatApp.chatWidgetStateSession);
            }
        }

        chatApp.sessionProfileId = getCurrentProfileId();

        var originalInitializeSession = chatApp.initializeSession;
        if (typeof originalInitializeSession === 'function') {
            chatApp.initializeSession = function () {
                var result = originalInitializeSession.apply(this, arguments);

                if (!this.sessionProfileId) {
                    this.sessionProfileId = getCurrentProfileId();
                }

                return result;
            };
        }

        var originalResetSession = chatApp.resetSession;
        if (typeof originalResetSession === 'function') {
            chatApp.resetSession = function () {
                this.sessionProfileId = getCurrentProfileId();
                return originalResetSession.apply(this, arguments);
            };
        }

        var originalUploadFiles = chatApp.uploadFiles;
        if (typeof originalUploadFiles === 'function') {
            chatApp.uploadFiles = function () {
                var currentProfileId = getCurrentProfileId();
                var currentSessionId = typeof this.getSessionId === 'function' ? this.getSessionId() : null;

                if (currentSessionId && currentProfileId && this.sessionProfileId && this.sessionProfileId !== currentProfileId) {
                    clearMismatchedSession();
                    this.documents = [];

                    if (typeof this.renderDocumentBar === 'function') {
                        this.renderDocumentBar();
                    }
                }

                return originalUploadFiles.apply(this, arguments);
            };
        }

        if (chatApp.connection && typeof chatApp.connection.on === 'function') {
            chatApp.connection.on("LoadSession", function (data) {
                var sessionProfileId = data && data.profile ? data.profile.id : null;
                var currentProfileId = getCurrentProfileId();

                chatApp.sessionProfileId = sessionProfileId || currentProfileId;

                if (!chatApp.widgetIsInitialized || !sessionProfileId || !currentProfileId || sessionProfileId === currentProfileId) {
                    return;
                }

                chatApp.resetSession();
            });
        }

        return chatApp;
    };
})(window);
