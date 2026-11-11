#!/usr/bin/env node
/**
 * Rewrite the favicon declarations in every published page.
 *
 * WHY
 * ---
 * Three things were wrong with the old declarations, and each one is a
 * reason a search engine or a phone can end up showing the wrong picture:
 *
 * 1. THE URL NEVER CHANGED. When the artwork was redrawn in September the
 *    filenames stayed the same, so a search engine that had already fetched
 *    one of those URLs kept the bytes it had cached under it. Google's own
 *    documentation says the favicon it shows is fetched from the URL in the
 *    page and is keyed by hostname, and that it is only refreshed when the
 *    page is recrawled - so the URL is the cache key. The site's convention
 *    is already "?v=<version>" for exactly this reason, and the icons were
 *    the one asset family left out of it.
 *
 * 2. THE APPLE-TOUCH-ICON WAS A WEBP. Apple does not accept WebP for
 *    apple-touch-icon, so iOS fell back to a screenshot of the page.
 *
 * 3. THE PAGES DECLARED DIFFERENT SETS. The home page had four icons and a
 *    manifest link; privacy.html had one; 404.html had one, at an absolute
 *    path. A crawler comparing pages, or a browser picking the largest
 *    candidate, could land on a different file on each page.
 *
 * Every page now declares the same set, from the same source, with the same
 * version query:
 *
 *   favicon.ico          the multi-frame icon (all browsers)
 *   favicon-32.png       the size a browser tab actually asks for
 *   favicon-48.png       the size a bookmark list asks for
 *   favicon-64.png       the size a high-density desktop asks for
 *   app_icon-192.png     the size a PWA installer asks for
 *   apple-touch-icon.png the size iOS asks for (PNG, as iOS requires)
 *
 * The manifest link stays where it is - it is a different concern, and the
 * pages that have it have always had it in their own place - but its version
 * query is brought up to date with the rest.
 *
 * USAGE
 * -----
 *   node scripts/update_icon_links.js          rewrite the pages
 *   node scripts/update_icon_links.js --check  fail if any page is stale
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');
const check = process.argv.includes('--check');

/**
 * The site's asset version. It is a cache-buster, not a release number: it
 * changes whenever an asset's bytes change, so that a browser and a crawler
 * both fetch the new file instead of the copy they already have.
 *
 * It is read from the service worker's cache name, which is where
 * scripts/set_asset_version.js writes it, so that the icons cannot drift away
 * from the rest of the site's assets.
 */
const VERSION = (() => {
  const sw = fs.readFileSync(path.join(docsDir, 'sw.js'), 'utf8');
  const m = /deltempo-cache-v(\d+\.\d+\.\d+)/.exec(sw);
  if (!m) {
    console.error('Could not read the asset version from docs/sw.js.');
    process.exit(1);
  }
  return m[1];
})();

/**
 * The icons, in the order a page should declare them. The prefix is the path
 * from the page to the site root: empty for the home page, "../" for a page
 * one directory down.
 */
function iconLinks(prefix, indent) {
  const p = (file) => `${prefix}${file}`;
  return [
    `<link rel="icon" type="image/x-icon" href="${p('favicon.ico')}?v=${VERSION}">`,
    `<link rel="icon" type="image/png" sizes="32x32" href="${p('favicon-32.png')}?v=${VERSION}">`,
    `<link rel="icon" type="image/png" sizes="48x48" href="${p('favicon-48.png')}?v=${VERSION}">`,
    `<link rel="icon" type="image/png" sizes="64x64" href="${p('favicon-64.png')}?v=${VERSION}">`,
    `<link rel="icon" type="image/png" sizes="192x192" href="${p('app_icon-192.png')}?v=${VERSION}">`,
    `<link rel="apple-touch-icon" sizes="180x180" href="${p('apple-touch-icon.png')}?v=${VERSION}">`,
  ].map((line) => indent + line);
}

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
 * Find the contiguous run of icon links in a page.
 *
 * Only the icon family is matched. The manifest link is deliberately not part
 * of it: the manifest is a separate concern that lives in its own place in
 * each page, and the font preloads and the stylesheet link sit between the
 * icons and the manifest on some pages.
 */
function findIconRun(html) {
  const linkRe = /^[ \t]*<link[^>]+rel="(?:icon|apple-touch-icon)"[^>]*>[ \t]*\r?\n?/gm;
  const matches = [...html.matchAll(linkRe)];
  if (matches.length === 0) return null;

  const first = matches[0];
  const last = matches[matches.length - 1];

  const indentMatch = first[0].match(/^[ \t]*/);
  const indent = indentMatch ? indentMatch[0] : '  ';

  return { start: first.index, end: last.index + last[0].length, indent };
}

/**
 * The line ending the file already uses.
 *
 * Git checks this repository out with CRLF on Windows and LF everywhere else,
 * so a script that writes LF unconditionally leaves a Windows checkout with
 * mixed line endings, and then reports the file it just wrote as stale on the
 * next run. The file's own ending is used instead, so the script leaves the
 * file exactly as it found it apart from the lines it means to change.
 */
function lineEnding(text) {
  const crlf = (text.match(/\r\n/g) || []).length;
  const lf = (text.match(/\n/g) || []).length - crlf;
  return crlf > lf ? '\r\n' : '\n';
}

/**
 * Compare two texts without being confused by line endings.
 *
 * The comparison has to be this forgiving for the same reason: a file checked
 * out on Linux has LF and the same file checked out on Windows has CRLF, and
 * the committed content is the same either way.
 */
function sameText(a, b) {
  return a.replace(/\r\n/g, '\n') === b.replace(/\r\n/g, '\n');
}

/**
 * Bring the manifest link's version query up to date, wherever the page
 * declares it. The link is matched on its own so that nothing around it
 * moves.
 */
function updateManifestVersion(html) {
  return html.replace(
    /(<link[^>]+rel="manifest"[^>]+href="[^"?]+)(\?v=[^"]*)?(")/i,
    (match, head, version, tail) => `${head}?v=${VERSION}${tail}`
  );
}

function main() {
  const problems = [];
  let changed = 0;

  for (const file of pageFiles(docsDir)) {
    const html = fs.readFileSync(file, 'utf8');
    const relative = path.relative(docsDir, file);

    const run = findIconRun(html);
    if (!run) {
      problems.push(`${relative}: no icon declarations found`);
      continue;
    }

    // A page one directory down refers to the site's root with "../"; the
    // home page refers to it with nothing. The 404 page is served at any
    // depth under the site, so it must use an absolute path.
    const isErrorPage = path.basename(file) === '404.html';
    const depth = relative.split(path.sep).length - 1;
    const prefix = isErrorPage ? '/Deltempo/' : depth === 0 ? '' : '../'.repeat(depth);

    const block = iconLinks(prefix, run.indent).join(lineEnding(html)) + lineEnding(html);
    let updated = html.slice(0, run.start) + block + html.slice(run.end);
    updated = updateManifestVersion(updated);

    if (sameText(updated, html)) continue;

    if (check) {
      problems.push(`${relative}: icon declarations are stale`);
      continue;
    }

    fs.writeFileSync(file, updated, 'utf8');
    console.log(`${relative}: icon declarations updated`);
    changed += 1;
  }

  if (problems.length > 0) {
    console.error('Icon declarations are out of date:');
    for (const problem of problems) console.error(`  ${problem}`);
    console.error('Run: node scripts/update_icon_links.js');
    process.exit(1);
  }

  console.log(`Icon declarations are current (${check ? 'checked' : changed + ' page(s) updated'}).`);
}

main();
