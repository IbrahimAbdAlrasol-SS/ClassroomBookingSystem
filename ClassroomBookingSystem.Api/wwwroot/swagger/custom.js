(function () {
  function pad(n) { return n < 10 ? '0' + n : '' + n; }
  function fmt(dt) {
    return (
      dt.getFullYear() + '-' + pad(dt.getMonth() + 1) + '-' + pad(dt.getDate()) +
      'T' + pad(dt.getHours()) + ':' + pad(dt.getMinutes()) + ':' + pad(dt.getSeconds())
    );
  }
  function computeTimes() {
    var now = new Date();
    var start = new Date(now); // الآن
    var end = new Date(now.getTime() + (60 + 20) * 60000); // + 1 ساعة و20 دقيقة
    return { start: fmt(start), end: fmt(end) };
  }

  function tryFillTextarea(textarea) {
    if (!textarea) return;
    var val = textarea.value || '';
    if (!val) return;

    // حاول كـ JSON أولاً
    try {
      var obj = JSON.parse(val);
      var times = computeTimes();
      var changed = false;
      if (Object.prototype.hasOwnProperty.call(obj, 'startsAt')) { obj.startsAt = times.start; changed = true; }
      if (Object.prototype.hasOwnProperty.call(obj, 'endsAt')) { obj.endsAt = times.end; changed = true; }
      if (changed) textarea.value = JSON.stringify(obj, null, 2);
      return;
    } catch (_) { /* ليس JSON صالحاً، سنحاول بالاستبدال النصي */ }

    try {
      var t = computeTimes();
      var updated = val.replace(/("startsAt"\s*:\s*")[^"]*(")/i, '$1' + t.start + '$2');
      updated = updated.replace(/("endsAt"\s*:\s*")[^"]*(")/i, '$1' + t.end + '$2');
      if (updated !== val) textarea.value = updated;
    } catch (_) { /* تجاهل */ }
  }

  function handleOpblock(opblock) {
    if (!opblock) return;
    var textareas = opblock.querySelectorAll('textarea');
    if (!textareas || !textareas.length) return;
    textareas.forEach(function (t) {
      var content = (t.value || t.textContent || '');
      if (/startsAt|endsAt/i.test(content)) {
        tryFillTextarea(t);
      }
    });
  }

  function hookTryItOut() {
    document.body.addEventListener('click', function (e) {
      var btn = e.target && e.target.closest ? e.target.closest('button') : null;
      if (!btn) return;
      var className = btn.className || '';
      if (className.indexOf('try-out') !== -1) {
        // انتظر حتى يُنشئ Swagger مربع النص
        setTimeout(function () {
          var opblock = btn.closest('.opblock');
          handleOpblock(opblock);
        }, 100);
      }
    }, true);
  }

  function initialFill() {
    // لو أنّ هناك مربعات نص ظاهرة مسبقاً
    setTimeout(function () {
      document.querySelectorAll('.opblock').forEach(handleOpblock);
    }, 800);
  }

  function observeMutations() {
    var obs = new MutationObserver(function (mutations) {
      mutations.forEach(function (m) {
        m.addedNodes.forEach(function (node) {
          if (!node || node.nodeType !== 1) return;
          // لو تمت إضافة opblock أو textarea جديد
          if (node.matches && node.matches('.opblock')) {
            handleOpblock(node);
          } else {
            var tas = node.querySelectorAll ? node.querySelectorAll('textarea') : [];
            tas.forEach(function (t) {
              var content = (t.value || t.textContent || '');
              if (/startsAt|endsAt/i.test(content)) tryFillTextarea(t);
            });
          }
        });
      });
    });
    obs.observe(document.body, { childList: true, subtree: true });
  }

  function ready(fn) {
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', fn);
    else fn();
  }

  ready(function () {
    hookTryItOut();
    initialFill();
    observeMutations();
    console.log('Swagger custom.js loaded: auto-filling startsAt/endsAt with device time (+1h20m) for booking requests in Swagger only.');
  });
})();