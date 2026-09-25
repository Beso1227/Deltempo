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
      radius: window.innerWidth < 768 ? 190 : 280,
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
      mouse.radius = width < 768 ? 190 : 280;
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

    document.addEventListener('mouseleave', function () {
      mouse.x = -1000;
      mouse.y = -1000;
      mouse.active = false;
    });

    // Particle pool setup with 3D depth layers
    const isMobile = window.innerWidth < 768;
    const particleCount = isMobile ? 48 : 100;
    const particles = [];

    // Smooth camera / field 3D parallax tilt state
    let tiltX = 0;
    let tiltY = 0;

    const colorsDark = [
      { r: 0, g: 242, b: 176 },   // Cyber Mint #00F2B0
      { r: 13, g: 211, b: 186 },  // Electric Teal #0DD3BA
      { r: 52, g: 211, b: 153 },  // Pure Spring Mint #34D399
      { r: 16, g: 185, b: 129 },  // Neon Emerald #10B981
      { r: 240, g: 253, b: 249 }, // Star Crystal White
    ];

    const colorsLight = [
      { r: 5, g: 150, b: 105 },   // Deep Emerald #059669
      { r: 13, g: 148, b: 136 },  // Deep Teal #0D9488
      { r: 4, g: 120, b: 87 },    // Forest Mint #047857
      { r: 16, g: 185, b: 129 },  // Jade #10B981
      { r: 51, g: 65, b: 85 },    // Slate #334155
    ];

    let frameCount = 0;

    class Particle {
      constructor() {
        this.reset(true);
      }

      reset(initRandom) {
        this.x = initRandom ? Math.random() * width : (Math.random() > 0.5 ? 0 : width);
        this.y = initRandom ? Math.random() * height : Math.random() * height;
        
        // 3D Depth layer: 0.45 (far background) to 1.65 (foreground)
        this.z = Math.random() * 1.2 + 0.45;
        this.zPhase = Math.random() * Math.PI * 2;
        this.zSpeed = Math.random() * 0.016 + 0.008;
        this.currentZ = this.z;
        this.renderX = this.x;
        this.renderY = this.y;

        const speedScale = (isReducedMotion ? 0.12 : 0.55) * this.z;
        this.baseVx = (Math.random() - 0.5) * speedScale;
        this.baseVy = (Math.random() - 0.5) * speedScale;
        this.vx = this.baseVx;
        this.vy = this.baseVy;
        this.swirlDir = Math.random() < 0.5 ? 1 : -1;
        
        // 15% are Holographic Shield Nexus nodes; 85% are Faceted Clean Sparkles (✦)
        this.isNexus = Math.random() < 0.15;
        this.baseRadius = (this.isNexus ? Math.random() * 1.5 + 4.0 : Math.random() * 1.4 + 2.2) * this.z;
        this.radius = this.baseRadius;
        this.colorIdx = Math.floor(Math.random() * colorsDark.length);
        this.pulseSpeed = Math.random() * 0.03 + 0.015;
        this.pulseAngle = Math.random() * Math.PI * 2;
        this.rot = Math.random() * Math.PI * 2;
        this.rotSpeed = (Math.random() - 0.5) * (this.isNexus ? 0.012 : 0.024);
        this.alpha = (this.isNexus ? Math.random() * 0.2 + 0.82 : Math.random() * 0.3 + 0.58) * Math.min(1, this.z);
      }

      update() {
        this.pulseAngle += this.pulseSpeed;
        this.rot += this.rotSpeed;
        this.zPhase += this.zSpeed;

        // Dynamic 3D breathing Z oscillation
        this.currentZ = Math.max(0.35, this.z + Math.sin(this.zPhase) * 0.22);
        this.radius = this.baseRadius * (this.currentZ / this.z) + Math.sin(this.pulseAngle) * (this.isNexus ? 0.9 : 0.45) * this.currentZ;

        // Enhanced magnetic gravitational attraction & celestial orbital swirl around cursor
        if (mouse.active && mouse.x > 0 && mouse.y > 0) {
          const dx = mouse.x - this.renderX;
          const dy = mouse.y - this.renderY;
          const distSq = dx * dx + dy * dy;
          const magnetRadius = mouse.radius;
          const magnetRadiusSq = magnetRadius * magnetRadius;

          if (distSq < magnetRadiusSq && distSq > 4) {
            const dist = Math.sqrt(distSq);
            const normDist = dist / magnetRadius; // 0 (cursor center) to 1 (outer boundary)
            const angle = Math.atan2(dy, dx);

            if (dist > 45) {
              // Smooth gravitational pull curve with celestial swirling drift
              const pullForce = Math.pow(1 - normDist, 1.35) * (isReducedMotion ? 0.4 : 2.2) * this.currentZ;
              const swirlFactor = Math.sin(normDist * Math.PI) * (this.isNexus ? 0.8 : 0.55);
              const swirlAngle = angle + (Math.PI * 0.5 * this.swirlDir);

              this.vx += (Math.cos(angle) * pullForce * 0.45 + Math.cos(swirlAngle) * swirlFactor * 0.35);
              this.vy += (Math.sin(angle) * pullForce * 0.45 + Math.sin(swirlAngle) * swirlFactor * 0.35);
            } else {
              // Protective inner cushion around cursor: establishes a dynamic orbiting halo
              const cushionNorm = 1 - (dist / 45);
              const repulseForce = cushionNorm * 0.9;
              const orbitAngle = angle + (Math.PI * 0.5 * this.swirlDir);

              this.vx -= Math.cos(angle) * repulseForce * 0.45;
              this.vy -= Math.sin(angle) * repulseForce * 0.45;
              this.vx += Math.cos(orbitAngle) * (1 - cushionNorm) * 0.6;
              this.vy += Math.sin(orbitAngle) * (1 - cushionNorm) * 0.6;
            }

            // Magnetic damping inside active field prevents runaway velocity or jitter
            this.vx *= 0.935;
            this.vy *= 0.935;
          } else {
            // Smooth natural decay back to tranquil ambient drift
            this.vx = this.vx * 0.985 + this.baseVx * 0.015;
            this.vy = this.vy * 0.985 + this.baseVy * 0.015;
          }
        } else {
          this.vx = this.vx * 0.985 + this.baseVx * 0.015;
          this.vy = this.vy * 0.985 + this.baseVy * 0.015;
        }

        // Cap maximum velocity for silky smooth visual consistency
        const maxSpeed = (isReducedMotion ? 0.6 : 3.8) * this.currentZ;
        const currentSpeedSq = this.vx * this.vx + this.vy * this.vy;
        if (currentSpeedSq > maxSpeed * maxSpeed) {
          const currentSpeed = Math.sqrt(currentSpeedSq);
          this.vx = (this.vx / currentSpeed) * maxSpeed;
          this.vy = (this.vy / currentSpeed) * maxSpeed;
        }

        // Apply smooth 3D fluid wave displacement from clicks
        for (let i = shockwaves.length - 1; i >= 0; i--) {
          const sw = shockwaves[i];
          const sdx = this.renderX - sw.x;
          const sdy = this.renderY - sw.y;
          const sDist = Math.sqrt(sdx * sdx + sdy * sdy);
          const diff = sDist - sw.radius;

          if (Math.abs(diff) < sw.waveWidth) {
            const wavePhase = (diff / sw.waveWidth) * (Math.PI / 2);
            const impulse = Math.cos(wavePhase) * sw.strength * sw.alpha * this.currentZ;
            const angle = Math.atan2(sdy, sdx);
            this.x += Math.cos(angle) * impulse;
            this.y += Math.sin(angle) * impulse;
          }
        }

        this.x += this.vx;
        this.y += this.vy;

        // Wrap around viewport boundaries smoothly
        if (this.x < -50) this.x = width + 50;
        else if (this.x > width + 50) this.x = -50;
        if (this.y < -50) this.y = height + 50;
        else if (this.y > height + 50) this.y = -50;

        // Calculate 3D Parallax Screen Position
        this.renderX = this.x + tiltX * (this.currentZ - 1) * 45;
        this.renderY = this.y + tiltY * (this.currentZ - 1) * 45;
      }

      draw(isLight) {
        const c = (isLight ? colorsLight : colorsDark)[this.colorIdx];
        const effAlpha = this.alpha * (isLight ? 0.75 : 1);
        const px = this.renderX;
        const py = this.renderY;
        const cz = this.currentZ;

        ctx.save();
        ctx.translate(px, py);
        ctx.rotate(this.rot);

        if (this.isNexus) {
          // 3D Holographic Micro-Shield with Zero-ShadowBlur GPU Vector Bloom
          const s = this.radius * 1.5;

          // Outer luminous bloom pass
          ctx.beginPath();
          ctx.ellipse(0, 0, s * 2.5, s * 0.95, this.pulseAngle, 0, Math.PI * 2);
          ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.12})`;
          ctx.fill();

          // 1. Primary Orbital Ring (perspective foreshortened)
          ctx.beginPath();
          ctx.ellipse(0, 0, s * 2.3, s * 0.85, this.pulseAngle, 0, Math.PI * 2);
          ctx.strokeStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.55})`;
          ctx.lineWidth = 1;
          ctx.stroke();

          // 2. Secondary Counter-Rotating Orbital Ring
          ctx.beginPath();
          ctx.ellipse(0, 0, s * 1.75, s * 0.6, -this.pulseAngle * 1.3, 0, Math.PI * 2);
          ctx.strokeStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.38})`;
          ctx.lineWidth = 0.8;
          ctx.stroke();

          // 3. Four Quantum Orbital Ticks on Ring
          for (let k = 0; k < 4; k++) {
            const oAngle = this.pulseAngle + (k * Math.PI) / 2;
            const tx = Math.cos(oAngle) * (s * 2.3);
            const ty = Math.sin(oAngle) * (s * 0.85);
            ctx.beginPath();
            ctx.arc(tx, ty, 1.2 * cz, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.95})`;
            ctx.fill();
          }

          // 4. Shield Crest Silhouette - Soft Outer Aura
          ctx.beginPath();
          ctx.moveTo(0, -s * 1.15);
          ctx.lineTo(s * 0.98, -s * 0.52);
          ctx.lineTo(s * 0.98, s * 0.23);
          ctx.quadraticCurveTo(s * 0.8, s * 0.98, 0, s * 1.38);
          ctx.quadraticCurveTo(-s * 0.8, s * 0.98, -s * 0.98, s * 0.23);
          ctx.lineTo(-s * 0.98, -s * 0.52);
          ctx.closePath();
          ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.18})`;
          ctx.fill();

          // Shield Crest Silhouette - Main Crisp Body
          ctx.beginPath();
          ctx.moveTo(0, -s);
          ctx.lineTo(s * 0.85, -s * 0.45);
          ctx.lineTo(s * 0.85, s * 0.2);
          ctx.quadraticCurveTo(s * 0.7, s * 0.85, 0, s * 1.2);
          ctx.quadraticCurveTo(-s * 0.7, s * 0.85, -s * 0.85, s * 0.2);
          ctx.lineTo(-s * 0.85, -s * 0.45);
          ctx.closePath();
          ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.42})`;
          ctx.fill();
          ctx.strokeStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.95})`;
          ctx.lineWidth = 1.4;
          ctx.stroke();

          // 5. Pulsating Diamond Core Crystal
          ctx.beginPath();
          ctx.moveTo(0, -s * 0.35);
          ctx.lineTo(s * 0.3, 0);
          ctx.lineTo(0, s * 0.35);
          ctx.lineTo(-s * 0.3, 0);
          ctx.closePath();
          ctx.fillStyle = '#FFFFFF';
          ctx.fill();
        } else {
          // 3D Faceted Clean Sparkle / Quantum Diamond Shard (✦) with Double-Pass GPU Bloom
          const r = this.radius;
          const inner = r * 0.32;

          // Pass 1: Outer Soft Radiant Star Bloom (Zero shadowBlur overhead)
          const rOuter = r * 1.35;
          const innerOuter = inner * 1.35;
          ctx.beginPath();
          for (let k = 0; k < 4; k++) {
            const a1 = (k * Math.PI) / 2;
            const a2 = a1 + Math.PI / 4;
            if (k === 0) ctx.moveTo(Math.cos(a1) * rOuter, Math.sin(a1) * rOuter);
            else ctx.lineTo(Math.cos(a1) * rOuter, Math.sin(a1) * rOuter);
            ctx.quadraticCurveTo(0, 0, Math.cos(a2) * innerOuter, Math.sin(a2) * innerOuter);
          }
          ctx.closePath();
          ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha * 0.22})`;
          ctx.fill();

          // Pass 2: Main Crisp Sparkle Body
          ctx.beginPath();
          for (let k = 0; k < 4; k++) {
            const a1 = (k * Math.PI) / 2;
            const a2 = a1 + Math.PI / 4;
            if (k === 0) ctx.moveTo(Math.cos(a1) * r, Math.sin(a1) * r);
            else ctx.lineTo(Math.cos(a1) * r, Math.sin(a1) * r);
            ctx.quadraticCurveTo(0, 0, Math.cos(a2) * inner, Math.sin(a2) * inner);
          }
          ctx.closePath();
          ctx.fillStyle = `rgba(${c.r}, ${c.g}, ${c.b}, ${effAlpha})`;
          ctx.fill();

          // Facet internal cross-refraction lines for true gem sparkle
          if (cz > 0.8) {
            ctx.beginPath();
            ctx.moveTo(-r * 0.7, 0);
            ctx.lineTo(r * 0.7, 0);
            ctx.moveTo(0, -r * 0.7);
            ctx.lineTo(0, r * 0.7);
            ctx.strokeStyle = `rgba(255, 255, 255, ${effAlpha * 0.55})`;
            ctx.lineWidth = 0.75;
            ctx.stroke();

            ctx.beginPath();
            ctx.arc(0, 0, inner * 0.6, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(255, 255, 255, ${effAlpha * 0.95})`;
            ctx.fill();
          }
        }

        ctx.restore();
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
    const maxConnectionDistanceSq = maxConnectionDistance * maxConnectionDistance;
    const maxMouseDistance = isMobile ? 140 : 200;
    const maxMouseDistanceSq = maxMouseDistance * maxMouseDistance;

    function render() {
      animationFrameId = requestAnimationFrame(render);

      if (document.hidden) return;

      frameCount++;

      // Update 3D Camera Tilt
      if (mouse.active) {
        const targetTiltX = (mouse.x - width / 2) / (width / 2);
        const targetTiltY = (mouse.y - height / 2) / (height / 2);
        tiltX += (targetTiltX - tiltX) * 0.05;
        tiltY += (targetTiltY - tiltY) * 0.05;
      } else {
        tiltX += (0 - tiltX) * 0.03;
        tiltY += (0 - tiltY) * 0.03;
      }

      ctx.clearRect(0, 0, width, height);

      const isLight = document.documentElement.classList.contains('light');

      // 1. Update and render soft ethereal light wave in Cyber Mint
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
        const waveColor = isLight ? '5, 150, 105' : '0, 242, 176';
        const waveAlpha = sw.alpha * (isLight ? 0.15 : 0.28);

        waveGrad.addColorStop(0, `rgba(${waveColor}, 0)`);
        waveGrad.addColorStop(0.5, `rgba(${waveColor}, ${waveAlpha})`);
        waveGrad.addColorStop(1, `rgba(${waveColor}, 0)`);

        ctx.beginPath();
        ctx.arc(sw.x, sw.y, outerR, 0, Math.PI * 2);
        ctx.fillStyle = waveGrad;
        ctx.fill();
      }

      // 2. Update and render 3D particles
      for (let i = 0; i < particles.length; i++) {
        particles[i].update();
        particles[i].draw(isLight);
      }

      // 3. Draw 3D constellation filament lines between depth-matched particles
      for (let i = 0; i < particles.length; i++) {
        const p1 = particles[i];

        for (let j = i + 1; j < particles.length; j++) {
          const p2 = particles[j];
          const depthDiff = Math.abs(p1.currentZ - p2.currentZ);

          // Only connect particles that inhabit similar 3D spatial depth
          if (depthDiff > 0.55) continue;

          // Fast bounding box reject avoids ~90% of sqrt/distance math
          const dx = p1.renderX - p2.renderX;
          if (Math.abs(dx) > maxConnectionDistance) continue;
          const dy = p1.renderY - p2.renderY;
          if (Math.abs(dy) > maxConnectionDistance) continue;

          const distSq = dx * dx + dy * dy;
          if (distSq > maxConnectionDistanceSq) continue;

          const dist = Math.sqrt(distSq);
          const avgZ = (p1.currentZ + p2.currentZ) / 2;
          const alpha = (1 - dist / maxConnectionDistance) * (1 - depthDiff / 0.55) * (isLight ? 0.22 : 0.38) * avgZ;
          ctx.beginPath();
          ctx.moveTo(p1.renderX, p1.renderY);
          ctx.lineTo(p2.renderX, p2.renderY);
          const strokeColor = isLight ? '5, 150, 105' : '0, 242, 176';
          ctx.strokeStyle = `rgba(${strokeColor}, ${alpha})`;
          ctx.lineWidth = 0.9 * avgZ;
          ctx.stroke();

          // Animated photon packet travelling along active constellation lines
          if (alpha > 0.18 && ((i + j) % 3 === 0)) {
            const t = (frameCount * 0.012 + (i * 0.17)) % 1;
            const px = p1.renderX + (p2.renderX - p1.renderX) * t;
            const py = p1.renderY + (p2.renderY - p1.renderY) * t;

            // Outer soft photon halo (pure GPU vector)
            ctx.beginPath();
            ctx.arc(px, py, 2.6 * avgZ, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(${isLight ? '5, 150, 105' : '0, 242, 176'}, ${alpha * 0.35})`;
            ctx.fill();

            // Inner bright core
            ctx.beginPath();
            ctx.arc(px, py, 1.2 * avgZ, 0, Math.PI * 2);
            ctx.fillStyle = `rgba(240, 253, 249, ${alpha * 1.5})`;
            ctx.fill();
          }
        }

        // 4. Interactive energetic laser filaments connecting to mouse cursor
        if (mouse.active && mouse.x > 0 && mouse.y > 0 && p1.currentZ > 0.75) {
          const mdx = p1.renderX - mouse.x;
          if (Math.abs(mdx) <= maxMouseDistance) {
            const mdy = p1.renderY - mouse.y;
            if (Math.abs(mdy) <= maxMouseDistance) {
              const mDistSq = mdx * mdx + mdy * mdy;
              if (mDistSq < maxMouseDistanceSq) {
                const mDist = Math.sqrt(mDistSq);
                const mAlpha = (1 - mDist / maxMouseDistance) * (isLight ? 0.45 : 0.85) * p1.currentZ;
                ctx.beginPath();
                ctx.moveTo(p1.renderX, p1.renderY);
                ctx.lineTo(mouse.x, mouse.y);
                const mColor = isLight ? '13, 148, 136' : '0, 242, 176';
                ctx.strokeStyle = `rgba(${mColor}, ${mAlpha})`;
                ctx.lineWidth = 1.35 * p1.currentZ;
                ctx.stroke();
              }
            }
          }
        }
      }

      // 5. Ambient glowing energy aura at mouse cursor coordinates in Cyber Mint
      if (mouse.active && mouse.x > 0 && mouse.y > 0) {
        const mouseGrad = ctx.createRadialGradient(mouse.x, mouse.y, 2, mouse.x, mouse.y, 52);
        mouseGrad.addColorStop(0, `rgba(${isLight ? '5, 150, 105' : '0, 242, 176'}, ${isLight ? 0.22 : 0.38})`);
        mouseGrad.addColorStop(1, 'rgba(0, 242, 176, 0)');
        ctx.beginPath();
        ctx.arc(mouse.x, mouse.y, 52, 0, Math.PI * 2);
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
    let activeEmberCount = 0;

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

        // Spawn kinetic click pulse in Cyber Mint
        const pulse = document.createElement('div');
        pulse.className = 'cursor-click-pulse';
        pulse.style.left = e.clientX + 'px';
        pulse.style.top = e.clientY + 'px';
        document.body.appendChild(pulse);
        setTimeout(function () { // DevSkim: ignore DS172411
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
            ring.classList.add('cursor-magnetic');
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
          ring.classList.remove('cursor-magnetic');

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

          targetX = mouseX * 0.48 + centerX * 0.52;
          targetY = mouseY * 0.48 + centerY * 0.52;

          const maxTranslate = 4.5;
          const transX = Math.max(-maxTranslate, Math.min(maxTranslate, deltaX * 0.16));
          const transY = Math.max(-maxTranslate, Math.min(maxTranslate, deltaY * 0.16));
          currentMagneticEl.style.transform =
            'translate3d(' + transX.toFixed(2) + 'px, ' + transY.toFixed(2) + 'px, 0)';
        }

        // Smooth spring interpolation (0.35 factor for responsive tracking)
        ringX += (targetX - ringX) * 0.35;
        ringY += (targetY - ringY) * 0.35;

        // Compute velocity vector for aerodynamic stretch & kinetic ember trail
        const dX = ringX - lastRingX;
        const dY = ringY - lastRingY;
        const speed = Math.sqrt(dX * dX + dY * dY);

        if (speed > 1.2 && !currentMagneticEl) {
          currentAngle = Math.atan2(dY, dX) * (180 / Math.PI);
        }

        // Spawn kinetic cyber embers when moving with high velocity (zero DOM query layout thrashing)
        if (speed > 5.5 && !currentMagneticEl && Math.random() < 0.35 && activeEmberCount < 8) {
          activeEmberCount++;
          const ember = document.createElement('div');
          ember.className = 'cursor-ember';
          ember.style.left = ringX + 'px';
          ember.style.top = ringY + 'px';
          ember.style.setProperty('--tx', (-(dX * 0.4) + (Math.random() - 0.5) * 8).toFixed(1) + 'px');
          ember.style.setProperty('--ty', (-(dY * 0.4) + (Math.random() - 0.5) * 8).toFixed(1) + 'px');
          document.body.appendChild(ember);
          setTimeout(function () {
            if (ember.parentNode) ember.parentNode.removeChild(ember);
            activeEmberCount = Math.max(0, activeEmberCount - 1);
          }, 360);
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
            boostBtn.textContent = '✓ RAM Boosted (-6.8 GB)';
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
        '<span style="color:var(--accent-cyan);font-weight:700;">>>> Deltempo System Telemetry (v1.8.0)</span>\n' +
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

  /* ==========================================================================
     9. Global Responsive Mobile Menu Engine
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
      } else {
        mobileMenu.classList.remove('open');
        mobileMenu.setAttribute('aria-hidden', 'true');
        mobileToggle.setAttribute('aria-expanded', 'false');
        mobileToggle.setAttribute('aria-label', 'Open Navigation Menu');
        document.body.style.overflow = '';
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
     10. Active Navigation Scroll Spy
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
    updateActiveNav();
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
    initMobileMenu();
    initScrollSpy();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initializeAll);
  } else {
    initializeAll();
  }
})();
