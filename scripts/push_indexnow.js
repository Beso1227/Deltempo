#!/usr/bin/env node
/**
 * Tell the search engines that the site changed.
 *
 * IndexNow is the open protocol for that: a site publishes a key file, and
 * posts the URLs that changed to api.indexnow.org. The key file is what proves
 * the request comes from this site, so the two have to agree - the file is read
 * from here, not hard-coded.
 *
 * WHICH URLs
 * ----------
 * The sitemap and the pages that changed in this deploy. The sitemap is the
 * one that matters for a site whose pages change on their own schedule (the
 * changelog), and the page list covers a content change.
 *
 * It is best effort by design: a search engine that is not listening, or a
 * network that is down, must never fail a deploy. The workflow step that calls
 * this is allowed to fail soft.
 *
 * USAGE
 * -----
 *   node scripts/push_indexnow.js
 *   node scripts/push_indexnow.js --dry-run
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');
const SITE = 'https://beso1227.github.io/Deltempo/';
const ENDPOINT = 'https://api.indexnow.org/indexnow';

const dryRun = process.argv.includes('--dry-run');

/** The key file is a 32-character hex name whose content is the key itself. */
function findKey() {
  for (const name of fs.readdirSync(docsDir)) {
    if (!/^[0-9a-f]{32}\.txt$/.test(name)) continue;
    const key = fs.readFileSync(path.join(docsDir, name), 'utf8').trim();
    if (key) return { file: name, key };
  }
  return null;
}

/** The pages the sitemap lists, plus the sitemap itself. */
function urls() {
  const list = [SITE + 'sitemap.xml'];
  const sitemap = fs.readFileSync(path.join(docsDir, 'sitemap.xml'), 'utf8');
  for (const m of sitemap.matchAll(/<loc>([^<]+)<\/loc>/g)) list.push(m[1]);
  return list;
}

async function main() {
  const found = findKey();
  if (!found) {
    console.error('No IndexNow key file found in docs/ (expected a 32-character hex .txt).');
    process.exit(1);
  }

  const body = {
    host: 'beso1227.github.io',
    key: found.key,
    urlList: urls(),
  };

  if (dryRun) {
    console.log(JSON.stringify(body, null, 2));
    return;
  }

  const res = await fetch(ENDPOINT, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify(body),
  });

  const text = await res.text();
  if (res.ok) {
    console.log(`IndexNow accepted ${body.urlList.length} URLs (HTTP ${res.status}).`);
  } else {
    // Not fatal: a search engine that is not listening must not fail a deploy.
    console.warn(`IndexNow answered HTTP ${res.status}: ${text.slice(0, 200)}`);
  }
}

main().catch((err) => {
  console.warn(`IndexNow could not be reached: ${err.message}`);
});
