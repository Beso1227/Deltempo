#!/usr/bin/env node
/**
 * Render the host-root files for beso1227.github.io.
 *
 * WHY THIS EXISTS
 * ---------------
 * GitHub Pages serves the domain root of an account from exactly one repository:
 * the one named `<user>.github.io`. Every other repository's site is served from
 * a sub-path - Deltempo's is `https://beso1227.github.io/Deltempo/`. So the
 * domain root needs its own repository, and that repository used to hold three
 * hand-written files that nothing regenerated.
 *
 * That arrangement drifts. The host-root robots.txt was last touched on
 * 2026-10-15 while the project's own copy kept moving; the crawler policy in
 * the two files had already diverged, and the sitemap index still advertised a
 * lastmod from October for a site whose pages changed in November. A policy
 * that only one of two files agrees with is not a policy.
 *
 * So the content lives here, in the repository that owns the site, and this
 * script renders it. The host-root repository becomes a mirror with nothing
 * hand-maintained in it, and the two copies cannot disagree.
 *
 * WHAT IS RENDERED
 * ----------------
 * robots.txt  - the domain-wide crawler policy. GitHub serves /robots.txt from
 *               the host root, so this is the file a crawler actually reads
 *               when it hits the bare domain. The per-project policy in
 *               docs/robots.txt stays where it is and keeps serving
 *               /Deltempo/robots.txt; this file is derived from it, so the
 *               allow-list for AI crawlers is written down once.
 *
 * sitemap.xml - a sitemap index pointing at the project sitemap, which is what
 *               a sitemap index is for: one entry per site on the domain. The
 *               lastmod is the newest lastmod in the project sitemap, so the
 *               index never claims a date the site did not reach.
 *
 * index.html  - a redirect to the project site, because the bare domain is not
 *               the home page a visitor is looking for.
 *
 * DETERMINISM
 * -----------
 * The output depends only on the two input files, so re-running produces
 * byte-identical results. That is what lets CI prove the committed copy is
 * current: it regenerates and diffs instead of trusting that somebody ran it.
 *
 * USAGE
 * -----
 *   node scripts/render_host_root.js
 *   node scripts/render_host_root.js --out site-root
 *   node scripts/render_host_root.js --out /tmp/mirror --check
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');

function parseArgs(argv) {
  const options = { out: path.join(repoRoot, 'site-root'), check: false };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '--out') {
      options.out = path.resolve(argv[++i]);
    } else if (arg === '--check') {
      options.check = true;
    } else {
      throw new Error(`Unknown argument: ${arg}`);
    }
  }
  return options;
}

/**
 * The project sitemap is the source of truth for where the site lives: its
 * <loc> entries carry the absolute URLs, so the domain, the project path and
 * the canonical sitemap URL all come from the file the site actually
 * publishes. Nothing here is hard-coded to a domain name.
 */
function readProjectSitemap() {
  const file = path.join(docsDir, 'sitemap.xml');
  const xml = fs.readFileSync(file, 'utf8');

  const locs = [...xml.matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1]);
  if (locs.length === 0) {
    throw new Error(`No <loc> entries found in ${file}`);
  }

  const lastmods = [...xml.matchAll(/<lastmod>([^<]+)<\/lastmod>/g)].map((m) => m[1]);
  if (lastmods.length === 0) {
    throw new Error(`No <lastmod> entries found in ${file}`);
  }

  const projectSitemapUrl = locs.find((u) => u.endsWith('/sitemap.xml'));
  if (!projectSitemapUrl) {
    // The project sitemap is a urlset of pages, so it does not list itself.
    // Its own URL is the site root plus the sitemap file: the site root comes
    // from the first page URL, whose first path segment is the repository name
    // that GitHub serves this project site under.
    const first = new URL(locs[0]);
    const segment = first.pathname.split('/').filter(Boolean)[0];
    if (!segment) {
      throw new Error(`Cannot derive the project path from ${locs[0]}`);
    }
    return finish(first.origin, `/${segment}`, lastmods);
  }

  const url = new URL(projectSitemapUrl);
  const projectPath = url.pathname.replace(/\/sitemap\.xml$/, '');
  const origin = url.origin;

  return finish(origin, projectPath, lastmods);
}

function finish(origin, projectPath, lastmods) {
  const newest = lastmods
    .map((value) => value.trim().slice(0, 10))
    .sort()
    .at(-1);

  return {
    projectSitemapUrl: `${origin}${projectPath}/sitemap.xml`,
    origin,
    projectPath,
    newest,
  };
}

/**
 * Read the project policy as a list of user-agent blocks. The project file
 * allows the machine-readable files by their project-relative path
 * (/llms.txt); on the host root those paths live under the project prefix
 * (/Deltempo/llms.txt), so they are rewritten here rather than copied.
 */
function readProjectRobots() {
  const file = path.join(docsDir, 'robots.txt');
  const lines = fs.readFileSync(file, 'utf8').split(/\r?\n/);

  const blocks = [];
  let current = null;
  for (const raw of lines) {
    const line = raw.trim();
    if (line === '' || line.startsWith('#')) continue;

    const agent = /^user-agent:\s*(.+)$/i.exec(line);
    if (agent) {
      current = { agents: [agent[1].trim()], allows: [] };
      blocks.push(current);
      continue;
    }

    const allow = /^allow:\s*(.+)$/i.exec(line);
    if (allow && current) {
      current.allows.push(allow[1].trim());
    }
  }

  if (blocks.length === 0) {
    throw new Error(`No user-agent blocks found in ${file}`);
  }
  return blocks;
}

function renderRobots(blocks, site) {
  const out = [];
  out.push('# robots.txt for the beso1227.github.io host root.');
  out.push('#');
  out.push('# This file is authoritative for the domain root. GitHub Pages serves');
  out.push('# /robots.txt from the host root only, so a crawler that arrives at the bare');
  out.push('# domain reads this one; the project policy is served from');
  out.push(`# ${site.projectPath}/robots.txt and is derived from the same source.`);
  out.push('#');
  out.push('# GENERATED by scripts/render_host_root.js - do not edit by hand.');
  out.push('# Deltempo is fully open source (MIT) and intended for indexing by search');
  out.push('# engines and answer engines alike. Nothing here is private or user-specific.');
  out.push('');

  for (const block of blocks) {
    for (const agent of block.agents) {
      out.push(`User-agent: ${agent}`);
    }
    const isCatchAll = block.agents.some((a) => a === '*');
    const seen = new Set();
    for (const rule of block.allows) {
      // The catch-all block names the machine-readable files; on the host root
      // they sit under the project prefix. The named AI agents are allowed the
      // whole domain, so their rules stay as they are.
      const value = isCatchAll && rule.startsWith('/') && !rule.startsWith(site.projectPath)
        ? `${site.projectPath}${rule}`
        : rule;
      if (seen.has(value)) continue;
      seen.add(value);
      out.push(`Allow: ${value}`);
    }
    out.push('');
  }

  out.push('# Machine-readable context is deliberately public.');
  out.push('');
  out.push(`Sitemap: ${site.origin}/sitemap.xml`);
  out.push(`Sitemap: ${site.projectSitemapUrl}`);
  out.push('');
  return out.join('\n');
}

function renderSitemapIndex(site) {
  // The namespace on the sitemapindex line is a fixed specification identifier and
  // is never fetched, so it carries the same suppression the project sitemap
  // carries in docs/sitemap.xml.
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<!-- Sitemap index for the beso1227.github.io host root: one entry per project site. -->',
    '<!-- GENERATED by scripts/render_host_root.js - do not edit by hand. -->',
    '<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><!-- DevSkim: ignore DS137138 (the sitemap namespace is a fixed specification identifier, never fetched) -->',
    '  <sitemap>',
    `    <loc>${site.projectSitemapUrl}</loc>`,
    `    <lastmod>${site.newest}</lastmod>`,
    '  </sitemap>',
    '</sitemapindex>',
    '',
  ].join('\n');
}

function renderIndex(site) {
  return [
    '<!DOCTYPE html>',
    '<html lang="en">',
    '<head>',
    '  <meta charset="UTF-8">',
    '  <meta name="viewport" content="width=device-width, initial-scale=1.0">',
    '  <!-- This host root exists to serve robots.txt and sitemap.xml for the whole',
    '       domain. It forwards visitors and crawlers to the Deltempo project site. -->',
    '  <!-- GENERATED by scripts/render_host_root.js - do not edit by hand. -->',
    '  <title>Deltempo - Open-Source Windows Cleaner &amp; Memory Optimizer</title>',
    '  <meta name="description" content="Home of Deltempo: a free, portable, open-source Windows disk cleaner, deep app uninstaller, duplicate file finder, and native NT memory optimizer. Zero telemetry.">',
    `  <link rel="canonical" href="${site.origin}${site.projectPath}/">`,
    `  <meta http-equiv="refresh" content="0; url=${site.origin}${site.projectPath}/">`,
    '  <meta name="robots" content="index, follow">',
    '  <style>',
    '    body { margin: 0; min-height: 100vh; display: grid; place-items: center;',
    '           background: #0B0F10; color: #E6F1EF;',
    '           font-family: ui-sans-serif, system-ui, "Segoe UI", sans-serif; }',
    '',
    '    main { text-align: center; padding: 2rem; }',
    '    a { color: #00F2B0; }',
    '  </style>',
    '</head>',
    '<body>',
    '  <main>',
    '    <h1>Deltempo</h1>',
    '    <p>Free, portable, open-source Windows clean &amp; memory optimizer.</p>',
    `    <p>Continue to <a href="${site.origin}${site.projectPath}/">${site.origin}${site.projectPath}/</a>.</p>`,
    '  </main>',
    '</body>',
    '</html>',
    '',
  ].join('\n');
}

function main() {
  const options = parseArgs(process.argv.slice(2));
  const site = readProjectSitemap();
  const blocks = readProjectRobots();

  const outputs = {
    'robots.txt': renderRobots(blocks, site),
    'sitemap.xml': renderSitemapIndex(site),
    'index.html': renderIndex(site),
  };

  if (options.check) {
    // The comparison ignores line endings: git checks the files out with CRLF on
    // Windows and LF everywhere else, while the canonical content is LF. Comparing
    // raw bytes would fail the check on Windows for a difference that is not one.
    const normalize = (text) => text.replace(/\r\n/g, '\n');
    const stale = [];
    for (const [name, content] of Object.entries(outputs)) {
      const file = path.join(options.out, name);
      if (!fs.existsSync(file) || normalize(fs.readFileSync(file, 'utf8')) !== normalize(content)) {
        stale.push(name);
      }
    }
    if (stale.length > 0) {
      console.error(`Host-root files are out of date: ${stale.join(', ')}`);
      console.error('Run: node scripts/render_host_root.js');
      process.exit(1);
    }
    console.log(`Host-root files are current (${Object.keys(outputs).length} files).`);
    return;
  }

  fs.mkdirSync(options.out, { recursive: true });
  for (const [name, content] of Object.entries(outputs)) {
    fs.writeFileSync(path.join(options.out, name), content, 'utf8');
    console.log(`Wrote ${path.relative(repoRoot, path.join(options.out, name))}`);
  }
}

main();
