/**
 * Deltempo Cybernetic Motion Engine v2.1.2
 * High-performance interactive visual systems:
 * - Cybernetic Interactive Constellation & Quantum Particle Canvas
 * - Fluid spring-interpolated precision dual-ring cursor with magnetic snap & click ripple
 * - Complete, universal suppression of Windows OS cursor across all DOM elements
 * - Card surface reactive radial lighting & micro-tilt
 * - IntersectionObserver scroll reveals
 * - Full prefers-reduced-motion & touch-device compliance
 */

(function () {
  'use strict';

  // 1. Feature & Preference Checks
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const isFinePointer = window.matchMedia('(hover: hover) and (pointer: fine)');
  const isReducedMotion = prefersReducedMotion.matches;

  /* ==========================================================================
     2. IntersectionObserver Scroll Reveal System
     ========================================================================== */
  function initScrollReveals() {
    const revealElements = document.querySelectorAll('.reveal-on-scroll');
    if (!revealElements.length) return;

    if (isReducedMotion || !('IntersectionObserver' in window)) {
      revealElements.forEach(function (el) {
        el.classList.add('revealed');
      });
      return;
    }

    const observer = new IntersectionObserver(
      function (entries, obs) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            entry.target.classList.add('revealed');
            obs.unobserve(entry.target);
          }
        });
      },
      {
        root: null,
        rootMargin: '0px 0px -40px 0px',
        threshold: 0.1,
      }
    );

    revealElements.forEach(function (el) {
      observer.observe(el);
    });
  }

  /* ==========================================================================
     3. Cybernetic Interactive Particle Constellation Canvas
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

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let width = 0;
    let height = 0;
    let dpr = 1;
    let animationFrameId = null;

    // Interactive mouse coordinates and shockwave state
    const mouse = {
      x: -1000,
      y: -1000,
      radius: 180,
      active: false,
    };

    // Kinetic shockwave impulses for particle reaction (physics only, no visible circle drawn)
    const shockwaves = [];

    function resize() {
      width = window.innerWidth;
      height = window.innerHeight;
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = Math.floor(width * dpr);
      canvas.height = Math.floor(height * dpr);
      canvas.style.width = width + 'px';
      canvas.style.height = height + 'px';
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    resize();
    window.addEventListener('resize', resize, { passive: true });

    // Track pointer position across entire viewport
    function updatePointer(x, y) {
      mouse.x = x;
      mouse.y = y;
      mouse.active = true;
    }

    window.addEventListener(
      'pointermove',
      function (e) {
        updatePointer(e.clientX, e.clientY);
      },
      { passive: true }
    );

    window.addEventListener(
      'mousemove',
      function (e) {
        updatePointer(e.clientX, e.clientY);
      },
      { passive: true }
    );

    document.addEventListener('mouseleave', function () {
      mouse.x = -1000;
      mouse.y = -1000;
      mouse.active = false;
    });

    // Particle pool setup
    const isMobile = window.innerWidth < 768;
    const particleCount = isMobile ? 48 : 100;
    const particles = [];

    const colorsDark = [
      { r: 0, g: 229, b: 255 },   // Electric Cyan #00E5FF
      { r: 168, g: 85, b: 247 },  // Neon Violet #A855F7
      { r: 56, g: 189, b: 248 },  // Sky Blue #38BDF8
      { r: 16, g: 185, b: 129 },  // Cyber Emerald #10B981
      { r: 255, g: 255, b: 255 }, // Star White
    ];

    const colorsLight = [
      { r: 2, g: 132, b: 199 },   // Azure #0284C7
      { r: 124, g: 58, b: 237 },  // Royal Purple #7C3AED
      { r: 14, g: 165, b: 233 },  // Deep Sky #0EA5E9
      { r: 5, g: 150, b: 105 },   // Deep Emerald #059669
      { r: 71, g: 85, b: 105 },   // Slate
    ];

    class Particle {
      constructor() {
        this.reset(true);
      }

      reset(initRandom) {
        this.x = initRandom ? Math.random() * width : (Math.random() > 0.5 ? 0 : width);
        this.y = initRandom ? Math.random() * height : Math.random() * height;
        const speedScale = isReducedMotion ? 0.15 : 0.65;
        this.vx = (Math.random() - 0.5) * speedScale;
        this.vy = (Math.random() - 0.5) * speedScale;
        
        // 15% are larger Star Nexus nodes with luminous aura
        this.isNexus = Math.random() < 0.15;
        this.baseRadius = this.isNexus ? Math.random() * 1.5 + 3.2 : Math.random() * 1.4 + 1.8;
        this.radius = this.baseRadius;
        this.colorIdx = Math.floor(Math.random() * colorsDark.length);
        this.pulseSpeed = Math.random() * 0.03 + 0.015;
        this.pulseAngle = Math.random() * Math.PI * 2;
        this.alpha = this.isNexus ? Math.random() * 0.2 + 0.75 : Math.random() * 0.3 + 0.55;
      }

      update() {
        this.pulseAngle += this.pulseSpeed;
        this.radius = this.baseRadius + Math.sin(this.pulseAngle) * (this.isNexus ? 0.8 : 0.4);

        // Interaction with mouse proximity
        if (mouse.active && mouse.x > 0 && mouse.y > 0) {
          const dx = mouse.x - this.x;
          const dy = mouse.y - this.y;
          const dist = Math.sqrt(dx * dx + dy * dy);

          if (dist < mouse.radius && dist > 0) {
            // Gentle elastic repulsion when very close, light magnetic attraction when at perimeter
            const force = (1 - dist / mouse.radius);
            const angle = Math.atan2(dy, dx);
            const push = dist < 70 ? force * 2.2 : -force * 0.6;
            this.x -= Math.cos(angle) * push;
            this.y -= Math.sin(angle) * push;
          }
        }

        // Apply smooth fluid wave displacement when wavefront passes
        for (let i = shockwaves.length - 1; i >= 0; i--) {
          const sw = shockwaves[i];
          const sdx = this.x - sw.x;
          const sdy = this.y - sw.y;
          const sDist = Math.sqrt(sdx * sdx + sdy * sdy);
          const diff = sDist - sw.radius;

          if (Math.abs(diff) < sw.waveWidth) {
            // Sinusoidal wave ripple: lifts particles outward and eases them smoothly
            const wavePhase = (diff / sw.waveWidth) * (Math.PI / 2);
            const impulse = Math.cos(wavePhase) * sw.strength * sw.alpha;
            const angle = Math.atan2(sdy, sdx);
            this.x += Math.cos(angle) * impulse;
            this.y += Math.sin(angle) * impulse;
          }
        }

        this.x += this.vx;
        this.y += this.vy;

        // Wrap around viewport boundaries smoothly
        if (this.x < -30) this.x = width + 30;
        else if (this.x > width + 30) this.x = -30;
        if (this.y < -30) this.y = height + 30;
        else if (this.y > height + 30) this.y = -30;
      }

      draw(isLight) {
        const c = (isLight ? colorsLight : colorsDark)[this.colorIdx];
        const effAlpha = this.alpha * (isLight ? 0.75 : 1);

        ctx.beginPath();
        ctx.arc(this.x, this.y, this.radius, 0, Math.PI * 2);

        if (this.isNexus) {
          // Radiant glowing halo for Star Nexus nodes
          const haloRadius = this.radius * 3.5;
          const grad = ctx.createRadialGradient(this.x, this.y, this.radius * 0.5, this.x, this.y, haloRadius);
          grad.addColorStop(0, `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.6})`);
          grad.addColorStop(1, `rgba(${c.r}, ${c.g}, ${c.b}, 0)`);
          ctx.fillStyle = grad;
          ctx.fill();
        }

        ctx.beginPath();
        ctx.arc(this.x, this.y, this.radius, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha})`;
        ctx.shadowColor = `rgba(${c.r}, ${c.g}, ${c.b}, ${isLight ? 0.5 : 0.9})`;
        ctx.shadowBlur = this.isNexus ? 12 : 6;
        ctx.fill();
        ctx.shadowBlur = 0;
      }
    }

    for (let i = 0; i < particleCount; i++) {
      particles.push(new Particle());
    }

    // Trigger fluid wave ripple on click (soft radiant wave band + sinusoidal particle undulation, ZERO hard circle line)
    window.addEventListener(
      'pointerdown',
      function (e) {
        shockwaves.push({
          x: e.clientX,
          y: e.clientY,
          radius: 0,
          maxRadius: Math.max(width, height) * 0.55,
          speed: 8.5,
          waveWidth: 48,
          strength: 4.8,
          alpha: 1.0,
        });
      },
      { passive: true }
    );

    const maxConnectionDistance = isMobile ? 95 : 140;
    const maxMouseDistance = isMobile ? 120 : 180;

    function render() {
      animationFrameId = requestAnimationFrame(render);

      if (document.hidden) return;

      ctx.clearRect(0, 0, width, height);

      const isLight = document.documentElement.classList.contains('light');

      // 1. Update and render soft ethereal light wave (wide smooth gradient wash, NO sharp stroked outline)
      for (let i = shockwaves.length - 1; i >= 0; i--) {
        const sw = shockwaves[i];
        sw.radius += sw.speed;
        sw.alpha = Math.max(0, 1 - sw.radius / sw.maxRadius);

        if (sw.radius >= sw.maxRadius || sw.alpha <= 0) {
          shockwaves.splice(i, 1);
          continue;
        }

        const innerR = Math.max(0, sw.radius - sw.waveWidth);
        const outerR = sw.radius + sw.waveWidth;
        const waveGrad = ctx.createRadialGradient(sw.x, sw.y, innerR, sw.x, sw.y, outerR);
        const waveColor = isLight ? '2, 132, 199' : '0, 229, 255';
        const waveAlpha = sw.alpha * (isLight ? 0.15 : 0.26);

        waveGrad.addColorStop(0, `rgba(${waveColor}, 0)`);
        waveGrad.addColorStop(0.5, `rgba(${waveColor}, ${waveAlpha})`);
        waveGrad.addColorStop(1, `rgba(${waveColor}, 0)`);

        ctx.beginPath();
        ctx.arc(sw.x, sw.y, outerR, 0, Math.PI * 2);
        ctx.fillStyle = waveGrad;
        ctx.fill();
      }

      // 1. Update and render particles
      for (let i = 0; i < particles.length; i++) {
        particles[i].update();
        particles[i].draw(isLight);
      }

      // 3. Draw constellation filament lines between nearby particles
      for (let i = 0; i < particles.length; i++) {
        const p1 = particles[i];

        for (let j = i + 1; j < particles.length; j++) {
          const p2 = particles[j];
          const dx = p1.x - p2.x;
          const dy = p1.y - p2.y;
          const dist = Math.sqrt(dx * dx + dy * dy);

          if (dist < maxConnectionDistance) {
            const alpha = (1 - dist / maxConnectionDistance) * (isLight ? 0.22 : 0.38);
            ctx.beginPath();
            ctx.moveTo(p1.x, p1.y);
            ctx.lineTo(p2.x, p2.y);
            const strokeColor = isLight ? '124, 58, 237' : '0, 229, 255';
            ctx.strokeStyle = `rgba(${strokeColor}, ${alpha})`;
            ctx.lineWidth = 1.0;
            ctx.stroke();
          }
        }

        // 4. Interactive energetic laser filaments connecting to mouse cursor
        if (mouse.active && mouse.x > 0 && mouse.y > 0) {
          const mdx = p1.x - mouse.x;
          const mdy = p1.y - mouse.y;
          const mDist = Math.sqrt(mdx * mdx + mdy * mdy);

          if (mDist < maxMouseDistance) {
            const mAlpha = (1 - mDist / maxMouseDistance) * (isLight ? 0.45 : 0.75);
            ctx.beginPath();
            ctx.moveTo(p1.x, p1.y);
            ctx.lineTo(mouse.x, mouse.y);
            const mColor = isLight ? '2, 132, 199' : '168, 85, 247';
            ctx.strokeStyle = `rgba(${mColor}, ${mAlpha})`;
            ctx.lineWidth = 1.35;
            ctx.stroke();
          }
        }
      }

      // 5. Ambient glowing energy aura at mouse cursor coordinates
      if (mouse.active && mouse.x > 0 && mouse.y > 0) {
        const mouseGrad = ctx.createRadialGradient(mouse.x, mouse.y, 2, mouse.x, mouse.y, 50);
        mouseGrad.addColorStop(0, `rgba(${isLight ? '2, 132, 199' : '0, 229, 255'}, ${isLight ? 0.25 : 0.4})`);
        mouseGrad.addColorStop(1, 'rgba(0, 229, 255, 0)');
        ctx.beginPath();
        ctx.arc(mouse.x, mouse.y, 50, 0, Math.PI * 2);
        ctx.fillStyle = mouseGrad;
        ctx.fill();
      }
    }

    render();
  }

  /* ==========================================================================
     4. Card Reactive Surface Lighting & Micro 3D Tilt
     ========================================================================== */
  function initCardInteractivity() {
    const cards = document.querySelectorAll(
      '.double-bezel, .card-inner, .feature-card, .cap-item, .hero-winget, .showcase-wrapper, .arch-card, .dl-card, .faq-item'
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
              card.classList.contains('feature-card'))
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
     5. Upgraded Cybernetic Dual-Ring Cursor with Aerodynamic Stretch & Snap
     ========================================================================== */
  function initPrecisionCursor() {
    if (!isFinePointer.matches) return;

    // Enforce full OS cursor suppression on document and body
    document.documentElement.classList.add('has-custom-cursor');
    document.body.classList.add('has-custom-cursor');
    document.documentElement.style.cursor = 'none';
    document.body.style.cursor = 'none';

    // Create cursor DOM elements if not already present
    let dot = document.querySelector('.precision-cursor-dot');
    let ring = document.querySelector('.precision-cursor-ring');

    if (!dot) {
      dot = document.createElement('div');
      dot.className = 'precision-cursor-dot';
      dot.setAttribute('aria-hidden', 'true');
      document.body.appendChild(dot);
    }

    if (!ring) {
      ring = document.createElement('div');
      ring.className = 'precision-cursor-ring';
      ring.setAttribute('aria-hidden', 'true');
      document.body.appendChild(ring);
    }

    let mouseX = -100;
    let mouseY = -100;
    let ringX = -100;
    let ringY = -100;
    let lastRingX = -100;
    let lastRingY = -100;
    let isVisible = false;
    let currentMagneticEl = null;

    let currentAngle = 0;
    let currentStretch = 1;
    let currentCompress = 1;

    function onPointerMove(e) {
      mouseX = e.clientX;
      mouseY = e.clientY;

      if (!isVisible) {
        isVisible = true;
        ringX = mouseX;
        ringY = mouseY;
        lastRingX = mouseX;
        lastRingY = mouseY;
        dot.style.opacity = '1';
        ring.style.opacity = '1';
      }

      // Direct hardware-accelerated tracking for instantaneous dot response (zero lag)
      dot.style.transform = 'translate3d(' + mouseX + 'px, ' + mouseY + 'px, 0) translate(-50%, -50%)';
    }

    window.addEventListener('pointermove', onPointerMove, { passive: true });
    window.addEventListener('mousemove', onPointerMove, { passive: true });

    // Window enter / leave handling
    document.addEventListener('mouseleave', function () {
      isVisible = false;
      dot.style.opacity = '0';
      ring.style.opacity = '0';
      document.documentElement.style.cursor = '';
      document.body.style.cursor = '';
    });

    document.addEventListener('mouseenter', function (e) {
      document.documentElement.style.cursor = 'none';
      document.body.style.cursor = 'none';
      mouseX = e.clientX;
      mouseY = e.clientY;
      ringX = mouseX;
      ringY = mouseY;
      lastRingX = mouseX;
      lastRingY = mouseY;
      isVisible = true;
      dot.style.opacity = '1';
      ring.style.opacity = '1';
    });

    // Active click states with kinetic shockwave
    window.addEventListener(
      'pointerdown',
      function (e) {
        dot.classList.add('cursor-active');
        ring.classList.add('cursor-active');

        // Spawn kinetic click pulse
        const pulse = document.createElement('div');
        pulse.className = 'cursor-click-pulse';
        pulse.style.left = e.clientX + 'px';
        pulse.style.top = e.clientY + 'px';
        document.body.appendChild(pulse);
        setTimeout(function () {
          if (pulse.parentNode) pulse.parentNode.removeChild(pulse);
        }, 550);
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

    // Interactive element hover & magnetic detection
    const interactiveQuery =
      'a, button, input, textarea, select, [role="button"], [role="tab"], .btn-pill, .btn-icon, .hero-winget, .card-inner, .faq-item, .mobile-nav-link, .nav-link, summary, .calc-profile-btn, .cmd-pill';
    const magneticQuery =
      '.btn-pill-primary, .hero-winget, #themeToggleBtn, .btn-icon, .nav-github-btn, .dl-btn-primary, .app-tab';

    document.addEventListener(
      'pointerover',
      function (e) {
        const target = e.target.closest(interactiveQuery);
        if (target) {
          dot.classList.add('cursor-hover');
          ring.classList.add('cursor-hover');

          if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
            ring.classList.add('cursor-text');
            dot.classList.add('cursor-text');
          } else {
            ring.classList.remove('cursor-text');
            dot.classList.remove('cursor-text');
          }

          const magneticTarget = target.closest(magneticQuery);
          if (magneticTarget) {
            currentMagneticEl = magneticTarget;
          }
        }
      },
      { passive: true }
    );

    document.addEventListener(
      'pointerout',
      function (e) {
        const target = e.target.closest(interactiveQuery);
        if (target) {
          dot.classList.remove('cursor-hover');
          ring.classList.remove('cursor-hover');
          ring.classList.remove('cursor-text');
          dot.classList.remove('cursor-text');

          if (currentMagneticEl) {
            currentMagneticEl.style.transform = '';
            currentMagneticEl = null;
          }
        }
      },
      { passive: true }
    );

    // 60-144fps fluid aerodynamic spring physics loop
    function renderLoop() {
      if (isVisible) {
        let targetX = mouseX;
        let targetY = mouseY;

        // Apply magnetic pull when hovering primary action elements
        if (currentMagneticEl) {
          const rect = currentMagneticEl.getBoundingClientRect();
          const centerX = rect.left + rect.width / 2;
          const centerY = rect.top + rect.height / 2;
          const deltaX = mouseX - centerX;
          const deltaY = mouseY - centerY;

          targetX = mouseX * 0.52 + centerX * 0.48;
          targetY = mouseY * 0.52 + centerY * 0.48;

          const maxTranslate = 4.0;
          const transX = Math.max(-maxTranslate, Math.min(maxTranslate, deltaX * 0.14));
          const transY = Math.max(-maxTranslate, Math.min(maxTranslate, deltaY * 0.14));
          currentMagneticEl.style.transform =
            'translate3d(' + transX.toFixed(2) + 'px, ' + transY.toFixed(2) + 'px, 0)';
        }

        // Smooth spring interpolation (0.32 factor for responsive tracking)
        ringX += (targetX - ringX) * 0.32;
        ringY += (targetY - ringY) * 0.32;

        // Compute velocity vector for aerodynamic stretch
        const dX = ringX - lastRingX;
        const dY = ringY - lastRingY;
        const speed = Math.sqrt(dX * dX + dY * dY);

        if (speed > 1.2 && !currentMagneticEl) {
          currentAngle = Math.atan2(dY, dX) * (180 / Math.PI);
        }

        const targetStretch = currentMagneticEl ? 1 : 1 + Math.min(speed * 0.012, 0.4);
        const targetCompress = currentMagneticEl ? 1 : 1 - Math.min(speed * 0.006, 0.2);
        currentStretch += (targetStretch - currentStretch) * 0.22;
        currentCompress += (targetCompress - currentCompress) * 0.22;

        lastRingX = ringX;
        lastRingY = ringY;

        ring.style.transform =
          'translate3d(' + ringX.toFixed(2) + 'px, ' + ringY.toFixed(2) + 'px, 0) ' +
          'translate(-50%, -50%) ' +
          'rotate(' + currentAngle.toFixed(1) + 'deg) ' +
          'scale(' + currentStretch.toFixed(3) + ', ' + currentCompress.toFixed(3) + ')';
      }

      requestAnimationFrame(renderLoop);
    }

    requestAnimationFrame(renderLoop);
  }

  /* ==========================================================================
     6. Interactive 7-Workspace Live Simulator Engine
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
          '✓ Complete • 26 scopes scanned • 10,407 items verified'
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
        setTimeout(function () {
          junkTotal.textContent = '0 B';
          cleanBtn.textContent = 'Cleaned!';
          scanDock.innerHTML = '✓ Reclaimed 14.8 GB • 0 locked-file errors • System Clean';
          setTimeout(function () {
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
            boostBtn.textContent = '✓ RAM Boosted (-6.8 GB)';
            setTimeout(function () {
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
        '<span style="color:var(--accent-cyan);font-weight:700;">>>> Deltempo System Telemetry (v1.6.5)</span>\n' +
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
            cliOutput.innerHTML =
              '<span style="color:var(--accent-cyan);">$</span> ' + cmd + '\n\n' + cliResponses[cmd];
          }
        });
      });
    }
  }

  /* ==========================================================================
     7. Interactive Space Reclaim Calculator
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
     8. Initialization on DOMContentLoaded
     ========================================================================== */
  function initServiceWorker() {
    if ('serviceWorker' in navigator && window.location.protocol.startsWith('http')) {
      window.addEventListener('load', function () {
        const swPath = window.location.pathname.includes('/docs/') ||
          window.location.pathname.includes('/download/') ||
          window.location.pathname.includes('/changelog/') ||
          window.location.pathname.includes('/faq/')
          ? '../sw.js'
          : 'sw.js';
        navigator.serviceWorker.register(swPath).catch(function (err) {
          console.debug('ServiceWorker registration note:', err);
        });
      });
    }
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
      window.scrollTo({
        top: 0,
        behavior: 'smooth'
      });
    });
  }

  function initializeAll() {
    initScrollReveals();
    initInteractiveBackground();
    initCardInteractivity();
    initPrecisionCursor();
    initWorkspaceSimulator();
    initReclaimCalculator();
    initServiceWorker();
    initBackToTop();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAll);
  } else {
    initializeAll();
  }
})();
