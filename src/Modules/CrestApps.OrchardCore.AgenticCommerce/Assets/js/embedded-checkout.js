// @ts-check
/*
 * UCP Embedded Checkout Protocol (ECP), the store's side.
 *
 * An agent platform shows the store's checkout inside its own page by framing the checkout's continue_url with
 * ec_version. That page (the "embedded checkout") holds the protocol channel for the whole session and shows the
 * store's checkout pages in a frame of its own, so the buyer moves from step to step without the channel being set up
 * again. It speaks JSON-RPC 2.0 over postMessage to the platform (the "host"):
 *
 * - ec.ready, the handshake, a request. It lists the delegations the store accepts (none) and, when the host must give
 *   a credential, asks for one. A MessagePort the host gives in its answer replaces the window channel.
 * - ec.auth, a request, before the host's credential runs out.
 * - ec.start, once the checkout is showing; ec.complete, once the order is placed.
 * - ec.line_items.change, ec.buyer.change, ec.fulfillment.change, ec.payment.change, ec.totals.change and
 *   ec.messages.change, notifications with the full checkout, whenever a checkout page loads with a change.
 * - ec.error, a notification, when the session can't go on; it carries continue_url so the host can hand the buyer off.
 *
 * Messages only go to, and are only taken from, the host origins the platform lists. Native apps inject
 * EmbeddedCheckoutProtocolConsumer instead of framing the page.
 */
(function () {
  'use strict';

  var CONFIG_ID = 'agent-embedded-checkout-config';
  var FRAME_ID = 'agent-embedded-checkout-frame';

  // The checkout members whose change has its own notification, in the order they are sent: a domain's change first,
  // then the totals it moved (ECP requires that order), then the messages.
  var CHANGES = [
    ['line_items', 'ec.line_items.change'],
    ['buyer', 'ec.buyer.change'],
    ['fulfillment', 'ec.fulfillment.change'],
    ['payment', 'ec.payment.change'],
    ['totals', 'ec.totals.change'],
    ['messages', 'ec.messages.change'],
  ];

  function same(a, b) {
    return JSON.stringify(a === undefined ? null : a) === JSON.stringify(b === undefined ? null : b);
  }

  // The notifications a change from one checkout state to the next calls for, in the order they are sent.
  function changedMethods(previous, current) {
    var methods = [];

    CHANGES.forEach(function (change) {
      if (!same(previous && previous[change[0]], current && current[change[0]])) {
        methods.push(change[1]);
      }
    });

    return methods;
  }

  // Whether an address is one of the store checkout pages the frame may show.
  function isCheckoutUrl(href, origin, paths) {
    var url;

    try {
      url = new URL(href, origin);
    } catch (e) {
      return false;
    }

    if (url.origin !== origin) {
      return false;
    }

    var path = url.pathname.toLowerCase();

    return (paths || []).some(function (prefix) {
      var p = String(prefix).toLowerCase().replace(/\/+$/, '');

      return path === p || path.indexOf(p + '/') === 0;
    });
  }

  function nativeConsumer(win) {
    if (win.EmbeddedCheckoutProtocolConsumer && typeof win.EmbeddedCheckoutProtocolConsumer.postMessage === 'function') {
      return win.EmbeddedCheckoutProtocolConsumer;
    }

    var handlers = win.webkit && win.webkit.messageHandlers;

    if (handlers && handlers.EmbeddedCheckoutProtocolConsumer && typeof handlers.EmbeddedCheckoutProtocolConsumer.postMessage === 'function') {
      return handlers.EmbeddedCheckoutProtocolConsumer;
    }

    return null;
  }

  // The channel to a native app: it injects a consumer to send to, and calls EmbeddedCheckoutProtocol.postMessage.
  function createNativeChannel(win, consumer) {
    return {
      kind: 'native',
      send: function (message) {
        consumer.postMessage(JSON.stringify(message));
      },
      listen: function (receive) {
        win.EmbeddedCheckoutProtocol = {
          postMessage: function (message) {
            var data = message;

            if (typeof message === 'string') {
              try {
                data = JSON.parse(message);
              } catch (e) {
                return;
              }
            }

            receive(data, { origin: null, ports: [] });
          },
        };
      },
      lock: function () { },
      upgrade: function () { return false; },
    };
  }

  // The channel to the framing page. Until the host answers the handshake, a message is posted to each allowed origin
  // and the browser only delivers it to the one the parent really has; after that, only to the host's origin.
  function createWindowChannel(win, hostOrigins) {
    var parent = win.parent;
    var allowed = (hostOrigins || []).slice();
    var hostOrigin = null;
    var port = null;
    var ancestors = win.location && win.location.ancestorOrigins;
    var ancestor = ancestors && ancestors.length ? ancestors[0] : null;

    function targets() {
      if (hostOrigin) {
        return [hostOrigin];
      }

      if (ancestor) {
        return allowed.indexOf(ancestor) >= 0 ? [ancestor] : [];
      }

      return allowed;
    }

    return {
      kind: 'window',
      send: function (message) {
        if (port) {
          port.postMessage(message);

          return;
        }

        targets().forEach(function (origin) {
          parent.postMessage(message, origin);
        });
      },
      listen: function (receive) {
        win.addEventListener('message', function (event) {
          if (port || event.source !== parent || allowed.indexOf(event.origin) < 0 || (hostOrigin && event.origin !== hostOrigin)) {
            return;
          }

          receive(event.data, { origin: event.origin, ports: event.ports || [] });
        });
      },
      lock: function (origin) {
        if (!hostOrigin && origin && allowed.indexOf(origin) >= 0) {
          hostOrigin = origin;
        }
      },
      upgrade: function (newPort, receive) {
        if (!newPort || typeof newPort.postMessage !== 'function') {
          return false;
        }

        port = newPort;
        port.onmessage = function (event) {
          receive(event.data, { origin: hostOrigin, ports: event.ports || [] });
        };

        if (typeof port.start === 'function') {
          port.start();
        }

        return true;
      },
    };
  }

  /*
   * One embedded checkout session.
   *
   * options.config         the page's settings (version, delegate, auth, continueUrl)
   * options.channel        the channel to the host
   * options.fetchCheckout  () => Promise of the checkout as UCP JSON
   * options.verifyCredential (credential) => Promise of { valid, expires_in }
   * options.loadCheckout   () => void, shows the store's checkout once the handshake is done
   * options.setTimeout     optional timer, for tests
   */
  function createSession(options) {
    var config = options.config || {};
    var channel = options.channel;
    var setTimer = options.setTimeout || function (callback, delay) { return setTimeout(callback, delay); };
    var auth = config.auth || null;
    var state = 'idle';
    var nextId = 1;
    var pending = {};
    var last = null;
    var completed = false;

    function send(message) {
      if (state !== 'closed') {
        channel.send(message);
      }
    }

    function notify(method, params) {
      send({ jsonrpc: '2.0', method: method, params: params });
    }

    function request(method, params) {
      var id = method + '_' + nextId++;

      return new Promise(function (resolve) {
        pending[id] = resolve;
        send({ jsonrpc: '2.0', id: id, method: method, params: params });
      });
    }

    function receive(data, meta) {
      if (!data || typeof data !== 'object' || data.jsonrpc !== '2.0') {
        return;
      }

      // The host only answers; the embedded checkout serves no request of its own.
      if (typeof data.method === 'string') {
        if (data.id !== undefined && data.id !== null && state !== 'closed') {
          channel.send({ jsonrpc: '2.0', id: data.id, error: { code: -32601, message: 'Method not found.' } });
        }

        return;
      }

      if (data.id === undefined || data.id === null || !Object.prototype.hasOwnProperty.call(pending, data.id)) {
        return;
      }

      var resolve = pending[data.id];

      delete pending[data.id];
      resolve({ result: data.result, error: data.error, meta: meta || {} });
    }

    function sessionError(code, content) {
      notify('ec.error', {
        error: {
          ucp: { version: config.version, status: 'error' },
          messages: [{ type: 'error', code: code, content: content, severity: 'unrecoverable' }],
          continue_url: config.continueUrl,
        },
      });

      state = 'closed';
    }

    function isSuccess(result) {
      return !!(result && result.ucp && result.ucp.status !== 'error');
    }

    function scheduleRefresh(expiresIn) {
      if (typeof expiresIn !== 'number' || expiresIn <= 0) {
        return;
      }

      // Ask for a new credential a minute before this one runs out.
      setTimer(function () { refreshCredential(1); }, Math.max(expiresIn - 60, 5) * 1000);
    }

    function verify(credential) {
      return Promise.resolve(options.verifyCredential(credential)).then(function (answer) {
        if (answer && answer.valid) {
          scheduleRefresh(answer.expires_in);

          return true;
        }

        return false;
      }, function () {
        return false;
      });
    }

    function refreshCredential(attempt) {
      if (state !== 'ready') {
        return Promise.resolve();
      }

      return request('ec.auth', { type: auth.type }).then(function (answer) {
        var result = answer.result;

        if (isSuccess(result) && typeof result.credential === 'string' && result.credential) {
          return verify(result.credential).then(function (valid) {
            if (!valid) {
              sessionError('not_supported_error', 'The credential the host gave is not valid for this checkout.');
            }
          });
        }

        var messages = (result && result.messages) || [];
        var recoverable = messages.some(function (message) { return message && message.severity === 'recoverable'; });

        if (recoverable && attempt < 3) {
          setTimer(function () { refreshCredential(attempt + 1); }, 5000);

          return undefined;
        }

        sessionError((messages[0] && messages[0].code) || 'not_supported_error', 'The host could not renew its credential.');

        return undefined;
      });
    }

    function begin() {
      state = 'ready';

      if (auth && !auth.required) {
        scheduleRefresh(auth.expiresIn);
      }

      options.loadCheckout();

      return true;
    }

    function ready() {
      state = 'handshaking';

      var params = { delegate: (config.delegate || []).slice() };

      if (auth && auth.required) {
        params.auth = { type: auth.type };
      }

      return request('ec.ready', params).then(function (answer) {
        var result = answer.result;

        // A handshake error ends the session: nothing more may be sent.
        if (answer.error || !isSuccess(result)) {
          state = 'closed';

          return false;
        }

        channel.lock(answer.meta.origin);

        if (result.upgrade) {
          var port = result.upgrade.port || (answer.meta.ports && answer.meta.ports[0]);

          if (channel.upgrade(port, receive)) {
            return ready();
          }
        }

        if (result.ucp.version && result.ucp.version !== config.version) {
          sessionError('invalid_state_error', 'The host confirmed another UCP version than the checkout\'s.');

          return false;
        }

        if (params.auth) {
          if (typeof result.credential !== 'string' || !result.credential) {
            sessionError('not_supported_error', 'The host gave no credential.');

            return false;
          }

          return verify(result.credential).then(function (valid) {
            if (!valid) {
              sessionError('not_supported_error', 'The credential the host gave is not valid for this checkout.');

              return false;
            }

            return begin();
          });
        }

        return begin();
      });
    }

    // Reads the checkout after a checkout page loads and tells the host what changed.
    function refresh() {
      if (state !== 'ready') {
        return Promise.resolve();
      }

      return Promise.resolve(options.fetchCheckout()).then(function (checkout) {
        if (!checkout || state !== 'ready') {
          return;
        }

        if (!last) {
          notify('ec.start', { checkout: checkout });
        } else {
          changedMethods(last, checkout).forEach(function (method) {
            notify(method, { checkout: checkout });
          });
        }

        last = checkout;

        if (!completed && checkout.status === 'completed') {
          completed = true;
          notify('ec.complete', { checkout: checkout });
        }
      }, function () { });
    }

    return {
      start: function () {
        channel.listen(receive);

        return ready();
      },
      refresh: refresh,
      receive: receive,
      get state() {
        return state;
      },
    };
  }

  function readConfig(doc) {
    var element = doc.getElementById(CONFIG_ID);

    if (!element) {
      return null;
    }

    try {
      return JSON.parse(element.textContent || 'null');
    } catch (e) {
      return null;
    }
  }

  // The checkout the page opens must be a page of this store: an http(s) address on the page's own origin. Anything
  // else (another site, a javascript: or data: address) is refused, so the page never navigates the frame to it.
  function sameOriginUrl(value, origin) {
    if (typeof value !== 'string' || value.length === 0) {
      return null;
    }

    var url;

    try {
      url = new URL(value, origin);
    } catch (e) {
      return null;
    }

    return (url.protocol === 'https:' || url.protocol === 'http:') && url.origin === origin ? url.href : null;
  }

  function applyColorScheme(doc, scheme) {
    if (scheme !== 'light' && scheme !== 'dark') {
      return;
    }

    doc.documentElement.setAttribute('data-bs-theme', scheme);
    doc.documentElement.style.colorScheme = scheme;
  }

  // Keeps the buyer in the checkout: a link to another page opens outside the frame.
  function keepInCheckout(win, doc, config) {
    doc.addEventListener('click', function (event) {
      var target = /** @type {Element} */ (event.target);
      var anchor = target && typeof target.closest === 'function' ? target.closest('a[href]') : null;

      if (!anchor || event.defaultPrevented) {
        return;
      }

      var href = /** @type {HTMLAnchorElement} */ (anchor).href;

      if (!/^https?:/i.test(href) || isCheckoutUrl(href, win.location.origin, config.checkoutPaths)) {
        return;
      }

      event.preventDefault();
      win.open(href, '_blank', 'noopener');
    }, true);
  }

  function start(win, doc) {
    var config = readConfig(doc);
    var frame = /** @type {HTMLIFrameElement} */ (doc.getElementById(FRAME_ID));

    var checkoutUrl = config ? sameOriginUrl(config.checkoutUrl, win.location.origin) : null;

    if (!config || !frame || !checkoutUrl) {
      return null;
    }

    applyColorScheme(doc, config.colorScheme);

    var consumer = nativeConsumer(win);

    // Opened on its own, not framed by a platform: there is no host to talk to, so show the checkout.
    if (!consumer && win.parent === win) {
      win.location.replace(checkoutUrl);

      return null;
    }

    var channel = consumer ? createNativeChannel(win, consumer) : createWindowChannel(win, config.hostOrigins);

    var session = createSession({
      config: config,
      channel: channel,
      loadCheckout: function () {
        frame.setAttribute('src', checkoutUrl);
      },
      fetchCheckout: function () {
        return win.fetch(config.stateUrl, { credentials: 'same-origin', cache: 'no-store' })
          .then(function (response) { return response.ok ? response.json() : null; })
          .then(function (json) { return json && json.checkout; });
      },
      verifyCredential: function (credential) {
        return win.fetch(config.authUrl, {
          method: 'POST',
          credentials: 'same-origin',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ credential: credential }),
        }).then(function (response) { return response.ok ? response.json() : { valid: false }; });
      },
    });

    frame.addEventListener('load', function () {
      if (!frame.getAttribute('src')) {
        return;
      }

      var inner;

      try {
        inner = frame.contentDocument;
      } catch (e) {
        inner = null;
      }

      if (inner) {
        applyColorScheme(inner, config.colorScheme);
        keepInCheckout(win, inner, config);
      }

      session.refresh();
    });

    session.start();

    return session;
  }

  globalThis.CrestAppsEmbeddedCheckout = {
    changedMethods: changedMethods,
    isCheckoutUrl: isCheckoutUrl,
    sameOriginUrl: sameOriginUrl,
    createSession: createSession,
    createWindowChannel: createWindowChannel,
    createNativeChannel: createNativeChannel,
    nativeConsumer: nativeConsumer,
    start: start,
  };

  if (typeof document !== 'undefined' && document.getElementById(CONFIG_ID)) {
    start(window, document);
  }
})();
