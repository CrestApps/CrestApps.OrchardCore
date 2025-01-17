/*
** NOTE: This file is generated by Gulp and should not be edited directly!
** Any changes made directly to this file will be overwritten next time its asset group is processed by Gulp.
*/

var openAIChatManager = function () {
  var defaultConfig = {
    messageTemplate: "\n            <div class=\"list-group\">\n                <div v-for=\"(message, index) in messages\" :key=\"index\" class=\"list-group-item\">\n                    <div class=\"d-flex\">\n                        <div class=\"p-2\">\n                            <i :class=\"message.role === 'user' ? 'fa-solid fa-user fa-2xl text-primary' : 'fa fa-robot fa-2xl text-success'\"></i>\n                        </div>\n                        <div class=\"p-2 lh-base\">\n                            <h4 v-if=\"message.title\">{{ message.title }}</h4>\n                            <div v-html=\"message.htmlContent || message.content\"></div>\n                        </div>\n                    </div>\n                    <div class=\"d-flex justify-content-center message-buttons-container\" v-if=\"message.role !== 'indicator'\">\n                        <button class=\"ms-2 btn btn-sm btn-outline-secondary button-message-toolbox\" @click=\"copyResponse(message.content)\" title=\"Click here to copy response to clipboard.\">\n                            <i class=\"fa-solid fa-copy fa-lg\"></i>\n                        </button>\n                    </div>\n                </div>\n            </div>\n        ",
    indicatorTemplate: "<div class=\"spinner-grow spinner-grow-sm\" role=\"status\"><span class=\"visually-hidden\">Loading...</span></div>"
  };
  var initialize = function initialize(instanceConfig) {
    var config = Object.assign({}, defaultConfig, instanceConfig);
    if (!config.chatUrl) {
      console.error('The chatUrl is required.');
      return;
    }
    if (!config.appElementSelector) {
      console.error('The appElementSelector is required.');
      return;
    }
    if (!config.chatContainerElementSelector) {
      console.error('The chatContainerElementSelector is required.');
      return;
    }
    if (!config.inputElementSelector) {
      console.error('The inputElementSelector is required.');
      return;
    }
    if (!config.sendButtonElementSelector) {
      console.error('The sendButtonElementSelector is required.');
      return;
    }
    var app = Vue.createApp({
      data: function data() {
        return {
          inputElement: null,
          buttonElement: null,
          chatContainer: null,
          placeholder: null,
          isSessionStarted: false,
          isPlaceholderVisible: true,
          chatWidgetStateName: null,
          chatWidgetStateSession: null,
          chatHistorySection: null,
          widgetIsInitialized: false,
          messages: [],
          prompt: ''
        };
      },
      methods: {
        addMessageInternal: function addMessageInternal(message) {
          var _this = this;
          this.fireEvent(new CustomEvent("addingOpenAIPromotMessage", {
            detail: {
              message: message
            }
          }));
          this.messages.push(message);
          this.$nextTick(function () {
            _this.fireEvent(new CustomEvent("addedOpenAIPromotMessage", {
              detail: {
                message: message
              }
            }));
          });
        },
        addMessage: function addMessage(message) {
          var _this2 = this;
          this.addMessageInternal(message);
          this.hidePlaceholder();
          this.$nextTick(function () {
            _this2.scrollToBottom();
          });
        },
        addMessages: function addMessages(messages) {
          var _this3 = this;
          for (var i = 0; i < messages.length; i++) {
            this.addMessageInternal(messages[i]);
          }
          this.hidePlaceholder();
          this.$nextTick(function () {
            _this3.scrollToBottom();
          });
        },
        hidePlaceholder: function hidePlaceholder() {
          if (this.placeholder) {
            this.placeholder.classList.add('d-none');
          }
          this.isPlaceholderVisible = false;
        },
        showPlaceholder: function showPlaceholder() {
          if (this.placeholder) {
            this.placeholder.classList.remove('d-none');
          }
          this.isPlaceholderVisible = true;
        },
        handleUserInput: function handleUserInput(event) {
          this.prompt = event.target.value;
        },
        getProfileId: function getProfileId() {
          return this.inputElement.getAttribute('data-profile-id');
        },
        resetSession: function resetSession() {
          this.inputElement.setAttribute('data-session-id', '');
          this.isSessionStarted = false;
          if (this.widgetIsInitialized) {
            localStorage.removeItem(this.chatWidgetStateSession);
          }
          this.messages = [];
          this.showPlaceholder();
        },
        initializeApp: function initializeApp() {
          var _this4 = this;
          this.inputElement = document.querySelector(config.inputElementSelector);
          this.buttonElement = document.querySelector(config.sendButtonElementSelector);
          this.chatContainer = document.querySelector(config.chatContainerElementSelector);
          this.placeholder = document.querySelector(config.placeholderElementSelector);
          this.inputElement.addEventListener('keyup', function (event) {
            if (event.key === "Enter" && !event.shiftKey) {
              _this4.buttonElement.dispatchEvent(new Event('click'));
            }
          });
          this.inputElement.addEventListener('input', function (e) {
            _this4.handleUserInput(e);
            if (e.target.value.trim()) {
              _this4.buttonElement.removeAttribute('disabled');
            } else {
              _this4.buttonElement.setAttribute('disabled', true);
            }
          });
          this.buttonElement.addEventListener('click', function () {
            _this4.sendMessage();
          });
          var promptGenerators = document.getElementsByClassName('profile-generated-prompt');
          for (var i = 0; i < promptGenerators.length; i++) {
            promptGenerators[i].addEventListener('click', function (e) {
              e.preventDefault();
              _this4.generatePrompt(e.target);
            });
          }
          var chatSessions = document.getElementsByClassName('chat-session-history-item');
          for (var i = 0; i < chatSessions.length; i++) {
            chatSessions[i].addEventListener('click', function (e) {
              e.preventDefault();
              var sessionId = e.target.getAttribute('data-session-id');
              if (!sessionId) {
                console.error('an element with the class chat-session-history-item with no data-session-id set.');
                return;
              }
              _this4.loadSession(sessionId);
              _this4.showChatScreen();
            });
          }
          for (var _i = 0; _i < config.messages.length; _i++) {
            this.addMessage(config.messages[_i]);
          }
        },
        reloadCurrentSession: function reloadCurrentSession() {
          var sessionId = this.getSessionId();
          if (sessionId) {
            this.loadSession(sessionId);
          }
        },
        initializeWidget: function initializeWidget() {
          var _this5 = this;
          if (!config.widget.showChatButton) {
            console.error('The widget showChatButton is required.');
            return;
          }
          if (!config.widget.chatWidgetContainer) {
            console.error('The widget chatWidgetContainer is required.');
            return;
          }
          if (!config.widget.chatWidgetStateName) {
            console.error('The widget chatWidgetStateName is required.');
            return;
          }
          var showChatButton = document.querySelector(config.widget.showChatButton);
          if (!showChatButton) {
            return;
          }
          var chatWidgetContainer = document.querySelector(config.widget.chatWidgetContainer);
          if (!chatWidgetContainer) {
            return;
          }
          if (config.widget.chatHistorySection) {
            this.chatHistorySection = document.querySelector(config.widget.chatHistorySection);
          }
          this.chatWidgetStateName = config.widget.chatWidgetStateName;
          this.chatWidgetStateSession = config.widget.chatWidgetStateName + 'Session';
          this.widgetIsInitialized = true;
          var isOpen = localStorage.getItem(this.chatWidgetStateName) === 'open';
          if (isOpen) {
            this.reloadCurrentSession();
            chatWidgetContainer.classList.remove('d-none');
          }
          showChatButton.addEventListener('click', function () {
            var isHidden = chatWidgetContainer.classList.contains('d-none');
            if (isHidden) {
              chatWidgetContainer.classList.remove('d-none');
              localStorage.setItem(_this5.chatWidgetStateName, 'open');
              _this5.reloadCurrentSession();
            } else {
              chatWidgetContainer.classList.add('d-none');
              localStorage.setItem(_this5.chatWidgetStateName, 'closed');
            }
          });
          if (config.widget.closeChatButton) {
            var closeChatButton = document.querySelector(config.widget.closeChatButton);
            if (closeChatButton) {
              closeChatButton.addEventListener('click', function () {
                chatWidgetContainer.classList.add('d-none');
                localStorage.setItem(_this5.chatWidgetStateName, 'closed');
              });
            }
          }
          if (config.widget.showHistoryButton && this.chatHistorySection) {
            var showHistoryButton = document.querySelector(config.widget.showHistoryButton);
            if (showHistoryButton) {
              showHistoryButton.addEventListener('click', function () {
                _this5.chatHistorySection.classList.toggle('show');
              });
            }
            if (config.widget.closeHistoryButton) {
              var closeHistoryButton = document.querySelector(config.widget.closeHistoryButton);
              if (closeHistoryButton) {
                closeHistoryButton.addEventListener('click', function () {
                  _this5.showChatScreen();
                });
              }
            }
          }
          if (config.widget.newChatButton) {
            var newChatButton = document.querySelector(config.widget.newChatButton);
            if (newChatButton) {
              newChatButton.addEventListener('click', function () {
                _this5.resetSession();
                _this5.showChatScreen();
              });
            }
          }
        },
        showChatScreen: function showChatScreen() {
          if (!this.chatHistorySection) {
            return;
          }
          this.chatHistorySection.classList.remove('show');
        },
        getSessionId: function getSessionId() {
          var sessionId = this.inputElement.getAttribute('data-session-id');
          if (!sessionId && this.widgetIsInitialized) {
            sessionId = localStorage.getItem(this.chatWidgetStateSession);
          }
          return sessionId;
        },
        copyResponse: function copyResponse(message) {
          navigator.clipboard.writeText(message);
        },
        sendMessage: function sendMessage() {
          var trimmedPrompt = this.prompt.trim();
          if (!trimmedPrompt) {
            return;
          }
          this.addMessage({
            role: 'user',
            content: trimmedPrompt
          });
          this.showTypingIndicator();
          this.inputElement.value = '';
          this.prompt = '';
          this.buttonElement.setAttribute('disabled', true);
          this.completeChat(this.getProfileId(), trimmedPrompt, this.getSessionId());
        },
        generatePrompt: function generatePrompt(element) {
          if (!element) {
            console.error('The element paramter is required.');
            return;
          }
          var profileId = element.getAttribute('data-profile-id');
          var sessionId = this.getSessionId();
          if (!profileId || !sessionId) {
            console.error('The given element is missing data-profile-id or the session has not yet started.');
            return;
          }
          this.showTypingIndicator();
          this.completeChat(profileId, null, sessionId);
        },
        completeChat: function completeChat(profileId, prompt, sessionId) {
          var _this6 = this;
          var sessionProfileId = this.getProfileId();
          fetch(config.chatUrl, {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json'
            },
            body: JSON.stringify({
              profileId: profileId,
              sessionId: sessionId,
              prompt: prompt,
              sessionProfileId: sessionProfileId == profileId ? null : sessionProfileId
            })
          }).then(function (response) {
            if (!response.ok) {
              throw new Error("Request failed with status ".concat(response.status));
            }
            return response.json();
          }).then(function (result) {
            _this6.initializeSession(result.sessionId, false);
            _this6.addMessage(result.message);
            _this6.hideTypingIndicator();
          })["catch"](function (error) {
            console.error('Failed to send the message.', error);
            _this6.hideTypingIndicator();
          });
        },
        createSessionUrl: function createSessionUrl(baseUrl, param, value) {
          var fullUrl = baseUrl.toLowerCase().startsWith('http') ? baseUrl : window.location.origin + baseUrl;
          var url = new URL(fullUrl);
          url.searchParams.set(param, value);
          return url.toString();
        },
        loadSession: function loadSession(sessionId) {
          var _this7 = this;
          if (!config.widget.sessionUrl) {
            console.error('The sessionUrl is required.');
            return;
          }
          var url = this.createSessionUrl(config.widget.sessionUrl, 'sessionId', sessionId);
          fetch(url, {
            method: 'GET'
          }).then(function (response) {
            if (!response.ok) {
              throw new Error("Request failed with status ".concat(response.status));
            }
            return response.json();
          }).then(function (result) {
            var _result$messages;
            _this7.initializeSession(result.sessionId, true);
            _this7.messages = (_result$messages = result.messages) !== null && _result$messages !== void 0 ? _result$messages : [];
            if (_this7.messages.length) {
              _this7.hidePlaceholder();
            } else {
              _this7.showPlaceholder();
            }
            _this7.$nextTick(function () {
              _this7.scrollToBottom();
            });
          })["catch"](function (error) {
            console.error('Failed to load session.', error);
            _this7.hideTypingIndicator();
          });
        },
        fireEvent: function fireEvent(event) {
          document.dispatchEvent(event);
        },
        initializeSession: function initializeSession(sessionId, force) {
          if (this.isSessionStarted && !force) {
            return;
          }
          this.fireEvent(new CustomEvent("initializingSessionOpenAIChat", {
            detail: {
              sessionId: sessionId
            }
          }));
          this.inputElement.setAttribute('data-session-id', sessionId);
          this.isSessionStarted = true;
          if (this.widgetIsInitialized) {
            localStorage.setItem(this.chatWidgetStateSession, sessionId);
          }
        },
        showTypingIndicator: function showTypingIndicator() {
          this.addMessage({
            role: 'indicator',
            htmlContent: config.indicatorTemplate
          });
        },
        hideTypingIndicator: function hideTypingIndicator() {
          this.messages = this.messages.filter(function (msg) {
            return msg.role != 'indicator';
          });
        },
        scrollToBottom: function scrollToBottom() {
          var _this8 = this;
          setTimeout(function () {
            _this8.chatContainer.scrollTop = _this8.chatContainer.scrollHeight - _this8.chatContainer.clientHeight;
          }, 50);
        }
      },
      mounted: function mounted() {
        this.initializeApp();
        if (config.widget) {
          this.initializeWidget();
        }
      },
      template: config.messageTemplate
    }).mount(config.appElementSelector);
    return app;
  };
  return {
    initialize: initialize
  };
}();