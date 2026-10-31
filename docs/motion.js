/**
 * Deltempo Cybernetic Motion Engine v3.0.0
 * High-performance interactive visual systems:
 * - Lenis 1.3 inertial scrolling with a scroll progress rail, glass-nav state and
 *   scroll-driven background parallax (native scroll when reduced motion is on)
 * - Deep-space starfield: depth-layered stars that streak into shooting stars as
 *   the page scrolls, part and brighten around the cursor, flare on click, and
 *   idle meteors crossing the sky
 * - Three-layer spring cursor (core dot, elastic ring, lagging halo) with magnetic
 *   snap, drag affordance, text caret and kinetic click ripples
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
     2. Inertial Smooth Scrolling (Lenis 1.3) + scroll-driven chrome
     ========================================================================== */
  // Shared, read-only snapshot of the scroll position for the background field.
  const scrollState = {
    y: 0,
    velocity: 0,
    progress: 0,
    lenis: null,
  };

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
     3. IntersectionObserver Scroll Reveal System
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
     4. Deep-space starfield: scroll streaks, pointer wake, idle shooting stars
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
      vx: 0,
      vy: 0,
      speed: 0,
      active: false,
      radius: 190,
    };

    // Click pulses: a single expanding ring that lights the stars it passes.
    const pulses = [];

    // Scroll motion, smoothed so a flick produces one long streak instead of a
    // row of disconnected dashes.
    let lastScrollY = window.scrollY || 0;
    let scrollMomentum = 0;

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
      sprites.dust = makeSprite('30, 41, 59');
      sprites.dustMint = makeSprite('4, 120, 87');
      sprites.streak = makeStreak('226, 240, 255');
      sprites.streakMint = makeStreak('140, 255, 222');
      sprites.streakDust = makeStreak('71, 85, 105');
      sprites.streakDustMint = makeStreak('4, 120, 87');
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
      star.dirX = 0;
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
      const x = e.clientX;
      const y = e.clientY;
      if (pointer.x > -5000) {
        pointer.vx = pointer.vx * 0.4 + (x - pointer.x) * 0.6;
        pointer.vy = pointer.vy * 0.4 + (y - pointer.y) * 0.6;
        pointer.speed = Math.sqrt(pointer.vx * pointer.vx + pointer.vy * pointer.vy);
      }
      pointer.x = x;
      pointer.y = y;
      pointer.active = true;
    }

    if (!isReducedMotion && isFinePointer.matches) {
      window.addEventListener('pointermove', onPointerMove, { passive: true });
      document.addEventListener('mouseleave', function () {
        pointer.active = false;
        pointer.x = -9999;
        pointer.y = -9999;
        pointer.vx = 0;
        pointer.vy = 0;
        pointer.speed = 0;
      });
      window.addEventListener('pointerdown', function (e) {
        if (e.button !== 0) return;
        pulses.push({ x: e.clientX, y: e.clientY, r: 0, alpha: 0.9 });
        if (pulses.length > 3) pulses.shift();
      }, { passive: true });
    }

    // --- Idle shooting stars ------------------------------------------------
    function spawnMeteor() {
      // Enter from the upper half at a shallow angle, so the streak crosses the
      // sky rather than dropping straight down like a scratch.
      const fromLeft = Math.random() < 0.55;
      const angle = (Math.PI / 180) * (fromLeft ? 24 + Math.random() * 16 : 140 + Math.random() * 16);
      const speed = 10 + Math.random() * 7;
      meteors.push({
        x: fromLeft ? -60 - Math.random() * width * 0.15 : width + 60 + Math.random() * width * 0.15,
        y: Math.random() * height * 0.45,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        life: 0,
        maxLife: 80 + Math.floor(Math.random() * 60),
        len: 150 + Math.random() * 130,
        width: 1 + Math.random() * 0.8,
      });
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

      const isLight = document.documentElement.classList.contains('light');
      const skySprite = isLight ? sprites.dust : sprites.star;
      const mintSprite = isLight ? sprites.dustMint : sprites.mint;
      const skyRGB = isLight ? '30, 41, 59' : '236, 248, 255';
      const mintRGB = isLight ? '4, 120, 87' : '120, 255, 214';
      const lightScale = isLight ? 0.42 : 1;

      // Scroll: stars physically slide against the page, and the distance they
      // travel this frame becomes the length of their streak.
      const y = scrollState.y;
      const scrollDelta = y - lastScrollY;
      lastScrollY = y;
      scrollMomentum += (scrollDelta - scrollMomentum) * Math.min(1, 0.3 * dt);

      // Pointer decay (velocity is per-frame, so it is frame-rate scaled).
      pointer.vx *= Math.pow(0.86, dt);
      pointer.vy *= Math.pow(0.86, dt);
      pointer.speed *= Math.pow(0.86, dt);

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
        ctx.strokeStyle = 'rgba(' + (isLight ? '5, 150, 105' : '0, 242, 176') + ', ' + (p.alpha * 0.35).toFixed(3) + ')';
        ctx.lineWidth = 1;
        ctx.stroke();
      }

      // 2. Stars.
      const wakeRadius = pointer.radius;
      const wakeRadiusSq = wakeRadius * wakeRadius;
      const wakeActive = pointer.active && pointer.speed > 0.6 && !isReducedMotion;

      for (let i = 0; i < stars.length; i++) {
        const s = stars[i];

        // Parallax: near stars travel further, which is what makes the field
        // feel like space instead of a wallpaper.
        const parallax = 0.055 + s.z * 0.16;
        let mx = 0;
        let my = -scrollDelta * parallax;

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

        // Pointer: part the field gently, brighten what is close, and drag a
        // wake behind a fast flick.
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
            if (wakeActive) {
              const wake = near * Math.min(pointer.speed, 26) * 0.14;
              mx += pointer.vx * wake;
              my += pointer.vy * wake;
            }
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

        // Streak: length is how far the star actually moved this frame.
        const motion = Math.sqrt(mx * mx + my * my);
        if (motion > 0.05) {
          s.dirX = mx / motion;
          s.dirY = my / motion;
        }
        const targetStreak = Math.min(motion * 3.4, 150);
        s.streak += (targetStreak - s.streak) * Math.min(1, 0.28 * dt);

        const twinkle = 0.72 + 0.28 * Math.sin(s.twinklePhase);
        s.twinklePhase += s.twinkleSpeed * dt * 6;

        let a = s.alpha * twinkle * focusFade(s.x, s.y) * lightScale;
        a *= 1 + s.hot * 1.4;
        if (a <= 0.004) continue;
        if (a > 1) a = 1;

        const sprite = s.mint ? mintSprite : skySprite;
        const rgb = s.mint ? mintRGB : skyRGB;
        const streakSprite = isLight
          ? (s.mint ? sprites.streakDustMint : sprites.streakDust)
          : (s.mint ? sprites.streakMint : sprites.streak);

        // Meteor tail. Only stars that actually travelled a visible distance get
        // one: short dashes everywhere read as drizzle, not as a sky moving.
        if (s.streak > 4) {
          const len = Math.min(s.streak * 2.2, 260);
          const wid = Math.max(2.4, s.r * 6);
          ctx.globalAlpha = Math.min(1, a * 0.9);
          if (s.dirX > -0.06 && s.dirX < 0.06) {
            // Straight up or down (scroll and most wakes): no rotation, but the
            // tail still has to trail behind the head in the direction of travel.
            if (s.dirY < 0) {
              ctx.save();
              ctx.translate(s.x, s.y);
              ctx.scale(1, -1);
              ctx.drawImage(streakSprite, -wid / 2, -len, wid, len);
              ctx.restore();
            } else {
              ctx.drawImage(streakSprite, s.x - wid / 2, s.y - len, wid, len);
            }
          } else {
            ctx.save();
            ctx.translate(s.x, s.y);
            ctx.rotate(Math.atan2(s.dirY, s.dirX) - Math.PI / 2);
            ctx.drawImage(streakSprite, -wid / 2, -len, wid, len);
            ctx.restore();
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
          // Light theme gets a whisper only: a bright meteor on white paper
          // reads as a scratch.
          const fade = Math.sin(Math.PI * t) * (isLight ? 0.28 : 1);
          const speed = Math.sqrt(m.vx * m.vx + m.vy * m.vy) || 1;
          const dx = m.vx / speed;
          const dy = m.vy / speed;

          // Tapered tail from the shared sprite, rotated onto the flight path.
          const tailSprite = isLight ? sprites.streakDust : sprites.streakMint;
          const tailLen = m.len * (0.6 + 0.4 * fade);
          const tailWid = 5 + m.width * 5;
          ctx.globalAlpha = Math.min(1, fade * 0.9);
          ctx.save();
          ctx.translate(m.x, m.y);
          ctx.rotate(Math.atan2(dy, dx) - Math.PI / 2);
          ctx.drawImage(tailSprite, -tailWid / 2, -tailLen, tailWid, tailLen);
          ctx.restore();

          const headSize = 20 + fade * 14;
          ctx.globalAlpha = Math.min(1, fade * 0.95);
          ctx.drawImage(skySprite, m.x - headSize / 2, m.y - headSize / 2, headSize, headSize);
          ctx.globalAlpha = 1;
        }
      }

      // 4. Pointer bloom: a single soft light so the sky reacts to the cursor
      // even between stars.
      if (pointer.active && !isReducedMotion) {
        const tint = isLight ? '5, 150, 105' : '0, 242, 176';
        const bloom = ctx.createRadialGradient(pointer.x, pointer.y, 0, pointer.x, pointer.y, 150);
        bloom.addColorStop(0, 'rgba(' + tint + ', ' + (isLight ? 0.05 : 0.07) + ')');
        bloom.addColorStop(0.45, 'rgba(' + tint + ', ' + (isLight ? 0.02 : 0.03) + ')');
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
     5. Card Reactive Surface Lighting & Micro 3D Tilt
     ========================================================================== */
  function initCardInteractivity() {
    if (!isFinePointer.matches) return;
    const cards = document.querySelectorAll(
      '.double-bezel, .card-inner, .feature-card, .cap-item, .hero-oneshot, .showcase-wrapper, .arch-card, .dl-card, .faq-item, .scope-card, .tier-card, .doc-code-card, .download-card, .trust-banner'
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
              card.classList.contains('showcase-wrapper') ||
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
     6. Three-layer spring cursor: core dot, elastic ring, trailing halo
     ========================================================================== */
  function initPrecisionCursor() {
    // A custom cursor is itself motion: reduced-motion users keep the OS pointer.
    if (!isFinePointer.matches || isReducedMotion) return;

    const docEl = document.documentElement;
    docEl.classList.add('has-custom-cursor');
    document.body.classList.add('has-custom-cursor');

    function ensure(className) {
      let el = document.querySelector('.' + className);
      if (!el) {
        el = document.createElement('div');
        el.className = className;
        el.setAttribute('aria-hidden', 'true');
        document.body.appendChild(el);
      }
      return el;
    }

    const dot = ensure('precision-cursor-dot');
    const ring = ensure('precision-cursor-ring');
    const halo = ensure('precision-cursor-halo');

    let pointerX = -300;
    let pointerY = -300;
    let isVisible = false;
    let magneticEl = null;
    let lastTime = 0;

    // Ring is snappy (slight overshoot), halo trails far behind for depth.
    const ringSpring = { x: -300, y: -300, vx: 0, vy: 0 };
    const haloSpring = { x: -300, y: -300, vx: 0, vy: 0 };

    let currentAngle = 0;
    let currentStretch = 1;
    let currentCompress = 1;
    // The halo lags the ring, so its own velocity vector is a truer reading of
    // where the pointer has been. Stretching it along that vector turns the
    // trail into a comet instead of a uniformly growing blob.
    let haloAngle = 0;
    let currentHaloStretch = 1;

    // Semi-implicit Euler spring: stable for these stiffness values at 60-144Hz.
    function step(spring, tx, ty, stiffness, damping, dt) {
      spring.vx += (tx - spring.x) * stiffness * dt;
      spring.vy += (ty - spring.y) * stiffness * dt;
      spring.vx -= spring.vx * damping * dt;
      spring.vy -= spring.vy * damping * dt;
      spring.x += spring.vx * dt;
      spring.y += spring.vy * dt;
    }

    function show() {
      if (isVisible) return;
      isVisible = true;
      ringSpring.x = pointerX;
      ringSpring.y = pointerY;
      ringSpring.vx = 0;
      ringSpring.vy = 0;
      haloSpring.x = pointerX;
      haloSpring.y = pointerY;
      haloSpring.vx = 0;
      haloSpring.vy = 0;
      // A class, not an inline style: an inline opacity would outrank every
      // state rule below (hover recedes the halo to 0.45, invert to 0.55).
      dot.classList.add('cursor-visible');
      ring.classList.add('cursor-visible');
      halo.classList.add('cursor-visible');
    }

    function hide() {
      isVisible = false;
      dot.classList.remove('cursor-visible');
      ring.classList.remove('cursor-visible');
      halo.classList.remove('cursor-visible');
      if (magneticEl) {
        magneticEl.style.transform = '';
        magneticEl = null;
      }
    }

    window.addEventListener(
      'pointermove',
      function (e) {
        pointerX = e.clientX;
        pointerY = e.clientY;
        if (!isVisible) show();
        // The core dot is never interpolated: it is the true pointer position.
        dot.style.transform =
          'translate3d(' + pointerX + 'px, ' + pointerY + 'px, 0) translate(-50%, -50%)';
      },
      { passive: true }
    );

    document.addEventListener('mouseleave', hide);
    document.addEventListener('mouseenter', function (e) {
      pointerX = e.clientX;
      pointerY = e.clientY;
      show();
    });
    window.addEventListener('blur', hide);

    // One delegated pass decides every state, so classes can never go stale.
    const interactiveSelector =
      'a, button, input, textarea, select, [role="button"], [role="tab"], .btn-pill, .btn-icon, .hero-oneshot, .card-inner, .faq-item, .mobile-nav-link, .nav-link, summary, .calc-profile-btn, .cmd-pill';
    const magneticSelector = '.btn-pill-primary, .hero-oneshot, #themeToggleBtn, .btn-icon, .dl-btn-primary';
    const dragSelector = '[data-cursor="drag"], .comp-table-wrap';
    // Solid mint surfaces swallow a mint cursor: the dot and ring need to
    // invert to near-black there or they simply disappear on the CTA.
    const accentSurfaceSelector = '.btn-pill-primary, .nav-download-btn, .pane-btn-primary';

    document.addEventListener(
      'pointerover',
      function (e) {
        const t = e.target;
        if (!(t instanceof Element)) return;

        const dragTarget = t.closest(dragSelector);
        const interactive = t.closest(interactiveSelector);
        const magnetic = t.closest(magneticSelector);
        const textField = t.closest('input, textarea');
        const accentSurface = t.closest(accentSurfaceSelector);

        ring.classList.toggle('cursor-drag', !!dragTarget && !magnetic);
        dot.classList.toggle('cursor-drag', !!dragTarget && !magnetic);
        ring.classList.toggle('cursor-hover', !!interactive);
        dot.classList.toggle('cursor-hover', !!interactive);
        halo.classList.toggle('cursor-hover', !!interactive);
        ring.classList.toggle('cursor-invert', !!accentSurface);
        dot.classList.toggle('cursor-invert', !!accentSurface);
        halo.classList.toggle('cursor-invert', !!accentSurface);
        ring.classList.toggle('cursor-text', !!textField);
        dot.classList.toggle('cursor-text', !!textField);
        ring.classList.toggle('cursor-magnetic', !!magnetic);

        if (magnetic !== magneticEl) {
          if (magneticEl) magneticEl.style.transform = '';
          magneticEl = magnetic || null;
        }
      },
      { passive: true }
    );

    window.addEventListener(
      'pointerdown',
      function (e) {
        dot.classList.add('cursor-active');
        ring.classList.add('cursor-active');

        const pulse = document.createElement('div');
        pulse.className = 'cursor-click-pulse';
        pulse.style.left = e.clientX + 'px';
        pulse.style.top = e.clientY + 'px';
        document.body.appendChild(pulse);

        // Second, tighter ring: two waves read as a deliberate "press" rather
        // than a single ambient blip.
        const inner = document.createElement('div');
        inner.className = 'cursor-click-pulse pulse-inner';
        inner.style.left = e.clientX + 'px';
        inner.style.top = e.clientY + 'px';
        document.body.appendChild(inner);

        setTimeout(function () { // DevSkim: ignore DS172411
          if (pulse.parentNode) pulse.parentNode.removeChild(pulse);
          if (inner.parentNode) inner.parentNode.removeChild(inner);
        }, 620);
      },
      { passive: true }
    );

    window.addEventListener(
      'pointerup',
      function () {
        dot.classList.remove('cursor-active');
        ring.classList.remove('cursor-active');
      },
      { passive: true }
    );

    function renderLoop(time) {
      const dt = lastTime ? Math.min(0.05, (time - lastTime) / 1000) : 0.016;
      lastTime = time;

      if (isVisible) {
        let targetX = pointerX;
        let targetY = pointerY;

        // Magnetic pull toward the centre of primary actions.
        if (magneticEl && magneticEl.isConnected) {
          const rect = magneticEl.getBoundingClientRect();
          const cx = rect.left + rect.width / 2;
          const cy = rect.top + rect.height / 2;

          targetX = pointerX * 0.45 + cx * 0.55;
          targetY = pointerY * 0.45 + cy * 0.55;

          const maxTranslate = 3.5;
          const tx = Math.max(-maxTranslate, Math.min(maxTranslate, (pointerX - cx) * 0.14));
          const ty = Math.max(-maxTranslate, Math.min(maxTranslate, (pointerY - cy) * 0.14));
          magneticEl.style.transform =
            'translate3d(' + tx.toFixed(2) + 'px, ' + ty.toFixed(2) + 'px, 0)';
        } else if (magneticEl) {
          magneticEl = null;
        }

        step(ringSpring, targetX, targetY, 460, 32, dt);
        step(haloSpring, targetX, targetY, 120, 24, dt);

        const speed = Math.sqrt(ringSpring.vx * ringSpring.vx + ringSpring.vy * ringSpring.vy);
        // The pill has fixed chevrons; stretching or rotating it would tilt them.
        const isDrag = ring.classList.contains('cursor-drag');

        if (speed > 40 && !magneticEl && !isDrag) {
          currentAngle = Math.atan2(ringSpring.vy, ringSpring.vx) * (180 / Math.PI);
        }

        // No spark particles: the comet halo already carries the motion, and
        // random spawns read as noise rather than feedback.

        // Velocity-based aerodynamic stretch along the direction of travel.
        const targetStretch = magneticEl || isDrag ? 1 : 1 + Math.min(speed * 0.00045, 0.34);
        const targetCompress = magneticEl || isDrag ? 1 : 1 - Math.min(speed * 0.00028, 0.16);
        const ease = Math.min(1, dt * 16);
        currentStretch += (targetStretch - currentStretch) * ease;
        currentCompress += (targetCompress - currentCompress) * ease;

        ring.style.transform =
          'translate3d(' + ringSpring.x.toFixed(2) + 'px, ' + ringSpring.y.toFixed(2) + 'px, 0) ' +
          'translate(-50%, -50%) ' +
          'rotate(' + currentAngle.toFixed(1) + 'deg) ' +
          'scale(' + currentStretch.toFixed(3) + ', ' + currentCompress.toFixed(3) + ')';

        const haloScale = 1 + Math.min(speed * 0.0005, 0.35);
        const haloVelocity = Math.sqrt(haloSpring.vx * haloSpring.vx + haloSpring.vy * haloSpring.vy);
        if (haloVelocity > 60 && !isDrag) {
          haloAngle = Math.atan2(haloSpring.vy, haloSpring.vx) * (180 / Math.PI);
        }
        const targetHaloStretch = isDrag ? 1 : 1 + Math.min(haloVelocity * 0.00034, 0.26);
        currentHaloStretch += (targetHaloStretch - currentHaloStretch) * ease;
        halo.style.transform =
          'translate3d(' + haloSpring.x.toFixed(2) + 'px, ' + haloSpring.y.toFixed(2) + 'px, 0) ' +
          'translate(-50%, -50%) ' +
          'rotate(' + haloAngle.toFixed(1) + 'deg) ' +
          'scale(' + (haloScale * currentHaloStretch).toFixed(3) + ', ' +
          (haloScale / currentHaloStretch).toFixed(3) + ')';
      }

      requestAnimationFrame(renderLoop);
    }

    requestAnimationFrame(renderLoop);
  }

  /* ==========================================================================
     7. Interactive 7-Workspace Live Simulator Engine
     ========================================================================== */
  function initWorkspaceSimulator() {
    const tabsIsland = document.querySelector('.app-tabs-island');
    const tabs = document.querySelectorAll('.app-tab');
    const tabNavLeft = document.getElementById('tabNavLeft');
    const tabNavRight = document.getElementById('tabNavRight');

    if (!tabs.length) return;

    // Arrow button visibility helper
    function updateArrows() {
      if (!tabsIsland || !tabNavLeft || !tabNavRight) return;
      const maxScroll = tabsIsland.scrollWidth - tabsIsland.clientWidth - 4;
      tabNavLeft.classList.toggle('disabled', tabsIsland.scrollLeft <= 4);
      tabNavRight.classList.toggle('disabled', tabsIsland.scrollLeft >= maxScroll);
    }

    if (tabsIsland) {
      // 1. Mouse wheel horizontal scrolling (supports mouse roll in either orientation)
      tabsIsland.addEventListener('wheel', function (e) {
        if (tabsIsland.scrollWidth > tabsIsland.clientWidth) {
          const delta = Math.abs(e.deltaY) > Math.abs(e.deltaX) ? e.deltaY : e.deltaX;
          if (delta !== 0) {
            e.preventDefault();
            tabsIsland.scrollLeft += delta;
            updateArrows();
          }
        }
      }, { passive: false });

      // 2. Click & drag mouse panning
      let isDown = false;
      let startX = 0;
      let scrollLeft = 0;

      tabsIsland.addEventListener('mousedown', function (e) {
        if (e.button !== 0) return;
        isDown = true;
        startX = e.pageX - tabsIsland.offsetLeft;
        scrollLeft = tabsIsland.scrollLeft;
        tabsIsland.style.cursor = 'grabbing';
      });

      window.addEventListener('mouseup', function () {
        if (isDown && tabsIsland) {
          isDown = false;
          tabsIsland.style.cursor = '';
        }
      });

      tabsIsland.addEventListener('mousemove', function (e) {
        if (!isDown) return;
        e.preventDefault();
        const x = e.pageX - tabsIsland.offsetLeft;
        const walk = (x - startX) * 1.5;
        tabsIsland.scrollLeft = scrollLeft - walk;
        updateArrows();
      });

      tabsIsland.addEventListener('scroll', updateArrows, { passive: true });
      window.addEventListener('resize', updateArrows);
      setTimeout(updateArrows, 100);

      // Arrow navigation button handlers
      if (tabNavLeft) {
        tabNavLeft.addEventListener('click', function () {
          tabsIsland.scrollBy({ left: -220, behavior: 'smooth' });
          setTimeout(updateArrows, 300);
        });
      }
      if (tabNavRight) {
        tabNavRight.addEventListener('click', function () {
          tabsIsland.scrollBy({ left: 220, behavior: 'smooth' });
          setTimeout(updateArrows, 300);
        });
      }
    }

    tabs.forEach(function (tab) {
      tab.addEventListener('click', function () {
        tabs.forEach(function (t) {
          t.classList.remove('active');
          t.setAttribute('aria-selected', 'false');
        });
        document.querySelectorAll('.tab-pane').forEach(function (p) {
          p.classList.remove('active');
        });

        tab.classList.add('active');
        tab.setAttribute('aria-selected', 'true');
        tab.scrollIntoView({ behavior: 'smooth', block: 'nearest', inline: 'center' });
        setTimeout(updateArrows, 300);

        const target = tab.getAttribute('data-tab');
        const pane = document.getElementById('pane-' + target);
        if (pane) pane.classList.add('active');
      });
    });

    // 1. Cleaner Scan Simulation
    const scanBtn = document.getElementById('simScanBtn');
    const cleanBtn = document.getElementById('simCleanBtn');
    const scanDock = document.getElementById('simScanDock');
    const junkTotal = document.getElementById('simJunkTotal');

    if (scanBtn && scanDock && junkTotal) {
      scanBtn.addEventListener('click', function () {
        scanBtn.disabled = true;
        scanBtn.textContent = 'Scanning...';
        scanDock.style.display = 'flex';
        let step = 0;
        const phases = [
          'Volume C:\\ • 2,400 files/s • 45 MB/s • ETA: 3s',
          'Volume C:\\ • 4,200 files/s • 115 MB/s • ETA: 2s',
          'Volume D:\\ (NVMe) • 6,100 files/s • 190 MB/s • ETA: 1s',
          '<svg class="i" aria-hidden="true"><use href="#i-check"></use></svg> Complete • 26 scopes scanned • 10,407 items verified'
        ];

        const interval = setInterval(function () {
          if (step < phases.length) {
            scanDock.innerHTML = '<span class="status-pulse-green"></span> ' + phases[step];
            step++;
          } else {
            clearInterval(interval);
            scanBtn.disabled = false;
            scanBtn.textContent = 'Re-Scan';
            junkTotal.textContent = '14.8 GB';
            if (cleanBtn) cleanBtn.disabled = false;
          }
        }, 600);
      });
    }

    if (cleanBtn && junkTotal && scanDock) {
      cleanBtn.addEventListener('click', function () {
        cleanBtn.disabled = true;
        cleanBtn.textContent = 'Cleaning...';
        scanDock.innerHTML = '<span class="status-pulse-green"></span> Purging verified caches & bottom-up empty directories...';
        setTimeout(function () { // DevSkim: ignore DS172411
          junkTotal.textContent = '0 B';
          cleanBtn.textContent = 'Cleaned!';
          scanDock.innerHTML = '✓ Reclaimed 14.8 GB • 0 locked-file errors • System Clean';
          setTimeout(function () { // DevSkim: ignore DS172411
            cleanBtn.textContent = 'Clean Verified Junk';
            cleanBtn.disabled = true;
          }, 3000);
        }, 1200);
      });
    }

    // 2. RAM Boost Simulation
    const boostBtn = document.getElementById('simBoostBtn');
    const ramGauge = document.getElementById('simRamGauge');
    const ramPercent = document.getElementById('simRamPercent');
    const ramDetail = document.getElementById('simRamDetail');

    if (boostBtn && ramGauge && ramPercent) {
      boostBtn.addEventListener('click', function () {
        boostBtn.disabled = true;
        boostBtn.textContent = 'Optimizing NT Kernel...';

        let current = 76;
        const target = 34;
        const isLight = document.documentElement.classList.contains('light');
        const trackBg = isLight ? 'rgba(0,0,0,0.06)' : 'rgba(255,255,255,0.06)';

        const anim = setInterval(function () {
          if (current > target) {
            current -= 3;
            ramPercent.textContent = current + '%';
            ramGauge.style.background = 'conic-gradient(var(--accent-cyan) ' + (current * 3.6) + 'deg, ' + trackBg + ' 0deg)';
          } else {
            clearInterval(anim);
            ramPercent.textContent = '34%';
            ramGauge.style.background = 'conic-gradient(var(--accent-emerald) 122.4deg, ' + trackBg + ' 0deg)';
            if (ramDetail) ramDetail.textContent = '5.4 GB / 16.0 GB (Flushed 6.8 GB Standby RAM)';
            boostBtn.innerHTML = '<svg class="i" aria-hidden="true"><use href="#i-check"></use></svg> RAM Boosted (-6.8 GB)';
            setTimeout(function () { // DevSkim: ignore DS172411
              boostBtn.textContent = '1-Click Boost Memory';
              boostBtn.disabled = false;
            }, 3000);
          }
        }, 40);
      });
    }

    // 3. Interactive CLI Console
    const cliOutput = document.getElementById('simCliOutput');
    const cmdPills = document.querySelectorAll('.cmd-pill');

    const cliResponses = {
      'deltempo status':
        '<span style="color:var(--accent-cyan);font-weight:700;">>>> Deltempo System Telemetry (v2.0.0)</span>\n' +
        '  OS Platform: Windows 11 Pro 64-bit (24H2)\n' +
        '  Privileges: Standard Invoker (Restart Manager available)\n' +
        '  Physical Memory: 15.9 GB total | 5.2 GB active | 5.8 GB standby\n' +
        '  Storage: C:\\ (NVMe) 84.2 GB free / 512.0 GB (83.5% Used)\n' +
        '  Status: Ready for optimization',
      'deltempo boost --all':
        '<span style="color:var(--accent-emerald);font-weight:700;">>>> NT Kernel Memory Deep Purge:</span>\n' +
        '  [OK] Trimmed 18 inactive application working sets\n' +
        '  [OK] Flushed Windows NT Standby Page Lists (Pri 0-7)\n' +
        '  [OK] Cleared System File Cache memory pages\n' +
        '  SUCCESS: Freed 5.84 GB physical RAM in 42ms.',
      'deltempo smart --dry-run':
        '<span style="color:var(--accent-amber);font-weight:700;">>>> Deltempo Smart Clean (Dry Run Simulation):</span>\n' +
        '  Safety Shield: ACTIVE (<24h protected)\n' +
        '  Selected Scopes: 14 safe disposable cache targets\n' +
        '  - User & Windows Temp: 4.8 GB (1,420 files)\n' +
        '  - DirectX & GPU Shaders: 1.2 GB (890 files)\n' +
        '  - Developer Caches (npm/pip/gradle): 5.4 GB (8,120 files)\n' +
        '  - Delivery Optimization: 2.1 GB (12 chunks)\n' +
        '  TOTAL RECLAIMABLE: 13.5 GB across 10,442 items (0 files modified).',
      'deltempo scan temp':
        '<span style="color:var(--accent-cyan);font-weight:700;">>>> Scanning Category: [temp]</span>\n' +
        '  Scanning Volume C:\\ (%TEMP% & Windows Temp)...\n' +
        '  Found: 4,820 MB across 1,420 files past 24-hour shield.\n' +
        '  Protected files untouched: 0 credential or personal documents.'
    };

    if (cliOutput && cmdPills.length) {
      cmdPills.forEach(function (pill) {
        pill.addEventListener('click', function () {
          cmdPills.forEach(p => p.classList.remove('active'));
          pill.classList.add('active');
          const cmd = pill.getAttribute('data-cmd');
          if (cliResponses[cmd]) {
            const safeCmd = String(cmd).replace(/[&<>"']/g, function (m) {
              return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m];
            });
            cliOutput.innerHTML =
              '<span style="color:var(--accent-cyan);">$</span> ' + safeCmd + '\n\n' + cliResponses[cmd];
          }
        });
      });
    }
  }

  /* ==========================================================================
     8. Interactive Space Reclaim Calculator
     ========================================================================== */
  function initReclaimCalculator() {
    const profileBtns = document.querySelectorAll('.calc-profile-btn');
    const calcStorage = document.getElementById('calcStorageValue');
    const calcRam = document.getElementById('calcRamValue');
    const calcBar1 = document.getElementById('calcBarTemp');
    const calcBar2 = document.getElementById('calcBarShader');
    const calcBar3 = document.getElementById('calcBarDev');
    const calcSummary = document.getElementById('calcSummaryText');

    if (!profileBtns.length || !calcStorage || !calcRam) return;

    const data = {
      gamer: {
        storage: '32.4 GB',
        ram: '4.8 GB',
        bars: { temp: '35%', shader: '45%', dev: '20%' },
        summary: 'Cleans massive DirectX, NVIDIA, AMD, and Unreal Engine shader caches plus game installer dumps.'
      },
      dev: {
        storage: '26.8 GB',
        ram: '6.2 GB',
        bars: { temp: '20%', shader: '15%', dev: '65%' },
        summary: 'Reclaims npm-cache, pip wheels, .gradle, Cargo registry caches, and heavy Docker temp staging.'
      },
      everyday: {
        storage: '14.5 GB',
        ram: '3.4 GB',
        bars: { temp: '60%', shader: '10%', dev: '30%' },
        summary: 'Safely flushes browser HTTP caches, Windows Update Delivery Optimization, and app temporary logs.'
      },
      power: {
        storage: '48.2 GB',
        ram: '8.4 GB',
        bars: { temp: '30%', shader: '35%', dev: '35%' },
        summary: 'Complete multi-drive sweep across 26 verified scopes, crash dumps, and full NT memory purge.'
      }
    };

    profileBtns.forEach(function (btn) {
      btn.addEventListener('click', function () {
        profileBtns.forEach(b => b.classList.remove('active'));
        btn.classList.add('active');
        const prof = btn.getAttribute('data-profile');
        const item = data[prof];
        if (item) {
          calcStorage.textContent = item.storage;
          calcRam.textContent = item.ram;
          if (calcBar1) calcBar1.style.width = item.bars.temp;
          if (calcBar2) calcBar2.style.width = item.bars.shader;
          if (calcBar3) calcBar3.style.width = item.bars.dev;
          if (calcSummary) calcSummary.textContent = item.summary;
        }
      });
    });
  }

  /* ==========================================================================
     9. Initialization on DOMContentLoaded
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
     10. Global Responsive Mobile Menu Engine
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
     11. Active Navigation Scroll Spy
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

  function initializeAll() {
    initSmoothScroll();
    initScrollReveals();
    initInteractiveBackground();
    initCardInteractivity();
    initPrecisionCursor();
    initWorkspaceSimulator();
    initReclaimCalculator();
    initServiceWorker();
    initBackToTop();
    initMobileMenu();
    initScrollSpy();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAll);
  } else {
    initializeAll();
  }
})();
