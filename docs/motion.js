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
    let wasScrolled = null;

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
      const scrolled = y > 6;
      if (scrolled !== wasScrolled) {
        wasScrolled = scrolled;
        docEl.classList.toggle('is-scrolled', scrolled);
      }
    }

    // The bar's Download CTA is held back while the hero owns the screen, then
    // springs into the bar once the hero has scrolled past the header. Only
    // pages with a hero opt in, so sub-pages keep the CTA from the first frame.
    // rootMargin pulls the observation line down to the header's bottom edge,
    // so the swap happens the moment the hero clears the bar instead of when it
    // leaves the viewport entirely.
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

    // Reduced motion keeps the native, instant scroll — no hijacking, no easing.
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
      sprites.streak = makeStreak('226, 240, 255');
      sprites.streakMint = makeStreak('140, 255, 222');
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
      }
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

      for (let i = 0; i < stars.length; i++) {
        const s = stars[i];

        // Parallax: near stars travel further, which is what makes the field
        // feel like space instead of a wallpaper. The near layer is fast enough
        // that a normal scroll smears it into a visible shooting star.
        const parallax = 0.09 + s.z * 0.24;
        const my = -scrollDelta * parallax;

        s.x += s.driftX * dt + s.vx * dt;
        s.y += s.driftY * dt + s.vy * dt;
        s.vx *= Math.pow(0.9, dt);
        s.vy *= Math.pow(0.9, dt);
        s.y += my;

        // Wrap: a star that leaves the top re-enters from the bottom, keeping
        // the sky density constant through any amount of scrolling.
        if (s.x < -20) s.x = width + 20;
        else if (s.x > width + 20) s.x = -20;
        if (s.y < -20) s.y = height + 20;
        else if (s.y > height + 20) s.y = -20;

        // Pointer: part the field gently and brighten what is close. The
        // pointer never draws a tail — streaks belong to scroll and meteors.
        let hotTarget = 0;
        if (pointer.active) {
          const dx = s.x - pointer.x;
          const dy = s.y - pointer.y;
          const distSq = dx * dx + dy * dy;
          if (distSq < wakeRadiusSq) {
            const dist = Math.sqrt(distSq) || 1;
            const near = 1 - dist / wakeRadius;
            hotTarget = near * near * 1.15;
            const push = near * near * (isReducedMotion ? 0 : 0.5) * s.z;
            s.vx += (dx / dist) * push;
            s.vy += (dy / dist) * push;
          }
        }

        // Click pulses light the ring as it passes.
        for (let k = 0; k < pulses.length; k++) {
          const p = pulses[k];
          const dx = s.x - p.x;
          const dy = s.y - p.y;
          const band = Math.abs(Math.sqrt(dx * dx + dy * dy) - p.r);
          if (band < 30) hotTarget += (1 - band / 30) * p.alpha * 0.9;
        }

        s.hot += (hotTarget - s.hot) * Math.min(1, 0.18 * dt);

        // Streak: length is how far the star actually moved this frame, which
        // only scroll parallax can produce. The gain is deliberately generous:
        // a normal wheel notch has to read as a shooting star, not as a 2px
        // twitch, while a full-page jump still tops out at a long tail.
        const motion = Math.abs(my);
        if (motion > 0.05) s.dirY = my > 0 ? 1 : -1;
        const targetStreak = Math.min(motion * 10, 300);
        s.streak += (targetStreak - s.streak) * Math.min(1, 0.5 * dt);

        const twinkle = 0.72 + 0.28 * Math.sin(s.twinklePhase);
        s.twinklePhase += s.twinkleSpeed * dt * 6;

        let a = s.alpha * twinkle * focusFade(s.x, s.y);
        a *= 1 + s.hot * 1.4;
        if (a <= 0.004) continue;
        if (a > 1) a = 1;

        const sprite = s.mint ? mintSprite : skySprite;
        const rgb = s.mint ? mintRGB : skyRGB;
        const streakSprite = s.mint ? sprites.streakMint : sprites.streak;

        // Shooting-star tail. Drawn as soon as a star starts moving, so a gentle
        // scroll still smears it; the length is capped so a page jump does not
        // turn the whole sky into one line.
        if (s.streak > 1.5) {
          const len = Math.min(s.streak * 2.6, 420);
          const wid = Math.max(2.4, s.r * 6);
          ctx.globalAlpha = Math.min(1, a * 0.9);
          // Scroll only ever moves stars straight up or down, so the tail needs
          // no rotation — just the flip that keeps it behind the head.
          if (s.dirY < 0) {
            ctx.save();
            ctx.translate(s.x, s.y);
            ctx.scale(1, -1);
            ctx.drawImage(streakSprite, -wid / 2, -len, wid, len);
            ctx.restore();
          } else {
            ctx.drawImage(streakSprite, s.x - wid / 2, s.y - len, wid, len);
          }
          ctx.globalAlpha = 1;
        }

        // Star head: soft halo, then a crisp core so the point reads as light.
        const size = (s.streak > 6 ? 4.2 : 5.4) + s.r * 6.2;
        ctx.globalAlpha = a;
        ctx.drawImage(sprite, s.x - size / 2, s.y - size / 2, size, size);
        ctx.globalAlpha = Math.min(1, a * 1.05);
        ctx.beginPath();
        ctx.arc(s.x, s.y, Math.max(0.45, s.r * 0.5), 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(' + rgb + ', 1)';
        ctx.fill();
        ctx.globalAlpha = 1;

        // A few bright stars get the four-point diffraction cross that makes a
        // point of light read as a star rather than a pixel.
        if (s.sparkle && s.streak < 8 && a > 0.35) {
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

    resize();
    if (!isReducedMotion) render(performance.now());

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
      // Resolve against motion.js itself, so every page — including the ones
      // nested a level deep — lands on the single shared worker regardless of
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

  /* ==========================================================================
     6. Global Responsive Mobile Menu Engine
     ========================================================================== */
  function initMobileMenu() {
    const mobileToggle = document.getElementById('mobileToggle');
    const mobileMenu = document.getElementById('mobileMenu');
    if (!mobileToggle || !mobileMenu) return;

    function toggleMobileMenu(open) {
      if (open) {
        mobileMenu.classList.add('open');
        mobileMenu.setAttribute('aria-hidden', 'false');
        mobileToggle.setAttribute('aria-expanded', 'true');
        mobileToggle.setAttribute('aria-label', 'Close Navigation Menu');
        document.body.style.overflow = 'hidden';
        // Freeze the inertial scroller too, or the page keeps gliding behind the sheet.
        if (scrollState.lenis) scrollState.lenis.stop();
      } else {
        mobileMenu.classList.remove('open');
        mobileMenu.setAttribute('aria-hidden', 'true');
        mobileToggle.setAttribute('aria-expanded', 'false');
        mobileToggle.setAttribute('aria-label', 'Open Navigation Menu');
        document.body.style.overflow = '';
        if (scrollState.lenis) scrollState.lenis.start();
      }
    }

    mobileToggle.addEventListener('click', function () {
      const isOpen = mobileMenu.classList.contains('open');
      toggleMobileMenu(!isOpen);
    });

    document.querySelectorAll('.mobile-nav-link').forEach(function (link) {
      link.addEventListener('click', function () {
        toggleMobileMenu(false);
      });
    });

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape' && mobileMenu.classList.contains('open')) {
        toggleMobileMenu(false);
        mobileToggle.focus();
      }
    });

    window.addEventListener('resize', function () {
      if (window.innerWidth > 960 && mobileMenu.classList.contains('open')) {
        toggleMobileMenu(false);
      }
    });
  }

  /* ==========================================================================
     7. Active Navigation Scroll Spy
     ========================================================================== */
  function initScrollSpy() {
    const navLinks = document.querySelectorAll('.nav-links .nav-link');
    if (!navLinks.length) return;

    const tracked = [];
    navLinks.forEach(function (link) {
      const href = link.getAttribute('href');
      if (href && href.startsWith('#') && href.length > 1) {
        const target = document.getElementById(href.substring(1));
        if (target) {
          tracked.push({ link: link, target: target });
        }
      }
    });

    if (!tracked.length) return;

    function updateActiveNav() {
      const scrollPos = window.scrollY + 160;
      let currentActive = null;

      for (let i = 0; i < tracked.length; i++) {
        const top = tracked[i].target.offsetTop;
        const height = tracked[i].target.offsetHeight;
        if (scrollPos >= top && scrollPos < top + height) {
          currentActive = tracked[i].link;
          break;
        }
      }

      tracked.forEach(function (item) {
        if (item.link === currentActive) {
          item.link.classList.add('active');
        } else {
          item.link.classList.remove('active');
        }
      });
    }

    window.addEventListener('scroll', updateActiveNav, { passive: true });
    if (typeof requestIdleCallback === 'function') {
      requestIdleCallback(updateActiveNav);
    } else {
      setTimeout(updateActiveNav, 100);
    }
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
      if (elapsed < endAt + 1700) {
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
        }, 1400);
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

      const nav = document.querySelector('.nav-island');
      const ceiling = Math.max(rect.top, (nav ? nav.getBoundingClientRect().bottom : 0) + pad);
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
     12. Word reveal, written by the scroll
     A plain-text heading is split into words once. The scroll position then
     writes two properties on it: --wr-p, its own progress through the viewport,
     and --wr-n, how many words it holds. Each word derives its opacity from its
     index (see .wr-word), so the line is written as the visitor scrolls down and
     un-written as they scroll back up. No timer, no observer: the reveal is the
     scroll itself, which is what makes it read as writing rather than as a
     one-shot fade. Headings with inline markup are left alone so links and
     emphasis survive.
     ========================================================================== */
  function initWordReveal() {
    const headings = document.querySelectorAll('.section-title, .section-subtitle');
    if (!headings.length) return;

    const tracked = [];

    headings.forEach(function (el) {
      if (el.children.length) return;
      const text = el.textContent;
      if (!text || !text.trim()) return;

      const parts = text.split(/(\s+)/);
      el.textContent = '';
      let index = 0;
      for (let i = 0; i < parts.length; i++) {
        const part = parts[i];
        if (!part) continue;
        if (/^\s+$/.test(part)) {
          el.appendChild(document.createTextNode(part));
          continue;
        }
        const word = document.createElement('span');
        word.className = 'wr-word';
        word.style.setProperty('--wr-i', String(index++));
        word.textContent = part;
        el.appendChild(word);
      }
      if (!index) return;

      el.style.setProperty('--wr-n', String(index));

      // A heading that cannot be scrubbed is simply already written: the words
      // must never be the reason the text is unreadable.
      if (isReducedMotion) {
        el.classList.add('no-word-reveal');
        return;
      }
      tracked.push(el);
    });

    if (!tracked.length) return;

    function scrub() {
      const vh = window.innerHeight || 1;
      // Written across the last stretch of the approach: by the time the heading
      // settles at reading height, every word is present.
      const start = vh * 0.94;
      const span = vh * 0.55;

      for (let i = 0; i < tracked.length; i++) {
        const el = tracked[i];
        const rect = el.getBoundingClientRect();
        // Parked well outside the viewport: keep its last value, skip the write.
        if (rect.bottom < -vh * 0.15 || rect.top > vh * 1.15) continue;
        const p = Math.min(1, Math.max(0, (start - rect.top) / span));
        el.style.setProperty('--wr-p', p.toFixed(4));
      }
    }

    onScrollFrame(scrub);
    window.addEventListener('resize', scrub, { passive: true });
    scrub();
  }

  function initializeAll() {
    initSmoothScroll();
    initScrollReveals();
    initInteractiveBackground();
    initCardInteractivity();
    initServiceWorker();
    initBackToTop();
    initMobileMenu();
    initScrollSpy();
    initHeroTypewriter();
    initIconBand();
    initVideoStage();
    initFooterWordmark();
    initWordReveal();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAll);
  } else {
    initializeAll();
  }
})();
