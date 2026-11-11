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
    // The host root publishes the machine-readable files at its own root as
    // well, so the catch-all has to allow them there too. Without this an
    // agent that reads the domain root's robots.txt is told not to fetch the
    // very files it came there for.
    if (isCatchAll) {
      for (const { to } of HOST_ROOT_CONTEXT) {
        const value = `/${to}`;
        if (seen.has(value)) continue;
        seen.add(value);
        out.push(`Allow: ${value}`);
      }
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

/**
 * The site's asset version, read from the service worker's cache name - the
 * same place scripts/set_asset_version.js writes it. The icon URLs in the
 * host root carry it so that a changed icon is a changed URL and a search
 * engine refetches it instead of showing the copy it already has.
 */
function readAssetVersion() {
  const sw = fs.readFileSync(path.join(docsDir, 'sw.js'), 'utf8');
  const m = /deltempo-cache-v(\d+\.\d+\.\d+)/.exec(sw);
  if (!m) {
    throw new Error('Could not read the asset version from docs/sw.js.');
  }
  return m[1];
}

/**
 * The icons the host root publishes.
 *
 * The host root is the page a search engine reads to find the site's icon,
 * so it needs the icon files at its own root as well as the declarations in
 * its HTML. They are copies of the project's icons: one artwork, generated
 * once, published in both places.
 */
const HOST_ROOT_ICONS = [
  'favicon.ico',
  'favicon-32.png',
  'favicon-48.png',
  'favicon-64.png',
  'app_icon-192.png',
  'apple-touch-icon.png',
];

/**
 * The machine-readable files the host root publishes.
 *
 * llms.txt is the file an AI agent reads to learn what a site is and where
 * its documentation lives, and the llms.txt specification says it may sit at
 * any path, covering the pages under that path. It is published at BOTH the
 * project path and the origin root, because an agent that arrives at the bare
 * domain looks there first - and the bare domain is where Google finds the
 * favicon too.
 *
 * ai-catalog.json is the same product metadata in machine-readable form, and
 * it is published at the origin root as well so that an agent looking for the
 * site catalog finds it.
 *
 * NOT published at the origin root: /.well-known/ai-catalog.json. That path
 * belongs to the AI Catalog specification, which requires a document with
 * specVersion and entries - a catalog of agent artifacts such as MCP servers
 * and A2A agents. Deltempo publishes no such artifact, and the file here is a
 * schema.org SoftwareApplication description. Serving it at that reserved
 * path would make Lighthouse's ard-schema audit fail - measured, not assumed:
 * putting a schema.org document at the origin root's well-known path took
 * Agentic Browsing from 100 to 67. A file at a path that means "an agent
 * catalog" has to be an agent catalog.
 */
const HOST_ROOT_CONTEXT = [
  { from: 'llms.txt', to: 'llms.txt' },
  { from: 'llms-full.txt', to: 'llms-full.txt' },
  { from: 'ai-catalog.json', to: 'ai-catalog.json' },
];

function renderIndex(site, version) {
  return [
    '<!DOCTYPE html>',
    '<html lang="en">',
    '<head>',
    '  <meta charset="UTF-8">',
    '  <meta name="viewport" content="width=device-width, initial-scale=1.0">',
    '  <!-- This host root exists to serve robots.txt and sitemap.xml for the whole',
    '       domain, and to be the page a search engine reads to find the site\'s',
    '       icon. It forwards visitors and crawlers to the Deltempo project site. -->',
    '  <!-- GENERATED by scripts/render_host_root.js - do not edit by hand. -->',
    '  <title>Deltempo - Open-Source Windows Cleaner &amp; Memory Optimizer</title>',
    '  <meta name="description" content="Home of Deltempo: a free, portable, open-source Windows disk cleaner, deep app uninstaller, duplicate file finder, and native NT memory optimizer. Zero telemetry.">',
    `  <link rel="canonical" href="${site.origin}${site.projectPath}/">`,
    `  <meta http-equiv="refresh" content="0; url=${site.origin}${site.projectPath}/">`,
    '  <meta name="robots" content="index, follow">',
    '',
    '  <!-- The icon. Google shows one favicon per hostname and finds it by',
    '       crawling the home page of that hostname, which for this domain is',
    '       this page. Without these the search engine keeps whatever it',
    '       cached for the hostname - which is how the site ended up showing',
    '       the logo it had before the rebrand. The URLs carry the site\'s asset',
    '       version, so a changed icon is a changed URL and is refetched. -->',
    `  <link rel="icon" type="image/x-icon" href="${site.origin}${site.projectPath}/favicon.ico?v=${version}">`,
    `  <link rel="icon" type="image/png" sizes="32x32" href="${site.origin}${site.projectPath}/favicon-32.png?v=${version}">`,
    `  <link rel="icon" type="image/png" sizes="48x48" href="${site.origin}${site.projectPath}/favicon-48.png?v=${version}">`,
    `  <link rel="icon" type="image/png" sizes="64x64" href="${site.origin}${site.projectPath}/favicon-64.png?v=${version}">`,
    `  <link rel="icon" type="image/png" sizes="192x192" href="${site.origin}${site.projectPath}/app_icon-192.png?v=${version}">`,
    `  <link rel="apple-touch-icon" sizes="180x180" href="${site.origin}${site.projectPath}/apple-touch-icon.png?v=${version}">`,
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

/**
 * The host root's 404 page.
 *
 * WHY IT EXISTS
 * -------------
 * Without a 404 page of its own, GitHub Pages answers a request for a file
 * the host root does not have with its own generic error page - and it
 * answers that request with a 503, not a 404. That was measured: 35 of 100
 * requests for a missing path on the origin root came back 503, the rest
 * came back 404.
 *
 * It matters because an agent that finds a path does not expect is not the
 * only thing that notices. The ARD audit probes "/.well-known/ard.json" on
 * the origin root and reads a 503 as "the server is broken" rather than "the
 * file is not here", which fails Lighthouse's agentic-browsing category at
 * random - the score moved between 100 and 75 from one run to the next with
 * nothing on the site changing.
 *
 * A 404 page makes the answer unambiguous: the file is not here, and the
 * server is fine. The page itself sends the visitor to the project site,
 * because a mistyped address on this domain is almost always meant to be an
 * address on the project site.
 */
function renderNotFound(site) {
  return [
    '<!DOCTYPE html>',
    '<html lang="en">',
    '<head>',
    '  <meta charset="UTF-8">',
    '  <meta name="viewport" content="width=device-width, initial-scale=1.0">',
    '  <!-- GENERATED by scripts/render_host_root.js - do not edit by hand. -->',
    '  <title>Page not found - Deltempo</title>',
    '  <meta name="robots" content="noindex">',
    `  <link rel="canonical" href="${site.origin}${site.projectPath}/">`,
    '  <style>',
    '    body { margin: 0; min-height: 100vh; display: grid; place-items: center;',
    '           background: #0B0F10; color: #E6F1EF;',
    '           font-family: ui-sans-serif, system-ui, "Segoe UI", sans-serif; }',
    '',
    '    main { text-align: center; padding: 2rem; }',
    '    a { color: #00F2B0; }',
    '    h1 { font-size: 1.6rem; margin-bottom: 0.5rem; }',
    '    p { color: #9FB3B0; }',
    '  </style>',
    '</head>',
    '<body>',
    '  <main>',
    '    <h1>Page not found</h1>',
    '    <p>That address is not on this site.</p>',
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
  const version = readAssetVersion();

  const outputs = {
    'robots.txt': renderRobots(blocks, site),
    'sitemap.xml': renderSitemapIndex(site),
    'index.html': renderIndex(site, version),
    '404.html': renderNotFound(site),
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
    // The icons are compared byte for byte: they are copied, not re-encoded, so
    // the only thing that can differ is that the copy is out of date.
    for (const name of HOST_ROOT_ICONS) {
      const source = path.join(docsDir, name);
      const copy = path.join(options.out, name);
      if (!fs.existsSync(copy) || !fs.existsSync(source)
          || !fs.readFileSync(copy).equals(fs.readFileSync(source))) {
        stale.push(name);
      }
    }
    // The machine-readable files are copies too, so the same applies.
    for (const { from, to } of HOST_ROOT_CONTEXT) {
      const source = path.join(docsDir, from);
      const copy = path.join(options.out, to);
      if (!fs.existsSync(copy) || !fs.existsSync(source)
          || !fs.readFileSync(copy).equals(fs.readFileSync(source))) {
        stale.push(to);
      }
    }
    if (stale.length > 0) {
      console.error(`Host-root files are out of date: ${stale.join(', ')}`);
      console.error('Run: node scripts/render_host_root.js');
      process.exit(1);
    }
    const total = Object.keys(outputs).length + HOST_ROOT_ICONS.length + HOST_ROOT_CONTEXT.length;
    console.log(`Host-root files are current (${total} files).`);
    return;
  }

  fs.mkdirSync(options.out, { recursive: true });
  for (const [name, content] of Object.entries(outputs)) {
    fs.writeFileSync(path.join(options.out, name), content, 'utf8');
    console.log(`Wrote ${path.relative(repoRoot, path.join(options.out, name))}`);
  }
  for (const name of HOST_ROOT_ICONS) {
    fs.copyFileSync(path.join(docsDir, name), path.join(options.out, name));
    console.log(`Copied ${path.relative(repoRoot, path.join(docsDir, name))}`);
  }
  for (const { from, to } of HOST_ROOT_CONTEXT) {
    const target = path.join(options.out, to);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.copyFileSync(path.join(docsDir, from), target);
    console.log(`Copied ${path.relative(repoRoot, path.join(docsDir, from))}`);
  }
}

main();
