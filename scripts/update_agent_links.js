#!/usr/bin/env node
/**
 * Declare the machine-readable context a page is covered by, and version the
 * URLs that point at it.
 *
 * WHY
 * ---
 * The llms.txt specification says a page should announce the file that covers
 * it, using the "describedby" link relation: the agent reads the page, sees
 * which llms.txt describes it, and fetches that. Without the relation an
 * agent has to guess the path - it usually guesses the site root, and for a
 * project published under a sub-path of a shared host the site root is a
 * different site entirely.
 *
 * WHY THE URLS CARRY A VERSION
 * ----------------------------
 * A crawler that has already read llms.txt keeps the copy it read. The only
 * thing that tells it the copy is stale is a URL that changed - the same
 * lesson the favicon work established, where the artwork was redrawn and the
 * URL stayed the same, so the search engine kept showing the old mark.
 *
 * These files change far more often than a release does - the documentation
 * moves on its own schedule - so the version is derived from the files'
 * contents rather than taken from the release number. It moves exactly when
 * they move, and nothing has to remember to bump it.
 *
 * The version is stamped on every URL that points at the context: the links
 * in the pages, the link in llms.txt to the fuller context, and the catalog's
 * pointers back to the context files. A change to any one of them changes
 * all of them, so an agent that follows the links cannot end up mixing a
 * fresh page with a stale context.
 *
 * The catalog is published twice - at the project path and at the discovery
 * path - and the two copies must stay byte-identical, so they are rewritten
 * together.
 *
 * USAGE
 * -----
 *   node scripts/update_agent_links.js          rewrite the pages and context
 *   node scripts/update_agent_links.js --check  fail if any of them is stale
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const agentContext = require('./agent_context');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');
const check = process.argv.includes('--check');

/**
 * The version of the machine-readable context.
 *
 * It is computed from the files themselves, so it cannot drift from what they
 * actually contain.
 */
const VERSION = agentContext.computeContextVersion(docsDir);

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
 * The relations a page declares, in the order they should appear.
 *
 * "describedby" points at the llms.txt that covers the page. "alternate"
 * points at the files themselves, with the media type each one is served as.
 *
 * There is deliberately no "ai-catalog" relation here. That relation tells an
 * agent the document is an AI Catalog - a manifest of agent artifacts such as
 * MCP servers and A2A agents, with specVersion and an entries array. The
 * catalog this site publishes is a schema.org SoftwareApplication description
 * of the product, and advertising it under the catalog relation makes agents
 * and Lighthouse's ard-schema audit try to read it as an AI Catalog and fail
 * on it - measured, not assumed: adding the relation took Agentic Browsing
 * from 100 to 75. A file at a path that means one thing has to be that thing.
 */
function agentLinks(prefix, indent) {
  const p = (file) => `${prefix}${file}`;
  const v = (file) => `${p(file)}?v=${VERSION}`;
  return [
    `<link rel="describedby" href="${v('llms.txt')}" title="llms.txt - the LLM-readable context for Deltempo">`,
    `<link rel="alternate" type="text/plain" href="${v('llms.txt')}" title="llms.txt - LLM context for Deltempo">`,
    `<link rel="alternate" type="text/plain" href="${v('llms-full.txt')}" title="llms-full.txt - the complete LLM-readable context for Deltempo">`,
    `<link rel="alternate" type="application/json" href="${v('ai-catalog.json')}" title="AI Agent Catalog">`,
  ].map((line) => indent + line);
}

/**
 * The line ending the file already uses.
 *
 * Git checks this repository out with CRLF on Windows and LF everywhere else,
 * so a script that writes LF unconditionally leaves a Windows checkout with
 * mixed line endings and then reports the file it just wrote as stale. The
 * file's own ending is used instead, so the script leaves the file exactly as
 * it found it apart from the lines it means to change.
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
 * out on Windows has CRLF and the same file checked out on Linux has LF, and
 * the committed content is the same either way.
 */
function sameText(a, b) {
  return a.replace(/\r\n/g, '\n') === b.replace(/\r\n/g, '\n');
}

/**
 * Rewrite the pages: the block of machine-readable declarations.
 */
function rewritePages(problems) {
  let changed = 0;

  for (const file of pageFiles(docsDir)) {
    const html = fs.readFileSync(file, 'utf8');
    const relative = path.relative(docsDir, file).replace(/\\/g, '/');

    // The error page is not indexed and describes nothing; the relations would
    // only point a reader at files for a page they cannot reach.
    if (path.basename(file) === '404.html') continue;

    // The existing declarations, which the new ones replace.
    const existing = [...html.matchAll(/^[ \t]*<link[^>]+rel="(?:alternate|describedby|ai-catalog)"[^>]*>[ \t]*\r?\n?/gm)];
    if (existing.length === 0) {
      problems.push(`${relative}: no machine-readable link declarations found`);
      continue;
    }

    const indentMatch = existing[0][0].match(/^[ \t]*/);
    const indent = indentMatch ? indentMatch[0] : '  ';

    // The path from the page to the site root.
    const depth = relative.split('/').length - 1;
    const prefix = depth === 0 ? '' : '../'.repeat(depth);

    const start = existing[0].index;
    const end = existing[existing.length - 1].index + existing[existing.length - 1][0].length;

    const eol = lineEnding(html);
    const block = agentLinks(prefix, indent).join(eol) + eol;
    const updated = html.slice(0, start) + block + html.slice(end);

    if (sameText(updated, html)) continue;

    if (check) {
      problems.push(`${relative}: the machine-readable link declarations are stale`);
      continue;
    }

    fs.writeFileSync(file, updated, 'utf8');
    console.log(`${relative}: machine-readable links updated`);
    changed += 1;
  }

  return changed;
}

/**
 * Rewrite the context files: every URL inside them that points at another
 * context file.
 */
function rewriteContextFiles(problems) {
  let changed = 0;

  for (const name of agentContext.CONTEXT_FILES) {
    const file = path.join(docsDir, name);
    if (!fs.existsSync(file)) {
      problems.push(`${name}: missing; the machine-readable context is incomplete`);
      continue;
    }

    const text = fs.readFileSync(file, 'utf8');
    const updated = agentContext.applyVersionToContextUrls(text, VERSION);

    if (sameText(updated, text)) continue;

    if (check) {
      problems.push(`${name}: the links to the machine-readable context are stale`);
      continue;
    }

    fs.writeFileSync(file, updated, 'utf8');
    console.log(`${name}: context links updated`);
    changed += 1;
  }

  // The catalog is published a second time at the discovery path. The two
  // copies must be the same document, so they are rewritten the same way.
  const wellKnown = path.join(docsDir, '.well-known', 'ai-catalog.json');
  if (fs.existsSync(wellKnown)) {
    const text = fs.readFileSync(wellKnown, 'utf8');
    const updated = agentContext.applyVersionToContextUrls(text, VERSION);
    if (!sameText(updated, text)) {
      if (check) {
        problems.push('.well-known/ai-catalog.json: the links to the machine-readable context are stale');
      } else {
        fs.writeFileSync(wellKnown, updated, 'utf8');
        console.log('.well-known/ai-catalog.json: context links updated');
        changed += 1;
      }
    }
  }

  return changed;
}

function main() {
  const problems = [];
  const changed = rewritePages(problems) + rewriteContextFiles(problems);

  if (problems.length > 0) {
    console.error('The machine-readable links are out of date:');
    for (const problem of problems) console.error(`  ${problem}`);
    console.error('Run: node scripts/update_agent_links.js');
    process.exit(1);
  }

  console.log(
    `The machine-readable links are current (context version ${VERSION}; ` +
    `${check ? 'checked' : changed + ' file(s) updated'}).`
  );
}

main();
