#!/usr/bin/env node
/**
 * Build the published site: inline the stylesheet into every page.
 *
 * WHY
 * ---
 * The stylesheet was a render-blocking request. On a phone-sized connection
 * that is the second round trip before anything can be painted, and PageSpeed
 * measured it at 260 ms of the mobile budget. A stylesheet that arrives with
 * the document removes the wait entirely - and unlike loading it
 * asynchronously, it cannot flash unstyled content, because there is no moment
 * in which the styles are missing.
 *
 * The trade is bytes: the stylesheet is now part of every page instead of a
 * file the browser can fetch once. Compressed, that is roughly 30 KB a page.
 * The pages are the point of the site and most visits read one of them.
 *
 * The stylesheet's own URLs are relative to the stylesheet's directory
 * (fonts/...), so they are rewritten to absolute paths before inlining: an
 * inlined rule on /faq/index.html would otherwise look for /faq/fonts/... .
 *
 * The repository keeps the readable, unbuilt sources. This runs in the deploy
 * workflow, on the copy that is published.
 *
 * USAGE
 * -----
 *   node scripts/build_site.js
 *   node scripts/build_site.js --check    # report what it would do, change nothing
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');
const SITE_ROOT = '/Deltempo';
const check = process.argv.includes('--check');

/** Every page loads the same stylesheet; the query string busts the cache. */
const STYLESHEET_RE = /<link[^>]*rel=["']stylesheet["'][^>]*href=["']([^"']+)["'][^>]*>/i;

function pageFiles(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!entry.name.startsWith('.')) pageFiles(full, out);
    } else if (entry.name.endsWith('.html')) {
      out.push(full);
    }
  }
  return out;
}

/**
 * Make the stylesheet's own relative URLs absolute. A data: URL or an
 * absolute URL is left alone.
 */
function absolutise(css) {
  return css.replace(/url\(\s*(['"]?)(?!data:)(?!https?:)(?!\/)([^'")]+)\1\s*\)/gi, (match, quote, target) => {
    const clean = target.trim();
    // A fragment reference inside an inline SVG (url(%23filter)) points at
    // something in the same document, not at a file.
    if (clean.startsWith('/') || clean.startsWith('#') || clean.startsWith('%')) return match;
    return `url(${quote}${SITE_ROOT}/${clean}${quote})`;
  });
}

function build() {
  const cssFile = path.join(docsDir, 'style.css');
  if (!fs.existsSync(cssFile)) {
    console.error('docs/style.css is missing; nothing to inline.');
    process.exit(1);
  }
  const css = absolutise(fs.readFileSync(cssFile, 'utf8'));

  let inlined = 0;
  let skipped = 0;

  for (const file of pageFiles(docsDir)) {
    const html = fs.readFileSync(file, 'utf8');
    const link = STYLESHEET_RE.exec(html);
    if (!link) {
      skipped += 1;
      continue;
    }
    const eol = html.includes('\r\n') ? '\r\n' : '\n';
    const style = `<style>${eol}${css}${eol}</style>`;
    const updated = html.replace(link[0], style);
    if (check) {
      console.log(`${path.relative(repoRoot, file)}: would inline the stylesheet`);
    } else {
      fs.writeFileSync(file, updated, 'utf8');
      console.log(`${path.relative(repoRoot, file)}: stylesheet inlined`);
    }
    inlined += 1;
  }

  console.log(`${inlined} page(s) ${check ? 'would be' : 'were'} built, ${skipped} skipped.`);
}

build();
