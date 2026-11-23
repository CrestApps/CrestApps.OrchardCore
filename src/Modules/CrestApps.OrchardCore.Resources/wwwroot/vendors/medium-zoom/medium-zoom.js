/*! medium-zoom 1.1.2 | MIT License | https://github.com/francoischalifour/medium-zoom */
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
(function (global, factory) {
  typeof exports === 'object' && typeof module !== 'undefined' ? module.exports = factory() : typeof define === 'function' && define.amd ? define([], factory) : (global = typeof globalThis !== 'undefined' ? globalThis : global || self, global.mediumZoom = factory());
})(this, function () {
  "use strict";

  // src/utils.js
  var isSupported = function isSupported(node) {
    return node.tagName === "IMG";
  };
  var isNodeList = function isNodeList(selector) {
    return NodeList.prototype.isPrototypeOf(selector);
  };
  var isNode = function isNode(selector) {
    return selector && selector.nodeType === 1;
  };
  var isSvg = function isSvg(image) {
    return (image.currentSrc || image.src).substr(-4).toLowerCase() === ".svg";
  };
  var getImagesFromSelector = function getImagesFromSelector(selector) {
    try {
      if (Array.isArray(selector)) return selector.filter(isSupported);
      if (isNodeList(selector)) return [].slice.call(selector).filter(isSupported);
      if (isNode(selector)) return [selector].filter(isSupported);
      if (typeof selector === "string") return [].slice.call(document.querySelectorAll(selector)).filter(isSupported);
      return [];
    } catch (err) {
      throw new TypeError("The provided selector is invalid.\nExpects a CSS selector, a Node element, a NodeList or an array.\nSee: https://github.com/francoischalifour/medium-zoom");
    }
  };
  var createOverlay = function createOverlay(background) {
    var overlay = document.createElement("div");
    overlay.classList.add("medium-zoom-overlay");
    overlay.style.background = background;
    return overlay;
  };
  var cloneTarget = function cloneTarget(template) {
    var _template$getBounding = template.getBoundingClientRect(),
      top = _template$getBounding.top,
      left = _template$getBounding.left,
      width = _template$getBounding.width,
      height = _template$getBounding.height;
    var clone = template.cloneNode();
    var scrollTop = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
    var scrollLeft = window.pageXOffset || document.documentElement.scrollLeft || document.body.scrollLeft || 0;
    clone.removeAttribute("id");
    clone.style.position = "absolute";
    clone.style.top = top + scrollTop + "px";
    clone.style.left = left + scrollLeft + "px";
    clone.style.width = width + "px";
    clone.style.height = height + "px";
    clone.style.transform = "";
    return clone;
  };
  var createCustomEvent = function createCustomEvent(type, detail) {
    if (typeof window.CustomEvent === "function") return new CustomEvent(type, {
      detail: detail
    });
    var customEvent = document.createEvent("CustomEvent");
    customEvent.initCustomEvent(type, false, false, detail);
    return customEvent;
  };

  // src/medium-zoom.js
  var _mediumZoom = function mediumZoom(selector, options) {
    if (options === void 0) {
      options = {};
    }
    /**
    * Ensure the compatibility with IE11 if no Promise polyfill are used.
    */
    var Promise = window.Promise || function Promise(fn) {
      function noop() {}
      fn(noop, noop);
    };
    var _dispatchEvent = function _dispatchEvent(image, type) {
      return image.dispatchEvent(createCustomEvent("medium-zoom:" + type, {
        zoom: zoom
      }));
    };
    var _handleClick = function _handleClick(event) {
      var target = event.target;
      if (target === overlay) {
        _close();
        return;
      }
      if (images.indexOf(target) === -1) return;
      toggle({
        target: target
      });
    };
    var _handleScroll = function _handleScroll() {
      if (!active.__mz_original) return;
      var currentScroll = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
      if (active.__mz_isPinching || window.visualViewport && window.visualViewport.scale > active.__mz_viewportScale) {
        scrollTop = currentScroll;
        clearTimeout(active.__mz_scrollTimer);
        active.__mz_scrollTimer = null;
        return;
      }
      if (isAnimating) return;
      if (Math.abs(scrollTop - currentScroll) > zoomOptions.scrollOffset && !active.__mz_scrollTimer) active.__mz_scrollTimer = setTimeout(_close, 150);
    };
    var _handleTouch = function _handleTouch(event) {
      if (event.touches.length >= 2) active.__mz_isPinching = true;
      if (active.__mz_isPinching) _handleScroll();
      if (event.touches.length === 0) active.__mz_isPinching = false;
    };
    var _handleKeyUp = function _handleKeyUp(event) {
      var key = event.key || event.keyCode;
      if (key === "Escape" || key === "Esc" || key === 27) _close();
    };
    var update = function update(options) {
      if (options === void 0) {
        options = {};
      }
      var newOptions = options;
      if (options.background) overlay.style.background = options.background;
      if (options.container && options.container instanceof Object) newOptions.container = _extends({}, zoomOptions.container, options.container);
      if (options.template) newOptions.template = isNode(options.template) ? options.template : document.querySelector(options.template);
      zoomOptions = _extends({}, zoomOptions, newOptions);
      images.forEach(function (image) {
        _dispatchEvent(image, "update");
      });
      return zoom;
    };
    var clone = function clone(options) {
      return _mediumZoom(_extends({}, zoomOptions, options));
    };
    var clickListening = false;
    var _updateClickListener = function _updateClickListener() {
      var enabled = images.length > 0;
      if (clickListening === enabled) return;
      document[enabled ? "addEventListener" : "removeEventListener"]("click", _handleClick);
      clickListening = enabled;
    };
    var attach = function attach() {
      for (var _len = arguments.length, selectors = new Array(_len), _key = 0; _key < _len; _key++) {
        selectors[_key] = arguments[_key];
      }
      var newImages = selectors.reduce(function (imagesAccumulator, currentSelector) {
        return [].concat(imagesAccumulator, getImagesFromSelector(currentSelector));
      }, []);
      newImages.filter(function (newImage) {
        return images.indexOf(newImage) === -1;
      }).forEach(function (newImage) {
        images.push(newImage);
        newImage.classList.add("medium-zoom-image");
      });
      eventListeners.forEach(function (_ref) {
        var type = _ref.type,
          listener = _ref.listener,
          options = _ref.options;
        newImages.forEach(function (image) {
          image.addEventListener(type, listener, options);
        });
      });
      _updateClickListener();
      return zoom;
    };
    var detach = function detach() {
      if (active.__mz_zoomed) _close();
      for (var _len2 = arguments.length, selectors = new Array(_len2), _key2 = 0; _key2 < _len2; _key2++) {
        selectors[_key2] = arguments[_key2];
      }
      var imagesToDetach = selectors.length > 0 ? selectors.reduce(function (imagesAccumulator, currentSelector) {
        return [].concat(imagesAccumulator, getImagesFromSelector(currentSelector));
      }, []) : images;
      imagesToDetach.forEach(function (image) {
        image.classList.remove("medium-zoom-image");
        _dispatchEvent(image, "detach");
      });
      images = images.filter(function (image) {
        return imagesToDetach.indexOf(image) === -1;
      });
      _updateClickListener();
      return zoom;
    };
    var on = function on(type, listener, options) {
      if (options === void 0) {
        options = {};
      }
      var eventType = "medium-zoom:" + type;
      images.forEach(function (image) {
        image.addEventListener(eventType, listener, options);
      });
      eventListeners.push({
        type: eventType,
        listener: listener,
        options: options
      });
      return zoom;
    };
    var off = function off(type, listener, options) {
      if (options === void 0) {
        options = {};
      }
      var eventType = "medium-zoom:" + type;
      images.forEach(function (image) {
        image.removeEventListener(eventType, listener, options);
      });
      eventListeners = eventListeners.filter(function (eventListener) {
        return !(eventListener.type === eventType && eventListener.listener.toString() === listener.toString());
      });
      return zoom;
    };
    var globalListening = false;
    var _updateGlobalEvents = function _updateGlobalEvents(enabled) {
      if (globalListening === enabled) return;
      var method = enabled ? "addEventListener" : "removeEventListener";
      document[method]("keyup", _handleKeyUp);
      document[method]("scroll", _handleScroll);
      var touchOptions = {
        passive: true
      };
      ["touchstart", "touchend", "touchcancel"].forEach(function (type) {
        return document[method](type, _handleTouch, touchOptions);
      });
      window[method]("resize", _close);
      globalListening = enabled;
    };
    var _waitForTransition = function _waitForTransition(image, done) {
      var timer;
      var frame;
      var finished = false;
      var query = window.matchMedia("(prefers-reduced-motion)");
      var getTime = function getTime(value) {
        return Math.max.apply(Math, value.split(",").map(parseFloat));
      };
      var finish = function finish(event) {
        if (finished || event && event.propertyName !== "transform") return;
        cleanup();
        done();
      };
      var motionChange = function motionChange() {
        if (query.matches) finish();
      };
      var cleanup = function cleanup() {
        finished = true;
        clearTimeout(timer);
        window.cancelAnimationFrame(frame);
        image.removeEventListener("transitionend", finish);
        query.removeListener(motionChange);
      };
      image.addEventListener("transitionend", finish);
      query.addListener(motionChange);
      var style = window.getComputedStyle(image);
      var duration = (getTime(style.transitionDuration) + getTime(style.transitionDelay)) * 1e3;
      timer = setTimeout(finish, Math.max(0, duration) + 50);
      if (duration <= 0) frame = window.requestAnimationFrame(function () {
        return finish();
      });
      return cleanup;
    };
    var open = function open(_temp) {
      var _ref2 = _temp === void 0 ? {} : _temp,
        target = _ref2.target;
      var current = {};
      var _animate = function _animate() {
        var container = {
          width: document.documentElement.clientWidth,
          height: document.documentElement.clientHeight,
          left: 0,
          top: 0,
          right: 0,
          bottom: 0
        };
        var viewportWidth;
        var viewportHeight;
        if (zoomOptions.container) {
          if (zoomOptions.container instanceof Object) {
            container = _extends({}, container, zoomOptions.container);
            viewportWidth = container.width - container.left - container.right - zoomOptions.margin * 2;
            viewportHeight = container.height - container.top - container.bottom - zoomOptions.margin * 2;
          } else {
            var _getBoundingClientRec = (isNode(zoomOptions.container) ? zoomOptions.container : document.querySelector(zoomOptions.container)).getBoundingClientRect(),
              _width = _getBoundingClientRec.width,
              _height = _getBoundingClientRec.height,
              _left = _getBoundingClientRec.left,
              _top = _getBoundingClientRec.top;
            container = _extends({}, container, {
              width: _width,
              height: _height,
              left: _left,
              top: _top
            });
          }
        }
        viewportWidth = viewportWidth || container.width - zoomOptions.margin * 2;
        viewportHeight = viewportHeight || container.height - zoomOptions.margin * 2;
        var zoomTarget = current.__mz_zoomedHd || current.__mz_original;
        var naturalWidth = isSvg(zoomTarget) ? viewportWidth : zoomTarget.naturalWidth || viewportWidth;
        var naturalHeight = isSvg(zoomTarget) ? viewportHeight : zoomTarget.naturalHeight || viewportHeight;
        var _zoomTarget$getBoundi = zoomTarget.getBoundingClientRect(),
          top = _zoomTarget$getBoundi.top,
          left = _zoomTarget$getBoundi.left,
          width = _zoomTarget$getBoundi.width,
          height = _zoomTarget$getBoundi.height;
        if (!width || !height) return;
        var scaleX = Math.min(Math.max(width, naturalWidth), viewportWidth) / width;
        var scaleY = Math.min(Math.max(height, naturalHeight), viewportHeight) / height;
        var scale = Math.min(scaleX, scaleY);
        var transform = "scale(" + scale + ") translate3d(" + (-left + (viewportWidth - width) / 2 + zoomOptions.margin + container.left) / scale + "px, " + (-top + (viewportHeight - height) / 2 + zoomOptions.margin + container.top) / scale + "px, 0)";
        current.__mz_zoomed.style.transform = transform;
        if (current.__mz_zoomedHd) current.__mz_zoomedHd.style.transform = transform;
      };
      return new Promise(function (resolve) {
        if (active.__mz_original || target && images.indexOf(target) === -1) {
          resolve(zoom);
          return;
        }
        active = current;
        var cancelTransition = function cancelTransition() {};
        var cancelImage = function cancelImage() {};
        var frame;
        var finishOpening = function finishOpening(opened) {
          if (!current.__mz_cancelOpen) return;
          cancelTransition();
          cancelImage();
          window.cancelAnimationFrame(frame);
          current.__mz_cancelOpen = null;
          isAnimating = false;
          resolve(zoom);
          if (opened) {
            document.body.classList.add("medium-zoom--opened");
            _dispatchEvent(current.__mz_original, "opened");
          }
        };
        current.__mz_cancelOpen = finishOpening;
        current.__mz_original = target || images[0];
        if (!current.__mz_original) {
          resolve(zoom);
          return;
        }
        current.__mz_viewportScale = window.visualViewport ? window.visualViewport.scale : 1;
        _updateGlobalEvents(true);
        isAnimating = true;
        _dispatchEvent(current.__mz_original, "open");
        if (current.__mz_closing) return;
        scrollTop = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
        current.__mz_zoomed = cloneTarget(current.__mz_original);
        document.body.appendChild(overlay);
        if (zoomOptions.template) {
          var template = isNode(zoomOptions.template) ? zoomOptions.template : document.querySelector(zoomOptions.template);
          current.__mz_template = document.createElement("div");
          current.__mz_template.appendChild(template.content.cloneNode(true));
          document.body.appendChild(current.__mz_template);
        }
        if (current.__mz_original.parentElement && current.__mz_original.parentElement.tagName === "PICTURE" && current.__mz_original.currentSrc) current.__mz_zoomed.src = current.__mz_original.currentSrc;
        document.body.appendChild(current.__mz_zoomed);
        frame = window.requestAnimationFrame(function () {
          document.body.classList.add("medium-zoom--opened");
        });
        current.__mz_original.classList.add("medium-zoom-image--hidden");
        current.__mz_zoomed.classList.add("medium-zoom-image--opened");
        current.__mz_zoomed.addEventListener("click", _close);
        var animate = function animate() {
          _animate();
          cancelTransition = _waitForTransition(current.__mz_zoomed, function () {
            return finishOpening(true);
          });
        };
        var zoomSrc = current.__mz_original.getAttribute("data-zoom-src");
        if (zoomSrc || current.__mz_original.hasAttribute("srcset")) {
          var hd = document.createElement("img");
          [].slice.call(current.__mz_zoomed.attributes).forEach(function (_ref3) {
            var name = _ref3.name,
              value = _ref3.value;
            if (!/^(src|srcset|sizes|loading)$/.test(name)) hd.setAttribute(name, value);
          });
          current.__mz_zoomedHd = hd;
          var finishImage = function finishImage() {
            cancelImage();
            if (current.__mz_closing) return;
            if (hd.naturalWidth) {
              hd.classList.add("medium-zoom-image--opened");
              hd.addEventListener("click", _close);
              document.body.appendChild(hd);
            } else {
              console.warn("Unable to reach the zoom image target " + (hd.currentSrc || hd.src));
              current.__mz_zoomedHd = null;
            }
            animate();
          };
          cancelImage = function cancelImage() {
            hd.removeEventListener("load", finishImage);
            hd.removeEventListener("error", finishImage);
          };
          hd.addEventListener("load", finishImage);
          hd.addEventListener("error", finishImage);
          if (!zoomSrc) hd.srcset = current.__mz_zoomed.srcset;
          hd.src = zoomSrc || current.__mz_zoomed.src;
        } else animate();
      });
    };
    var _close = function close() {
      return new Promise(function (resolve) {
        var current = active;
        if (!current.__mz_original || current.__mz_closing) {
          resolve(zoom);
          return;
        }
        current.__mz_closing = true;
        if (current.__mz_cancelOpen) current.__mz_cancelOpen();
        clearTimeout(current.__mz_scrollTimer);
        isAnimating = true;
        var finishClosing = function finishClosing() {
          current.__mz_original.classList.remove("medium-zoom-image--hidden");
          [current.__mz_zoomed, current.__mz_zoomedHd, current.__mz_template, overlay].forEach(function (node) {
            if (!node) return;
            node.removeEventListener("click", _close);
            if (node.parentNode) node.parentNode.removeChild(node);
          });
          active = {};
          isAnimating = false;
          _updateGlobalEvents(false);
          resolve(zoom);
          _dispatchEvent(current.__mz_original, "closed");
        };
        if (current.__mz_zoomed) _waitForTransition(current.__mz_zoomed, finishClosing);
        document.body.classList.remove("medium-zoom--opened");
        if (current.__mz_zoomed) current.__mz_zoomed.style.transform = "";
        if (current.__mz_zoomedHd) current.__mz_zoomedHd.style.transform = "";
        if (current.__mz_template) {
          current.__mz_template.style.transition = parseFloat(window.getComputedStyle(current.__mz_zoomed).transitionDuration) ? "opacity 150ms" : "none";
          current.__mz_template.style.opacity = 0;
        }
        _dispatchEvent(current.__mz_original, "close");
        if (!current.__mz_zoomed) finishClosing();
      });
    };
    var toggle = function toggle(_temp2) {
      var _ref4 = _temp2 === void 0 ? {} : _temp2,
        target = _ref4.target;
      if (active.__mz_original) return _close();
      return open({
        target: target
      });
    };
    var getOptions = function getOptions() {
      return zoomOptions;
    };
    var getImages = function getImages() {
      return images;
    };
    var getZoomedImage = function getZoomedImage() {
      return active.__mz_original || null;
    };
    var images = [];
    var eventListeners = [];
    var isAnimating = false;
    var scrollTop = 0;
    var zoomOptions = options;
    var active = {};
    if (Object.prototype.toString.call(selector) === "[object Object]") zoomOptions = selector;else if (selector || typeof selector === "string") attach(selector);
    zoomOptions = _extends({
      margin: 0,
      background: "#fff",
      scrollOffset: 40,
      container: null,
      template: null
    }, zoomOptions);
    var overlay = createOverlay(zoomOptions.background);
    var zoom = {
      open: open,
      close: _close,
      toggle: toggle,
      update: update,
      clone: clone,
      attach: attach,
      detach: detach,
      on: on,
      off: off,
      getOptions: getOptions,
      getImages: getImages,
      getZoomedImage: getZoomedImage
    };
    return zoom;
  };

  // \0medium-zoom-stylesheet
  if (typeof document !== "undefined") {
    var style = document.createElement("style");
    style.textContent = ".medium-zoom-overlay{opacity:0;will-change:opacity;transition:opacity .3s;position:fixed;top:0;bottom:0;left:0;right:0}.medium-zoom--opened .medium-zoom-overlay{cursor:pointer;cursor:zoom-out;opacity:1}.medium-zoom-image{cursor:pointer;cursor:zoom-in;transition:transform .3s cubic-bezier(.2,0,.2,1)!important}.medium-zoom-image--hidden{visibility:hidden}.medium-zoom-image--opened{cursor:pointer;cursor:zoom-out;will-change:transform;position:relative}@media (prefers-reduced-motion:reduce){.medium-zoom-image,.medium-zoom-overlay{transition:none!important}}";
    document.head.appendChild(style);
  }

  // src/index.js
  var src_default = _mediumZoom;
  return src_default;
});