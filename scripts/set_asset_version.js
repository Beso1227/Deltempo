#!/usr/bin/env node
/**
 * Set the site's asset version across every file that carries it.
 *
 * WHY
 * ---
 * The "version" in "?v=3.9.46" is a cache-buster, not a release number: it
 * changes whenever an asset's bytes change, so that a browser and a crawler
 * both fetch the new file instead of the copy they already have. That only
 * works if every reference agrees - a page asking for "motion.js?v=3.9.45"
 * while the service worker precaches "motion.js?v=3.9.46" means the two never
 * meet, and a page whose icon links say 3.9.46 while its stylesheet says
 * 3.9.45 is two different sites wearing one name.
 *
 * The version therefore lives in exactly one place: this script. Every file
 * that mentions it is rewritten together, and CI proves they agree.
 *
 * USAGE
 * -----
 *   node scripts/set_asset_version.js            print the current version
 *   node scripts/set_asset_version.js 3.9.47    set it
 *   node scripts/set_asset_version.js --check    fail if the files disagree
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');

/**
 * The version query the site's own assets carry.
 *
 * A file that is not one of the site's assets keeps its own version: the
 * download page's script is versioned separately, because it is a different
 * file with its own release history.
 */
const SITE_ASSETS = /(style\.css|motion\.js|manifest\.json|sw\.js|app_icon\.png|favicon[\w-]*\.png|favicon\.ico|apple-touch-icon\.png)\?v=\d+\.\d+\.\d+/g;

/**
 * The version query, for reading what a file currently declares.
 */
const VERSION_RE = /\?v=\d+\.\d+\.\d+/g;

/**
 * Compare two texts without being confused by line endings.
 *
 * Git checks this repository out with CRLF on Windows and LF everywhere else.
 * The version lives in the text, not in the line endings, so the comparison
 * has to ignore them - otherwise a file that is correct on one platform
 * looks stale on the other.
 */
function sameText(a, b) {
  return a.replace(/\r\n/g, '\n') === b.replace(/\r\n/g, '\n');
}

/** The files that carry the version, and the version they must agree on. */
function versionedFiles() {
  const files = [];

  const pages = [];
  const walk = (dir) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) {
        if (!entry.name.startsWith('.')) walk(full);
      } else if (entry.name.endsWith('.html')) {
        pages.push(full);
      }
    }
  };
  walk(docsDir);
  files.push(...pages);

  // The service worker precaches the same assets, so it carries the same
  // version - in the cache name and in each precache entry.
  files.push(path.join(docsDir, 'sw.js'));

  return files;
}

/**
 * Read the version. The service worker's cache name carries it first, so that
 * is the one that decides; the pages are checked against it.
 */
function readVersion(files) {
  const sw = fs.readFileSync(path.join(docsDir, 'sw.js'), 'utf8');
  const m = /deltempo-cache-v(\d+\.\d+\.\d+)/.exec(sw);
  return m ? m[1] : null;
}

function main() {
  const files = versionedFiles();
  const current = readVersion(files);
  if (!current) {
    console.error('Could not read the asset version from docs/sw.js.');
    process.exit(1);
  }

  const arg = process.argv[2];
  const check = arg === '--check';

  if (check) {
    const stale = [];
    for (const file of files) {
      const text = fs.readFileSync(file, 'utf8');
      const versions = [...text.matchAll(SITE_ASSETS)].map((m) => m[0]);
      const others = [...new Set(versions.filter((v) => !v.endsWith(`?v=${current}`)))];
      if (others.length > 0) {
        stale.push(`${path.relative(repoRoot, file)}: ${others.join(', ')}`);
      }
    }
    if (stale.length > 0) {
      console.error(`The asset version is not ${current} everywhere:`);
      for (const line of stale) console.error(`  ${line}`);
      console.error('Run: node scripts/set_asset_version.js');
      process.exit(1);
    }
    console.log(`The asset version is ${current} everywhere (${files.length} files).`);
    return;
  }

  if (!arg) {
    console.log(current);
    return;
  }

  if (!/^\d+\.\d+\.\d+$/.test(arg)) {
    console.error(`"${arg}" is not a version of the form 1.2.3`);
    process.exit(1);
  }

  let changed = 0;
  for (const file of files) {
    const text = fs.readFileSync(file, 'utf8');
    const updated = text
      .replace(SITE_ASSETS, (match) => match.replace(VERSION_RE, `?v=${arg}`))
      .replace(/deltempo-cache-v\d+\.\d+\.\d+/, `deltempo-cache-v${arg}`);
    if (sameText(updated, text)) continue;
    fs.writeFileSync(file, updated, 'utf8');
    console.log(`${path.relative(repoRoot, file)}: version set to ${arg}`);
    changed += 1;
  }

  console.log(`The asset version is ${arg} (${changed} file(s) changed).`);
}

main();
