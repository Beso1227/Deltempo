#!/usr/bin/env node
/**
 * Declare the machine-readable context a page is covered by.
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
 * The catalog is announced the same way. The AI Catalog specification gives
 * its own link relation, "ai-catalog", for a document that lists the agent
 * artifacts a site publishes.
 *
 * USAGE
 * -----
 *   node scripts/update_agent_links.js          rewrite the pages
 *   node scripts/update_agent_links.js --check  fail if any page is stale
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');
const check = process.argv.includes('--check');

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
  return [
    `<link rel="describedby" href="${p('llms.txt')}" title="llms.txt - the LLM-readable context for Deltempo">`,
    `<link rel="alternate" type="text/plain" href="${p('llms.txt')}" title="llms.txt - LLM context for Deltempo">`,
    `<link rel="alternate" type="text/plain" href="${p('llms-full.txt')}" title="llms-full.txt - the complete LLM-readable context for Deltempo">`,
    `<link rel="alternate" type="application/json" href="${p('ai-catalog.json')}" title="AI Agent Catalog">`,
  ].map((line) => indent + line);
}

function main() {
  const problems = [];
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

    const block = agentLinks(prefix, indent).join('\n') + '\n';
    const updated = html.slice(0, start) + block + html.slice(end);

    if (updated === html) continue;

    if (check) {
      problems.push(`${relative}: the machine-readable link declarations are stale`);
      continue;
    }

    fs.writeFileSync(file, updated, 'utf8');
    console.log(`${relative}: machine-readable links updated`);
    changed += 1;
  }

  if (problems.length > 0) {
    console.error('The machine-readable link declarations are out of date:');
    for (const problem of problems) console.error(`  ${problem}`);
    console.error('Run: node scripts/update_agent_links.js');
    process.exit(1);
  }

  console.log(`The machine-readable link declarations are current (${check ? 'checked' : changed + ' page(s) updated'}).`);
}

main();
