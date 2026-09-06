/**
 * Deltempo Cybernetic Motion Engine
 * High-performance, restrained interactive motion:
 * - Fluid spring-interpolated precision cursor with magnetic element snap
 * - Card surface reactive radial lighting and micro-tilt
 * - IntersectionObserver scroll reveals
 * - Full prefers-reduced-motion & touch-device compliance
 */

(function () {
  'use strict';

  // 1. Feature & Preference Checks
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const isFinePointer = window.matchMedia('(hover: hover) and (pointer: fine)');

  // If reduced motion is requested, reveal all scroll elements immediately and exit
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
     3. Card Reactive Surface Lighting & Micro 3D Tilt
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

          // Set CSS custom properties for radial lighting
          card.style.setProperty('--mouse-x', x + 'px');
          card.style.setProperty('--mouse-y', y + 'px');

          // Subtle micro-tilt for large feature cards and window showcase
          if (
            card.classList.contains('double-bezel') ||
            card.classList.contains('showcase-wrapper') ||
            card.classList.contains('feature-card')
          ) {
            const centerX = rect.width / 2;
            const centerY = rect.height / 2;
            const tiltX = ((y - centerY) / centerY) * -1.5; // Max 1.5 deg
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
     4. Precision Cybernetic Dual-Ring Cursor with Magnetic Snap
     ========================================================================== */
  function initPrecisionCursor() {
    if (!isFinePointer.matches) return;

    // Create cursor DOM elements if not already present
    let dot = document.querySelector('.precision-cursor-dot');
    let ring = document.querySelector('.precision-cursor-ring');

    if (!dot) {
      dot = document.createElement('div');
      dot.className = 'precision-cursor-dot cursor-hidden';
      dot.setAttribute('aria-hidden', 'true');
      document.body.appendChild(dot);
    }

    if (!ring) {
      ring = document.createElement('div');
      ring.className = 'precision-cursor-ring cursor-hidden';
      ring.setAttribute('aria-hidden', 'true');
      document.body.appendChild(ring);
    }

    let mouseX = -200;
    let mouseY = -200;
    let ringX = -200;
    let ringY = -200;
    let isVisible = false;
    let isHovered = false;
    let currentMagneticEl = null;
    let idleTimer = null;

    function resetIdleTimer() {
      clearTimeout(idleTimer);
      if (!isVisible) {
        isVisible = true;
        dot.classList.remove('cursor-hidden');
        ring.classList.remove('cursor-hidden');
      }
      idleTimer = setTimeout(function () {
        if (!isHovered) {
          isVisible = false;
          dot.classList.add('cursor-hidden');
          ring.classList.add('cursor-hidden');
        }
      }, 3500);
    }

    // Pointer move listener
    window.addEventListener(
      'pointermove',
      function (e) {
        mouseX = e.clientX;
        mouseY = e.clientY;

        if (ringX === -200) {
          ringX = mouseX;
          ringY = mouseY;
        }

        // Instant dot tracking via transform3d for hardware acceleration
        dot.style.transform = 'translate3d(' + mouseX + 'px, ' + mouseY + 'px, 0)';

        resetIdleTimer();
      },
      { passive: true }
    );

    // Window enter/leave
    document.addEventListener('mouseleave', function () {
      isVisible = false;
      dot.classList.add('cursor-hidden');
      ring.classList.add('cursor-hidden');
    });

    document.addEventListener('mouseenter', function () {
      isVisible = true;
      dot.classList.remove('cursor-hidden');
      ring.classList.remove('cursor-hidden');
      resetIdleTimer();
    });

    // Active click states
    window.addEventListener(
      'pointerdown',
      function () {
        dot.classList.add('cursor-active');
        ring.classList.add('cursor-active');
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
      'a, button, input, textarea, select, [role="button"], .btn-pill, .btn-icon, .hero-winget, .card-inner, .faq-item, .mobile-nav-link, .nav-link';
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
          } else {
            ring.classList.remove('cursor-text');
          }

          // Magnetic element detection
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

          if (currentMagneticEl) {
            currentMagneticEl.style.transform = '';
            currentMagneticEl = null;
          }
        }
      },
      { passive: true }
    );

    // 60-120fps Fluid Spring Interpolation loop for Ring and Magnetic micro-translation
    function renderLoop() {
      if (isVisible) {
        let targetX = mouseX;
        let targetY = mouseY;

        // Apply magnetic micro-pull if hovering over a primary action element
        if (currentMagneticEl) {
          const rect = currentMagneticEl.getBoundingClientRect();
          const centerX = rect.left + rect.width / 2;
          const centerY = rect.top + rect.height / 2;
          const deltaX = mouseX - centerX;
          const deltaY = mouseY - centerY;

          // Pull ring 40% towards center of button
          targetX = mouseX * 0.6 + centerX * 0.4;
          targetY = mouseY * 0.6 + centerY * 0.4;

          // Micro-translate button by up to 3px towards mouse
          const maxTranslate = 3.5;
          const transX = Math.max(-maxTranslate, Math.min(maxTranslate, deltaX * 0.12));
          const transY = Math.max(-maxTranslate, Math.min(maxTranslate, deltaY * 0.12));
          currentMagneticEl.style.transform =
            'translate3d(' + transX.toFixed(2) + 'px, ' + transY.toFixed(2) + 'px, 0)';
        }

        // Fluid spring lerp interpolation (lerp factor 0.18)
        ringX += (targetX - ringX) * 0.18;
        ringY += (targetY - ringY) * 0.18;

        ring.style.transform =
          'translate3d(' + ringX.toFixed(2) + 'px, ' + ringY.toFixed(2) + 'px, 0)';
      }

      requestAnimationFrame(renderLoop);
    }

    requestAnimationFrame(renderLoop);
  }

  /* ==========================================================================
     5. Initialization on DOMContentLoaded
     ========================================================================== */
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () {
      initScrollReveals();
      initCardInteractivity();
      initPrecisionCursor();
    });
  } else {
    initScrollReveals();
    initCardInteractivity();
    initPrecisionCursor();
  }
})();
