/**
 * Deltempo Cybernetic Motion Engine v3.0.0
 * High-performance interactive visual systems:
 * - Lenis 1.3 inertial scrolling with a scroll progress rail, glass-nav state and
 *   scroll-driven background parallax (native scroll when reduced motion is on)
 * - Deep-space starfield: depth-layered stars that streak into shooting stars as
 *   the page scrolls, part and brighten around the cursor, flare on click, and
 *   idle meteors crossing the sky
 * - Native OS cursor everywhere: no drawn pointer, no magnetic offset
 * - Card surface reactive radial lighting & micro-tilt
 * - IntersectionObserver scroll reveals with directional variants + grid stagger
 * - Full prefers-reduced-motion (static starfield, native scroll) & touch-device compliance
 */

(function () {
  'use strict';

  // 1. Feature & Preference Checks
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const isFinePointer = window.matchMedia('(hover: hover) and (pointer: fine)');
  const isReducedMotion = prefersReducedMotion.matches;

  /* ==========================================================================
     1. Inertial Smooth Scrolling (Lenis 1.3) + scroll-driven chrome
     ========================================================================== */
  // Shared, read-only snapshot of the scroll position for the background field.
  const scrollState = {
    y: 0,
    velocity: 0,
    progress: 0,
    lenis: null,
  };

  // Scroll-linked features subscribe here instead of each attaching its own
  // scroll listener. There is exactly one paint per scroll tick (Lenis rAF or
  // the native listener below), so scrub work stays in one read/write pass.
  const scrollHooks = [];

  function onScrollFrame(fn) {
    scrollHooks.push(fn);
  }

  function initSmoothScroll() {
    const docEl = document.documentElement;

    // Thin progress rail: created once, reused by every page.
    let progressBar = document.getElementById('scrollProgressBar');
    if (!progressBar) {
      progressBar = document.createElement('div');
      progressBar.id = 'scrollProgressBar';
      progressBar.className = 'scroll-progress-bar';
      progressBar.setAttribute('aria-hidden', 'true');
      document.body.appendChild(progressBar);
    }

    // Both scroll readouts are consumed by exactly one element each. A custom
    // property is not free: writing one forces a style recalculation whether
    // it lands on :root or on the consumer. The parallax therefore drives
    // `transform` directly (compositor-only, no recalc), and the ring is
    // quantised to a step that is sub-pixel on a 46px button.
    const auroraEl = document.querySelector('.ambient-aurora');
    const topBtn = document.querySelector('.back-to-top');
    let lastScrollY = -1;
    let lastProgress = -1;

    function paint() {
      const y = window.scrollY || window.pageYOffset || 0;
      const max = Math.max(1, docEl.scrollHeight - window.innerHeight);
      const p = Math.min(1, Math.max(0, y / max));

      scrollState.y = y;
      scrollState.progress = p;

      for (let h = 0; h < scrollHooks.length; h++) {
        scrollHooks[h](y, p);
      }

      progressBar.style.transform = 'scaleX(' + p.toFixed(4) + ')';

      if (auroraEl && !isReducedMotion) {
        const sy = Math.min(y, 1600);
        if (sy !== lastScrollY) {
          lastScrollY = sy;
          auroraEl.style.transform =
            'translate3d(0,' + (sy * -0.02).toFixed(2) + 'px,0) scale(1.12)';
        }
      }
      // Read by the back-to-top button's conic progress ring. 0.005 turn is
      // under a pixel of arc at 46px, and the ring only matters once visible.
      if (topBtn && topBtn.classList.contains('visible')) {
        const pr = Math.round(p * 200) / 200;
        if (pr !== lastProgress) {
          lastProgress = pr;
          topBtn.style.setProperty('--scroll-progress', String(pr));
        }
      }
    }

    // The bar's Download pill is held back while the hero owns the screen, then
    // drops into the top-right corner once the hero has scrolled past. Only
    // pages with a hero opt in, so sub-pages keep the CTA from the first frame.
    // rootMargin pulls the observation line down to where the old bar's bottom
    // edge used to be, so the swap happens the moment the hero clears the top of
    // the page instead of when it leaves the viewport entirely.
    const heroEl = document.querySelector('.hero');
    if (heroEl && 'IntersectionObserver' in window && !isReducedMotion) {
      docEl.classList.add('nav-cta-staged');
      const ctaObserver = new IntersectionObserver(
        function (entries) {
          const entry = entries[entries.length - 1];
          const pastHero = !entry.isIntersecting && entry.boundingClientRect.bottom <= 96;
          docEl.classList.toggle('nav-cta-live', pastHero);
        },
        { rootMargin: '-96px 0px 0px 0px', threshold: 0 }
      );
      ctaObserver.observe(heroEl);
    }

    const LenisCtor = window.Lenis;

    // Reduced motion keeps the native, instant scroll â€” no hijacking, no easing.
    if (typeof LenisCtor === 'function' && !isReducedMotion) {
      const lenis = new LenisCtor({
        // 0.085 is the showcase-weight glide: weighty without feeling laggy.
        lerp: 0.085,
        wheelMultiplier: 0.95,
        touchMultiplier: 1.5,
        smoothWheel: true,
        syncTouch: false,
        autoRaf: false,
        // scroll-padding-top (96px) is read by Lenis itself, so an extra
        // offset here would double the gap above the target section.
        anchors: { offset: 0, duration: 1.05 },
        stopInertiaOnNavigate: true,
        respectReducedMotion: true,
        prevent: function (node) {
          return node instanceof Element && !!node.closest('[data-lenis-prevent]');
        },
      });

      scrollState.lenis = lenis;
      docEl.classList.add('has-lenis');

      lenis.on('scroll', function (e) {
        scrollState.velocity = e && typeof e.velocity === 'number' ? e.velocity : 0;
        paint();
      });

      const raf = function (time) {
        lenis.raf(time);
        requestAnimationFrame(raf);
      };
      requestAnimationFrame(raf);

      // Dynamic height changes (reveals, tab switches) would otherwise desync.
      if ('ResizeObserver' in window) {
        const ro = new ResizeObserver(function () {
          lenis.resize();
        });
        ro.observe(document.body);
      }
      window.addEventListener(
        'resize',
        function () {
          lenis.resize();
          paint();
        },
        { passive: true }
      );

      paint();
      return;
    }

    window.addEventListener('scroll', paint, { passive: true });
    window.addEventListener('resize', paint, { passive: true });
    paint();
  }

  /* ==========================================================================
     2. IntersectionObserver Scroll Reveal System
     ========================================================================== */
  function initScrollReveals() {
    const revealElements = document.querySelectorAll('.reveal-on-scroll, [data-reveal]');
    // Grid containers whose direct children stagger in one after another.
    const cascadeGroups = document.querySelectorAll('[data-cascade]');

    if (isReducedMotion || !('IntersectionObserver' in window)) {
      revealElements.forEach(function (el) {
        el.classList.add('revealed');
      });
      cascadeGroups.forEach(function (el) {
        el.classList.add('is-revealed');
      });
      return;
    }

    if (!revealElements.length && !cascadeGroups.length) return;

    const observer = new IntersectionObserver(
      function (entries, obs) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          const el = entry.target;
          el.classList.add(el.hasAttribute('data-cascade') ? 'is-revealed' : 'revealed');
          obs.unobserve(el);
        });
      },
      {
        root: null,
        // Fire a touch before the block's top edge clears the fold so the
        // motion finishes as it settles into view, not after.
        rootMargin: '0px 0px -12% 0px',
        threshold: 0.08,
      }
    );

    revealElements.forEach(function (el) {
      observer.observe(el);
    });

    cascadeGroups.forEach(function (el) {
      observer.observe(el);
    });
  }

  /* ==========================================================================
     3. Deep-space starfield: scroll streaks, pointer repulsion, idle shooting stars
     ========================================================================== */
  function initInteractiveBackground() {
    let canvas = document.getElementById('ambientCanvas');
    if (!canvas) {
      canvas = document.createElement('canvas');
      canvas.id = 'ambientCanvas';
      canvas.className = 'ambient-canvas';
      canvas.setAttribute('aria-hidden', 'true');
      const aurora = document.querySelector('.ambient-aurora');
      if (aurora && aurora.parentNode) {
        aurora.parentNode.insertBefore(canvas, aurora.nextSibling);
      } else {
        document.body.insertBefore(canvas, document.body.firstChild);
      }
    }

    const ctx = canvas.getContext('2d', { alpha: true });
    if (!ctx) return;

    let width = window.innerWidth || 360;
    let height = window.innerHeight || 640;
    // 2x is the point where a 0.6px star still reads as a point instead of a
    // grey smudge; beyond that we only pay fill rate. Phones sit at 1.5x: the
    // canvas covers the whole viewport, so every extra device pixel is cleared
    // and repainted 60 times a second.
    function targetDpr() {
      const w = window.innerWidth || 360;
      return Math.min(window.devicePixelRatio || 1, w < 768 ? 1.5 : 2);
    }
    let dpr = targetDpr();
    let animationFrameId = null;
    let lastFrameTime = 0;

    // A sky, not a swarm. Desktop gets enough points to read as depth; phones
    // get a lighter field because every star is also a streak candidate.
    const STAR_COUNT_DESKTOP = 220;
    const STAR_COUNT_MOBILE = 110;

    const pointer = {
      x: -9999,
      y: -9999,
      active: false,
      radius: 190,
    };

    // Click pulses: a single expanding ring that lights the stars it passes.
    const pulses = [];

    // Scroll motion: the raw delta is what turns into streak length.
    let lastScrollY = window.scrollY || 0;

    // Idle shooting stars.
    const meteors = [];
    let nextMeteorAt = 240 + Math.random() * 300;
    let frameCount = 0;

    // --- Star sprites -------------------------------------------------------
    // One pre-rendered glow per tint. Drawing a 64px sprite is far cheaper than
    // building a radial gradient per star per frame, and it keeps the core
    // crisp while the halo stays soft.
    const sprites = {};

    function makeSprite(rgb) {
      const size = 64;
      const c = document.createElement('canvas');
      c.width = size;
      c.height = size;
      const g = c.getContext('2d');
      const grad = g.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
      // Tight core, short bloom. A wide falloff is what makes a point of light
      // read as a dust mote instead of a star.
      grad.addColorStop(0, 'rgba(' + rgb + ', 1)');
      grad.addColorStop(0.1, 'rgba(' + rgb + ', 0.92)');
      grad.addColorStop(0.22, 'rgba(' + rgb + ', 0.24)');
      grad.addColorStop(0.44, 'rgba(' + rgb + ', 0.05)');
      grad.addColorStop(1, 'rgba(' + rgb + ', 0)');
      g.fillStyle = grad;
      g.fillRect(0, 0, size, size);
      return c;
    }

    // A tapered meteor tail: bright at the head (bottom), transparent at the
    // tip, soft on both sides. Drawing one scaled sprite is cheaper than
    // stroking a per-star gradient every frame.
    function makeAura(rgb) {
      const size = 128;
      const c = document.createElement('canvas');
      c.width = size;
      c.height = size;
      const g = c.getContext('2d');
      const grad = g.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
      grad.addColorStop(0, 'rgba(' + rgb + ', 0.55)');
      grad.addColorStop(0.16, 'rgba(' + rgb + ', 0.22)');
      grad.addColorStop(0.46, 'rgba(' + rgb + ', 0.055)');
      grad.addColorStop(1, 'rgba(' + rgb + ', 0)');
      g.fillStyle = grad;
      g.fillRect(0, 0, size, size);
      return c;
    }

    function makeStreak(rgb) {
      const w = 32;
      const h = 128;
      const c = document.createElement('canvas');
      c.width = w;
      c.height = h;
      const g = c.getContext('2d');
      const vg = g.createLinearGradient(0, 0, 0, h);
      vg.addColorStop(0, 'rgba(' + rgb + ', 0)');
      vg.addColorStop(0.45, 'rgba(' + rgb + ', 0.16)');
      vg.addColorStop(0.82, 'rgba(' + rgb + ', 0.62)');
      vg.addColorStop(1, 'rgba(' + rgb + ', 1)');
      g.fillStyle = vg;
      g.fillRect(0, 0, w, h);
      // Only the alpha of this pass survives, so the colour is irrelevant.
      g.globalCompositeOperation = 'destination-in';
      const hg = g.createLinearGradient(0, 0, w, 0);
      hg.addColorStop(0, 'rgba(0, 0, 0, 0)');
      hg.addColorStop(0.34, 'rgba(0, 0, 0, 0.75)');
      hg.addColorStop(0.5, 'rgba(0, 0, 0, 1)');
      hg.addColorStop(0.66, 'rgba(0, 0, 0, 0.75)');
      hg.addColorStop(1, 'rgba(0, 0, 0, 0)');
      g.fillStyle = hg;
      g.fillRect(0, 0, w, h);
      return c;
    }

    function buildSprites() {
      sprites.star = makeSprite('236, 248, 255');
      sprites.mint = makeSprite('120, 255, 214');
      sprites.violet = makeSprite('167, 139, 250');
      sprites.streak = makeStreak('226, 240, 255');
      sprites.streakMint = makeStreak('140, 255, 222');
      sprites.streakViolet = makeStreak('186, 166, 255');
      sprites.auraMint = makeAura('40, 255, 208');
      sprites.auraViolet = makeAura('167, 139, 250');
    }

    buildSprites();

    // --- Star model ---------------------------------------------------------
    const stars = [];

    function spawnStar(star) {
      star.z = 0.32 + Math.random() * 1.08;
      star.x = Math.random() * width;
      star.y = Math.random() * height;
      star.r = (0.45 + Math.random() * 1.05) * star.z;
      // Depth comes from size and speed, not from dimming: a far star is a
      // small point, not a faded one, or the sky reads as noise.
      star.alpha = (0.4 + Math.random() * 0.55) * (0.72 + 0.28 * Math.min(1, star.z));
      star.mint = Math.random() < 0.2;
      star.sparkle = Math.random() < 0.09 && star.z > 0.95;
      star.twinklePhase = Math.random() * Math.PI * 2;
      star.twinkleSpeed = 0.004 + Math.random() * 0.012;
      star.driftX = (Math.random() - 0.5) * 0.05 * star.z;
      star.driftY = (Math.random() - 0.5) * 0.05 * star.z;
      star.vx = 0;
      star.vy = 0;
      star.streak = 0;
      star.dirY = 1;
      star.hot = 0;
      // Constellation membership. `null` until the field is assigned a shape;
      // a member keeps its slot on the panel it is currently tracing.
      star.cseed = null;
      star.gvx = 0;
      star.gvy = 0;
      star.gprev = 0;
      star.fx = 0;
      star.fy = 1;
      return star;
    }

    function starTarget() {
      return width < 768 ? STAR_COUNT_MOBILE : STAR_COUNT_DESKTOP;
    }

    function resetField() {
      const target = starTarget();
      while (stars.length < target) stars.push(spawnStar({}));
      if (stars.length > target) stars.length = target;
      for (let i = 0; i < stars.length; i++) spawnStar(stars[i]);
    }

    // On mobile the URL bar sliding in and out fires `resize` on every scroll
    // gesture. Re-seeding the sky there would look like the whole field
    // teleporting, so an existing field is only remapped into the new box.
    function remapField(prevWidth, prevHeight) {
      const sx = prevWidth > 0 ? width / prevWidth : 1;
      const sy = prevHeight > 0 ? height / prevHeight : 1;
      for (let i = 0; i < stars.length; i++) {
        const s = stars[i];
        s.x *= sx;
        s.y *= sy;
        if (s.x < 0) s.x = 0;
        else if (s.x > width) s.x = width;
        if (s.y < 0) s.y = 0;
        else if (s.y > height) s.y = height;
      }
    }

    // Stars fade where the hero copy sits: the sky must not fight the headline.
    function focusFade(x, y) {
      const cx = width * 0.5;
      const cy = height * 0.42;
      const rx = Math.min(width * 0.36, 560);
      const ry = Math.min(height * 0.48, 540);
      const dx = (x - cx) / rx;
      const dy = (y - cy) / ry;
      const d = Math.sqrt(dx * dx + dy * dy);
      if (d >= 1) return 1;
      return 0.45 + 0.55 * d * d;
    }

    // --- Scope constellation -------------------------------------------------
    // The sky is never doubled. A slice of the ambient field is re-tasked to
    // become the icon of the capability pillar that owns the viewport: every
    // member takes a fixed slot on the glyph's own outline, and the same slot
    // exists on every pillar, so the shape can be carried from one icon to the
    // next and morph between them instead of dissolving into noise.
    const scopes = [];
    const scopeState = {
      presence: 0,
      u: 0,
      from: null,
      to: null,
      arcX: 0,
      arcY: 0,
      releasing: false,
    };
    const SCOPE_RAMP = 0.46; // viewport fraction the shape forms around
    const SVG_NS = 'http://www.w3.org/2000/svg';
    const glyphCache = {};

    let scopeCanvas = null;
    let scopeCtx = null;
    let scopeLive = false;
    let scopeWasLive = false;
    let memberCount = 0;

    // Point scratch: the target maths runs for every member every frame, so it
    // writes here instead of returning a fresh object.
    let scopeTX = 0;
    let scopeTY = 0;
    let scopeLX = 0;
    let scopeLY = 0;

    function smooth01(t) {
      if (t <= 0) return 0;
      if (t >= 1) return 1;
      return t * t * (3 - 2 * t);
    }

    function ensureScopeCanvas() {
      if (scopeCanvas || isReducedMotion) return;
      scopeCanvas = document.createElement('canvas');
      scopeCanvas.className = 'scope-canvas';
      scopeCanvas.setAttribute('aria-hidden', 'true');
      document.body.appendChild(scopeCanvas);
      scopeCtx = scopeCanvas.getContext('2d', { alpha: true });
    }

    // Document-space position that ignores reveal transforms: the pillars are
    // translated into place on first paint, and a rect measured then would
    // bake that offset into every target for the rest of the session.
    function layoutPos(el) {
      let x = 0;
      let y = 0;
      let node = el;
      while (node) {
        x += node.offsetLeft || 0;
        y += node.offsetTop || 0;
        node = node.offsetParent;
      }
      return { x: x, y: y };
    }

    // The three panel icons are single-path symbols, so their real geometry can
    // be walked and resampled once. Returns points in the glyph's rendered
    // pixels, relative to its centre.
    function glyphPoints(use, renderWidth) {
      if (!use || !(renderWidth > 0)) return [];
      const href = use.getAttribute('href') || use.getAttribute('xlink:href') || '';
      if (!href) return [];
      let data = glyphCache[href];
      if (data === undefined) {
        data = null;
        const symbol = document.querySelector(href);
        if (symbol) {
          const vb = (symbol.getAttribute('viewBox') || '0 0 24 24').split(/[\s,]+/);
          const vbX = Number(vb[0]) || 0;
          const vbY = Number(vb[1]) || 0;
          const vbW = Number(vb[2]) || 24;
          const vbH = Number(vb[3]) || 24;
          const probe = document.createElementNS(SVG_NS, 'svg');
          probe.setAttribute('viewBox', [vbX, vbY, vbW, vbH].join(' '));
          probe.style.cssText = 'position:absolute;left:-9999px;top:0;width:0;height:0;overflow:hidden';
          document.body.appendChild(probe);
          const parts = [];
          let total = 0;
          const nodes = symbol.querySelectorAll('path, rect, circle, ellipse, line, polyline, polygon');
          for (let i = 0; i < nodes.length; i++) {
            const src = nodes[i];
            const clone = document.createElementNS(SVG_NS, src.tagName);
            for (let a = 0; a < src.attributes.length; a++) {
              clone.setAttribute(src.attributes[a].name, src.attributes[a].value);
            }
            probe.appendChild(clone);
            let len = 0;
            try {
              len = clone.getTotalLength();
            } catch (err) {
              len = 0;
            }
            if (len > 0.5) {
              parts.push({ el: clone, len: len, start: total });
              total += len;
            }
          }
          const count = 260;
          const pts = [];
          if (total > 0 && parts.length) {
            for (let i = 0; i < count; i++) {
              const d = (i / count) * total;
              let k = 0;
              while (k < parts.length - 1 && d > parts[k].start + parts[k].len) k++;
              const part = parts[k];
              const p = part.el.getPointAtLength(Math.max(0, Math.min(part.len, d - part.start)));
              pts.push([p.x - (vbX + vbW / 2), p.y - (vbY + vbH / 2)]);
            }
          }
          document.body.removeChild(probe);
          data = { unit: vbW, points: pts };
        }
        glyphCache[href] = data;
      }
      if (!data || !data.points.length) return [];
      const k = renderWidth / data.unit;
      const out = [];
      for (let i = 0; i < data.points.length; i++) {
        out.push([data.points[i][0] * k, data.points[i][1] * k]);
      }
      return out;
    }

    function buildScopes() {
      const els = document.querySelectorAll('[data-scope-visual]');
      scopes.length = 0;
      for (let i = 0; i < els.length; i++) {
        const el = els[i];
        const icon = el.querySelector('.cap-visual-icon');
        const rgb = window.getComputedStyle(icon || el).color.match(/[\d.]+/g) || [];
        scopes.push({
          el: el,
          icon: icon,
          accent: [Number(rgb[0]) || 0, Number(rgb[1]) || 242, Number(rgb[2]) || 176],
          left: 0,
          top: 0,
          width: 0,
          height: 0,
          center: 0,
          iconCx: 0,
          iconCy: 0,
          iconR: 60,
          glyph: [],
          form: -1,
        });
      }
    }

    function measureScopes() {
      for (let i = 0; i < scopes.length; i++) {
        const sc = scopes[i];
        const el = sc.el;
        const pos = layoutPos(el);
        sc.left = pos.x;
        sc.top = pos.y;
        sc.width = el.offsetWidth;
        sc.height = el.offsetHeight;
        sc.center = pos.y + sc.height / 2;

        const icon = sc.icon;
        if (icon) {
          // Relative rects cancel the reveal transform that still sits on the
          // grid when the page first paints.
          const pr = el.getBoundingClientRect();
          const ir = icon.getBoundingClientRect();
          sc.iconCx = sc.left + (ir.left - pr.left) + ir.width / 2;
          sc.iconCy = sc.top + (ir.top - pr.top) + ir.height / 2;
          sc.iconR = ir.width / 2;
          const svg = icon.querySelector('svg');
          sc.glyph = svg
            ? glyphPoints(svg.querySelector('use'), svg.getBoundingClientRect().width)
            : [];
        }
        sc.form = -1;
      }
    }

    // The whole field belongs to the sky; a slice of it is asked to become the
    // icon of the pillar in focus. Every member takes a fixed slot on the
    // glyph's outline, and the same slot exists on every pillar, so the shape
    // can be carried from one to the next and morph through the icons instead
    // of dissolving back into the sky.
    function assignMembers() {
      if (!scopes.length) return;
      const want = width < 768 ? 60 : 88;
      memberCount = want;

      for (let i = 0; i < stars.length; i++) stars[i].cseed = null;

      // Depth ordering: the stars the eye already follows are the ones that
      // gather, so the shape reads immediately.
      const order = [];
      for (let i = 0; i < stars.length; i++) order.push(i);
      order.sort(function (a, b) {
        return stars[b].z - stars[a].z;
      });

      for (let k = 0; k < want && k < order.length; k++) {
        // Arc-length slots, evenly spread with a little organic jitter, so the
        // outline is drawn by points rather than ruled by a machine.
        const t = Math.min(1, Math.max(0, (k + 0.5 + (Math.random() - 0.5) * 0.85) / want));
        stars[order[k]].cseed = {
          t: t,
          // A small stagger on entry and exit keeps the edge alive without
          // breaking the shape apart while it travels.
          lag: Math.random() * 0.12,
          // A quorum of brighter points gives the outline depth instead of an
          // even string of identical beads.
          big: Math.random() < 0.18,
          jx: (Math.random() - 0.5) * 2.4,
          jy: (Math.random() - 0.5) * 2.4,
        };
      }
    }

    // Where a member sits *relative to its icon's centre*. Keeping the slot in
    // local space is what lets the formation travel as one rigid body instead
    // of every point drifting on its own line.
    function scopeLocal(sc, seed) {
      if (sc.glyph.length) {
        const g = sc.glyph[Math.min(sc.glyph.length - 1, (seed.t * sc.glyph.length) | 0)];
        scopeLX = g[0] + seed.jx;
        scopeLY = g[1] + seed.jy;
        return;
      }
      // Fallback if the symbol could not be sampled: a ring, so the sky still
      // forms something deliberate.
      const a = seed.t * Math.PI * 2;
      scopeLX = Math.cos(a) * (sc.iconR * 0.8 + seed.jx);
      scopeLY = Math.sin(a) * (sc.iconR * 0.8 + seed.jy);
    }

    // The formation crosses the gap as one object. The icon is translated
    // whole along a single shared curve and only re-fits its points into the
    // next glyph near the end of the flight, so mid-flight the eye sees the
    // icon it already knows moving through space, not two shapes blended into
    // a smear. One shared arc, one shared rhythm.
    function scopeTarget(seed) {
      const from = scopeState.from;
      const to = scopeState.to;
      if (!from) return;
      scopeLocal(from, seed);
      const lax = scopeLX;
      const lay = scopeLY;
      let lbx = lax;
      let lby = lay;
      const crossing = !!(to && to !== from);
      if (crossing) {
        scopeLocal(to, seed);
        lbx = scopeLX;
        lby = scopeLY;
      }
      // Per-point lag is tiny: it softens the edge of the formation without
      // letting the shape fall apart on the way across.
      const u = smooth01((scopeState.u - seed.lag * 0.5) / (1 - seed.lag * 0.5));
      // Rigid for most of the trip, then a short re-fit into the new glyph.
      const morph = crossing && u > 0.58 ? smooth01((u - 0.58) / 0.42) : 0;
      let cx = from.iconCx;
      let cy = from.iconCy;
      if (crossing) {
        cx += (to.iconCx - from.iconCx) * u;
        cy += (to.iconCy - from.iconCy) * u;
      }
      let x = cx + lax + (lbx - lax) * morph;
      let y = cy - scrollState.y + lay + (lby - lay) * morph;
      if (u > 0.001 && u < 0.999) {
        const arc = Math.sin(Math.PI * u);
        x += scopeState.arcX * arc;
        y += scopeState.arcY * arc;
      }
      scopeTX = x;
      scopeTY = y;
    }

    function updateScopes(y) {
      if (isReducedMotion || !scopes.length) return;

      const first = scopes[0];
      const last = scopes[scopes.length - 1];
      const focus = y + height * SCOPE_RAMP;

      // Presence: the field starts gathering while the first panel climbs
      // toward the focus line, and lets go after the last one has passed it.
      const lead = Math.min(height * 0.62, 760);
      const enterA = first.center - lead;
      const enterB = first.center - Math.min(height * 0.14, 170);
      const exitA = last.center + Math.min(height * 0.16, 200);
      const exitB = last.center + Math.min(height * 0.62, 760);

      let presence = 1;
      if (focus <= enterA || focus >= exitB) presence = 0;
      else if (focus < enterB) presence = smooth01((focus - enterA) / (enterB - enterA));
      else if (focus > exitA) presence = smooth01(1 - (focus - exitA) / (exitB - exitA));

      let from = first;
      let to = first;
      let u = 0;
      if (focus >= last.center) {
        from = last;
        to = last;
      } else if (focus > first.center) {
        for (let i = 0; i < scopes.length - 1; i++) {
          if (focus < scopes[i + 1].center) {
            from = scopes[i];
            to = scopes[i + 1];
            break;
          }
        }
        const span = to.center - from.center;
        // The journey is deliberately compressed into the middle of the gap:
        // the shape holds on its panel while it is still on screen, crosses
        // in a burst, and is already whole again before the next panel
        // settles on the focus line.
        const progress = span > 0 ? (focus - from.center) / span : 1;
        u = smooth01((progress - 0.2) / 0.34);
      }

      // Each panel materialises exactly as much as the shape currently sits on
      // it, which is what turns the points into the surface they trace.
      for (let i = 0; i < scopes.length; i++) {
        const sc = scopes[i];
        let form = 0;
        if (presence > 0) {
          if (sc === from && sc === to) form = presence;
          else if (sc === from) form = presence * (1 - u);
          else if (sc === to) form = presence * u;
        }
        form = Math.round(form * 100) / 100;
        if (form !== sc.form) {
          sc.form = form;
          sc.el.style.setProperty('--sc-form', form.toFixed(2));
        }
      }

      // One arc for the whole formation, perpendicular to the line between the
      // two icons: the constellation sweeps across the gap as a single object
      // instead of cutting a straight diagonal through the copy between them.
      let arcX = 0;
      let arcY = 0;
      if (from !== to) {
        const dx = to.iconCx - from.iconCx;
        const dy = to.iconCy - from.iconCy;
        const len = Math.sqrt(dx * dx + dy * dy) || 1;
        const amp = Math.min(150, len * 0.18);
        arcX = (-dy / len) * amp;
        arcY = (dx / len) * amp;
      }

      scopeState.releasing = presence < scopeState.presence - 0.0001;
      scopeState.presence = presence;
      scopeState.u = u;
      scopeState.from = from;
      scopeState.to = to;
      scopeState.arcX = arcX;
      scopeState.arcY = arcY;
    }

    function resize() {
      const prevWidth = width;
      const prevHeight = height;
      width = window.innerWidth || 360;
      height = window.innerHeight || 640;
      dpr = targetDpr();
      pointer.radius = width < 768 ? 150 : 190;
      canvas.width = Math.floor(width * dpr);
      canvas.height = Math.floor(height * dpr);
      canvas.style.width = width + 'px';
      canvas.style.height = height + 'px';
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      if (stars.length && stars.length === starTarget() && prevWidth > 0) {
        remapField(prevWidth, prevHeight);
      } else {
        resetField();
        assignMembers();
      }
      if (memberCount !== (width < 768 ? 60 : 88)) assignMembers();

      if (scopeCanvas && scopeCtx) {
        scopeCanvas.width = Math.floor(width * dpr);
        scopeCanvas.height = Math.floor(height * dpr);
        scopeCanvas.style.width = width + 'px';
        scopeCanvas.style.height = height + 'px';
        scopeCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
      }

      measureScopes();
      updateScopes(scrollState.y);
      if (isReducedMotion) render();
    }

    window.addEventListener('resize', resize, { passive: true });

    // --- Pointer ------------------------------------------------------------
    function onPointerMove(e) {
      pointer.x = e.clientX;
      pointer.y = e.clientY;
      pointer.active = true;
    }

    if (!isReducedMotion && isFinePointer.matches) {
      window.addEventListener('pointermove', onPointerMove, { passive: true });
      document.addEventListener('mouseleave', function () {
        pointer.active = false;
        pointer.x = -9999;
        pointer.y = -9999;
      });
      window.addEventListener('pointerdown', function (e) {
        if (e.button !== 0) return;
        pulses.push({ x: e.clientX, y: e.clientY, r: 0, alpha: 0.9 });
        if (pulses.length > 3) pulses.shift();
      }, { passive: true });
    }

    // --- Idle shooting stars ------------------------------------------------
    // Each meteor picks its own entry edge, entry point and heading, and the
    // next one is never allowed to repeat the previous heading: the sky should
    // read as a busy sky, not the same two diagonal lines on a loop.
    let lastMeteorAngle = -10;

    function meteorCandidate() {
      // Heading variety: shallow grazes, mid diagonals, and the occasional
      // steep dive. Measured in degrees below the horizontal.
      const tier = Math.random();
      const rise = tier < 0.45 ? 7 + Math.random() * 16
        : tier < 0.85 ? 23 + Math.random() * 27
          : 54 + Math.random() * 26;

      const edge = Math.random();
      const toRight = edge < 0.5;
      let x;
      let y;
      let angle;
      if (edge < 0.84) {
        // In from a side edge. The entry height is bounded by how much height
        // this heading covers across the frame, so a shallow meteor enters near
        // the top instead of burning out above the fold.
        angle = toRight ? rise : 180 - rise;
        const drop = (width + 140) * Math.tan(angle * Math.PI / 180);
        const lowY = -Math.min(80, drop * 0.15);
        const highY = Math.max(lowY + 40, Math.min(height * 0.8, height - drop * 0.3));
        x = toRight ? -60 - Math.random() * 170 : width + 60 + Math.random() * 170;
        y = lowY + Math.random() * (highY - lowY);
      } else {
        // Straight out of the top, always diving so it lands in view. The entry
        // sits on the side it is heading away from, so it crosses more sky.
        angle = 40 + Math.random() * 40;
        const right = Math.random() < 0.5;
        if (!right) angle = 180 - angle;
        x = width * (right ? 0.08 + Math.random() * 0.6 : 0.32 + Math.random() * 0.6);
        y = -60 - Math.random() * 150;
      }

      const rad = angle * Math.PI / 180;
      const speed = 8 + Math.random() * 9;
      // Life is set from the distance the meteor should cover, so slow ones are
      // not cut off halfway across the sky.
      const travel = (width + height) * (0.6 + Math.random() * 0.5);
      return {
        x: x,
        y: y,
        angle: rad,
        vx: Math.cos(rad) * speed,
        vy: Math.sin(rad) * speed,
        life: 0,
        maxLife: Math.max(60, Math.round(travel / speed)),
        len: 130 + Math.random() * 170,
        width: 0.8 + Math.random() * 1.1,
        // Distance variety: small far meteors and big near ones.
        scale: 0.75 + Math.random() * 0.6,
        mint: Math.random() < 0.35,
      };
    }

    function spawnMeteor() {
      let m = meteorCandidate();
      // A new heading that echoes the last one looks like a loop; re-roll.
      for (let i = 0; i < 3 && Math.abs(m.angle - lastMeteorAngle) < 0.35; i++) {
        m = meteorCandidate();
      }
      lastMeteorAngle = m.angle;
      meteors.push(m);
      if (meteors.length > 2) meteors.shift();
    }

    // --- Frame --------------------------------------------------------------
    function render(now) {
      if (!isReducedMotion) {
        animationFrameId = requestAnimationFrame(render);
      } else {
        animationFrameId = null;
      }

      if (document.hidden) return;

      const dt = isReducedMotion
        ? 1
        : Math.min(Math.max((now - lastFrameTime) / 16.667, 0.2), 3);
      lastFrameTime = now;
      frameCount += dt;

      const skySprite = sprites.star;
      const mintSprite = sprites.mint;
      const skyRGB = '236, 248, 255';
      const mintRGB = '120, 255, 214';

      // Scroll: stars physically slide against the page, and the distance they
      // travel this frame becomes the length of their streak.
      const y = scrollState.y;
      const scrollDelta = y - lastScrollY;
      lastScrollY = y;

      ctx.clearRect(0, 0, width, height);

      // 1. Click pulses: expanding ring, one arc, fading fast.
      for (let i = pulses.length - 1; i >= 0; i--) {
        const p = pulses[i];
        p.r += 11 * dt;
        p.alpha -= 0.018 * dt;
        if (p.alpha <= 0) {
          pulses.splice(i, 1);
          continue;
        }
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
        ctx.strokeStyle = 'rgba(0, 242, 176, ' + (p.alpha * 0.35).toFixed(3) + ')';
        ctx.lineWidth = 1;
        ctx.stroke();
      }

      // 2. Stars.
      const wakeRadius = pointer.radius;
      const wakeRadiusSq = wakeRadius * wakeRadius;

      // The constellation is the same sky composited on a second sheet: the
      // panel surfaces are almost opaque on a phone, so the points that form
      // them have to sit in front of the cards rather than behind. The sheet
      // only exists while a shape is in play, and costs nothing at rest.
      const scopeOn = scopeState.presence > 0.002 && !!scopeCtx;
      if (scopeCtx && (scopeOn || scopeWasLive)) {
        scopeCtx.clearRect(0, 0, width, height);
      }
      scopeWasLive = scopeOn;
      if (scopeCanvas && scopeOn !== scopeLive) {
        scopeLive = scopeOn;
        scopeCanvas.classList.toggle('is-live', scopeOn);
        // The glyph steps back to a ghost while the sky is forming it; the
        // points become the icon instead of decorating one.
        for (let k = 0; k < scopes.length; k++) {
          scopes[k].el.style.setProperty('--sc-draw', scopeOn ? '0.05' : '1');
        }
      }

      let scopeR = 0;
      let scopeG = 242;
      let scopeB = 176;
      if (scopeOn && scopeState.from) {
        const fa = scopeState.from.accent;
        const ta = (scopeState.to || scopeState.from).accent;
        const mix = scopeState.u;
        scopeR = Math.round(fa[0] + (ta[0] - fa[0]) * mix);
        scopeG = Math.round(fa[1] + (ta[1] - fa[1]) * mix);
        scopeB = Math.round(fa[2] + (ta[2] - fa[2]) * mix);
      }
      const scopeRGB = 'rgb(' + scopeR + ', ' + scopeG + ', ' + scopeB + ')';
      const mintish = scopeG > scopeB * 1.08;
      const scopeTrail = mintish ? sprites.streakMint : sprites.streakViolet;
      const scopeAura = mintish ? sprites.auraMint : sprites.auraViolet;
      const skyFill = 'rgb(' + skyRGB + ')';
      const mintFill = 'rgb(' + mintRGB + ')';

      for (let i = 0; i < stars.length; i++) {
        const s = stars[i];
        const seed = s.cseed;
        const prevX = s.x;
        const prevY = s.y;

        // 0 = a free star, 1 = a point in the constellation. The entrance is
        // gated per star, so the outline is drawn rather than popped.
        let gather = 0;
        let targetX = 0;
        let targetY = 0;
        if (seed && scopeOn) {
          const gate = smooth01((scopeState.presence - seed.lag * 0.55) / (1 - seed.lag * 0.55));
          if (gate > 0) {
            gather = gate;
            scopeTarget(seed);
            targetX = scopeTX;
            targetY = scopeTY;
          }
        }
        const free = 1 - gather;

        // Parallax: near stars travel further, which is what makes the field
        // feel like space instead of a wallpaper. Only the scattered part of
        // the field rides the scroll; a point being carried is driven by the
        // shape instead.
        const parallax = (0.09 + s.z * 0.24) * free;
        const my = -scrollDelta * parallax;

        s.x += (s.driftX + s.vx) * dt * free;
        s.y += (s.driftY + s.vy) * dt * free;
        s.vx *= Math.pow(0.9, dt);
        s.vy *= Math.pow(0.9, dt);
        s.y += my;

        // A damped pull rather than a hard set: the star arrives with weight
        // and keeps a pixel or two of lag while the panel is scrolling.
        if (gather > 0.001) {
          const pull = 0.028 * gather;
          s.gvx = (s.gvx + (targetX - s.x) * pull * dt) * Math.pow(0.87, dt);
          s.gvy = (s.gvy + (targetY - s.y) * pull * dt) * Math.pow(0.87, dt);
          s.x += s.gvx * dt;
          s.y += s.gvy * dt;
        } else {
          s.gvx *= Math.pow(0.86, dt);
          s.gvy *= Math.pow(0.86, dt);
        }

        // When the shape lets go, the energy comes from its own unmaking: each
        // frame of falling gather turns into an outward kick, so the icon
        // unpicks itself from the centre into the sky. The kick is proportional
        // to the drop, so a parked page scatters nothing.
        if (seed) {
          if (gather < s.gprev) {
            const sc = scopeState.from;
            if (sc) {
              const rdx = s.x - sc.iconCx;
              const rdy = s.y - (sc.iconCy - scrollState.y);
              const rd = Math.sqrt(rdx * rdx + rdy * rdy) || 1;
              const kick = (s.gprev - gather) * 14;
              s.vx += (rdx / rd) * kick;
              s.vy += (rdy / rd) * kick;
            }
          }
          s.gprev = gather;
        } else {
          s.gprev = 0;
        }

        // Wrap: a star that leaves the top re-enters from the bottom, keeping
        // the sky density constant through any amount of scrolling. A member
        // on the shape is exempt, or it would teleport away from its slot.
        if (free > 0.985) {
          if (s.x < -20) s.x = width + 20;
          else if (s.x > width + 20) s.x = -20;
          if (s.y < -20) s.y = height + 20;
          else if (s.y > height + 20) s.y = -20;
        }

        // Pointer: part the field gently and brighten what is close. The
        // pointer never draws a tail - streaks belong to scroll and meteors.
        let hotTarget = 0;
        if (pointer.active && free > 0.02) {
          const dx = s.x - pointer.x;
          const dy = s.y - pointer.y;
          const distSq = dx * dx + dy * dy;
          if (distSq < wakeRadiusSq) {
            const dist = Math.sqrt(distSq) || 1;
            const near = 1 - dist / wakeRadius;
            hotTarget = near * near * 1.15 * free;
            const push = near * near * (isReducedMotion ? 0 : 0.5) * s.z * free;
            s.vx += (dx / dist) * push;
            s.vy += (dy / dist) * push;
          }
        }

        // Click pulses light the ring as it passes, in the scattered sky only.
        if (free > 0.5) {
          for (let k = 0; k < pulses.length; k++) {
            const p = pulses[k];
            const dx = s.x - p.x;
            const dy = s.y - p.y;
            const band = Math.abs(Math.sqrt(dx * dx + dy * dy) - p.r);
            if (band < 30) hotTarget += (1 - band / 30) * p.alpha * 0.9;
          }
        }

        s.hot += (hotTarget - s.hot) * Math.min(1, 0.18 * dt);

        // Streak: a free star's length is how far scroll parallax moved it this
        // frame. A member instead trails its own flight, which is what turns
        // the hand-off into a shower of small shooting stars.
        if (gather > 0.02) {
          const mdx = s.x - prevX;
          const mdy = s.y - prevY;
          const speed = Math.sqrt(mdx * mdx + mdy * mdy);
          if (speed > 0.35) {
            s.fx = mdx / speed;
            s.fy = mdy / speed;
            s.streak += (Math.min(speed * 8.5, 240) - s.streak) * Math.min(1, 0.5 * dt);
          } else {
            s.streak += (0 - s.streak) * Math.min(1, 0.22 * dt);
          }
        } else {
          const motion = Math.abs(my);
          if (motion > 0.05) s.dirY = my > 0 ? 1 : -1;
          const targetStreak = Math.min(motion * 10, 300);
          s.streak += (targetStreak - s.streak) * Math.min(1, 0.5 * dt);
        }

        const twinkle = gather > 0.5
          ? 0.7 + 0.34 * Math.sin(s.twinklePhase)
          : 0.72 + 0.28 * Math.sin(s.twinklePhase);
        s.twinklePhase += s.twinkleSpeed * dt * 6;

        // A formed point is exempt from the hero's centre fade: it is the
        // content there, not sky noise behind it.
        let a = s.alpha * twinkle * (focusFade(s.x, s.y) * free + gather);
        a *= 1 + s.hot * 1.4;
        if (gather > 0) a *= 1 + gather * 0.18;
        if (a <= 0.004) continue;
        if (a > 1) a = 1;

        // A gathered point is drawn on the sheet that sits above the cards;
        // a scattered one stays on the sky behind everything.
        const sheet = gather > 0.004 ? scopeCtx : ctx;
        // Formed points burn white-hot: they sit on the icon's own stroke, so
        // a mint bead would sink into a mint bar. The accent lives in the halo.
        const sprite = gather > 0.3 ? sprites.star : (s.mint ? mintSprite : skySprite);
        const rgb = s.mint ? mintRGB : skyRGB;

        // Shooting-star tail. Drawn as soon as a star starts moving, so a gentle
        // scroll still smears it; the length is capped so a page jump does not
        // turn the whole sky into one line.
        if (s.streak > 1.5) {
          const len = Math.min(s.streak * 2.6, 420);
          const wid = Math.max(2.4, s.r * 6);
          sheet.globalAlpha = Math.min(1, a * 0.9);
          if (gather > 0.02) {
            // Flight has a direction, so the tail rotates onto it.
            sheet.save();
            sheet.translate(s.x, s.y);
            sheet.rotate(Math.atan2(s.fy, s.fx) - Math.PI / 2);
            sheet.drawImage(scopeTrail, -wid / 2, -len, wid, len);
            sheet.restore();
          } else if (s.dirY < 0) {
            // Scroll only ever moves stars straight up or down, so the tail
            // needs no rotation - just the flip that keeps it behind the head.
            sheet.save();
            sheet.translate(s.x, s.y);
            sheet.scale(1, -1);
            sheet.drawImage(s.mint ? sprites.streakMint : sprites.streak, -wid / 2, -len, wid, len);
            sheet.restore();
          } else {
            sheet.drawImage(s.mint ? sprites.streakMint : sprites.streak, s.x - wid / 2, s.y - len, wid, len);
          }
          sheet.globalAlpha = 1;
        }

        // Star head: soft halo, then a crisp core so the point reads as light.
        let size = ((s.streak > 6 ? 4.2 : 5.4) + s.r * 6.2) * (1 - 0.1 * gather);
        // A quorum of the members are drawn larger, which keeps the outline
        // from reading as an even string of identical beads.
        if (gather > 0.3 && seed && seed.big) size *= 1.4;
        // A wide aura under the formed points, drawn only once they are on the
        // shape: the outline glows instead of being a thin dotted line.
        if (gather > 0.2) {
          const halo = size * 2.5;
          sheet.globalAlpha = Math.min(1, a * gather * 0.36);
          sheet.drawImage(scopeAura, s.x - halo / 2, s.y - halo / 2, halo, halo);
        }
        sheet.globalAlpha = a;
        sheet.drawImage(sprite, s.x - size / 2, s.y - size / 2, size, size);
        sheet.globalAlpha = Math.min(1, a * 1.05);
        sheet.beginPath();
        sheet.arc(s.x, s.y, Math.max(0.45, s.r * 0.5), 0, Math.PI * 2);
        sheet.fillStyle = gather > 0.25 ? 'rgb(244, 255, 252)' : (s.mint ? mintFill : skyFill);
        sheet.fill();
        sheet.globalAlpha = 1;

        // A few bright stars get the four-point diffraction cross that makes a
        // point of light read as a star rather than a pixel. Formed points skip
        // it: the outline should read as a line of light, not a row of crosses.
        if (s.sparkle && s.streak < 8 && a > 0.35 && gather < 0.12) {
          const arm = s.r * 3.2;
          ctx.strokeStyle = 'rgba(' + rgb + ', ' + (a * 0.3).toFixed(3) + ')';
          ctx.lineWidth = 0.6;
          ctx.beginPath();
          ctx.moveTo(s.x - arm, s.y);
          ctx.lineTo(s.x + arm, s.y);
          ctx.moveTo(s.x, s.y - arm);
          ctx.lineTo(s.x, s.y + arm);
          ctx.stroke();
        }
      }

      // 3. Idle shooting stars.
      if (!isReducedMotion) {
        if (frameCount > nextMeteorAt) {
          nextMeteorAt = frameCount + 420 + Math.random() * 660;
          spawnMeteor();
        }

        for (let i = meteors.length - 1; i >= 0; i--) {
          const m = meteors[i];
          m.life += dt;
          m.x += m.vx * dt;
          m.y += m.vy * dt;

          const t = m.life / m.maxLife;
          if (t >= 1 || m.x < -400 || m.x > width + 400 || m.y > height + 400) {
            meteors.splice(i, 1);
            continue;
          }

          // Sine envelope: fade in, peak mid-flight, fade out. No hard start.
          const fade = Math.sin(Math.PI * t);
          const speed = Math.sqrt(m.vx * m.vx + m.vy * m.vy) || 1;
          const dx = m.vx / speed;
          const dy = m.vy / speed;

          // Tapered tail from the shared sprite, rotated onto the flight path.
          const tailSprite = m.mint ? sprites.streakMint : sprites.streak;
          const tailLen = m.len * (0.6 + 0.4 * fade);
          const tailWid = (4.5 + m.width * 5) * m.scale;
          ctx.globalAlpha = Math.min(1, fade * 0.9);
          ctx.save();
          ctx.translate(m.x, m.y);
          ctx.rotate(Math.atan2(dy, dx) - Math.PI / 2);
          ctx.drawImage(tailSprite, -tailWid / 2, -tailLen, tailWid, tailLen);
          ctx.restore();

          const headSprite = m.mint ? sprites.mint : sprites.star;
          const headSize = (18 + fade * 14) * m.scale;
          ctx.globalAlpha = Math.min(1, fade * 0.95);
          ctx.drawImage(headSprite, m.x - headSize / 2, m.y - headSize / 2, headSize, headSize);
          ctx.globalAlpha = 1;
        }
      }

      // 4. Pointer bloom: a single soft light so the sky reacts to the cursor
      // even between stars.
      if (pointer.active && !isReducedMotion) {
        const tint = '0, 242, 176';
        const bloom = ctx.createRadialGradient(pointer.x, pointer.y, 0, pointer.x, pointer.y, 150);
        bloom.addColorStop(0, 'rgba(' + tint + ', 0.07)');
        bloom.addColorStop(0.45, 'rgba(' + tint + ', 0.03)');
        bloom.addColorStop(1, 'rgba(' + tint + ', 0)');
        ctx.beginPath();
        ctx.arc(pointer.x, pointer.y, 150, 0, Math.PI * 2);
        ctx.fillStyle = bloom;
        ctx.fill();
      }
    }

    buildScopes();
    if (scopes.length) ensureScopeCanvas();
    onScrollFrame(updateScopes);
    resize();
    if (!isReducedMotion) render(performance.now());

    // Fonts and the reveal transform settle after the first layout, and both
    // move the pillars; the slots are measured again once the page is stable.
    window.addEventListener('load', function () {
      measureScopes();
      updateScopes(scrollState.y);
    });
    if ('ResizeObserver' in window && scopes.length) {
      const grid = scopes[0].el.closest('.cap-columns');
      if (grid) {
        const ro = new ResizeObserver(function () {
          measureScopes();
          updateScopes(scrollState.y);
        });
        ro.observe(grid);
      }
    }

    // Stop the field entirely while the tab is hidden; resume without a jump.
    document.addEventListener('visibilitychange', function () {
      if (document.hidden) {
        if (animationFrameId) {
          cancelAnimationFrame(animationFrameId);
          animationFrameId = null;
        }
        return;
      }
      if (!isReducedMotion && !animationFrameId) {
        lastFrameTime = performance.now();
        animationFrameId = requestAnimationFrame(render);
      }
    });
  }

  /* ==========================================================================
     4. Card Reactive Surface Lighting & Micro 3D Tilt
     ========================================================================== */
  function initCardInteractivity() {
    if (!isFinePointer.matches) return;
    const cards = document.querySelectorAll(
      '.double-bezel, .card-inner, .feature-card, .cap-item, .hero-oneshot, .arch-card, .dl-card, .faq-item, .scope-card, .tier-card, .doc-code-card, .download-card, .trust-banner'
    );

    if (!cards.length) return;

    cards.forEach(function (card) {
      // The player's frame is answered by the scroll, not by the pointer: its
      // border must not tilt, brighten or shift when the cursor crosses it.
      if (card.classList.contains('video-double-bezel')) return;

      let isTicking = false;

      function onPointerMove(e) {
        if (isTicking) return;
        isTicking = true;

        requestAnimationFrame(function () {
          const rect = card.getBoundingClientRect();
          const x = e.clientX - rect.left;
          const y = e.clientY - rect.top;

          card.style.setProperty('--mouse-x', x + 'px');
          card.style.setProperty('--mouse-y', y + 'px');

          if (
            !isReducedMotion &&
            (card.classList.contains('double-bezel') ||
              card.classList.contains('feature-card') ||
              card.classList.contains('tier-card') ||
              card.classList.contains('scope-card'))
          ) {
            const centerX = rect.width / 2;
            const centerY = rect.height / 2;
            const tiltX = ((y - centerY) / centerY) * -1.5;
            const tiltY = ((x - centerX) / centerX) * 1.5;

            card.style.setProperty('--tilt-x', tiltX.toFixed(2) + 'deg');
            card.style.setProperty('--tilt-y', tiltY.toFixed(2) + 'deg');
          }

          isTicking = false;
        });
      }

      function onPointerLeave() {
        card.style.removeProperty('--tilt-x');
        card.style.removeProperty('--tilt-y');
      }

      card.addEventListener('pointermove', onPointerMove, { passive: true });
      card.addEventListener('pointerleave', onPointerLeave, { passive: true });
    });
  }

  /* ==========================================================================
     5. Initialization on DOMContentLoaded
     ========================================================================== */
  function initServiceWorker() {
    if (!('serviceWorker' in navigator) || !window.location.protocol.startsWith('http')) return;

    window.addEventListener('load', function () {
      // Resolve against motion.js itself, so every page â€” including the ones
      // nested a level deep â€” lands on the single shared worker regardless of
      // whether the site is mounted at the domain root or under /Deltempo/.
      const script = document.querySelector('script[src*="motion.js"]');
      const swUrl = script ? new URL('sw.js', script.src).href : new URL('sw.js', document.baseURI).href;

      navigator.serviceWorker.register(swUrl).catch(function (err) {
        console.debug('ServiceWorker registration note:', err);
      });
    });
  }

  function initBackToTop() {
    const btn = document.getElementById('backToTopBtn');
    if (!btn) return;

    let ticking = false;
    window.addEventListener('scroll', function () {
      if (!ticking) {
        window.requestAnimationFrame(function () {
          if (window.scrollY > 360) {
            btn.classList.add('visible');
          } else {
            btn.classList.remove('visible');
          }
          ticking = false;
        });
        ticking = true;
      }
    }, { passive: true });

    btn.addEventListener('click', function () {
      // Ride the same easing as every other scroll on the page.
      if (scrollState.lenis) {
        scrollState.lenis.scrollTo(0, { duration: 1.2 });
        return;
      }
      window.scrollTo({
        top: 0,
        behavior: isReducedMotion ? 'auto' : 'smooth',
      });
    });
  }

// SPA-grade instant page transitions across GitHub Pages without full reloads
// (Note: Static multi-page routing is used to guarantee 100% reliable GitHub Pages navigation without 404 or DOM corruption)

  /* ==========================================================================
     8. Hero typewriter cursor
     The characters type themselves in CSS (see html.hero-intro .tw-char), so
     this function only has to walk the same clock and park the caret at the
     last character that has appeared. Nothing here gates the text itself.
     ========================================================================== */
  function initHeroTypewriter() {
    const docEl = document.documentElement;
    if (isReducedMotion || !docEl.classList.contains('hero-intro')) return;

    const h1 = document.querySelector('.hero-headline');
    if (!h1) return;
    const chars = Array.prototype.slice.call(h1.querySelectorAll('.tw-char'));
    if (!chars.length) return;

    const cursor = document.createElement('span');
    cursor.className = 'tw-cursor';
    cursor.setAttribute('aria-hidden', 'true');
    h1.appendChild(cursor);

    const cssStep = parseFloat(getComputedStyle(h1).getPropertyValue('--tw-step'));
    const step = cssStep > 0 ? cssStep : 22;
    const startAt = 120;
    const endAt = startAt + chars.length * step;

    let lastIndex = -2;
    let blinking = false;

    function place(index) {
      const char = chars[Math.max(0, Math.min(chars.length - 1, index))];
      const box = char.getBoundingClientRect();
      const head = h1.getBoundingClientRect();
      cursor.style.transform =
        'translate3d(' + (box.right - head.left).toFixed(1) + 'px, ' +
        (box.top - head.top).toFixed(1) + 'px, 0)';
    }

    const t0 = performance.now();
    cursor.classList.add('tw-cursor-on');
    place(0);

    function frame(now) {
      const elapsed = now - t0;
      const index = elapsed < startAt ? 0 : Math.floor((elapsed - startAt) / step);
      if (index !== lastIndex) {
        lastIndex = index;
        place(index);
      }
      if (elapsed < endAt + 380) {
        requestAnimationFrame(frame);
        return;
      }
      if (!blinking) {
        blinking = true;
        cursor.classList.add('tw-cursor-blink');
        setTimeout(function () { // DevSkim: ignore DS172411
          cursor.classList.remove('tw-cursor-on', 'tw-cursor-blink');
          setTimeout(function () {
            if (cursor.parentNode) cursor.parentNode.removeChild(cursor);
          }, 260);
        }, 520);
      }
    }
    requestAnimationFrame(frame);

    // A late web font swaps glyph widths underneath the caret; re-measure.
    if (document.fonts && document.fonts.ready) {
      document.fonts.ready.then(function () {
        lastIndex = -2;
        place(chars.length - 1);
      });
    }
  }

  /* ==========================================================================
     9. Capability icon band
     Two motions on two layers: the strip drifts against the page on scroll
     (one transform on the track), while each cell rides its own sine phase
     (one transform per cell). The loop stops as soon as the band leaves the
     viewport, so an idle band costs nothing.
     ========================================================================== */
  function initIconBand() {
    const track = document.querySelector('[data-icon-band]');
    if (!track) return;
    const band = track.closest('.icon-band') || track;
    const cells = Array.prototype.slice.call(track.children);
    if (!cells.length) return;

    const mobile = window.innerWidth < 768;
    const driftRange = mobile ? 120 : 250;

    function scrub() {
      const rect = band.getBoundingClientRect();
      const vh = window.innerHeight || 1;
      const total = vh + rect.height;
      const p = Math.min(1, Math.max(0, (vh - rect.top) / total));
      track.style.transform = 'translate3d(' + (-driftRange * p).toFixed(1) + 'px, 0, 0)';
    }

    if (isReducedMotion) {
      track.style.transform = 'none';
      return;
    }

    onScrollFrame(scrub);
    scrub();

    const amplitude = mobile ? 12 : 22;
    const period = 7000;
    let phase = 0;
    let last = 0;
    let rafId = null;
    let visible = false;

    function render(now) {
      const dt = last ? Math.min(64, now - last) : 16;
      last = now;
      phase = (phase + dt) % period;
      const t = (phase / period) * Math.PI * 2;
      for (let i = 0; i < cells.length; i++) {
        const offset = (i / cells.length) * Math.PI * 2;
        cells[i].style.transform =
          'translate3d(0, ' + (Math.sin(t + offset) * amplitude).toFixed(2) + 'px, 0)';
      }
      rafId = requestAnimationFrame(render);
    }

    function start() {
      if (rafId !== null) return;
      last = 0;
      rafId = requestAnimationFrame(render);
    }

    function stop() {
      if (rafId === null) return;
      cancelAnimationFrame(rafId);
      rafId = null;
    }

    if (!('IntersectionObserver' in window)) {
      start();
      return;
    }

    new IntersectionObserver(
      function (entries) {
        visible = entries[entries.length - 1].isIntersecting;
        if (visible) start();
        else stop();
      },
      { rootMargin: '120px 0px' }
    ).observe(band);
  }

  /* ==========================================================================
     10. Video stage
     The film grows out of the page as it arrives (0.5 -> 1 on desktop,
     0.92 -> 1 on phones) and never plays on its own: the mint pill over the
     poster is the only way in, so the page stays quiet until the visitor asks
     for the film. Native controls appear on that first click and own every
     state after it, so the pill retires for good.
     On a desktop pointer the pill also rides the cursor, the way Antigravity's
     play affordance does: it pops in under the hand instead of waiting to be
     found in a corner of the poster.
     ========================================================================== */
  function initVideoStage() {
    const stage = document.querySelector('[data-video-stage]');
    if (!stage) return;
    const section = stage.closest('.video-section') || stage;

    function scrub() {
      const rect = stage.getBoundingClientRect();
      const vh = window.innerHeight || 1;
      const mobile = window.innerWidth <= 768;
      const start = vh;
      const end = mobile ? vh * 0.2 : vh * 0.5;
      const p = Math.min(1, Math.max(0, (start - rect.top) / Math.max(1, start - end)));
      // power2.out, the curve Antigravity's scrub carries by default.
      const eased = 1 - Math.pow(1 - p, 2);
      const from = mobile ? 0.92 : 0.5;
      stage.style.transform = 'scale(' + (from + (1 - from) * eased).toFixed(4) + ')';
    }

    if (isReducedMotion) {
      // No scale: the film is simply present at its final size.
      stage.style.transform = 'none';
    } else {
      onScrollFrame(scrub);
      window.addEventListener('resize', scrub, { passive: true });
      scrub();
    }

    const shell = section.querySelector('[data-video-shell]');
    const video = shell && shell.querySelector('video');
    const button = stage.querySelector('[data-video-play]');
    if (!video || !button) return;

    let started = false;

    // ---- Pill rides the pointer (Antigravity's play affordance) ------------
    // The pill is planted on the frame and translated to the cursor, so the
    // label lands under the hand that is about to press it. The trail and the
    // pop are CSS transitions over --play-pill-x/y; this only measures.
    // A mouse can be plugged into a phone-sized window, and a hybrid device can
    // switch pointers mid-visit, so the mode is decided here and re-decided on
    // a media change instead of being assumed once.
    const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)');
    const wideWindow = window.matchMedia('(min-width: 641px)');

    function syncPill() {
      const armed = !started && !isReducedMotion && finePointer.matches && wideWindow.matches;
      stage.classList.toggle('is-pill-armed', armed);
      if (!armed) {
        stage.classList.remove('is-pill-live');
        stage.classList.remove('is-pill-tracking');
      }
    }

    // The nav island is fixed and paints above the frame (z-index 1000), and the
    // top of a tall frame is usually off screen while the film is in view. A
    // pill following the raw pointer would slide under the bar and be sliced in
    // half, so the tracked point is clamped to the band of frame the visitor can
    // actually see and reach: never outside the frame, never under the nav.
    function track(clientX, clientY) {
      if (started) return;
      const rect = stage.getBoundingClientRect();
      // The stage is scaled by the scroll scrub, so one viewport pixel is not
      // one local pixel: divide by the live scale or the pill drifts off the
      // cursor while the frame is still growing.
      const scale = rect.width > 0 ? rect.width / stage.offsetWidth : 1;
      const pad = 12;
      const halfW = (button.offsetWidth * scale) / 2;
      const halfH = (button.offsetHeight * scale) / 2;

      const chrome = document.querySelector('.nav-brand');
      const ceiling = Math.max(rect.top, (chrome ? chrome.getBoundingClientRect().bottom : 0) + pad);
      const floor = Math.min(rect.bottom, window.innerHeight || rect.bottom);

      // A band narrower than the pill (the visitor parked the frame under the
      // bar) takes its centre rather than an inverted clamp.
      function clamp(value, lo, hi) {
        return hi < lo ? (lo + hi) / 2 : Math.min(Math.max(value, lo), hi);
      }

      const x = clamp(clientX, rect.left + pad + halfW, rect.right - pad - halfW);
      const y = clamp(clientY, ceiling + pad + halfH, floor - pad - halfH);

      button.style.setProperty('--play-pill-x', ((x - rect.left) / scale).toFixed(1) + 'px');
      button.style.setProperty('--play-pill-y', ((y - rect.top) / scale).toFixed(1) + 'px');
      stage.classList.add('is-pill-live');
    }

    // Coming in from outside the frame, the pill has to appear where the hand
    // already is. Its position is written first and the travel transition is
    // held off until the next move, so it pops in place instead of sliding
    // across the poster from its parked rail - that slide read as a stray
    // second button. From the next move on it trails, which is the reference.
    stage.addEventListener('pointerenter', function (e) {
      stage.classList.remove('is-pill-tracking');
      track(e.clientX, e.clientY);
    }, { passive: true });

    stage.addEventListener('pointermove', function (e) {
      if (!stage.classList.contains('is-pill-live')) {
        stage.classList.remove('is-pill-tracking');
        track(e.clientX, e.clientY);
      } else {
        stage.classList.add('is-pill-tracking');
        track(e.clientX, e.clientY);
      }
    }, { passive: true });

    stage.addEventListener('pointerleave', function () {
      stage.classList.remove('is-pill-live');
      stage.classList.remove('is-pill-tracking');
    }, { passive: true });

    syncPill();
    [finePointer, wideWindow].forEach(function (query) {
      if (typeof query.addEventListener === 'function') query.addEventListener('change', syncPill);
      else if (typeof query.addListener === 'function') query.addListener(syncPill);
    });

    function start() {
      // Native transport only once the visitor has asked for the film, so the
      // idle poster stays a single, unambiguous affordance.
      if (!started) {
        started = true;
        stage.classList.add('is-started');
        video.controls = true;
        syncPill();
      }
      const attempt = video.play();
      if (attempt && typeof attempt.catch === 'function') {
        attempt.catch(function () {});
      }
    }

    button.addEventListener('click', start);

    // The poster is not a hit target of its own while the film is idle (see the
    // pointer-events rule on the frame), so the click that starts it is taken
    // here. Once the film is running the native transport owns the pointer and
    // this listener must not fight it - a click on the pause button must pause.
    shell.addEventListener('click', function () {
      if (!started) start();
    });
  }

  /* ==========================================================================
     11. Footer letterform scrub
     The wordmark rises 60px as the footer enters, one letter after the next.
     ========================================================================== */
  function initFooterWordmark() {
    const mark = document.querySelector('.footer-wordmark');
    if (!mark) return;
    const inner = mark.querySelector('span');
    if (!inner) return;

    // Split once so each letter can lag the one before it.
    const text = inner.textContent;
    inner.textContent = '';
    const letters = [];
    for (let i = 0; i < text.length; i++) {
      const span = document.createElement('span');
      span.className = 'fw-letter';
      span.textContent = text.charAt(i);
      inner.appendChild(span);
      letters.push(span);
    }
    mark.setAttribute('aria-hidden', 'true');

    if (isReducedMotion) return;

    let lastProgress = -1;

    function scrub() {
      const rect = mark.getBoundingClientRect();
      const vh = window.innerHeight || 1;
      const p = Math.min(1, Math.max(0, (vh - rect.top) / (vh * 0.62)));
      const rounded = Math.round(p * 100) / 100;
      if (rounded === lastProgress) return;
      lastProgress = rounded;
      mark.style.transform = 'translate3d(0, ' + (-60 * (1 - p)).toFixed(1) + 'px, 0)';
      for (let i = 0; i < letters.length; i++) {
        const lit = Math.min(1, Math.max(0, (p - i * 0.045) / 0.5));
        letters[i].style.opacity = (0.35 + 0.65 * lit).toFixed(3);
      }
    }

    onScrollFrame(scrub);
    scrub();
  }

  /* ==========================================================================
     12. Text written by the scroll
     Every prose block is split once into word spans. One scroll pass then writes
     a single property per block, --wr-p, and each word derives its own opacity
     from it (see .wr-word). A line is therefore written as the visitor scrolls
     and un-written as they scroll back: nothing is timed, the scroll is the
     clock, which is what makes it read as writing rather than as a one-shot
     fade. The hero is left out - it types itself on load (section 8).

     The split is layout-neutral: a word box lays out exactly like the word did.
     Three cases are left alone, all because a box of their own would join a
     layout that is not this text line: a word already inside a word span, text
     sitting directly inside a flex or grid container (such text nodes are
     anonymous items), and text inside a control that answers on hover instead
     of on scroll. Words stay inline boxes, so wrap points, text-wrap balancing
     and block heights do not move. Inline markup survives because only text
     nodes are split: links, <strong> and <em> keep working.

     Ownership is resolved per text node, from the node outwards, so a footer
     link or a badge inside a flex row is written with the block that holds it
     rather than being skipped for having an inline parent. Each element's
     display is resolved once and remembered, so the walk costs one computed
     style per element instead of one per text node.

     Classification is a read-only pass that runs to completion before anything
     is mutated, so no layout is forced mid-split. Splitting happens in slices:
     what sits near the viewport splits before the first paint, the rest in idle
     slices, and a block is split and painted in the same task so it is never
     shown whole and then dimmed. At scroll time every block's document position
     was already measured, so the pass is arithmetic plus the properties that
     actually moved - it never asks for a layout, and a block whose progress did
     not change is not written at all.
     ========================================================================== */
  const TEXT_SKIP_ELEMENTS = 'script, style, noscript, template, pre, code, kbd, samp, textarea, input, select, option, iframe, video, canvas, svg, math, header, nav';
  const TEXT_SKIP_SELECTOR = [
    '.hero',                 // the hero has its own intro
    '.wr-word',
    '.footer-wordmark',       // letterform scrub, see section 6
    '.sr-only',
    '.btn-pill',              // calls to action answer on hover/press instead
    '.cmd-pill',
    '.copy-btn',
    '[class*="doc-cli"]',     // terminal mockups are monospace pictures
    '[aria-hidden="true"]',
    '[data-no-scrub]',
  ].join(',');
  const TEXT_SKIP_COMBINED = TEXT_SKIP_ELEMENTS + ',' + TEXT_SKIP_SELECTOR;

  function initTextScrub() {
    if (isReducedMotion) return;

    const roots = document.querySelectorAll('main, footer');
    if (!roots.length) return;

    // Display is asked once per element and remembered: the walk below would
    // otherwise compute it again for every text node in a run.
    const displays = new Map();

    function displayOf(el) {
      let d = displays.get(el);
      if (d === undefined) {
        d = getComputedStyle(el).display;
        displays.set(el, d);
      }
      return d;
    }

    // Each element is classified once and remembered: whether it sits under a
    // skip, and which block owns the words it starts. Resolving this from the
    // element outwards (and memoising the answer for the parent) costs one
    // selector match per element rather than two per text node.
    const status = new Map();

    function statusOf(el) {
      let s = status.get(el);
      if (s) return s;
      const up = el.parentElement && el.parentElement !== document.body
        ? statusOf(el.parentElement)
        : { blocked: false, owner: null };
      const d = displayOf(el);
      s = {
        blocked: up.blocked || el.matches(TEXT_SKIP_COMBINED),
        owner: d !== 'inline' && d !== 'contents' ? el : up.owner,
      };
      status.set(el, s);
      return s;
    }

    // One entry per owning block, in document order, holding the text nodes it
    // writes. A node is claimed by exactly one owner, so nothing is split twice.
    const runs = new Map();

    for (let r = 0; r < roots.length; r++) {
      const root = roots[r];
      const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null);
      let node;
      while ((node = walker.nextNode())) {
        if (!node.nodeValue.trim()) continue;
        const parent = node.parentElement;
        if (!parent) continue;

        const s = statusOf(parent);
        if (s.blocked || !s.owner) continue;

        // A text node sitting directly inside a flex or grid container is an
        // anonymous item; a box per word would make each word an item instead.
        if (parent === s.owner) {
          const d = displayOf(s.owner);
          if (d.indexOf('flex') >= 0 || d.indexOf('grid') >= 0) continue;
        }

        const nodes = runs.get(s.owner);
        if (nodes) nodes.push(node);
        else runs.set(s.owner, [node]);
      }
    }

    if (!runs.size) return;

    function splitWords(node, index) {
      const parts = node.nodeValue.split(/(\s+)/);
      const frag = document.createDocumentFragment();
      for (let i = 0; i < parts.length; i++) {
        const part = parts[i];
        if (!part) continue;
        if (/^\s+$/.test(part)) {
          frag.appendChild(document.createTextNode(part));
          continue;
        }
        const word = document.createElement('span');
        word.className = 'wr-word';
        word.style.setProperty('--wr-i', String(index++));
        word.textContent = part;
        frag.appendChild(word);
      }
      node.parentNode.replaceChild(frag, node);
      return index;
    }

    const tracked = [];
    const next = [];
    let docHeight = 0;

    // Where each block sits in the document. Measured once, because writing the
    // scrub never changes layout: a block's position only moves when the page
    // reflows, which is a resize, a late font or a late image, and each of those
    // re-measures. With the position cached the scroll pass reads nothing, so it
    // never asks the browser to lay the page out again - the only work a frame
    // pays for is restyling the blocks whose progress actually moved.
    function measure() {
      const sy = window.scrollY || window.pageYOffset || 0;
      docHeight = document.documentElement.scrollHeight || 0;
      for (let i = 0; i < tracked.length; i++) {
        const rect = tracked[i].getBoundingClientRect();
        tracked[i].__wry = rect.top + sy;
        tracked[i].__wrh = rect.height;
      }
    }

    // How far a block has been written at a given scroll position. A block is
    // finished before it reaches reading height, so anything the visitor has
    // stopped to read is whole; only the strip entering from the bottom is
    // still being written. A tall paragraph is given more travel than a
    // heading, so the words arrive at a similar rate rather than in one gulp.
    function progressAt(top, height, scrolled, vh, tail) {
      const span = Math.min(Math.max(vh * 0.32, height * 1.1), vh * 0.9);
      let p = (vh * 0.96 - (top - scrolled)) / span;
      p = p < 0 ? 0 : p > 1 ? 1 : p;
      if (tail) p += (1 - p) * tail;
      // 500 steps keeps the number short enough that unchanged blocks compare
      // equal, and is under a third of a pixel of travel per step.
      return Math.round(p * 500) / 500;
    }

    // Splitting a few thousand words into boxes is not free, so it is done in
    // slices: whatever sits near the viewport is split before the first paint,
    // the rest in idle slices. A block is split and painted in the same task,
    // so it can never be shown fully written and then dimmed.
    const near = [];
    const far = [];
    const vh0 = window.innerHeight || 1;
    runs.forEach(function (nodes, el) {
      (el.getBoundingClientRect().top < vh0 * 1.5 ? near : far).push({ el: el, nodes: nodes });
    });
    const order = near.concat(far);
    let qi = 0;

    docHeight = document.documentElement.scrollHeight || 0;

    function splitRun(run) {
      let index = 0;
      for (let i = 0; i < run.nodes.length; i++) index = splitWords(run.nodes[i], index);
      if (!index) return false;
      run.el.style.setProperty('--wr-n', String(index));
      tracked.push(run.el);
      return true;
    }

    // Paint freshly split blocks, and remember where each of them sits while
    // the rects are already in hand: every rect is read before any property is
    // written, so the slice pays for one layout instead of one per block.
    function paint(list) {
      const vh = window.innerHeight || 1;
      const sy = window.scrollY || window.pageYOffset || 0;
      const values = [];
      for (let i = 0; i < list.length; i++) {
        const rect = list[i].getBoundingClientRect();
        list[i].__wry = rect.top + sy;
        list[i].__wrh = rect.height;
        values[i] = progressAt(rect.top, rect.height, sy, vh, 0);
      }
      for (let i = 0; i < list.length; i++) {
        list[i].__wrp = values[i];
        list[i].style.setProperty('--wr-p', String(values[i]));
      }
    }

    const schedule = window.requestIdleCallback
      ? function () { window.requestIdleCallback(splitSlice, { timeout: 200 }); }
      : function () { window.setTimeout(splitSlice, 16); };

    function splitSlice() {
      const started = performance.now();
      const fresh = [];
      while (qi < order.length) {
        if (splitRun(order[qi++])) fresh.push(order[qi - 1].el);
        if (performance.now() - started > 4) break;
      }
      if (fresh.length) paint(fresh);
      if (qi < order.length) schedule();
    }

    splitSlice();

    function scrub(y) {
      const vh = window.innerHeight || 1;
      const scrolled = typeof y === 'number' ? y : (window.scrollY || window.pageYOffset || 0);
      const maxY = Math.max(0, docHeight - vh);

      // The end of the document has no scroll left to write with. Rather than
      // stranding the footer's words half-lit, the last third of a viewport
      // finishes whatever is still open.
      let tail = (scrolled - (maxY - vh * 0.35)) / (vh * 0.35);
      tail = tail < 0 ? 0 : tail > 1 ? 1 : tail;

      // Pure arithmetic: the positions were measured once, so this pass reads
      // nothing from the document and never forces a layout.
      for (let i = 0; i < tracked.length; i++) {
        next[i] = progressAt(tracked[i].__wry, tracked[i].__wrh, scrolled, vh, tail);
      }

      // Write pass. The last value written lives on the element, so a block
      // painted when it was split is not written again for the same value.
      for (let i = 0; i < tracked.length; i++) {
        if (next[i] === tracked[i].__wrp) continue;
        tracked[i].__wrp = next[i];
        tracked[i].style.setProperty('--wr-p', String(next[i]));
      }
    }

    function remeasure() {
      measure();
      scrub();
    }

    onScrollFrame(scrub);
    window.addEventListener('resize', remeasure, { passive: true });
    window.addEventListener('load', remeasure);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(remeasure);
    scrub();
  }

  function initializeAll() {
    initSmoothScroll();
    initScrollReveals();
    initInteractiveBackground();
    initCardInteractivity();
    initServiceWorker();
    initBackToTop();
    initHeroTypewriter();
    initIconBand();
    initVideoStage();
    initFooterWordmark();
    initTextScrub();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAll);
  } else {
    initializeAll();
  }
})();
