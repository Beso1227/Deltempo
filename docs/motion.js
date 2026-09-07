/**
 * Deltempo Cybernetic Motion Engine
 * High-performance interactive visual systems:
 * - Cybernetic Interactive Constellation & Quantum Particle Canvas
 * - Fluid spring-interpolated precision dual-ring cursor with magnetic snap & click ripple
 * - Complete suppression of OS cursor via .has-custom-cursor
 * - Card surface reactive radial lighting & micro-tilt
 * - IntersectionObserver scroll reveals
 * - Full prefers-reduced-motion & touch-device compliance
 */

(function () {
  'use strict';

  // 1. Feature & Preference Checks
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const isFinePointer = window.matchMedia('(hover: hover) and (pointer: fine)');

  // If reduced motion is requested, reveal all scroll elements immediately
  if (prefersReducedMotion.matches) {
    document.querySelectorAll('.reveal-on-scroll').forEach(function (el) {
      el.classList.add('revealed');
    });
    return;
  }

  /* ==========================================================================
     2. IntersectionObserver Scroll Reveal System
     ========================================================================== */
  function initScrollReveals() {
    const revealElements = document.querySelectorAll('.reveal-on-scroll');
    if (!revealElements.length) return;

    if (!('IntersectionObserver' in window)) {
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
      const aurora = document.querySelector('.ambient-aurora');
      if (aurora) {
        canvas = document.createElement('canvas');
        canvas.id = 'ambientCanvas';
        canvas.className = 'ambient-canvas';
        canvas.setAttribute('aria-hidden', 'true');
        aurora.insertBefore(canvas, aurora.firstChild);
      } else {
        return;
      }
    }

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let width = 0;
    let height = 0;
    let dpr = 1;
    let animationFrameId = null;
    let isTabVisible = !document.hidden;

    // Mouse coordinates for particle attraction & proximity connection
    const mouse = {
      x: -1000,
      y: -1000,
      radius: 150,
    };

    function resize() {
      width = window.innerWidth;
      height = window.innerHeight;
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      canvas.width = width * dpr;
      canvas.height = height * dpr;
      canvas.style.width = width + 'px';
      canvas.style.height = height + 'px';
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    resize();
    window.addEventListener('resize', resize, { passive: true });

    // Track pointer position across entire viewport
    window.addEventListener(
      'pointermove',
      function (e) {
        mouse.x = e.clientX;
        mouse.y = e.clientY;
      },
      { passive: true }
    );

    document.addEventListener('mouseleave', function () {
      mouse.x = -1000;
      mouse.y = -1000;
    });

    // Particle pool setup
    const isMobile = window.innerWidth < 768;
    const particleCount = isMobile ? 35 : 65;
    const particles = [];

    const colorsDark = [
      { r: 0, g: 229, b: 255 },   // Cyan #00E5FF
      { r: 139, g: 92, b: 246 },  // Violet #8B5CF6
      { r: 56, g: 189, b: 248 },  // Sky Blue #38BDF8
      { r: 16, g: 185, b: 129 }   // Emerald #10B981
    ];

    const colorsLight = [
      { r: 2, g: 132, b: 199 },   // Azure #0284C7
      { r: 124, g: 58, b: 237 },  // Royal Purple #7C3AED
      { r: 14, g: 165, b: 233 },  // Deep Sky #0EA5E9
      { r: 5, g: 150, b: 105 }    // Deep Emerald #059669
    ];

    class Particle {
      constructor() {
        this.reset(true);
      }

      reset(initRandom) {
        this.x = initRandom ? Math.random() * width : (Math.random() > 0.5 ? 0 : width);
        this.y = initRandom ? Math.random() * height : Math.random() * height;
        this.vx = (Math.random() - 0.5) * 0.6;
        this.vy = (Math.random() - 0.5) * 0.6;
        this.baseRadius = Math.random() * 1.8 + 1.2;
        this.radius = this.baseRadius;
        this.colorIdx = Math.floor(Math.random() * colorsDark.length);
        this.pulseSpeed = Math.random() * 0.02 + 0.01;
        this.pulseAngle = Math.random() * Math.PI * 2;
        this.alpha = Math.random() * 0.35 + 0.25;
      }

      update() {
        this.pulseAngle += this.pulseSpeed;
        this.radius = this.baseRadius + Math.sin(this.pulseAngle) * 0.4;

        // Interaction with mouse proximity
        const dx = mouse.x - this.x;
        const dy = mouse.y - this.y;
        const dist = Math.sqrt(dx * dx + dy * dy);

        if (dist < mouse.radius && dist > 0) {
          const force = (1 - dist / mouse.radius) * 1.4;
          const angle = Math.atan2(dy, dx);
          this.x -= Math.cos(angle) * force;
          this.y -= Math.sin(angle) * force;
          this.radius = this.baseRadius * 1.5;
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
        ctx.beginPath();
        ctx.arc(this.x, this.y, this.radius, 0, Math.PI * 2);
        ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${this.alpha * (isLight ? 0.75 : 1)})`;
        ctx.shadowColor = `rgba(${c.r}, ${c.g}, ${c.b}, ${isLight ? 0.4 : 0.8})`;
        ctx.shadowBlur = isLight ? 4 : 8;
        ctx.fill();
        ctx.shadowBlur = 0;
      }
    }

    for (let i = 0; i < particleCount; i++) {
      particles.push(new Particle());
    }

    const maxConnectionDistance = isMobile ? 85 : 115;
    const maxMouseConnectionDistance = 145;

    function render() {
      if (!isTabVisible) return;

      ctx.clearRect(0, 0, width, height);

      const isLight = document.documentElement.classList.contains('light');

      // 1. Update and render particles
      for (let i = 0; i < particles.length; i++) {
        particles[i].update();
        particles[i].draw(isLight);
      }

      // 2. Draw connections between nearby particles
      for (let i = 0; i < particles.length; i++) {
        const p1 = particles[i];

        for (let j = i + 1; j < particles.length; j++) {
          const p2 = particles[j];
          const dx = p1.x - p2.x;
          const dy = p1.y - p2.y;
          const dist = Math.sqrt(dx * dx + dy * dy);

          if (dist < maxConnectionDistance) {
            const alpha = (1 - dist / maxConnectionDistance) * (isLight ? 0.12 : 0.22);
            ctx.beginPath();
            ctx.moveTo(p1.x, p1.y);
            ctx.lineTo(p2.x, p2.y);
            const strokeColor = isLight ? '124, 58, 237' : '0, 229, 255';
            ctx.strokeStyle = `rgba(${strokeColor}, ${alpha})`;
            ctx.lineWidth = 0.85;
            ctx.stroke();
          }
        }

        // Connection from nearby particles to mouse cursor
        if (mouse.x > 0 && mouse.y > 0) {
          const mdx = p1.x - mouse.x;
          const mdy = p1.y - mouse.y;
          const mDist = Math.sqrt(mdx * mdx + mdy * mdy);

          if (mDist < maxMouseConnectionDistance) {
            const mAlpha = (1 - mDist / maxMouseConnectionDistance) * (isLight ? 0.28 : 0.45);
            ctx.beginPath();
            ctx.moveTo(p1.x, p1.y);
            ctx.lineTo(mouse.x, mouse.y);
            const mColor = isLight ? '2, 132, 199' : '139, 92, 246';
            ctx.strokeStyle = `rgba(${mColor}, ${mAlpha})`;
            ctx.lineWidth = 1.2;
            ctx.stroke();
          }
        }
      }

      animationFrameId = requestAnimationFrame(render);
    }

    // Freeze animation when tab is not active to conserve CPU & battery
    document.addEventListener('visibilitychange', function () {
      isTabVisible = !document.hidden;
      if (isTabVisible) {
        cancelAnimationFrame(animationFrameId);
        render();
      }
    });

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
            card.classList.contains('double-bezel') ||
            card.classList.contains('showcase-wrapper') ||
            card.classList.contains('feature-card')
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
     5. Precision Cybernetic Dual-Ring Cursor with Magnetic Snap & Ripple
     ========================================================================== */
  function initPrecisionCursor() {
    if (!isFinePointer.matches) return;

    // Suppress default Windows cursor
    document.documentElement.classList.add('has-custom-cursor');
    document.body.classList.add('has-custom-cursor');

    // Create cursor DOM elements if not already present
    let dot = document.querySelector('.precision-cursor-dot');
    let ring = document.querySelector('.precision-cursor-ring');
    let ripple = document.querySelector('.precision-cursor-ripple');

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

    if (!ripple) {
      ripple = document.createElement('div');
      ripple.className = 'precision-cursor-ripple';
      ripple.setAttribute('aria-hidden', 'true');
      document.body.appendChild(ripple);
    }

    let mouseX = -100;
    let mouseY = -100;
    let ringX = -100;
    let ringY = -100;
    let isVisible = false;
    let isHovered = false;
    let currentMagneticEl = null;

    // Pointer move listener
    window.addEventListener(
      'pointermove',
      function (e) {
        mouseX = e.clientX;
        mouseY = e.clientY;

        if (!isVisible) {
          isVisible = true;
          ringX = mouseX;
          ringY = mouseY;
          dot.style.opacity = '1';
          ring.style.opacity = '1';
        }

        // Direct hardware-accelerated tracking for instantaneous dot response (zero lag)
        dot.style.transform = 'translate3d(' + mouseX + 'px, ' + mouseY + 'px, 0) translate(-50%, -50%)';
      },
      { passive: true }
    );

    // Window enter / leave handling
    document.addEventListener('mouseleave', function () {
      isVisible = false;
      dot.style.opacity = '0';
      ring.style.opacity = '0';
      document.documentElement.classList.remove('has-custom-cursor');
      document.body.classList.remove('has-custom-cursor');
    });

    document.addEventListener('mouseenter', function (e) {
      document.documentElement.classList.add('has-custom-cursor');
      document.body.classList.add('has-custom-cursor');
      mouseX = e.clientX;
      mouseY = e.clientY;
      ringX = mouseX;
      ringY = mouseY;
      isVisible = true;
      dot.style.opacity = '1';
      ring.style.opacity = '1';
    });

    // Active click states and ripple pulse
    window.addEventListener(
      'pointerdown',
      function (e) {
        dot.classList.add('cursor-active');
        ring.classList.add('cursor-active');

        ripple.style.left = e.clientX + 'px';
        ripple.style.top = e.clientY + 'px';
        ripple.classList.remove('ripple-active');
        void ripple.offsetWidth; // Trigger reflow
        ripple.classList.add('ripple-active');
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
      'a, button, input, textarea, select, [role="button"], .btn-pill, .btn-icon, .hero-winget, .card-inner, .faq-item, .mobile-nav-link, .nav-link, summary';
    const magneticQuery =
      '.btn-pill-primary, .hero-winget, #themeToggleBtn, .btn-icon, .nav-github-btn, .dl-btn-primary';

    document.addEventListener(
      'pointerover',
      function (e) {
        const target = e.target.closest(interactiveQuery);
        if (target) {
          isHovered = true;
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
          isHovered = false;
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

    // 60-120fps fluid spring lerp interpolation for trailing ring & magnetic snap
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

          targetX = mouseX * 0.55 + centerX * 0.45;
          targetY = mouseY * 0.55 + centerY * 0.45;

          const maxTranslate = 3.5;
          const transX = Math.max(-maxTranslate, Math.min(maxTranslate, deltaX * 0.12));
          const transY = Math.max(-maxTranslate, Math.min(maxTranslate, deltaY * 0.12));
          currentMagneticEl.style.transform =
            'translate3d(' + transX.toFixed(2) + 'px, ' + transY.toFixed(2) + 'px, 0)';
        }

        // Fluid spring lerp
        ringX += (targetX - ringX) * 0.18;
        ringY += (targetY - ringY) * 0.18;

        ring.style.transform =
          'translate3d(' + ringX.toFixed(2) + 'px, ' + ringY.toFixed(2) + 'px, 0) translate(-50%, -50%)';
      }

      requestAnimationFrame(renderLoop);
    }

    requestAnimationFrame(renderLoop);
  }

  /* ==========================================================================
     6. Initialization on DOMContentLoaded
     ========================================================================== */
  function initializeAll() {
    initScrollReveals();
    initInteractiveBackground();
    initCardInteractivity();
    initPrecisionCursor();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAll);
  } else {
    initializeAll();
  }
})();
