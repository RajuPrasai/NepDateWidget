(function () {
  'use strict';

  var enc = encodeURIComponent;
  var STEP = 56;

  var PLATFORMS = [
    {
      id: 'x',
      label: 'Share on X',
      bg: '#0f0f0f',
      url: function (d) { return 'https://twitter.com/intent/tweet?url=' + enc(d.url) + '&text=' + enc(d.text); },
      svg: '<path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-4.714-6.231-5.401 6.231H2.726l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"/>'
    },
    {
      id: 'linkedin',
      label: 'Share on LinkedIn',
      bg: '#0a66c2',
      url: function (d) { return 'https://www.linkedin.com/sharing/share-offsite/?url=' + enc(d.url); },
      svg: '<path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433a2.062 2.062 0 01-2.063-2.065 2.064 2.064 0 112.063 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z"/>'
    },
    {
      id: 'facebook',
      label: 'Share on Facebook',
      bg: '#1877f2',
      url: function (d) { return 'https://www.facebook.com/sharer.php?u=' + enc(d.url); },
      svg: '<path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/>'
    },
    {
      id: 'reddit',
      label: 'Share on Reddit',
      bg: '#ff4500',
      url: function (d) { return 'https://www.reddit.com/submit?url=' + enc(d.url) + '&title=' + enc(d.title); },
      svg: '<path d="M12 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0zm5.01 4.744c.688 0 1.25.561 1.25 1.249a1.25 1.25 0 0 1-2.498.056l-2.597-.547-.8 3.747c1.824.07 3.48.632 4.674 1.488.308-.309.73-.491 1.207-.491.968 0 1.754.786 1.754 1.754 0 .716-.435 1.333-1.01 1.614a3.111 3.111 0 0 1 .042.52c0 2.694-3.13 4.87-7.004 4.87-3.874 0-7.004-2.176-7.004-4.87 0-.183.015-.366.043-.534A1.748 1.748 0 0 1 4.028 12c0-.968.786-1.754 1.754-1.754.463 0 .898.196 1.207.49 1.207-.883 2.878-1.43 4.744-1.487l.885-4.182a.342.342 0 0 1 .14-.197.35.35 0 0 1 .238-.042l2.906.617a1.214 1.214 0 0 1 1.108-.701zM9.25 12C8.561 12 8 12.562 8 13.25c0 .687.561 1.248 1.25 1.248.687 0 1.248-.561 1.248-1.249 0-.688-.561-1.249-1.249-1.249zm5.5 0c-.687 0-1.248.561-1.248 1.25 0 .687.561 1.248 1.249 1.248.688 0 1.249-.561 1.249-1.249 0-.687-.562-1.249-1.25-1.249zm-5.466 3.99a.327.327 0 0 0-.231.094.33.33 0 0 0 0 .463c.842.842 2.484.913 2.961.913.477 0 2.105-.056 2.961-.913a.361.361 0 0 0 .029-.463.33.33 0 0 0-.464 0c-.547.533-1.684.73-2.512.73-.828 0-1.979-.196-2.512-.73a.326.326 0 0 0-.232-.095z"/>'
    },
    {
      id: 'whatsapp',
      label: 'Share on WhatsApp',
      bg: '#25d366',
      url: function (d) { return 'https://wa.me/?text=' + enc(d.text + '\n' + d.url); },
      svg: '<path d="M17.472 14.382c-.297-.149-1.758-.867-2.03-.967-.273-.099-.471-.148-.67.15-.197.297-.767.966-.94 1.164-.173.199-.347.223-.644.075-.297-.15-1.255-.463-2.39-1.475-.883-.788-1.48-1.761-1.653-2.059-.173-.297-.018-.458.13-.606.134-.133.298-.347.446-.52.149-.174.198-.298.298-.497.099-.198.05-.371-.025-.52-.075-.149-.669-1.612-.916-2.207-.242-.579-.487-.5-.669-.51-.173-.008-.371-.01-.57-.01-.198 0-.52.074-.792.372-.272.297-1.04 1.016-1.04 2.479 0 1.462 1.065 2.875 1.213 3.074.149.198 2.096 3.2 5.077 4.487.709.306 1.262.489 1.694.625.712.227 1.36.195 1.871.118.571-.085 1.758-.719 2.006-1.413.248-.694.248-1.289.173-1.413-.074-.124-.272-.198-.57-.347m-5.421 7.403h-.004a9.87 9.87 0 01-5.031-1.378l-.361-.214-3.741.982.998-3.648-.235-.374a9.86 9.86 0 01-1.51-5.26c.001-5.45 4.436-9.884 9.888-9.884 2.64 0 5.122 1.03 6.988 2.898a9.825 9.825 0 012.893 6.994c-.003 5.45-4.437 9.884-9.885 9.884m8.413-18.297A11.815 11.815 0 0012.05 0C5.495 0 .16 5.335.157 11.892c0 2.096.547 4.142 1.588 5.945L.057 24l6.305-1.654a11.882 11.882 0 005.683 1.448h.005c6.554 0 11.89-5.335 11.893-11.893a11.821 11.821 0 00-3.48-8.413z"/>'
    },
    {
      id: 'telegram',
      label: 'Share on Telegram',
      bg: '#229ed9',
      url: function (d) { return 'https://t.me/share/url?url=' + enc(d.url) + '&text=' + enc(d.text); },
      svg: '<path d="M11.944 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0a12 12 0 0 0-.056 0zm4.962 7.224c.1-.002.321.023.465.14a.506.506 0 0 1 .171.325c.016.093.036.306.02.472-.18 1.898-.962 6.502-1.36 8.627-.168.9-.499 1.201-.82 1.23-.696.065-1.225-.46-1.9-.902-1.056-.693-1.653-1.124-2.678-1.8-1.185-.78-.417-1.21.258-1.91.177-.184 3.247-2.977 3.307-3.23.007-.032.014-.15-.056-.212s-.174-.041-.249-.024c-.106.024-1.793 1.14-5.061 3.345-.48.33-.913.49-1.302.48-.428-.008-1.252-.241-1.865-.44-.752-.245-1.349-.374-1.297-.789.027-.216.325-.437.893-.663 3.498-1.524 5.83-2.529 6.998-3.014 3.332-1.386 4.025-1.627 4.476-1.635z"/>'
    },
    {
      id: 'copy',
      label: 'Copy link',
      bg: '#4b5563',
      url: null,
      svg: '<path d="M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z"/>'
    }
  ];

  var SITE_TITLE = 'Premium Nepali Calendar, Date Converter, and Productivity Tools for PC';
  var SITE_TAGLINE = 'A deceptively powerful nepali desktop companion living quietly on your taskbar. Packed with worthy 25+ thoughtfully crafted tools that speaks quality.';

  function getShareData() {
    var url = window.location.href.replace(/#.*$/, '');
    var ogTitle = document.querySelector('meta[property="og:title"]');
    var ogDesc = document.querySelector('meta[property="og:description"]');
    var metaDesc = document.querySelector('meta[name="description"]');
    var title = (ogTitle && ogTitle.getAttribute('content')) || document.title || SITE_TITLE;
    var text = (ogDesc && ogDesc.getAttribute('content')) || (metaDesc && metaDesc.getAttribute('content')) || SITE_TAGLINE;
    return { title: title, text: text, url: url };
  }

  var style = document.createElement('style');
  style.textContent = [
    '#sw-fab{position:fixed;bottom:22px;right:22px;z-index:2147483647;width:55px;height:55px;}',
    '#sw-fab *{box-sizing:border-box;}',
    '#sw-fab-t{position:relative;z-index:2;display:flex;align-items:center;justify-content:center;',
    'width:55px;height:55px;border-radius:50%;background:#dc3f10;border:none;cursor:pointer;',
    'color:#fff;box-shadow:0 4px 14px rgba(0,0,0,.32);transition:box-shadow .2s,transform .2s;outline:none;}',
    '#sw-fab-t:hover{transform:scale(1.07);box-shadow:0 6px 20px rgba(0,0,0,.38);}',
    '#sw-fab-t:focus-visible{box-shadow:0 0 0 3px rgba(220,63,16,.45),0 4px 14px rgba(0,0,0,.32);}',
    '#sw-fab-t svg{position:absolute;transition:transform .28s ease,opacity .2s ease;}',
    '#sw-fab-t .sw-si{transform:scale(1.5) rotate(0deg); margin-left:-4px;opacity:1;}',
    '#sw-fab-t.sw-open .sw-si{transform:scale(0) rotate(90deg);opacity:0;}',
    '#sw-fab-t .sw-xi{transform:scale(0) rotate(-90deg);opacity:0;}',
    '#sw-fab-t.sw-open .sw-xi{transform:scale(1) rotate(0deg);opacity:1;}',
    '.sw-pb{position:absolute;bottom:4px;right:4px;z-index:1;',
    'display:flex;align-items:center;justify-content:center;',
    'width:48px;height:48px;border-radius:50%;border:none;cursor:pointer;',
    'color:#fff;text-decoration:none;',
    'transform:scale(0);opacity:0;pointer-events:none;',
    'transition:transform .22s cubic-bezier(.34,1.56,.64,1),opacity .18s ease,box-shadow .2s;}',
    '.sw-pb:hover{filter:brightness(1.12);}',
    '.sw-pb:focus-visible{box-shadow:0 0 0 3px rgba(255,255,255,.45);}',
    '.sw-ck{display:none;}',
    '#sw-fab-t::before{content:"Share";position:absolute;right:calc(100% + 10px);top:50%;transform:translateY(-50%) translateX(8px);background:#dc3f10;color:#fff;border-radius:20px;padding:5px 12px;font-size:13px;font-weight:600;letter-spacing:.02em;font-family:system-ui,-apple-system,sans-serif;white-space:nowrap;opacity:0;pointer-events:none;box-shadow:0 2px 8px rgba(0,0,0,.2);transition:opacity .18s ease,transform .22s cubic-bezier(.34,1.56,.64,1);}',
    '#sw-fab-t:not(.sw-open):hover::before{opacity:1;transform:translateY(-50%) translateX(0);}'
  ].join('');
  document.head.appendChild(style);

  var fab = document.createElement('div');
  fab.id = 'sw-fab';
  fab.setAttribute('role', 'region');
  fab.setAttribute('aria-label', 'Share this page');

  var trig = document.createElement('button');
  trig.id = 'sw-fab-t';
  trig.setAttribute('type', 'button');
  trig.setAttribute('aria-label', 'Share this page');
  trig.setAttribute('aria-expanded', 'false');
  trig.innerHTML =
    '<svg class="sw-si" xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><line x1="8.59" y1="13.51" x2="15.42" y2="17.49"/><line x1="15.41" y1="6.51" x2="8.59" y2="10.49"/></svg>' +
    '<svg class="sw-xi" xmlns="http://www.w3.org/2000/svg" width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>';
  fab.appendChild(trig);

  var btns = {};
  PLATFORMS.forEach(function (p) {
    var el = document.createElement(p.url ? 'a' : 'button');
    el.className = 'sw-pb';
    el.setAttribute('aria-label', p.label);
    el.setAttribute('title', p.label);
    el.style.background = p.bg;
    el.style.boxShadow = p.id === 'x'
      ? '0 0 0 1px rgba(255,255,255,.12),0 2px 8px rgba(0,0,0,.25)'
      : '0 2px 8px rgba(0,0,0,.25)';
    if (p.url) {
      el.setAttribute('target', '_blank');
      el.setAttribute('rel', 'noopener noreferrer');
    } else {
      el.setAttribute('type', 'button');
    }
    if (p.id === 'copy') {
      el.innerHTML =
        '<svg class="sw-cpi" xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M16 1H4c-1.1 0-2 .9-2 2v14h2V3h12V1zm3 4H8c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h11c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm0 16H8V7h11v14z"/></svg>' +
        '<svg class="sw-ck" xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><polyline points="20 6 9 17 4 12"/></svg>';
    } else {
      el.innerHTML = '<svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">' + p.svg + '</svg>';
    }
    fab.appendChild(el);
    btns[p.id] = el;
  });

  document.body.appendChild(fab);
  updateURLs();

  var isOpen = false;

  function updateURLs() {
    var d = getShareData();
    PLATFORMS.forEach(function (p) {
      if (p.url) btns[p.id].href = p.url(d);
    });
  }

  function openFab() {
    updateURLs();
    isOpen = true;
    trig.classList.add('sw-open');
    trig.setAttribute('aria-expanded', 'true');
    var reversed = PLATFORMS.slice().reverse();
    reversed.forEach(function (p, i) {
      var y = -((i + 1) * STEP);
      setTimeout(function () {
        var el = btns[p.id];
        el.style.transform = 'translateY(' + y + 'px) scale(1)';
        el.style.opacity = '1';
        el.style.pointerEvents = 'auto';
      }, i * 40);
    });
  }

  function closeFab() {
    isOpen = false;
    trig.classList.remove('sw-open');
    trig.setAttribute('aria-expanded', 'false');
    PLATFORMS.forEach(function (p) {
      var el = btns[p.id];
      el.style.transform = '';
      el.style.opacity = '';
      el.style.pointerEvents = '';
    });
  }

  trig.addEventListener('click', function (e) {
    e.stopPropagation();
    if (!isOpen && navigator.share && window.innerWidth <= 768) {
      var d = getShareData();
      navigator.share({ title: d.title, text: d.text, url: d.url }).catch(function () {});
      return;
    }
    isOpen ? closeFab() : openFab();
  });

  btns['copy'].addEventListener('click', function (e) {
    e.stopPropagation();
    var url = getShareData().url;
    var btn = this;
    var cpi = btn.querySelector('.sw-cpi');
    var ck = btn.querySelector('.sw-ck');
    function showCheck() {
      cpi.style.display = 'none';
      ck.style.display = 'block';
      btn.setAttribute('aria-label', 'Copied!');
      setTimeout(function () {
        cpi.style.display = '';
        ck.style.display = '';
        btn.setAttribute('aria-label', 'Copy link');
      }, 2000);
    }
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(url).then(showCheck).catch(function () { fallbackCopy(url); showCheck(); });
    } else {
      fallbackCopy(url);
      showCheck();
    }
  });

  function fallbackCopy(text) {
    var ta = document.createElement('textarea');
    ta.value = text;
    ta.style.cssText = 'position:fixed;top:0;left:0;opacity:0;pointer-events:none;';
    document.body.appendChild(ta);
    ta.focus();
    ta.select();
    try { document.execCommand('copy'); } catch (_) {}
    document.body.removeChild(ta);
  }

  document.addEventListener('click', function (e) {
    if (isOpen && !fab.contains(e.target)) closeFab();
  });

  document.addEventListener('keydown', function (e) {
    if (isOpen && (e.key === 'Escape' || e.keyCode === 27)) {
      closeFab();
      trig.focus();
    }
  });
})();
