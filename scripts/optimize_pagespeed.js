const fs = require('fs');
const path = require('path');

// 1. UPDATE style.css
const stylePath = path.join(__dirname, '..', 'docs', 'style.css');
let styleContent = fs.readFileSync(stylePath, 'utf8');

// Color tokens in :root
styleContent = styleContent.replace(
  /--text-low: #64748B;/g,
  '--text-low: #94A3B8;'
);
styleContent = styleContent.replace(
  /--accent-amber: #F59E0B;/g,
  '--accent-amber: #FBBF24;'
);
styleContent = styleContent.replace(
  /--accent-amber-bg: rgba\(245, 158, 11, 0\.1\);/g,
  '--accent-amber-bg: rgba(251, 191, 36, 0.12);'
);
styleContent = styleContent.replace(
  /--accent-amber-border: rgba\(245, 158, 11, 0\.3\);/g,
  '--accent-amber-border: rgba(251, 191, 36, 0.35);'
);
styleContent = styleContent.replace(
  /--accent-purple: #8B5CF6;/g,
  '--accent-purple: #A78BFA;'
);
styleContent = styleContent.replace(
  /--accent-purple-bg: rgba\(139, 92, 246, 0\.1\);/g,
  '--accent-purple-bg: rgba(167, 139, 250, 0.12);'
);

// Light mode tokens
styleContent = styleContent.replace(
  /--border-focus: #059669;/g,
  '--border-focus: #047857;'
);
styleContent = styleContent.replace(
  /--text-med: #475569;/g,
  '--text-med: #334155;'
);
styleContent = styleContent.replace(
  /--text-low: #94A3B8;/g,
  '--text-low: #475569;'
);
styleContent = styleContent.replace(
  /--accent-cyan: #059669;/g,
  '--accent-cyan: #047857;'
);
styleContent = styleContent.replace(
  /--accent-cyan-bg: rgba\(5, 150, 105, 0\.08\);/g,
  '--accent-cyan-bg: rgba(4, 120, 87, 0.08);'
);
styleContent = styleContent.replace(
  /--accent-cyan-border: rgba\(5, 150, 105, 0\.25\);/g,
  '--accent-cyan-border: rgba(4, 120, 87, 0.25);'
);
styleContent = styleContent.replace(
  /--accent-emerald: #059669;/g,
  '--accent-emerald: #047857;'
);
styleContent = styleContent.replace(
  /--accent-emerald-bg: rgba\(5, 150, 105, 0\.08\);/g,
  '--accent-emerald-bg: rgba(4, 120, 87, 0.08);'
);
styleContent = styleContent.replace(
  /--accent-emerald-border: rgba\(5, 150, 105, 0\.25\);/g,
  '--accent-emerald-border: rgba(4, 120, 87, 0.25);'
);
styleContent = styleContent.replace(
  /--accent-amber: #D97706;/g,
  '--accent-amber: #B45309;'
);
styleContent = styleContent.replace(
  /--accent-amber-bg: rgba\(217, 119, 6, 0\.08\);/g,
  '--accent-amber-bg: rgba(180, 83, 9, 0.08);'
);
styleContent = styleContent.replace(
  /--accent-amber-border: rgba\(217, 119, 6, 0\.25\);/g,
  '--accent-amber-border: rgba(180, 83, 9, 0.25);'
);
styleContent = styleContent.replace(
  /--accent-purple: #7C3AED;/g,
  '--accent-purple: #6D28D9;'
);
styleContent = styleContent.replace(
  /--accent-purple-bg: rgba\(124, 58, 237, 0\.08\);/g,
  '--accent-purple-bg: rgba(109, 40, 217, 0.08);'
);

// Tier Badges contrast enhancement
styleContent = styleContent.replace(
  /\.tier-badge\.protected \{\r?\n\s*background: rgba\(244, 63, 94, 0\.12\);\r?\n\s*color: #F43F5E;/g,
  '.tier-badge.protected {\n  background: rgba(244, 63, 94, 0.12);\n  color: #FB7185;'
);
styleContent = styleContent.replace(
  /html\.light \.tier-badge\.protected \{\r?\n\s*background: rgba\(225, 29, 72, 0\.08\);\r?\n\s*color: #E11D48;/g,
  'html.light .tier-badge.protected {\n  background: rgba(225, 29, 72, 0.08);\n  color: #BE123C;'
);
styleContent = styleContent.replace(
  /\.tier-badge\.unknown \{\r?\n\s*background: rgba\(148, 163, 184, 0\.12\);\r?\n\s*color: var\(--text-low\);/g,
  '.tier-badge.unknown {\n  background: rgba(148, 163, 184, 0.12);\n  color: #CBD5E1;'
);

// Add light tier-badge.unknown if not present
if (!styleContent.includes('html.light .tier-badge.unknown')) {
  styleContent = styleContent.replace(
    /\.tier-badge\.unknown \{[\s\S]*?border: 1px solid rgba\(148, 163, 184, 0\.3\);\r?\n\}/,
    `$&

html.light .tier-badge.unknown {
  background: rgba(100, 116, 139, 0.1);
  color: #334155;
  border: 1px solid rgba(100, 116, 139, 0.25);
}`
  );
}

// Footer, download card meta, and winget text contrast
styleContent = styleContent.replace(
  /\.footer-copy-text \{\r?\n\s*font-size: 0\.8rem;\r?\n\s*color: var\(--text-low\);/g,
  '.footer-copy-text {\n  font-size: 0.8rem;\n  color: var(--text-med);'
);
styleContent = styleContent.replace(
  /\.download-meta \{\r?\n\s*font-size: 0\.85rem;\r?\n\s*color: var\(--text-low\);/g,
  '.download-meta {\n  font-size: 0.85rem;\n  color: var(--text-med);'
);
styleContent = styleContent.replace(
  /\.download-card-meta \{\r?\n\s*font-size: 0\.8rem;\r?\n\s*color: var\(--text-low\);/g,
  '.download-card-meta {\n  font-size: 0.8rem;\n  color: var(--text-med);'
);
styleContent = styleContent.replace(
  /\.download-winget-hint \{\r?\n\s*font-size: 0\.8rem;\r?\n\s*color: var\(--text-low\);/g,
  '.download-winget-hint {\n  font-size: 0.8rem;\n  color: var(--text-med);'
);

// Prevent image aspect-ratio distortion in flex boxes
if (!styleContent.includes('.window-title picture')) {
  styleContent += `

/* Strict 1:1 aspect ratio enforcement for brand & app icons */
.brand-logo-img,
.footer-logo-img,
.window-title img {
  aspect-ratio: 1 / 1;
  object-fit: contain;
  flex-shrink: 0;
}

.window-title picture {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.window-title picture img {
  width: 16px;
  height: 16px;
}
`;
}

fs.writeFileSync(stylePath, styleContent, 'utf8');
console.log('style.css updated successfully.');

// 2. UPDATE motion.js
const motionPath = path.join(__dirname, '..', 'docs', 'motion.js');
let motionContent = fs.readFileSync(motionPath, 'utf8');

// Replace lines 76-150 in motion.js
const oldBackgroundInitRegex = /let width = 0;[\s\S]*?const particles = \[\];/;
const newBackgroundInit = `let width = window.innerWidth || 360;
    let height = window.innerHeight || 640;
    let dpr = Math.min(window.devicePixelRatio || 1, 2);
    let animationFrameId = null;

    const isMobile = width < 768;
    const particleCount = isMobile ? 18 : 100;

    // Interactive mouse coordinates and shockwave state
    const mouse = {
      x: -1000,
      y: -1000,
      lastX: -1000,
      lastY: -1000,
      vx: 0,
      vy: 0,
      speed: 0,
      radius: isMobile ? 160 : 250,
      active: false,
    };

    // Kinetic shockwave impulses for particle reaction
    const shockwaves = [];

    function resize() {
      width = window.innerWidth || 360;
      height = window.innerHeight || 640;
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      mouse.radius = width < 768 ? 160 : 250;
      canvas.width = Math.floor(width * dpr);
      canvas.height = Math.floor(height * dpr);
      canvas.style.width = width + 'px';
      canvas.style.height = height + 'px';
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }

    resize();
    window.addEventListener('resize', resize, { passive: true });

    // Track pointer position across entire viewport with fluid velocity derivation
    function updatePointer(x, y) {
      if (mouse.x > -500 && mouse.y > -500) {
        const dx = x - mouse.x;
        const dy = y - mouse.y;
        mouse.vx = mouse.vx * 0.35 + dx * 0.65;
        mouse.vy = mouse.vy * 0.35 + dy * 0.65;
        mouse.speed = Math.sqrt(mouse.vx * mouse.vx + mouse.vy * mouse.vy);
      }
      mouse.lastX = mouse.x;
      mouse.lastY = mouse.y;
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
      mouse.vx = 0;
      mouse.vy = 0;
      mouse.speed = 0;
      mouse.active = false;
    });

    const particles = [];`;

if (oldBackgroundInitRegex.test(motionContent)) {
  motionContent = motionContent.replace(oldBackgroundInitRegex, newBackgroundInit);
  console.log('motion.js background init optimized (eliminated reflows at lines 90 & 146).');
} else {
  console.warn('Could not match oldBackgroundInitRegex in motion.js');
}

// Optimize initCardInteractivity to skip on touch devices
motionContent = motionContent.replace(
  /function initCardInteractivity\(\) \{\r?\n\s*const cards = document\.querySelectorAll\(/,
  `function initCardInteractivity() {
    if (!isFinePointer.matches) return;
    const cards = document.querySelectorAll(`
);

// Optimize pairwise relaxation in render loop to skip on mobile
motionContent = motionContent.replace(
  /\/\/ 2\. Pairwise particle relaxation: prevents particles from ever clumping or overlapping\r?\n\s*const sepDist = 38;/,
  `// 2. Pairwise particle relaxation: desktop only to keep mobile main-thread light
      if (!isMobile) {
      const sepDist = 38;`
);
motionContent = motionContent.replace(
  /p2\.vy -= fy;\r?\n\s*\}\r?\n\s*\}\r?\n\s*\}\r?\n\s*\/\/ 3\. Update and render 3D particles/,
  `p2.vy -= fy;
          }
        }
      }
      }

      // 3. Update and render 3D particles`
);

// Optimize initScrollSpy initial execution
motionContent = motionContent.replace(
  /window\.addEventListener\('scroll', updateActiveNav, \{ passive: true \}\);\r?\n\s*updateActiveNav\(\);/,
  `window.addEventListener('scroll', updateActiveNav, { passive: true });
    if (typeof requestIdleCallback === 'function') {
      requestIdleCallback(updateActiveNav);
    } else {
      setTimeout(updateActiveNav, 100);
    }`
);

fs.writeFileSync(motionPath, motionContent, 'utf8');
console.log('motion.js updated successfully.');

// 3. UPDATE HTML FILES
const htmlFiles = [
  path.join(__dirname, '..', 'docs', 'index.html'),
  path.join(__dirname, '..', 'docs', 'faq', 'index.html'),
  path.join(__dirname, '..', 'docs', 'download', 'index.html'),
  path.join(__dirname, '..', 'docs', 'docs', 'index.html'),
  path.join(__dirname, '..', 'docs', 'changelog', 'index.html'),
];

htmlFiles.forEach((file) => {
  if (!fs.existsSync(file)) return;
  let html = fs.readFileSync(file, 'utf8');

  // Add aria-label to nav-download-btn
  html = html.replace(
    /class="btn-pill btn-pill-primary nav-download-btn"/g,
    'class="btn-pill btn-pill-primary nav-download-btn" aria-label="Download Deltempo"'
  );

  // Update app_icon.webp inside picture source tags to lightweight app_icon-64.webp
  html = html.replace(
    /<source srcset="app_icon\.webp" type="image\/webp">/g,
    '<source srcset="app_icon-64.webp" type="image/webp">'
  );
  html = html.replace(
    /<source srcset="\.\.\/app_icon\.webp" type="image\/webp">/g,
    '<source srcset="../app_icon-64.webp" type="image/webp">'
  );

  // Non-blocking Google Fonts
  const fontSearch = /<link href="https:\/\/fonts\.googleapis\.com\/css2\?family=JetBrains\+Mono:wght@400;600&family=Plus\+Jakarta\+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">/;
  const fontAsync = `<link rel="preload" as="style" href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600&family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap">
  <link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600&family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet" media="print" onload="this.media='all'">
  <noscript>
    <link href="https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;600&family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap" rel="stylesheet">
  </noscript>`;

  if (fontSearch.test(html)) {
    html = html.replace(fontSearch, fontAsync);
  }

  // Specific to index.html: launch video, captions track, ai-catalog
  if (file.endsWith('docs' + path.sep + 'index.html') || file.endsWith('docs/index.html')) {
    html = html.replace(
      /id="launchVideo"\s+controls\s+preload="metadata"/g,
      'id="launchVideo" controls preload="none"'
    );
    if (!html.includes('deltempo-captions.vtt')) {
      html = html.replace(
        /<source src="deltempo\.mp4" type="video\/mp4">/g,
        '<source src="deltempo.mp4" type="video/mp4">\n                <track kind="captions" src="deltempo-captions.vtt" srclang="en" label="English" default>'
      );
    }
    // Explicit style on app icon inside window-title
    html = html.replace(
      /<img src="app_icon-64\.png" alt="App Icon" width="16" height="16">/g,
      '<img src="app_icon-64.png" alt="App Icon" width="16" height="16" style="width:16px;height:16px;aspect-ratio:1/1;">'
    );
    // Link ai-catalog.json if not present
    if (!html.includes('ai-catalog.json')) {
      html = html.replace(
        /<\/head>/,
        '  <link rel="alternate" type="application/json" href="ai-catalog.json" title="AI Agent Catalog">\n</head>'
      );
    }
  }

  fs.writeFileSync(file, html, 'utf8');
  console.log(`Updated ${path.basename(path.dirname(file))}/${path.basename(file)}`);
});
