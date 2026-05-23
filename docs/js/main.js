/* NepDate Documentation Site - main.js
 * Handles: theme toggle, mobile menu, code highlighting, copy buttons,
 *          sidebar search, sidebar collapse, scroll spy, right-side TOC,
 *          typing animation, FAQ accordion, install tabs, shrinking header.
 */
(function () {
  'use strict';

  /* ---------- Theme (light / dark) ----------
     Persisted as 'light' or 'dark'. If nothing is stored we follow the
     OS preference live (no pinned choice). The inline bootstrap script in
     <head> handles the first paint to avoid FOUC. */
  const THEME_KEY = 'nepdate-theme';
  const VALID_THEMES = ['light', 'dark'];
  const SYSTEM_QUERY = window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null;

  function osTheme() {
    return SYSTEM_QUERY && SYSTEM_QUERY.matches ? 'dark' : 'light';
  }

  function applyTheme(stored) {
    const isPinned = VALID_THEMES.includes(stored);
    const resolved = isPinned ? stored : osTheme();
    document.documentElement.setAttribute('data-theme', resolved);
    document.documentElement.setAttribute('data-theme-pref', isPinned ? stored : 'system');
    try {
      if (isPinned) localStorage.setItem(THEME_KEY, stored);
      else localStorage.removeItem(THEME_KEY);
    } catch (_) {}
    syncSwitch(resolved);
  }

  function syncSwitch(resolved) {
    document.querySelectorAll('.theme-switch').forEach(root => {
      root.setAttribute('data-active', resolved);
      root.querySelectorAll('button[data-theme-value]').forEach(b => {
        b.setAttribute('aria-pressed', String(b.dataset.themeValue === resolved));
      });
    });
  }

  function initTheme() {
    let stored = null;
    try { stored = localStorage.getItem(THEME_KEY); } catch (_) {}
    if (!VALID_THEMES.includes(stored)) stored = null;
    applyTheme(stored);

    // Track OS theme live when the user hasn't pinned a choice.
    if (SYSTEM_QUERY && SYSTEM_QUERY.addEventListener) {
      SYSTEM_QUERY.addEventListener('change', () => {
        const pref = document.documentElement.getAttribute('data-theme-pref') || 'system';
        if (pref === 'system') applyTheme(null);
      });
    }

    document.querySelectorAll('.theme-switch button[data-theme-value]').forEach(btn => {
      const v = btn.dataset.themeValue;
      if (!VALID_THEMES.includes(v)) return; // skip stale 'system' buttons
      btn.addEventListener('click', () => applyTheme(v));
    });

    // Backward compat: legacy single-button toggle still cycles light <-> dark.
    const legacy = document.querySelector('.theme-toggle');
    if (legacy) {
      legacy.addEventListener('click', () => {
        const cur = document.documentElement.getAttribute('data-theme');
        applyTheme(cur === 'dark' ? 'light' : 'dark');
      });
    }
  }

  /* ---------- Mobile menu ---------- */
  function initMobileMenu() {
    const btn = document.querySelector('.hamburger');
    const nav = document.querySelector('.mobile-nav');
    const overlay = document.querySelector('.overlay');
    if (!btn || !nav) return;
    const close = () => {
      nav.classList.remove('show');
      overlay && overlay.classList.remove('show');
      document.body.style.overflow = '';
    };
    btn.addEventListener('click', () => {
      const isOpen = nav.classList.toggle('show');
      overlay && overlay.classList.toggle('show', isOpen);
      document.body.style.overflow = isOpen ? 'hidden' : '';
    });
    overlay && overlay.addEventListener('click', close);
    document.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); });
    nav.querySelectorAll('a').forEach(a => a.addEventListener('click', close));
  }

  /* ---------- Docs sidebar toggle (mobile) ---------- */
  function initDocsSidebar() {
    const btn = document.querySelector('.docs-menu-btn');
    const sidebar = document.querySelector('.docs-sidebar');
    const overlay = document.querySelector('.overlay');
    if (!btn || !sidebar) return;
    const close = () => {
      sidebar.classList.remove('show');
      overlay && overlay.classList.remove('show');
      document.body.style.overflow = '';
    };
    btn.addEventListener('click', () => {
      const open = sidebar.classList.toggle('show');
      overlay && overlay.classList.toggle('show', open);
      document.body.style.overflow = open ? 'hidden' : '';
    });
    sidebar.querySelectorAll('a').forEach(a => a.addEventListener('click', () => {
      if (window.innerWidth < 769) close();
    }));
  }

  /* ---------- Syntax highlight (C# and bash) ---------- */
  const CS_KEYWORDS = new Set([
    'var','int','long','short','byte','bool','string','char','double','float','decimal','object','void',
    'new','using','public','private','protected','internal','static','readonly','class','struct','record',
    'interface','enum','namespace','return','if','else','true','false','null','out','ref','in','this',
    'foreach','for','while','do','switch','case','break','continue','default','try','catch','finally',
    'throw','async','await','partial','sealed','virtual','override','abstract','const','get','set',
    'is','as','typeof','nameof','params','yield','where'
  ]);
  const CS_TYPES = new Set([
    'NepaliDate','NepaliDateRange','DateTime','TimeSpan','DayOfWeek','CalendarInfo','SmartDateParser',
    'BulkConvert','FiscalYear','NepaliMonths','DateFormats','Separators','FiscalYearQuarters',
    'IEnumerable','IFormattable','IComparable','IEquatable','IParsable','ISpanFormattable',
    'ISpanParsable','TypeConverter','JsonConverter','JsonSerializer','JsonSerializerOptions',
    'JsonSerializerSettings','JsonConvert','XmlSerializer','NepaliDateXmlSerializer','Span',
    'List','Console','Enumerable','PersonRecord','Task','String','Int32','Boolean'
  ]);

  function escapeHtml(s) {
    return s.replace(/&/g, '-').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  }
  function highlightCSharp(src) {
    // tokenize sequentially to avoid nested replacements
    const tokens = [];
    let i = 0;
    const n = src.length;
    while (i < n) {
      const c = src[i];
      // line comment
      if (c === '/' && src[i + 1] === '/') {
        const end = src.indexOf('\n', i); const e = end === -1 ? n : end;
        tokens.push({ t: 'cmt', v: src.slice(i, e) }); i = e; continue;
      }
      // block comment
      if (c === '/' && src[i + 1] === '*') {
        const end = src.indexOf('*/', i + 2);
        const e = end === -1 ? n : end + 2;
        tokens.push({ t: 'cmt', v: src.slice(i, e) }); i = e; continue;
      }
      // string
      if (c === '"') {
        let j = i + 1;
        while (j < n && src[j] !== '"') {
          if (src[j] === '\\') j += 2; else j++;
        }
        j = Math.min(j + 1, n);
        tokens.push({ t: 'str', v: src.slice(i, j) }); i = j; continue;
      }
      if (c === '$' && src[i + 1] === '"') {
        let j = i + 2;
        while (j < n && src[j] !== '"') {
          if (src[j] === '\\') j += 2; else j++;
        }
        j = Math.min(j + 1, n);
        tokens.push({ t: 'str', v: src.slice(i, j) }); i = j; continue;
      }
      if (c === '@' && src[i + 1] === '"') {
        let j = i + 2;
        while (j < n) { if (src[j] === '"' && src[j + 1] !== '"') break; j++; }
        j = Math.min(j + 1, n);
        tokens.push({ t: 'str', v: src.slice(i, j) }); i = j; continue;
      }
      if (c === "'") {
        let j = i + 1;
        while (j < n && src[j] !== "'") { if (src[j] === '\\') j += 2; else j++; }
        j = Math.min(j + 1, n);
        tokens.push({ t: 'str', v: src.slice(i, j) }); i = j; continue;
      }
      // number
      if (/[0-9]/.test(c)) {
        let j = i;
        while (j < n && /[0-9_.xXa-fA-F]/.test(src[j])) j++;
        tokens.push({ t: 'num', v: src.slice(i, j) }); i = j; continue;
      }
      // identifier
      if (/[A-Za-z_]/.test(c)) {
        let j = i;
        while (j < n && /[A-Za-z0-9_]/.test(src[j])) j++;
        const word = src.slice(i, j);
        let type = null;
        if (CS_KEYWORDS.has(word)) type = 'kw';
        else if (CS_TYPES.has(word)) type = 'type';
        else if (src[j] === '(') type = 'fn';
        tokens.push({ t: type, v: word });
        i = j; continue;
      }
      tokens.push({ t: null, v: c }); i++;
    }
    return tokens.map(tk => tk.t
      ? '<span class="tok-' + tk.t + '">' + escapeHtml(tk.v) + '</span>'
      : escapeHtml(tk.v)).join('');
  }
  function highlightBash(src) {
    // highlight comments and common commands
    return escapeHtml(src)
      .replace(/(^|\n)(#.*)/g, (_, a, b) => a + '<span class="tok-cmt">' + b + '</span>')
      .replace(/\b(dotnet|npm|yarn|git|cd|ls|mkdir|Install-Package)\b/g, '<span class="tok-cmd">$1</span>')
      .replace(/(^|\s)(--?[a-zA-Z][\w-]*)/g, '$1<span class="tok-num">$2</span>');
  }
  function highlightAll() {
    document.querySelectorAll('pre > code').forEach(code => {
      if (code.dataset.highlighted) return;
      const cls = code.className || '';
      const raw = code.textContent;
      if (/language-csharp|language-cs/.test(cls)) {
        code.innerHTML = highlightCSharp(raw);
      } else if (/language-bash|language-shell|language-sh/.test(cls)) {
        code.innerHTML = highlightBash(raw);
      } else {
        code.innerHTML = escapeHtml(raw);
      }
      code.dataset.highlighted = '1';
    });
  }

  /* ---------- Copy buttons ---------- */
  function initCopyButtons() {
    document.querySelectorAll('pre').forEach(pre => {
      if (pre.querySelector('.copy-btn')) return;
      const btn = document.createElement('button');
      btn.className = 'copy-btn';
      btn.type = 'button';
      btn.textContent = 'Copy';
      btn.setAttribute('aria-label', 'Copy code');
      btn.addEventListener('click', () => {
        const code = pre.querySelector('code');
        const text = code ? code.textContent : pre.textContent;
        const done = () => {
          btn.textContent = 'Copied!';
          btn.classList.add('copied');
          setTimeout(() => { btn.textContent = 'Copy'; btn.classList.remove('copied'); }, 1800);
        };
        if (navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(text).then(done).catch(() => fallback(text, done));
        } else {
          fallback(text, done);
        }
      });
      pre.appendChild(btn);
    });
  }
  function fallback(text, done) {
    const ta = document.createElement('textarea');
    ta.value = text; ta.style.position = 'fixed'; ta.style.opacity = '0';
    document.body.appendChild(ta); ta.select();
    try { document.execCommand('copy'); done(); } catch (_) {}
    document.body.removeChild(ta);
  }

  /* ---------- Install tabs ---------- */
  function initTabs() {
    document.querySelectorAll('[data-tabs]').forEach(root => {
      const tabs = root.querySelectorAll('[data-tab]');
      const panels = root.querySelectorAll('[data-panel]');
      tabs.forEach(t => t.addEventListener('click', () => {
        const name = t.dataset.tab;
        tabs.forEach(x => x.classList.toggle('active', x === t));
        panels.forEach(p => p.classList.toggle('active', p.dataset.panel === name));
      }));
    });
  }

  /* ---------- FAQ accordion ---------- */
  function initFaq() {
    document.querySelectorAll('.faq-item').forEach(item => {
      const q = item.querySelector('.faq-q');
      if (!q) return;
      q.addEventListener('click', () => {
        const wasOpen = item.classList.contains('open');
        // single-open behaviour
        item.parentElement.querySelectorAll('.faq-item.open').forEach(o => o.classList.remove('open'));
        if (!wasOpen) item.classList.add('open');
      });
    });
  }

  /* ---------- Typing animation (hero) ---------- */
  function initTyping() {
    const el = document.querySelector('[data-typer]');
    if (!el) return;
    const lines = [
      'using NepDate;',
      '',
      'var today    = NepaliDate.Today;                  // 2083/01/08',
      'var ad       = today.EnglishDate;                 // 2026/04/21',
      'var parsed   = NepaliDate.Parse("Baisakh 15, 2080");',
      'var nepali   = today.ToUnicodeString();           // २०८३/०१/०८',
      'var info     = today.GetCalendarInfo();           // tithi + events',
      'var holiday  = today.IsPublicHoliday;             // false',
      'var monthEnd = today.MonthEndDate();              // 2083/01/31',
      'var month    = NepaliDateRange.ForMonth(2083, 1); // 31 days',
      'var fyStart  = today.FiscalYearStartDate();       // 2082/04/01'
    ];
    el.innerHTML = '';
    let li = 0, ci = 0;
    const cursor = document.createElement('span');
    cursor.className = 'cursor';
    let current = document.createElement('div');
    current.className = 'line';
    el.appendChild(current);
    el.appendChild(cursor);

    function tick() {
      if (li >= lines.length) return;
      const line = lines[li];
      if (ci < line.length) {
        current.textContent = (current.textContent || '') + line[ci];
        ci++;
        setTimeout(tick, 18 + Math.random() * 40);
      } else {
        // highlight the finished line
        const raw = current.textContent;
        current.innerHTML = highlightCSharp(raw);
        li++; ci = 0;
        if (li < lines.length) {
          current = document.createElement('div');
          current.className = 'line';
          el.insertBefore(current, cursor);
          setTimeout(tick, 180);
        }
      }
    }
    // start when visible
    const io = new IntersectionObserver((entries) => {
      entries.forEach(en => {
        if (en.isIntersecting) { io.disconnect(); setTimeout(tick, 300); }
      });
    });
    io.observe(el);
  }

  /* ---------- Docs sidebar: collapse + search + scroll spy ---------- */
  const COLLAPSE_KEY = 'nepdate-toc-collapsed';
  function initTocCollapse() {
    const saved = new Set();
    try { (sessionStorage.getItem(COLLAPSE_KEY) || '').split('|').filter(Boolean).forEach(x => saved.add(x)); } catch (_) {}
    document.querySelectorAll('.toc-group').forEach(group => {
      const head = group.querySelector('.toc-group-head');
      const id = group.dataset.group;
      if (id && saved.has(id)) group.classList.add('collapsed');
      head && head.addEventListener('click', () => {
        const collapsed = group.classList.toggle('collapsed');
        if (id) { collapsed ? saved.add(id) : saved.delete(id); }
        try { sessionStorage.setItem(COLLAPSE_KEY, Array.from(saved).join('|')); } catch (_) {}
      });
    });
  }

  function initSearch() {
    const input = document.querySelector('[data-search]');
    if (!input) return;
    const empty = document.querySelector('.search-empty');
    const items = Array.from(document.querySelectorAll('.toc-group li'));
    const groups = Array.from(document.querySelectorAll('.toc-group'));
    // Build index: link text + target heading text + next sibling paragraph
    const index = items.map(li => {
      const a = li.querySelector('a');
      const href = a ? a.getAttribute('href') : '';
      let extra = '';
      if (href && href.startsWith('#')) {
        const target = document.getElementById(href.slice(1));
        if (target) {
          extra = target.textContent + ' ';
          let sib = target.nextElementSibling;
          let count = 0;
          while (sib && count < 3) {
            extra += sib.textContent + ' ';
            sib = sib.nextElementSibling; count++;
          }
        }
      }
      return { li, text: ((a ? a.textContent : '') + ' ' + extra).toLowerCase() };
    });

    let t;
    input.addEventListener('input', () => {
      clearTimeout(t);
      t = setTimeout(() => {
        const q = input.value.trim().toLowerCase();
        let matches = 0;
        index.forEach(({ li, text }) => {
          const ok = !q || text.includes(q);
          li.classList.toggle('hidden', !ok);
          if (ok) matches++;
        });
        // hide groups with zero visible children
        groups.forEach(g => {
          const anyVisible = g.querySelectorAll('li:not(.hidden)').length > 0;
          g.style.display = anyVisible ? '' : 'none';
          if (q && anyVisible) g.classList.remove('collapsed');
        });
        if (empty) empty.classList.toggle('show', matches === 0);
      }, 120);
    });

    // Keyboard: Ctrl+K or / to focus
    document.addEventListener('keydown', (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault(); input.focus(); input.select();
      } else if (e.key === '/' && document.activeElement !== input &&
                 !['INPUT', 'TEXTAREA'].includes(document.activeElement.tagName)) {
        e.preventDefault(); input.focus();
      } else if (e.key === 'Escape' && document.activeElement === input) {
        input.value = ''; input.dispatchEvent(new Event('input')); input.blur();
      }
    });
  }

  function initScrollSpy() {
    const links = Array.from(document.querySelectorAll('.docs-sidebar a[href^="#"], .docs-toc a[href^="#"]'));
    if (!links.length) return;
    const map = new Map();
    links.forEach(a => {
      const id = a.getAttribute('href').slice(1);
      const t = document.getElementById(id);
      if (t) {
        if (!map.has(t)) map.set(t, []);
        map.get(t).push(a);
      }
    });
    const setActive = (target) => {
      links.forEach(a => a.classList.remove('active'));
      if (target && map.has(target)) map.get(target).forEach(a => a.classList.add('active'));
    };
    const observer = new IntersectionObserver((entries) => {
      // Pick the entry closest to top that's intersecting
      const visible = entries.filter(e => e.isIntersecting)
        .sort((a, b) => a.target.getBoundingClientRect().top - b.target.getBoundingClientRect().top);
      if (visible[0]) setActive(visible[0].target);
    }, { rootMargin: '-80px 0px -70% 0px', threshold: 0 });
    map.forEach((_, target) => observer.observe(target));
  }

  /* ---------- Right-side TOC (current page headings) ---------- */
  function initRightToc() {
    const toc = document.querySelector('.docs-toc ul');
    if (!toc) return;
    const heads = document.querySelectorAll('.docs-content h2, .docs-content h3');
    if (!heads.length) { document.querySelector('.docs-toc').style.display = 'none'; return; }
    heads.forEach(h => {
      if (!h.id) return;
      const li = document.createElement('li');
      if (h.tagName === 'H3') li.className = 'l3';
      const a = document.createElement('a');
      a.href = '#' + h.id;
      a.textContent = h.textContent;
      li.appendChild(a);
      toc.appendChild(li);
    });
  }

  /* ---------- Smooth scroll offset for sticky header ---------- */
  function initAnchorClicks() {
    document.addEventListener('click', (e) => {
      const a = e.target.closest('a[href^="#"]');
      if (!a) return;
      const id = a.getAttribute('href').slice(1);
      if (!id) return;
      const t = document.getElementById(id);
      if (!t) return;
      e.preventDefault();
      const y = t.getBoundingClientRect().top + window.pageYOffset - 72;
      window.scrollTo({ top: y, behavior: 'smooth' });
      try { history.replaceState(null, '', '#' + id); } catch (_) {}
    });
  }

  /* ---------- Sticky header scroll polish ---------- */
  function initStickyHeader() {
    var hdr = document.querySelector('.site-header');
    if (!hdr) return;
    var update = function () {
      var scrolled = window.scrollY > 10;
      hdr.classList.toggle('is-scrolled', scrolled);
      hdr.classList.toggle('scrolled', scrolled);
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
  }

  /* ---------- Hero taskbar dock ---------- */
  function initHeroTaskbarDock() {
    var taskbar = document.querySelector('.win-taskbar');
    var section = document.querySelector('#minibar');
    if (!taskbar || !section) return;
    var root = document.documentElement;
    var update = function () {
      var sectionRect = section.getBoundingClientRect();
      var viewportHeight = window.innerHeight;
      var dockThreshold = Math.max(viewportHeight - taskbar.offsetHeight - 24, 0);
      var atTop = window.scrollY <= 16;
      var sectionVisible = sectionRect.top < viewportHeight && sectionRect.bottom > 0;
      var shouldDock = sectionVisible && sectionRect.top <= dockThreshold;

      root.classList.toggle('taskbar-hidden', !atTop && !shouldDock);
      root.classList.toggle('taskbar-docked', shouldDock);
      taskbar.classList.toggle('is-fixed', atTop);
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
  }

  /* ---------- Ambient glow canvas ----------
     Injects 10 uniquely shaped, sized, positioned, coloured, and blurred
     blobs as a fixed overlay on every page. Each blob is fully independent.
     mix-blend-mode: soft-light means it blends through all section
     backgrounds without obscuring text. pointer-events: none = no
     interaction interference. z-index sits below the sticky header (100). */
  function initAmbientGlow() {
    var canvas = document.createElement('div');
    canvas.className = 'glow-canvas';
    canvas.setAttribute('aria-hidden', 'true');

    /* Each entry: [width, height, top%, left%, color, blur, opacity] */
    var blobs = [
      /* 1  extreme wide flat ·  top-left   · warm accent */
      ['1300px', '260px',  '2%',  '-5%', 'rgba(220,63,16,0.30)',  '60px', '0.55'],
      /* 2  near-circle      ·  right 18%  · deep red */
      ['480px',  '460px', '13%',  '72%', 'rgba(220,38,38,0.28)',  '40px', '0.50'],
      /* 3  tall narrow      ·  left 37%   · cool slate */
      ['300px',  '700px', '30%',   '5%', 'rgba(100,116,139,0.22)','50px', '0.45'],
      /* 4  huge shallow     ·  right 52%  · vivid accent */
      ['1100px', '380px', '48%',  '35%', 'rgba(220,63,16,0.25)',  '70px', '0.50'],
      /* 5  small tight      ·  center 43% · punchy red */
      ['260px',  '280px', '39%',  '43%', 'rgba(220,38,38,0.35)',  '30px', '0.55'],
      /* 6  wide moderate    ·  far left 68% · slate */
      ['780px',  '480px', '62%',  '-8%', 'rgba(71,85,105,0.20)',  '55px', '0.45'],
      /* 7  extreme flat     ·  bottom     · accent breath */
      ['1200px', '220px', '91%',  '10%', 'rgba(220,63,16,0.22)',  '65px', '0.50'],
      /* 8  tall ellipse     ·  upper-right · red */
      ['620px',  '820px', '22%',  '68%', 'rgba(220,38,38,0.20)',  '45px', '0.45'],
      /* 9  medium circle    ·  lower-center · neutral */
      ['540px',  '520px', '74%',  '52%', 'rgba(100,116,139,0.18)','50px', '0.42'],
      /* 10 large soft haze  ·  mid-center  · faint accent */
      ['900px',  '660px', '57%',  '20%', 'rgba(220,63,16,0.15)',  '80px', '0.40']
    ];

    blobs.forEach(function (b) {
      var el = document.createElement('div');
      el.style.cssText = [
        'position:absolute',
        'width:'  + b[0],
        'height:' + b[1],
        'top:'    + b[2],
        'left:'   + b[3],
        'background:radial-gradient(ellipse,' + b[4] + ' 0%,transparent 70%)',
        'filter:blur(' + b[5] + ')',
        'opacity:' + b[6],
        'pointer-events:none',
        'border-radius:50%'
      ].join(';');
      canvas.appendChild(el);
    });

    document.body.insertBefore(canvas, document.body.firstChild);
  }


  function initFeatNav() {
    const nav = document.querySelector('.feat-nav');
    if (!nav) return;
    const links = Array.from(nav.querySelectorAll('a[href^="#"]'));
    if (!links.length) return;
    const sections = links.map(a => document.getElementById(a.getAttribute('href').slice(1))).filter(Boolean);
    const setActive = (id) => {
      links.forEach(a => {
        a.classList.toggle('active', a.getAttribute('href') === '#' + id);
      });
    };
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(e => {
        if (e.isIntersecting) setActive(e.target.id);
      });
    }, { rootMargin: '-90px 0px -50% 0px', threshold: 0 });
    sections.forEach(s => observer.observe(s));
  }

  /* ---------- Nav aria-current ---------- */
  function initNavCurrent() {
    var page = location.pathname.split('/').pop() || 'index.html';
    document.querySelectorAll('.nav-main a, .mobile-nav a').forEach(function (a) {
      var href = a.getAttribute('href');
      if (href === page || (page === 'index.html' && href === 'index.html')) {
        a.setAttribute('aria-current', 'page');
      }
    });
  }

  /* ---------- Init ---------- */
  function init() {
    initNavCurrent();
    initAmbientGlow();
    initTheme();
    initStickyHeader();
    initHeroTaskbarDock();
    initMobileMenu();
    initDocsSidebar();
    highlightAll();
    initCopyButtons();
    initTabs();
    initFaq();
    initTyping();
    initRightToc();
    initTocCollapse();
    initSearch();
    initScrollSpy();
    initFeatNav();
    initAnchorClicks();
  }
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();

/* ==========================================================================
   NepDate Widget extensions: lightbox + live hero clock
   ========================================================================== */
(function () {
  'use strict';

  /* ---------- Lightbox ---------- */
  function initLightbox() {
    const triggers = Array.from(document.querySelectorAll('[data-lightbox]'));
    if (!triggers.length) return;

    const items = triggers.map(t => ({
      src: t.dataset.lightbox || (t.querySelector('img') && t.querySelector('img').src),
      cap: t.dataset.caption || (t.querySelector('img') && t.querySelector('img').alt) || ''
    }));

    const box = document.createElement('div');
    box.className = 'lightbox';
    box.setAttribute('role', 'dialog');
    box.setAttribute('aria-modal', 'true');
    box.setAttribute('aria-label', 'Image viewer');
    box.innerHTML = ''
      + '<button class="lb-close" type="button" aria-label="Close">'
      + '  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M18 6L6 18M6 6l12 12"/></svg>'
      + '</button>'
      + '<button class="lb-nav lb-prev" type="button" aria-label="Previous">'
      + '  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M15 18l-6-6 6-6"/></svg>'
      + '</button>'
      + '<button class="lb-nav lb-next" type="button" aria-label="Next">'
      + '  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M9 6l6 6-6 6"/></svg>'
      + '</button>'
      + '<img alt="" />'
      + '<div class="lb-cap"></div>';
    document.body.appendChild(box);

    const img = box.querySelector('img');
    const cap = box.querySelector('.lb-cap');
    const closeBtn = box.querySelector('.lb-close');
    const prevBtn = box.querySelector('.lb-prev');
    const nextBtn = box.querySelector('.lb-next');

    let idx = 0;
    let returnFocus = null;
    function show(i) {
      idx = (i + items.length) % items.length;
      img.src = items[idx].src;
      img.alt = items[idx].cap;
      cap.textContent = items[idx].cap;
    }
    function open(i) {
      returnFocus = document.activeElement;
      show(i);
      box.classList.add('show');
      document.body.style.overflow = 'hidden';
      closeBtn.focus();
    }
    function close() {
      box.classList.remove('show');
      document.body.style.overflow = '';
      if (returnFocus) returnFocus.focus();
    }

    triggers.forEach((t, i) => {
      if (!(t instanceof HTMLButtonElement) && !(t instanceof HTMLAnchorElement)) {
        if (!t.hasAttribute('tabindex')) t.tabIndex = 0;
        if (!t.hasAttribute('role')) t.setAttribute('role', 'button');
      }
      const openTrigger = (e) => {
        if (e.target.closest('a')) return;
        e.preventDefault();
        open(i);
      };
      t.addEventListener('click', openTrigger);
      t.addEventListener('keydown', (e) => {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        openTrigger(e);
      });
    });
    closeBtn.addEventListener('click', close);
    prevBtn.addEventListener('click', () => show(idx - 1));
    nextBtn.addEventListener('click', () => show(idx + 1));
    box.addEventListener('click', (e) => { if (e.target === box) close(); });
    document.addEventListener('keydown', (e) => {
      if (!box.classList.contains('show')) return;
      if (e.key === 'Escape') close();
      else if (e.key === 'ArrowLeft') show(idx - 1);
      else if (e.key === 'ArrowRight') show(idx + 1);
      else if (e.key === 'Tab') {
        var focusable = [closeBtn, prevBtn, nextBtn];
        var first = focusable[0], last = focusable[focusable.length - 1];
        if (e.shiftKey) { if (document.activeElement === first) { e.preventDefault(); last.focus(); } }
        else { if (document.activeElement === last) { e.preventDefault(); first.focus(); } }
      }
    });
  }

  /* ---------- Hero live clock (in mini-bar mock) ---------- */
  function initHeroClock() {
    const clock = document.querySelector('[data-hero-clock]');
    if (!clock) return;

    // Simple Bikram Sambat conversion table for current and next year only.
    // The site is static so we ship one year of month start dates and interpolate.
    // Range tied to today's BS year (2082-2083). Extending later only requires adding rows.
    const BS_MONTH_STARTS = [
      // [adYear, adMonth, adDay, bsYear, bsMonth, bsMonthName, daysInBsMonth]
      [2025,  4, 14, 2082,  1, 'Baishakh',   31],
      [2025,  5, 15, 2082,  2, 'Jestha',     31],
      [2025,  6, 15, 2082,  3, 'Ashadh',     32],
      [2025,  7, 17, 2082,  4, 'Shrawan',    31],
      [2025,  8, 17, 2082,  5, 'Bhadra',     31],
      [2025,  9, 17, 2082,  6, 'Ashwin',     31],
      [2025, 10, 18, 2082,  7, 'Kartik',     30],
      [2025, 11, 17, 2082,  8, 'Mangsir',    29],
      [2025, 12, 16, 2082,  9, 'Poush',      30],
      [2026,  1, 15, 2082, 10, 'Magh',       29],
      [2026,  2, 13, 2082, 11, 'Falgun',     30],
      [2026,  3, 15, 2082, 12, 'Chaitra',    30],
      [2026,  4, 14, 2083,  1, 'Baishakh',   31],
      [2026,  5, 15, 2083,  2, 'Jestha',     31],
      [2026,  6, 15, 2083,  3, 'Ashadh',     32],
      [2026,  7, 17, 2083,  4, 'Shrawan',    32],
      [2026,  8, 18, 2083,  5, 'Bhadra',     31],
      [2026,  9, 18, 2083,  6, 'Ashwin',     30],
      [2026, 10, 18, 2083,  7, 'Kartik',     30],
      [2026, 11, 17, 2083,  8, 'Mangsir',    29],
      [2026, 12, 16, 2083,  9, 'Poush',      30],
      [2027,  1, 15, 2083, 10, 'Magh',       29],
      [2027,  2, 13, 2083, 11, 'Falgun',     30],
      [2027,  3, 15, 2083, 12, 'Chaitra',    31]
    ];
    const MONTHS_AD = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    const DOW = ['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];

    function toBs(d) {
      const t = d.getTime();
      for (let i = BS_MONTH_STARTS.length - 1; i >= 0; i--) {
        const r = BS_MONTH_STARTS[i];
        const startMs = new Date(r[0], r[1] - 1, r[2]).getTime();
        if (t >= startMs) {
          const dayOffset = Math.floor((t - startMs) / 86400000);
          const day = dayOffset + 1;
          if (day <= r[6]) {
            return { y: r[3], m: r[4], name: r[5], d: day };
          }
        }
      }
      return null;
    }

    function pad(n) { return n < 10 ? '0' + n : '' + n; }

    function tick() {
      const now = new Date();
      let h = now.getHours();
      const ampm = h >= 12 ? 'PM' : 'AM';
      h = h % 12; if (h === 0) h = 12;
      const time = h + ':' + pad(now.getMinutes()) + ' ' + ampm;
      const bs = toBs(now);
      const bsStr = bs ? (bs.name + ' ' + pad(bs.d) + ', ' + bs.y) : '';
      const adStr = MONTHS_AD[now.getMonth()] + ' ' + now.getDate() + ', ' + now.getFullYear();
      const dow = DOW[now.getDay()];

      const r1 = clock.querySelector('.row1');
      const r2 = clock.querySelector('.row2');
      if (r1) r1.textContent = dow + ' | Kathmandu ' + time;
      if (r2) r2.textContent = bsStr + '  |  ' + adStr;
    }
    tick();
    setInterval(tick, 1000 * 30);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      initLightbox();
      initHeroClock();
    });
  } else {
    initLightbox();
    initHeroClock();
  }
})();
