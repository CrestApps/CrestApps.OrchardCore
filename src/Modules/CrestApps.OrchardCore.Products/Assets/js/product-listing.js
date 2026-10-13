/*
 * Product listing pages.
 *
 * The listing works without this script: the filters are an ordinary GET form and every chip, sort, layout and
 * page link is a real URL. The script only makes it quicker. On wide screens a filter applies as soon as it
 * changes, and the results are fetched and swapped in place, with the address bar kept in step so the back
 * button and bookmarks still work. On narrow screens the filters sit in an off-canvas panel, so they are applied
 * with its "Show results" button instead of on every tap.
 */
(function () {
  'use strict';

  var wideScreen = window.matchMedia('(min-width: 992px)');
  var pending = null;

  function containerOf(element) {
    return element.closest('[data-product-listing]');
  }

  function statusOf(container) {
    var id = container.getAttribute('data-product-listing');
    return document.querySelector('[data-product-listing-status="' + id + '"]');
  }

  function formUrl(form) {
    var params = new URLSearchParams();

    new FormData(form).forEach(function (value, key) {
      if (typeof value !== 'string' || value.trim() === '') {
        return;
      }

      // A sort, page size or layout that is already the default stays out of the address, so the unfiltered
      // listing keeps one URL.
      var element = form.elements.namedItem(key);

      if (element && element.getAttribute && element.getAttribute('data-default') === value) {
        return;
      }

      params.append(key, value.trim());
    });

    var query = params.toString();
    var action = form.getAttribute('action') || window.location.pathname;

    return query ? action + '?' + query : action;
  }

  function load(container, url, push) {
    if (pending) {
      pending.abort();
    }

    pending = new AbortController();
    container.setAttribute('aria-busy', 'true');
    container.classList.add('opacity-50');

    fetch(url, { signal: pending.signal, headers: { 'X-Requested-With': 'XMLHttpRequest' } })
      .then(function (response) {
        if (!response.ok) {
          throw new Error('The listing could not be loaded.');
        }

        return response.text();
      })
      .then(function (html) {
        var id = container.getAttribute('data-product-listing');
        var next = new DOMParser()
          .parseFromString(html, 'text/html')
          .querySelector('[data-product-listing="' + id + '"]');

        if (!next) {
          window.location.assign(url);
          return;
        }

        container.innerHTML = next.innerHTML;
        enhance(container);

        if (push) {
          window.history.pushState({ productListing: id }, '', url);
        }

        var status = statusOf(container);
        var summary = container.querySelector('[data-product-listing-summary]');

        if (status && summary) {
          status.textContent = summary.textContent.trim();
        }
      })
      .catch(function (error) {
        if (error.name !== 'AbortError') {
          window.location.assign(url);
        }
      })
      .finally(function () {
        container.removeAttribute('aria-busy');
        container.classList.remove('opacity-50');
      });
  }

  function enhance(container) {
    container.querySelectorAll('[data-product-listing-apply]').forEach(function (button) {
      button.classList.add('d-lg-none');
    });
  }

  document.addEventListener('change', function (event) {
    var input = event.target;
    var form = input.form;

    if (!form || !form.hasAttribute('data-product-listing-form') || !wideScreen.matches) {
      return;
    }

    // A minimum or a maximum is applied when the visitor leaves the box or presses Enter, not on each digit.
    if (input.type === 'number' && event.type === 'change' && document.activeElement === input) {
      return;
    }

    var container = containerOf(form);

    if (container) {
      load(container, formUrl(form), true);
    }
  });

  document.addEventListener('submit', function (event) {
    var form = event.target;

    if (!form.hasAttribute('data-product-listing-form') || !wideScreen.matches) {
      return;
    }

    var container = containerOf(form);

    if (container) {
      event.preventDefault();
      load(container, formUrl(form), true);
    }
  });

  document.addEventListener('click', function (event) {
    var link = event.target.closest('a[data-product-listing-link]');

    if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.button !== 0) {
      return;
    }

    var container = containerOf(link);

    if (container) {
      event.preventDefault();
      load(container, link.getAttribute('href'), true);
      container.scrollIntoView({ block: 'start', behavior: 'smooth' });
    }
  });

  window.addEventListener('popstate', function () {
    var container = document.querySelector('[data-product-listing]');

    if (container) {
      load(container, window.location.href, false);
    }
  });

  document.querySelectorAll('[data-product-listing]').forEach(enhance);
})();
