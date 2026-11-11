#!/usr/bin/env node
/**
 * Set each sitemap entry's lastmod to the date its page last changed.
 *
 * WHY
 * ---
 * lastmod is a claim to a search engine: "this URL's content last changed
 * on this date". A search engine uses it to decide how often to recrawl. Two
 * ways to get it wrong, and the site had both:
 *
 * - Claiming a date that is too old. Every page changed on 2026-11-11 (the
 *   icons were regenerated and the declarations rewritten) while the sitemap
 *   still said 2026-11-05, so the engine had no signal that anything moved.
 * - Claiming a date that is too new. A date in the future, or one that moves
 *   forward for a change that did not touch the page, teaches the engine that
 *   the dates are noise, and it stops trusting them.
 *
 * The date comes from git: the last commit that touched the page. That is
 * the day the page's bytes changed, which is exactly what lastmod claims.
 *
 * USAGE
 * -----
 *   node scripts/update_sitemap_lastmod.js            rewrite the sitemap
 *   node scripts/update_sitemap_lastmod.js --check    fail if any is stale
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const { execFileSync } = require('node:child_process');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');
const sitemapFile = path.join(docsDir, 'sitemap.xml');
const check = process.argv.includes('--check');

/**
 * The date git last touched a file, as YYYY-MM-DD.
 */
function lastCommitDate(file) {
  const out = execFileSync('git', ['log', '-1', '--format=%ad', '--date=short', '--', file], {
    cwd: repoRoot,
    encoding: 'utf8',
  }).trim();
  if (!out) {
    throw new Error(`No commit history for ${file}`);
  }
  return out;
}

function main() {
  const xml = fs.readFileSync(sitemapFile, 'utf8');

  // Each <url> block pairs a <loc> with a <lastmod>. The loc gives the page's
  // path, which maps back to the file in docs/.
  const blocks = [...xml.matchAll(/<url>[\s\S]*?<\/url>/g)].map((m) => m[0]);
  if (blocks.length === 0) {
    console.error('No <url> entries found in docs/sitemap.xml');
    process.exit(1);
  }

  const stale = [];
  const replacements = [];

  for (const block of blocks) {
    const loc = /<loc>([^<]+)<\/loc>/.exec(block);
    const lastmod = /<lastmod>([^<]+)<\/lastmod>/.exec(block);
    if (!loc || !lastmod) {
      replacements.push(block);
      continue;
    }

    // The URL is absolute: https://beso1227.github.io/Deltempo/<path>
    const url = new URL(loc[1]);
    const sitePath = url.pathname.replace(/^\/Deltempo\//, '');
    const local = path.join(docsDir, sitePath === '' ? 'index.html' : sitePath);
    if (!fs.existsSync(local)) {
      throw new Error(`${loc[1]} is in the sitemap but ${local} does not exist`);
    }

    const actual = lastCommitDate(path.relative(repoRoot, local));
    if (actual === lastmod[1]) {
      replacements.push(block);
      continue;
    }

    stale.push(`${sitePath || 'index.html'}: ${lastmod[1]} -> ${actual}`);
    replacements.push(block.replace(`<lastmod>${lastmod[1]}</lastmod>`, `<lastmod>${actual}</lastmod>`));
  }

  if (stale.length === 0) {
    console.log(`The sitemap's lastmod dates are current (${blocks.length} URLs).`);
    return;
  }

  if (check) {
    console.error('The sitemap\'s lastmod dates are stale:');
    for (const line of stale) console.error(`  ${line}`);
    console.error('Run: node scripts/update_sitemap_lastmod.js');
    process.exit(1);
  }

  // Replace the blocks in order. The blocks were matched in the order they
  // appear, so walking the string and swapping each one in turn leaves
  // everything around them exactly where it was.
  let rebuilt = '';
  let cursor = 0;
  let index = 0;
  for (const match of xml.matchAll(/<url>[\s\S]*?<\/url>/g)) {
    rebuilt += xml.slice(cursor, match.index) + replacements[index];
    cursor = match.index + match[0].length;
    index += 1;
  }
  rebuilt += xml.slice(cursor);

  fs.writeFileSync(sitemapFile, rebuilt, 'utf8');
  console.log(`Updated ${stale.length} lastmod date(s):`);
  for (const line of stale) console.log(`  ${line}`);
}

main();
