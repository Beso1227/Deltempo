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

    // Document height changes only on load and resize - reading it every
    // paint forces a layout pass per frame, so it is cached and refreshed
    // on those two moments only.
    let maxScroll = 1;
    function measureMaxScroll() {
      maxScroll = Math.max(1, docEl.scrollHeight - window.innerHeight);
    }
    measureMaxScroll();
    window.addEventListener('resize', measureMaxScroll, { passive: true });
    window.addEventListener('load', measureMaxScroll, { passive: true });

    // scaleX writes are the only thing this frame always does - but only
    // when the rail actually moved by a visible quantum.
    let lastRail = -1;

    function paint() {
      const y = window.scrollY || window.pageYOffset || 0;
      const p = Math.min(1, Math.max(0, y / maxScroll));

      scrollState.y = y;
      scrollState.progress = p;

      for (let h = 0; h < scrollHooks.length; h++) {
        scrollHooks[h](y, p);
      }

      const rail = Math.round(p * 500) / 500;
      if (rail !== lastRail) {
        lastRail = rail;
        progressBar.style.transform = 'scaleX(' + p.toFixed(4) + ')';
      }

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
          // Back at the hero the pill has not landed anywhere yet, so the
          // "already returned" pin must go: the next pass deserves the drop.
          if (!pastHero) docEl.classList.remove('cta-returned');
        },
        { rootMargin: '-96px 0px 0px 0px', threshold: 0 }
      );
      ctaObserver.observe(heroEl);
    }

  // ---- The pill's second act: it becomes the primary button --------------
    // Once the closing section is on screen the corner pill has nothing left
    // to point at, because the real download button is right there. So it
    // dives onto that button, grown to its size, and its surface dissolves
    // through the button's identical surface - the pill never parks anywhere
    // in the row.
    //
    // The landing point is measured when the scroll comes to REST, not when
    // the section appears: the row keeps rising as the page settles, so aiming
    // early would leave the pill landing below it.
    const dockTarget = document.querySelector('.cta-final .cta-primary');
    const dockRow = document.querySelector('.cta-final .cta-actions');
    const navPill = document.querySelector('.nav-download-btn');
    if (dockTarget && dockRow && navPill && 'IntersectionObserver' in window && !isReducedMotion) {
      // The middle button does not exist until the corner pill becomes it:
      // it is hidden from the very first frame of a scripted, motion-
      // friendly visit. No-JS and reduced-motion never carry the class,
      // so for them the button is simply always there.
      docEl.classList.add('cta-primary-hidden');
      let inSight = false;
      let docked = false;
      let dockRevealTimer = 0;
      // The scroll offset the pill was docked at. Two uses: it is the line the
      // visitor has to cross upwards to send the shell home, and the one they
      // have to come back down past before it may dive again.
      let dockScrollY = 0;
      // The pill's untransformed centre. The dive now launches while the page
      // is still moving and re-aims the whole way down, so the launch point
      // cannot be re-read from getBoundingClientRect mid-flight - it would
      // follow the shell. It is measured once, at launch.
      let dockBase = null;
      let trackY = -1;
      // The pill's untransformed rendered size. Measured once when the flight
      // starts, because offsetWidth/offsetHeight are rounded to whole pixels:
      // a 36.4px-tall pill scaled by 62/36 came out 62.7px tall, so the shell
      // was half a pixel taller than the button it was supposed to become.
      // Same source (fractional rects) for both boxes = the same box.
      let pillRest = null;

      // The flight target: dead centre on the primary button - or, when the
      // actions stack into full-width columns on phones, the button's own
      // icon bubble, so icon meets icon and the label is never covered.
      // Both the aim and the scale come from rendered rectangles, not from
      // offsetWidth/offsetHeight (which round to whole pixels and left the
      // shell ~0.6px taller than the button): the shell has to land on the
      // button's exact box, down to the fractional pixel, or the surface
      // changes size at the moment it hands over.
      function targetPoint() {
        const b = dockTarget.getBoundingClientRect();
        let cx = b.left + b.width / 2;
        let cy = b.top + b.height / 2;
        // Two-axis scale: a height-only scale left the shell ~18px narrower
        // on each side, so the real button's edges appeared outside the shell
        // during the handover and the button looked like it grew. The label is
        // already gone by then, so the only thing stretched is the bare mint
        // surface - and a capsule scaled on both axes is still a capsule of
        // the new box.
        let sx = b.width / (pillRest ? pillRest.w : navPill.offsetWidth);
        let sy = b.height / (pillRest ? pillRest.h : navPill.offsetHeight);
        if (getComputedStyle(dockRow).flexDirection === 'column') {
          // Phones stack the actions full-width, so the aim is the button's
          // own icon bubble: icon meets icon and the label is never covered.
          // The scale stays uniform there - a full-width button is not the
          // shape the shell is becoming.
          const bubble = dockTarget.querySelector('.btn-icon-bubble');
          if (bubble) {
            const r = bubble.getBoundingClientRect();
            cx = r.left + r.width / 2;
            cy = r.top + r.height / 2;
          }
          const s = Math.min(1.75, Math.max(1, b.height / (pillRest ? pillRest.h : navPill.offsetHeight)));
          sx = s;
          sy = s;
        }
        return { x: cx, y: cy, sx: sx, sy: sy };
      }

      // The pill's size with its transform removed, so the scale means "the
      // shell's CSS box becomes the button's CSS box" rather than "some
      // multiple of the shell's current on-screen size".
      function readPillRest() {
        const p = navPill.getBoundingClientRect();
        const tr = getComputedStyle(navPill).transform;
        let kx = 1;
        let ky = 1;
        if (tr && tr !== 'none') {
          const d = new DOMMatrixReadOnly(tr);
          kx = Math.abs(d.a) || 1;
          ky = Math.abs(d.d) || 1;
        }
        pillRest = { w: p.width / kx, h: p.height / ky };
        return p;
      }

      // The offsets are relative to wherever the reference point claims the
      // shell is: the resting corner for a launch, or the pill's current
      // visual position for a return.
      function writeAim(bx, by, t) {
        docEl.style.setProperty('--dock-x', (t.x - bx).toFixed(1) + 'px');
        docEl.style.setProperty('--dock-y', (t.y - by).toFixed(1) + 'px');
        docEl.style.setProperty('--dock-sx', t.sx.toFixed(4));
        docEl.style.setProperty('--dock-sy', t.sy.toFixed(4));
      }

      // The pill's corner position with its current transform removed. The
      // entry "drop" can still be running when a fast scroll reaches the
      // finale, and a raw rect would then measure the shell mid-drop - a
      // ~9px error baked into every later aim, because the flight's offsets
      // are relative to this base. The keyframes start from the untransformed
      // corner, so that is the number they must be given.
      function pillBase(p) {
        const tr = getComputedStyle(navPill).transform;
        let mx = 0;
        let my = 0;
        if (tr && tr !== 'none') {
          const d = new DOMMatrixReadOnly(tr);
          mx = d.e || 0;
          my = d.f || 0;
        }
        return { x: p.left + p.width / 2 - mx, y: p.top + p.height / 2 - my };
      }

      function aim() {
        const p = readPillRest();
        const t = targetPoint();
        dockBase = pillBase(p);
        writeAim(dockBase.x, dockBase.y, t);
      }

      // The dive no longer waits for the page to stop, so the button is still
      // climbing the viewport while the shell is in the air. Re-aiming on
      // every scroll frame keeps the landing point on the live button instead
      // of on the spot it occupied when the flight started. The CSS variables
      // feed the keyframes directly, so moving them bends the flight without
      // restarting it.
      function trackDock() {
        // Also runs while parked (.cta-docked): the shell is invisible there,
        // but it is the point the return flight launches from, so it has to
        // stay welded to the button even if the visitor keeps scrolling after
        // the dive has ended.
        if (!docked || !dockBase) return;
        if (window.scrollY === trackY) return;
        trackY = window.scrollY;
        // The return leaves 12px above the deepest point the visitor reached
        // while docked. The dive now starts mid-glide, so the launch offset is
        // already stale by the time they stop; tracking the peak keeps "scroll
        // up from where I am" true instead of asking them to climb back to
        // wherever the shell happened to take off.
        if (window.scrollY > dockScrollY) dockScrollY = window.scrollY;
        // Re-measure the shell's rest size too: a resize or rotation between
        // the dive and now would otherwise leave the scale aimed at the old
        // pill, and the invisible shell would drift off the return's origin.
        readPillRest();
        const t = targetPoint();
        writeAim(dockBase.x, dockBase.y, t);
      }
      onScrollFrame(trackDock);

      // The return flight must start from wherever the shell IS, not from
      // where it was aimed: layout can change between the dive and the
      // scroll-away (a window resize, a phone rotation), and a stale vector
      // would fly it home from the wrong place or teleport it mid-air. So
      // the launch point, scale and offsets are re-measured here, from the
      // transform the pill is genuinely carrying right now.
      function aimReturn() {
        const p = readPillRest();
        const t = targetPoint();
        const tr = getComputedStyle(navPill).transform;
        let mx = 0, my = 0, sx = 1, sy = 1;
        if (tr && tr !== 'none') {
          const d = new DOMMatrixReadOnly(tr);
          mx = d.e || 0;
          my = d.f || 0;
          sx = Math.max(0.05, d.a || 1);
          sy = Math.max(0.05, d.d || 1);
        }
        writeAim(p.left + p.width / 2 - mx, p.top + p.height / 2 - my, { x: t.x, y: t.y, sx: sx, sy: sy });
        return t;
      }

      function dock() {
        // No hero, no drop: without nav-cta-live the pill is parked off-screen
        // and flying an invisible element would only write style for nothing.
        if (docked || !inSight || !docEl.classList.contains('nav-cta-live')) return;
        // A return flight may still be running (the visitor scrolled out and
        // immediately back in). Stripping it mid-flight would teleport the
        // shell to the corner before the new dive; letting the end handler
        // re-arm keeps every frame continuous.
        if (docEl.classList.contains('cta-undock')) return;
        // Hysteresis: the return now leaves on the first upward scroll, so a
        // visitor parked a few pixels above that departure point would
        // otherwise be re-docked the moment the page stops again - a yo-yo.
        // They have to come back down to where they left before the pill
        // becomes the button again.
        if (window.scrollY < dockScrollY - 12) return;
        aim();
        trackY = window.scrollY;
        dockScrollY = window.scrollY;
        docEl.classList.remove('cta-returned', 'cta-undock', 'cta-docked');
        // Restart from frame 0 when the pill docks again after a return.
        void navPill.offsetWidth;
        docEl.classList.add('cta-dock');
        // The middle button materialises under the shell a beat after contact,
        // so the corner button visibly becomes it. It has to be AFTER the
        // shell has landed (56% of 0.58s is ~325ms) or the real button flashes
        // beside the incoming shell - and BEFORE the shell starts to dissolve
        // (74%, ~429ms) so the surface it burns through is already opaque.
        // That overlap is the whole trick: two identical mint surfaces, the
        // lower one solid, so the dissolve reveals the label with no dip in
        // brightness and no size jump.
        dockRevealTimer = setTimeout(function () {
          docEl.classList.remove('cta-primary-hidden');
        }, 340);
        docked = true;
      }

      // Undocking is not "wait until the button is half out of the viewport":
      // that only happens near the end of the gesture, so the pill looked like
      // it waited for the page to stop before it bothered to leave. The moment
      // the visitor scrolls up off the docked spot, the button is already
      // travelling away from the shell - the shell leaves with it.
      function undock() {
        if (!docked) return;
        docked = false;
        clearTimeout(dockRevealTimer);
        dockRevealTimer = 0;
        // Re-aim while the outgoing state is still applied: the read has to
        // see the transform the shell is actually carrying (held on the
        // button, or mid-flight), and must run before any class clears it.
        aimReturn();
        // The middle button un-exists the moment the shell commits to
        // leaving; the CLI path remains for anyone mid-click.
        docEl.classList.add('cta-primary-hidden');
        docEl.classList.remove('cta-dock', 'cta-docked');
        // Restart from frame 0 when the pill docks again after a return.
        void navPill.offsetWidth;
        docEl.classList.add('cta-undock');
      }

      function onScroll() {
        // Launch the moment the button is in sight. The old version waited
        // for three motionless 80ms samples, so the dive simply did not play
        // until the visitor stopped scrolling - and restarted the wait every
        // time they did not. Docking while the page is still moving is fine
        // now: trackDock re-aims at the live button on every frame.
        dock();
        // 12px of upward travel is a real gesture, not inertia breathing at
        // rest; anything smaller would fire on the tail of a downward scroll.
        if (docked && dockScrollY - window.scrollY > 12) undock();
      }

      new IntersectionObserver(
        function (entries) {
          // A quarter of the button is enough to commit: the shell is in the
          // air before the section has finished arriving, and flies to the
          // button rather than to where it happened to be at launch.
          inSight = entries[entries.length - 1].intersectionRatio >= 0.25;
          if (!inSight) {
            // Backstop for the fast cases the 12px scroll rule cannot see -
            // a resize, a jump, a rotation that lifts the button out of view
            // without a scroll event.
            undock();
          } else {
            dock();
          }
        },
        { threshold: [0, 0.25, 0.5, 0.99] }
      ).observe(dockTarget);

      window.addEventListener('scroll', onScroll, { passive: true });

      navPill.addEventListener('animationend', function (event) {
        if (event.target !== navPill) return;
        // navCtaDrop's end can land inside the dock if the visitor reaches the
        // finale right after the drop; only the dock's own end may hide it.
        if (event.animationName === 'navCtaReturn' && docEl.classList.contains('cta-undock')) {
          docEl.classList.remove('cta-undock');
          // navCtaReturn ends at transform:none, so removing it would let
          // navCtaDrop replay. The pin keeps the pill resting in the corner.
          docEl.classList.add('cta-returned');
          // A dock may have been requested while the return was airborne
          // (visitor back inside the finale); re-arm it now that the shell
          // has landed.
          if (inSight) dock();
        } else if (event.animationName === 'navCtaDock' && docEl.classList.contains('cta-dock')) {
          // Parked on the button: invisible, inert, and out of the tab order
          // until the finale leaves the viewport.
          clearTimeout(dockRevealTimer);
          dockRevealTimer = 0;
          // Belt and braces: the button is there when the shell comes to rest.
          docEl.classList.remove('cta-primary-hidden');
          docEl.classList.add('cta-docked');
        }
      });
    }

    const LenisCtor = window.Lenis;

    // Reduced motion keeps the native, instant scroll â€” no hijacking, no easing.
    if (typeof LenisCtor === 'function' && !isReducedMotion) {
      const lenis = new LenisCtor({
        // 0.085 is the showcase-weight glide: weighty without feeling laggy.
        lerp: 0.105,
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
  // Document-space position that ignores reveal transforms: elements parked
  // off-screen or hidden with transforms (the hidden CTA ships translateY and
  // scale) would otherwise bake those offsets into every measured point.
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
      sprites.ice = makeSprite('226, 238, 255');
      sprites.sun = makeSprite('255, 240, 205');
      sprites.ember = makeSprite('255, 216, 165');
      sprites.mint = makeSprite('120, 255, 214');
      sprites.violet = makeSprite('167, 139, 250');
      sprites.streak = makeStreak('226, 240, 255');
      sprites.streakMint = makeStreak('140, 255, 222');
      sprites.streakViolet = makeStreak('186, 166, 255');
      sprites.streakSun = makeStreak('255, 233, 180');
      sprites.auraMint = makeAura('40, 255, 208');
      sprites.auraViolet = makeAura('167, 139, 250');
      sprites.auraIce = makeAura('178, 210, 255');
      sprites.auraSun = makeAura('255, 224, 158');
    }

    buildSprites();

    // --- Star model ---------------------------------------------------------
    const stars = [];

    // Temperature order matches TINT_RGB and TINT_SPRITES below:
    // 0 = ice blue-white, 1 = warm gold, 2 = ember orange, 3 = the rare
    // mint from deep-space emission nebulae.
    const TINT_RGB = ['226, 238, 255', '255, 240, 205', '255, 216, 165', '120, 255, 214'];
    const BAND_ANG = (-19 * Math.PI) / 180;

    // Approximate gaussian in [-1, 1], dense at the spine - the profile of a
    // galactic disk, not a box.
    function gauss() {
      return (Math.random() + Math.random() + Math.random()) / 1.5 - 1;
    }

    function spawnStar(star) {
      star.z = 0.32 + Math.random() * 1.08;
      star.x = Math.random() * width;
      star.y = Math.random() * height;
      // Magnitude distribution follows a power law: most points are faint,
      // a few are commanding. Sizes follow magnitude so brightness and
      // girth arrive together.
      star.mag = Math.pow(Math.random(), 2.1);
      star.r = (0.48 + star.mag * 1.18) * (0.55 + 0.6 * star.z);
      // Depth comes from size and speed, not from dimming alone: a far
      // star is a small point, not just a faded one. The floor stays
      // visible though - a sky you cannot see is not a sky at all.
      star.alpha = (0.34 + star.mag * 0.56) * (0.72 + 0.28 * Math.min(1, star.z));

      // Temperature: a real sky is dominated by blue-white, salted with
      // gold and ember; the mint is a rare nebular member of the family.
      const roll = Math.random();
      star.tint = roll < 0.58 ? 0 : roll < 0.74 ? 1 : roll < 0.84 ? 2 : 3;

      star.dust = false;
      // The Milky Way: a shallow river of micro-stars running behind
      // everything else. One third of the field lives in the band, with
      // gaussian falloff from the spine and capped brightness - it reads
      // as structure, never as noise in front of content.
      if (Math.random() < 0.34) {
        const len = Math.hypot(width, height) * 1.18;
        const along = (Math.random() - 0.5) * len;
        const off = gauss() * height * 0.13;
        star.x = width * 0.44 + Math.cos(BAND_ANG) * along - Math.sin(BAND_ANG) * off;
        star.y = height * 0.46 + Math.sin(BAND_ANG) * along + Math.cos(BAND_ANG) * off;
        star.dust = true;
        star.r *= 0.62;
        star.alpha = Math.min(star.alpha, 0.30 + Math.random() * 0.12);
      }

      star.sparkle = !star.dust && Math.random() < 0.09 && star.z > 0.95;
      star.twinklePhase = Math.random() * Math.PI * 2;
      star.twinkleSpeed = 0.004 + Math.random() * 0.012;
      star.driftX = (Math.random() - 0.5) * 0.05 * star.z;
      star.driftY = (Math.random() - 0.5) * 0.05 * star.z;
      star.vx = 0;
      star.vy = 0;
      star.streak = 0;
      star.dirY = 1;
      star.hot = 0;
      // Constellation membership. `null` until the section is approached and
      // the near stars are drafted; a member carries its home snapshot with
      // it so the release can put the exact same star back.
      star.cseed = null;
      star.gvx = 0;
      star.gvy = 0;
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
      // A whisper of dimming behind the headline - never a blackout: the
      // sky must be unmistakably alive at the top of the page.
      return 0.8 + 0.2 * d * d;
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
    let membersArmed = false;

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

    // The three panel icons are single-path symbols, so their real geometry can
    // be walked and resampled once. Returns points in the glyph's rendered
    // pixels, relative to its centre. The geometry comes from the sprite
    // symbol itself - there is no drawn icon in the layout to sample.
    function glyphPoints(symbol, renderWidth) {
      if (!symbol || !(renderWidth > 0)) return [];
      const key = symbol.id || 'glyph';
      let data = glyphCache[key];
      if (data === undefined) {
        data = null;
        {
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
        glyphCache[key] = data;
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
        // There is no drawn icon in the layout: the anchor is an invisible
        // spacer that reserves the constellation's footprint, and the
        // geometry comes from the hidden sprite symbol named by data.
        const anchor = el.querySelector('.cap-visual-anchor');
        const glyphId = el.getAttribute('data-scope-glyph');
        const symbol = glyphId ? document.querySelector('#' + glyphId) : null;
        const rgb = window.getComputedStyle(anchor || el).color.match(/[\d.]+/g) || [];
        scopes.push({
          el: el,
          icon: anchor,
          glyphEl: symbol,
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

        const anchor = sc.icon;
        if (anchor) {
          // Relative rects cancel the reveal transform that still sits on the
          // grid when the page first paints.
          const pr = el.getBoundingClientRect();
          const ar = anchor.getBoundingClientRect();
          sc.iconCx = sc.left + (ar.left - pr.left) + ar.width / 2;
          sc.iconCy = sc.top + (ar.top - pr.top) + ar.height / 2;
          sc.iconR = ar.width / 2;
          sc.glyph = glyphPoints(sc.glyphEl, ar.width);
        }
        sc.form = -1;
      }
    }

    // The whole field belongs to the sky; a slice of it is asked to become the
    // icon of the pillar in focus. Every member takes a fixed slot on the
    // glyph's outline, and the same slot exists on every pillar, so the shape
    // can be carried from one to the next and morph through the icons instead
    // of dissolving back into the sky.
    //
    // The member set is drawn the moment the section is approached, never at
    // load: the section is 2,000px down the page, and a rank computed at
    // load would be a random line ordering. Assigning at approach time
    // ranks stars by their ACTUAL distance to the icon on the live layout,
    // and every member's full state (position AND motion) is snapshotted
    // into `home`, so the release can put the exact same stars back.
    function assignMembers() {
      if (!scopes.length || membersArmed) return;
      const want = width < 768 ? 76 : 124;
      memberCount = want;
      const reach = Math.max(width, height) * 0.6 + 240;

      const sc = scopeState.from || scopes[0];
      const fy = sc.iconCy - scrollState.y;
      const ranked = [];
      for (let i = 0; i < stars.length; i++) {
        const s = stars[i];
        if (s.cseed) continue;
        const dx = s.x - sc.iconCx;
        const dy = s.y - fy;
        ranked.push({ i: i, d: Math.sqrt(dx * dx + dy * dy), z: s.z });
      }
      ranked.sort(function (a, b) {
        return (a.d - b.d) || (b.z - a.z);
      });

      let filled = 0;
      for (let k = 0; k < ranked.length && filled < want; k++) {
        const s = stars[ranked[k].i];
        // Only inside the window: leaving one slot empty is better than
        // dragging a dot the full width of the screen into the shape.
        if (ranked[k].d > reach && filled >= want * 0.6) break;
        // Arc-length slots, evenly spread with a little organic jitter, so the
        // outline is drawn by points rather than ruled by a machine.
        const t = Math.min(1, Math.max(0, ((filled + 0.5 + (Math.random() - 0.5) * 0.85) / want)));
        s.cseed = {
          t: t,
          // A small stagger on entry and exit keeps the edge alive without
          // breaking the shape apart while it travels.
          lag: Math.random() * 0.12,
          // A quorum of brighter points gives the outline depth instead of an
          // even string of identical beads.
          big: Math.random() < 0.18,
          jx: (Math.random() - 0.5) * 2.4,
          jy: (Math.random() - 0.5) * 2.4,
          // The whole truth about this star at the moment it was drafted:
          // where it was, and exactly how it was moving. The release target
          // is derived from this, and the velocities come back verbatim, so
          // after the sequence every star is indistinguishable from its
          // never-recruited siblings.
          home: {
            docX: s.x,
            docY: s.y + scrollState.y,
            vx: s.vx, vy: s.vy, gvx: s.gvx, gvy: s.gvy,
            streak: s.streak, fx: s.fx, fy: s.fy, dirY: s.dirY,
          },
        };
        filled++;
      }
      membersArmed = true;
    }

    // Put one recruited star back exactly as it was drafted: the motion state
    // comes back verbatim and membership is dropped. Position is the caller's
    // business - the release glide lands it on its drafted spot, a jump lands
    // it somewhere on the screen the visitor actually ended up looking at.
    function restoreMember(s) {
      const home = s.cseed.home;
      s.vx = home.vx;
      s.vy = home.vy;
      s.gvx = 0;
      s.gvy = 0;
      s.streak = home.streak;
      s.fx = home.fx;
      s.fy = home.fy;
      s.dirY = home.dirY;
      s.cseed = null;
    }

    // Put every recruited star back exactly where and how it was: restore
    // the motion state verbatim and drop membership, so the sky is
    // bit-identical to the sky that was never touched.
    function releaseMembers() {
      for (let i = 0; i < stars.length; i++) {
        const s = stars[i];
        if (!s.cseed || !s.cseed.home) continue;
        restoreMember(s);
      }
      membersArmed = false;
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
    //
    // The gather is not a teleport-into-existence: each star slides from the
    // exact spot it occupied when it was drafted (its `home`) to its slot,
    // weighted by the same gate that fades it in. Presence rising draws the
    // shape; presence falling runs the identical blend backwards and the
    // star is home again. No mode flags, no cut points, endless reverse.
    function scopeTarget(seed, gather) {
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
      // Blend with home: at gather 0 the target IS the drafted position, at
      // gather 1 it is the slot. Anything between is a straight ramp, so the
      // release path is exactly the entry path reversed.
      if (seed.home && gather < 1) {
        const w = 1 - gather;
        const hy = seed.home.docY - scrollState.y;
        x = x * gather + seed.home.docX * w;
        y = y * gather + hy * w;
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

      // Draft the near stars when the section is about to own the screen.
      // Standing down is per-star: each member restores itself the moment
      // it is back home (see the render loop), so members never pop
      // mid-air - even if the page jumps past the section in one go.
      if (presence > 0.002 && !membersArmed) assignMembers();
      else if (presence <= 0.002) membersArmed = false;

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
      // A resize rebuilds the sky; membership does not survive it. Stand
      // the drafted stars down first so their home snapshots are honoured,
      // then let updateScopes re-draft from the new layout when needed.
      if (membersArmed) releaseMembers();
      if (stars.length && stars.length === starTarget() && prevWidth > 0) {
        remapField(prevWidth, prevHeight);
      } else {
        resetField();
      }

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

      const tintSprites = [sprites.ice, sprites.sun, sprites.ember, sprites.mint];
      const tintFills = ['rgb(226, 238, 255)', 'rgb(255, 240, 205)', 'rgb(255, 216, 165)', 'rgb(120, 255, 214)'];
      const streakSprites = [sprites.streak, sprites.streakSun, sprites.streakSun, sprites.streakMint];

      // Scroll: stars physically slide against the page, and the distance they
      // travel this frame becomes the length of their streak.
      const y = scrollState.y;
      const scrollDelta = y - lastScrollY;
      lastScrollY = y;
      // A single frame that moves the page by more than a couple of screens is
      // a jump, not travel: the corner logo, an in-page link, the browser
      // restoring a position. Everything below treats it as a cut.
      const jumped = Math.abs(scrollDelta) > 240;

      ctx.clearRect(0, 0, width, height);

      // Stars.
      const wakeRadius = pointer.radius;
      const wakeRadiusSq = wakeRadius * wakeRadius;

      // The constellation lives on a second sheet, above the content: the
      // panel surfaces on a phone are almost opaque. The sheet only exists
      // while a shape is in play, and costs nothing at rest.
      const scopeOn = scopeState.presence > 0.002 && !!scopeCtx;
      if (scopeCtx && (scopeOn || scopeWasLive)) {
        scopeCtx.clearRect(0, 0, width, height);
      }
      scopeWasLive = scopeOn;
      const sheetOn = scopeOn;
      if (scopeCanvas && sheetOn !== scopeLive) {
        scopeLive = sheetOn;
        scopeCanvas.classList.toggle('is-live', sheetOn);
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

      for (let i = 0; i < stars.length; i++) {
        const s = stars[i];
        const seed = s.cseed;
        const prevX = s.x;
        const prevY = s.y;

        // 0 = a free star, 1 = a point in the constellation. The entrance is
        // gated per star, so the outline is drawn rather than popped. The
        // target is ALWAYS computed for a drafted star - at gate 0 the
        // blend inside scopeTarget is home itself, so a released member
        // glides back to its drafted spot instead of collapsing to the
        // origin - and it does not depend on scopeOn, so the release still
        // works when the canvas has already hidden.
        let gather = 0;
        let targetX = 0;
        let targetY = 0;
        if (seed) {
          const gate = scopeOn
            ? smooth01((scopeState.presence - seed.lag * 0.55) / (1 - seed.lag * 0.55))
            : 0;
          gather = gate;
          scopeTarget(seed, gather);
          targetX = scopeTX;
          targetY = scopeTY;
          // The drawn glyph is part of the sky, so the pointer has to reach it
          // too. The deflection is applied to the SLOT, not to the star: the
          // slot is rebuilt from the glyph outline every frame, so letting go
          // of the cursor simply removes the offset and the existing pull
          // spring carries the point home. No extra state, no chance of an
          // outline left bent after the pointer leaves.
          if (pointer.active && gather > 0.02) {
            const pdx = targetX - pointer.x;
            const pdy = targetY - pointer.y;
            const reach = pointer.radius * 0.66;
            const d2 = pdx * pdx + pdy * pdy;
            if (d2 < reach * reach) {
              const d = Math.sqrt(d2) || 1;
              const near = 1 - d / reach;
              // Squared falloff keeps the deflected patch tight, and the
              // amplitude is capped so the glyph deforms under the cursor
              // instead of breaking apart.
              const push = near * near * 24 * gather;
              targetX += (pdx / d) * push;
              targetY += (pdy / d) * push;
            }
          }
        }
        const free = 1 - gather;

        // Parallax: near stars travel further, which is what makes the field
        // feel like space instead of a wallpaper. Only the scattered part of
        // the field rides the scroll; a point being carried is driven by the
        // shape instead.
        const parallax = (0.09 + s.z * 0.24) * free;
        // The jump itself adds no parallax: the sky belongs to the screen the
        // visitor is now looking at, it was not swept past them.
        const my = jumped ? 0 : -scrollDelta * parallax;

        s.x += (s.driftX + s.vx) * dt * free;
        s.y += (s.driftY + s.vy) * dt * free;
        s.vx *= Math.pow(0.9, dt);
        s.vy *= Math.pow(0.9, dt);
        s.y += my;

        // A damped pull rather than a hard set: the star arrives with weight
        // and keeps a pixel or two of lag while the panel is scrolling. The
        // pull runs for members even at gather 0 - that is the ride home.
        if (gather > 0.001 || seed) {
          const dx = targetX - s.x;
          const dy = targetY - s.y;
          const dist = Math.sqrt(dx * dx + dy * dy);
          if (dist > 0.05) {
            const arrive = Math.min(0.09, 0.045 + dist * 0.00055);
            const brake = Math.min(0.42, 0.3 + dist * 0.0004);
            s.gvx += (dx * arrive - s.gvx * brake) * dt;
            s.gvy += (dy * arrive - s.gvy * brake) * dt;
          }
          s.x += s.gvx * dt;
          s.y += s.gvy * dt;
        } else {
          s.gvx *= Math.pow(0.86, dt);
          s.gvy *= Math.pow(0.86, dt);
        }

        // Home means home: once the section is gone (presence 0) and the
        // member has glided back within a few pixels of its drafted spot,
        // restore its exact pre-draft motion verbatim and drop the member
        // tag - it rejoins the sky as if it had never been recruited.
        if (seed && scopeState.presence <= 0.002) {
          const hx = seed.home.docX;
          const hy = seed.home.docY - scrollState.y;
          const ddx = s.x - hx;
          const ddy = s.y - hy;
          if (jumped) {
            // The page cut to a different part of the story. Every member
            // rejoins the sky on this screen at once, scattered rather than
            // wrapped, or the glyph would reappear as a ghost of itself in
            // whatever corner the old section used to occupy.
            restoreMember(s);
            s.x = Math.random() * width;
            s.y = Math.random() * height;
          } else if (ddx * ddx + ddy * ddy < 25) {
            restoreMember(s);
          } else if (hy < -height || hy > height * 2) {
            // Safety net for a dash long enough to leave the drafted spot off
            // this screen without ever tripping the jump test: the glide is
            // aimed at a place the visitor cannot see, so the point simply
            // rejoins the field where it currently is. One that has already
            // been carried past the frame is scattered instead - wrapping a
            // whole formation round the same edge in one frame draws a line
            // of stars across the top of the sky.
            restoreMember(s);
            if (s.x < 0 || s.x > width || s.y < 0 || s.y > height) {
              s.x = Math.random() * width;
              s.y = Math.random() * height;
            }
          }
        }

        // No kick on release: the member glides back to its drafted spot by
        // the same blend that took it to its slot, so the icon unpicks
        // itself into the exact sky that was there before it arrived.

        // Wrap: a star that leaves the viewport re-enters from the opposite
        // edge, keeping the sky density constant through any amount of
        // scrolling. It lands just inside the frame rather than just outside
        // it: these stars drift a few hundredths of a pixel per frame, so one
        // parked past the edge would never find its way back into view.
        // Members are exempt while any membership remains, or a member flying
        // home could teleport to the other side mid-route.
        if (!seed) {
          const edge = 2;
          if (s.x < -edge) s.x = width - edge;
          else if (s.x > width + edge) s.x = edge;
          if (s.y < -edge) s.y = height - edge;
          else if (s.y > height + edge) s.y = edge;
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
        // A formed point answers the pointer as well: the stretch of the
        // outline under the cursor burns brighter, so the glyph feels like
        // touching the sky rather than pointing at a picture. Only the part
        // near the cursor lights up, which keeps the shape legible.
        if (pointer.active && gather > 0.02) {
          const dx = s.x - pointer.x;
          const dy = s.y - pointer.y;
          const reach = pointer.radius * 0.8;
          const distSq = dx * dx + dy * dy;
          if (distSq < reach * reach) {
            const near = 1 - Math.sqrt(distSq) / reach;
            const lit = near * near * 0.95 * gather;
            if (lit > hotTarget) hotTarget = lit;
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
            s.streak += (Math.min(speed * 5.2, 110) - s.streak) * Math.min(1, 0.35 * dt);
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
        // content there, not sky noise behind it, and it burns brighter than
        // the sky so the outline reads as light, not as a dotted line.
        let a = s.alpha * twinkle * (focusFade(s.x, s.y) * free + gather);
        a *= 1 + s.hot * 1.4;
        if (gather > 0) a *= 1 + gather * 0.5;
        if (a <= 0.004) continue;
        if (a > 1) a = 1;

        // A gathered point is drawn on the sheet that sits above the cards;
        // a scattered one stays on the sky behind everything.
        const sheet = gather > 0.004 ? scopeCtx : ctx;
        const sprite = gather > 0.3 ? sprites.star : tintSprites[s.tint];
        const rgb = TINT_RGB[s.tint];

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
            sheet.drawImage(streakSprites[s.tint], -wid / 2, -len, wid, len);
            sheet.restore();
          } else {
            sheet.drawImage(streakSprites[s.tint], s.x - wid / 2, s.y - len, wid, len);
          }
          sheet.globalAlpha = 1;
        }

        // Star head: soft halo, then a crisp core so the point reads as light.
        let size = ((s.streak > 6 ? 4.2 : 5.4) + s.r * 6.2) * (1 + 0.12 * gather);
        // A point under the cursor swells a little as it lights up, so the
        // interaction reads as touch rather than as a brightness slider.
        size *= 1 + s.hot * 0.2;
        // A quorum of the members are drawn larger, which keeps the outline
        // from reading as an even string of identical beads.
        if (gather > 0.3 && seed && seed.big) size *= 1.4;
        // The rare brilliant stars get a wide, faint bloom first - the
        // touch that separates a photographed sky from clip-art dots.
        if (s.mag > 0.55 && gather < 0.4) {
          const aura = s.tint === 1 || s.tint === 2 ? sprites.auraSun : sprites.auraIce;
          const spread = size * 3.6;
          sheet.globalAlpha = Math.min(1, a * 0.42);
          sheet.drawImage(aura, s.x - spread / 2, s.y - spread / 2, spread, spread);
        }
        // A wide aura under the formed points, drawn only once they are on the
        // shape: the outline glows instead of being a thin dotted line.
        if (gather > 0.2) {
          const halo = size * 2.9;
          sheet.globalAlpha = Math.min(1, a * gather * 0.5);
          sheet.drawImage(scopeAura, s.x - halo / 2, s.y - halo / 2, halo, halo);
        }
        sheet.globalAlpha = a;
        sheet.drawImage(sprite, s.x - size / 2, s.y - size / 2, size, size);
        sheet.globalAlpha = Math.min(1, a * 1.05);
        sheet.beginPath();
        sheet.arc(s.x, s.y, Math.max(0.45, s.r * 0.5), 0, Math.PI * 2);
        sheet.fillStyle = gather > 0.25 ? 'rgb(244, 255, 252)' : tintFills[s.tint];
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
      '.double-bezel, .card-inner, .feature-card, .cap-item, .hero-oneshot, .arch-card, .dl-card, .faq-item, .scope-card, .tier-card, .doc-code-card, .trust-banner'
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
      // Cinematic exhale: a long, soft-eased ride home (this is also what
      // lets Lenis stream the starfield past the camera).
      if (scrollState.lenis) {
        scrollState.lenis.scrollTo(0, {
          duration: 1.5,
          easing: function (t) { return 1 - Math.pow(2, -10 * t); },
        });
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
     6. Capability icon band
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
     7. Video stage
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
     8. Footer letterform scrub
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
     9. Text written by the scroll
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
