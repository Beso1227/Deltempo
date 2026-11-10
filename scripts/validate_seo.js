#!/usr/bin/env node
/**
 * Validate the published site's search-readiness, and repair the parts of it
 * that cannot be hand-maintained: the FAQ schema and the WebPage node.
 *
 * WHY THIS EXISTS
 * ---------------
 * A site can score 100 in a Lighthouse SEO audit and still be wrong about
 * structured data. Lighthouse checks the basics a crawler needs - a title, a
 * description, a canonical, crawlable links. It does not check that the
 * FAQPage schema still matches the questions the page actually shows.
 *
 * It did not. The FAQ page carried fifteen schema questions against the
 * seventeen on the page: three questions had been reworded and vanished from
 * the schema, five new questions were missing from it, and six answers no
 * longer matched the text a reader sees. Google requires the content a
 * FAQPage describes to be present on the page; a mismatch gets the markup
 * ignored, and an ignored FAQPage is worse than none.
 *
 * So the schema is derived from the page. --fix rewrites the FAQPage node
 * from the visible questions and answers and gives every page a WebPage node
 * that agrees with its own canonical link, leaving the rest of each graph
 * untouched. Without --fix the script only checks, and exits non-zero when
 * something is wrong - which is what CI runs.
 *
 * WHAT IS CHECKED
 * ---------------
 * Per page:
 * - exactly one application/ld+json block, and it parses
 * - a WebPage node whose url is the page's own canonical link
 * - every @id the graph references is defined somewhere on the site
 * - the last breadcrumb points at that same canonical
 * - a FAQPage, where the page has questions, lists exactly the questions the
 *   page shows, with the answers it shows
 * - one H1, a title, a meta description, a canonical, a viewport, a language,
 *   an Open Graph image and a Twitter image, and an alt on every image
 *
 * Site-wide:
 * - every page in the sitemap exists, and every page on disk is in the sitemap
 * - the robots.txt allows the site and points at the sitemap
 * - no page is linked from nowhere
 *
 * USAGE
 * -----
 *   node scripts/validate_seo.js           # check only (CI)
 *   node scripts/validate_seo.js --fix     # repair the schema, then check
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');

const repoRoot = path.resolve(__dirname, '..');
const docsDir = path.join(repoRoot, 'docs');
const SITE = 'https://beso1227.github.io/Deltempo';
const SITE_URL = `${SITE}/`;
const WEBSITE_ID = `${SITE_URL}#website`;
const OG_IMAGE = `${SITE_URL}social-preview.png`;

const BREADCRUMB_NAMES = {
  '/download/': 'Download',
  '/downloads/': 'Download statistics',
  '/compare/': 'Comparison',
  '/alternatives/': 'Alternatives',
  '/docs/': 'Documentation',
  '/faq/': 'FAQ',
  '/changelog/': 'Changelog',
  '/privacy.html': 'Privacy Policy',
};

const problems = [];
const notes = [];
const fail = (where, message) => problems.push(`${where}: ${message}`);
const note = (where, message) => notes.push(`${where}: ${message}`);

function stripTags(html) {
  return String(html)
    .replace(/<[^>]+>/g, ' ')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&mdash;/g, '\u2014')
    .replace(/&ndash;/g, '\u2013')
    .replace(/&rsquo;/g, '\u2019')
    .replace(/&quot;/g, '"')
    .replace(/\s+/g, ' ')
    .trim();
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

function urlOf(file) {
  const rel = path.relative(docsDir, file).replace(/\\/g, '/');
  if (rel === 'index.html') return '/';
  if (rel.endsWith('/index.html')) return '/' + rel.slice(0, -'index.html'.length);
  return '/' + rel;
}

function metaContent(html, attr, key) {
  const re = new RegExp(`<meta[^>]*${attr}=["']${key}["'][^>]*>`, 'i');
  const tag = re.exec(html);
  if (!tag) return null;
  const m = /content=["']([^"']*)["']/i.exec(tag[0]);
  return m ? m[1] : null;
}

function linkHref(html, rel) {
  const re = new RegExp(`<link[^>]*rel=["']${rel}["'][^>]*>`, 'i');
  const tag = re.exec(html);
  if (!tag) return null;
  const m = /href=["']([^"']*)["']/i.exec(tag[0]);
  return m ? m[1] : null;
}

/**
 * The questions and answers a reader actually sees, in document order. The
 * questions live in .faq-question buttons and the answers in .faq-answer
 * blocks, one of each per question.
 */
function visibleFaq(html) {
  const questions = [...html.matchAll(/<button[^>]*class="faq-question"[^>]*>([\s\S]*?)<\/button>/g)]
    .map((m) => stripTags(m[1]));
  const answers = [...html.matchAll(/<div class="faq-answer">([\s\S]*?)<\/div>/g)]
    .map((m) => stripTags(m[1]));
  const items = [];
  for (let i = 0; i < Math.min(questions.length, answers.length); i += 1) {
    if (questions[i] && answers[i]) items.push({ question: questions[i], answer: answers[i] });
  }
  return items;
}

function ldBlocks(html) {
  return [...html.matchAll(/<script\b[^>]*type="application\/ld\+json"[^>]*>([\s\S]*?)<\/script>/g)];
}

function serialize(graph) {
  return JSON.stringify(graph, null, 2)
    .split('\n')
    .map((line, index) => (index === 0 ? line : `  ${line}`))
    .join('\r\n');
}

function fixSchema(html, file) {
  const blocks = ldBlocks(html);
  if (blocks.length !== 1) return html;

  let graph;
  try {
    graph = JSON.parse(blocks[0][1]);
  } catch {
    return html;
  }
  const isArray = Array.isArray(graph);
  const nodes = isArray ? graph : (graph['@graph'] ?? [graph]);
  const eol = html.includes('\r\n') ? '\r\n' : '\n';
  const canonical = linkHref(html, 'canonical') ?? `${SITE_URL}${urlOf(file).replace(/^\//, '')}`;
  const title = stripTags(/<title[^>]*>([\s\S]*?)<\/title>/i.exec(html)?.[1] ?? '');
  const description = metaContent(html, 'name', 'description') ?? '';
  const image = metaContent(html, 'property', 'og:image') ?? OG_IMAGE;
  let changed = false;

  // The FAQ schema must describe the questions on the page.
  const visible = visibleFaq(html);
  if (visible.length) {
    const wanted = visible.map((v) => ({
      '@type': 'Question',
      name: v.question,
      acceptedAnswer: { '@type': 'Answer', text: v.answer },
    }));
    const faqIndex = nodes.findIndex((n) => n['@type'] === 'FAQPage');
    if (faqIndex < 0) {
      nodes.push({
        '@type': 'FAQPage',
        '@id': `${canonical}#faq`,
        mainEntity: wanted,
        isPartOf: { '@id': `${canonical}#webpage` },
      });
      changed = true;
    } else {
      const current = nodes[faqIndex].mainEntity ?? [];
      const same =
        current.length === wanted.length &&
        current.every(
          (c, i) =>
            (c.name ?? '').trim() === wanted[i].name.trim() &&
            (c.acceptedAnswer?.text ?? '').trim() === wanted[i].acceptedAnswer.text.trim()
        );
      if (!same) {
        nodes[faqIndex] = {
          ...nodes[faqIndex],
          '@id': nodes[faqIndex]['@id'] ?? `${canonical}#faq`,
          mainEntity: wanted,
        };
        changed = true;
      }
    }
  }

  // Every page declares what it is: one WebPage node, agreeing with its own
  // canonical link, its own description and its own image.
  const webPageIndex = nodes.findIndex((n) => n['@type'] === 'WebPage');
  const wantedWebPage = {
    '@type': 'WebPage',
    '@id': `${canonical}#webpage`,
    url: canonical,
    name: title,
    description,
    inLanguage: 'en',
    isPartOf: { '@id': WEBSITE_ID },
    primaryImageOfPage: { '@type': 'ImageObject', url: image },
  };
  if (webPageIndex < 0) {
    nodes.push(wantedWebPage);
    changed = true;
  } else {
    const current = nodes[webPageIndex];
    const same =
      current.url === wantedWebPage.url &&
      (current.name ?? '') === wantedWebPage.name &&
      (current.description ?? '') === wantedWebPage.description;
    if (!same) {
      nodes[webPageIndex] = { ...current, ...wantedWebPage };
      changed = true;
    }
  }

  // Every page below the homepage says where it sits.
  if (urlOf(file) !== '/') {
    const crumbsIndex = nodes.findIndex((n) => n['@type'] === 'BreadcrumbList');
    const name = BREADCRUMB_NAMES[urlOf(file)] ?? 'Page';
    if (crumbsIndex < 0) {
      nodes.push({
        '@type': 'BreadcrumbList',
        '@id': `${canonical}#breadcrumb`,
        itemListElement: [
          { '@type': 'ListItem', position: 1, name: 'Home', item: SITE_URL },
          { '@type': 'ListItem', position: 2, name, item: canonical },
        ],
      });
      changed = true;
    } else {
      const last = nodes[crumbsIndex].itemListElement?.slice(-1)[0];
      if (!last || last.item !== canonical) {
        nodes[crumbsIndex] = {
          ...nodes[crumbsIndex],
          itemListElement: [
            { '@type': 'ListItem', position: 1, name: 'Home', item: SITE_URL },
            { '@type': 'ListItem', position: 2, name, item: canonical },
          ],
        };
        changed = true;
      }
    }
  }

  if (!changed) return html;

  const rebuilt = isArray ? nodes : { ...graph, '@graph': nodes };
  const replacement = `<script type="application/ld+json">${eol}${serialize(rebuilt)}${eol}</script>`;
  return html.replace(blocks[0][0], replacement);
}

function checkPage(file, html, siteIds) {
  const rel = path.relative(repoRoot, file).replace(/\\/g, '/');
  const url = urlOf(file);
  const where = rel;

  // The error page is a different kind of page: there is no URL to canonicalise,
  // nobody shares a 404, and there is nothing on it to describe with structured
  // data. What it does need is a title, a description, one heading, and a
  // request not to be indexed.
  if (url === '/404.html') {
    if (!/<title[^>]*>[^<]+<\/title>/i.test(html)) fail(where, 'no <title>');
    if (!metaContent(html, 'name', 'description')) fail(where, 'no meta description');
    if (!metaContent(html, 'name', 'viewport')) fail(where, 'no viewport meta');
    if (!/<html[^>]*lang=["'][^"']+["']/i.test(html)) fail(where, 'no lang on <html>');
    if (!/name="robots"[^>]*noindex/i.test(html)) fail(where, 'the error page must ask not to be indexed');
    const h1s = [...html.matchAll(/<h1[^>]*>/gi)];
    if (h1s.length !== 1) fail(where, `${h1s.length} H1 elements (one per page)`);
    return;
  }

  const title = /<title[^>]*>([\s\S]*?)<\/title>/i.exec(html);
  const titleText = title ? stripTags(title[1]) : '';
  if (!titleText) fail(where, 'no <title>');
  else if (titleText.length < 30 || titleText.length > 70) {
    note(where, `title is ${titleText.length} characters (30-70 reads best): ${titleText}`);
  }

  const description = metaContent(html, 'name', 'description');
  if (!description) fail(where, 'no meta description');
  else if (description.length < 70 || description.length > 165) {
    note(where, `meta description is ${description.length} characters (70-165 reads best)`);
  }

  const canonical = linkHref(html, 'canonical');
  if (!canonical) fail(where, 'no canonical link');
  else if (!canonical.startsWith('https://')) fail(where, `canonical is not https: ${canonical}`);

  if (!metaContent(html, 'name', 'viewport')) fail(where, 'no viewport meta');
  if (!/<html[^>]*lang=["'][^"']+["']/i.test(html)) fail(where, 'no lang on <html>');
  if (!metaContent(html, 'property', 'og:image')) fail(where, 'no og:image');
  if (!metaContent(html, 'name', 'twitter:image')) fail(where, 'no twitter:image');
  if (!metaContent(html, 'property', 'og:title')) fail(where, 'no og:title');
  if (!metaContent(html, 'property', 'og:description')) fail(where, 'no og:description');

  const h1s = [...html.matchAll(/<h1[^>]*>/gi)];
  if (h1s.length === 0) fail(where, 'no H1');
  else if (h1s.length > 1) fail(where, `${h1s.length} H1 elements (one per page)`);

  for (const img of [...html.matchAll(/<img[^>]*>/gi)].map((m) => m[0])) {
    if (!/\balt\s*=/i.test(img)) fail(where, `an <img> has no alt attribute: ${img.slice(0, 80)}`);
  }

  const blocks = ldBlocks(html);
  if (blocks.length === 0) {
    fail(where, 'no application/ld+json block');
    return;
  }
  if (blocks.length > 1) fail(where, `${blocks.length} ld+json blocks (one page, one graph)`);

  let graph;
  try {
    graph = JSON.parse(blocks[0][1]);
  } catch (e) {
    fail(where, `the ld+json block does not parse: ${e.message}`);
    return;
  }
  const nodes = Array.isArray(graph) ? graph : (graph['@graph'] ?? [graph]);

  for (const n of nodes) {
    for (const key of ['isPartOf', 'about', 'publisher', 'author', 'mainEntityOfPage']) {
      const v = n[key];
      if (v && typeof v === 'object' && v['@id'] && !siteIds.has(v['@id'])) {
        fail(where, `${n['@type']}.${key} points at ${v['@id']}, which no page on this site defines`);
      }
    }
  }

  const webPage = nodes.find((n) => n['@type'] === 'WebPage');
  if (!webPage) fail(where, 'the graph has no WebPage node');
  else if (canonical && webPage.url && webPage.url !== canonical) {
    fail(where, `the WebPage url (${webPage.url}) is not the canonical link (${canonical})`);
  }

  const crumbs = nodes.find((n) => n['@type'] === 'BreadcrumbList');
  if (url !== '/' && !crumbs) fail(where, 'no BreadcrumbList below the homepage');
  if (crumbs && canonical) {
    const last = crumbs.itemListElement?.[crumbs.itemListElement.length - 1];
    if (last && last.item && last.item !== canonical) {
      fail(where, `the last breadcrumb (${last.item}) is not the canonical link (${canonical})`);
    }
  }

  const visible = visibleFaq(html);
  const faq = nodes.find((n) => n['@type'] === 'FAQPage');
  if (visible.length && !faq) {
    fail(where, `the page shows ${visible.length} questions but the schema has no FAQPage`);
  } else if (faq) {
    const entities = faq.mainEntity ?? [];
    const visibleQs = visible.map((v) => v.question);
    const schemaQs = entities.map((e) => (e.name ?? '').trim());
    for (const q of schemaQs) {
      if (!visibleQs.includes(q)) fail(where, `the schema has a question the page does not show: "${q}"`);
    }
    for (const q of visibleQs) {
      if (!schemaQs.includes(q)) fail(where, `the page shows a question the schema omits: "${q}"`);
    }
    for (const e of entities) {
      const i = visibleQs.indexOf((e.name ?? '').trim());
      if (i >= 0 && visible[i] && (e.acceptedAnswer?.text ?? '').trim() !== visible[i].answer.trim()) {
        fail(where, `the schema answer for "${e.name}" is not the answer the page shows`);
      }
    }
  }
}

function main() {
  const fix = process.argv.includes('--fix');
  const files = pageFiles(docsDir);

  if (fix) {
    for (const file of files) {
      const original = fs.readFileSync(file, 'utf8');
      const updated = fixSchema(original, file);
      if (updated !== original) {
        fs.writeFileSync(file, updated, 'utf8');
        console.log(`Repaired the structured data in ${path.relative(repoRoot, file)}`);
      }
    }
  }

  // Every @id the site defines, so a cross-page reference is not a failure.
  const siteIds = new Set();
  for (const file of files) {
    for (const block of ldBlocks(fs.readFileSync(file, 'utf8'))) {
      try {
        const g = JSON.parse(block[1]);
        const nodes = Array.isArray(g) ? g : (g['@graph'] ?? [g]);
        for (const n of nodes) if (n['@id']) siteIds.add(n['@id']);
      } catch {
        // reported per page below
      }
    }
  }

  for (const file of files) {
    checkPage(file, fs.readFileSync(file, 'utf8'), siteIds);
  }

  // Sitemap coverage. The 404 page is deliberately absent from the sitemap:
  // it is an error page, it asks not to be indexed, and it is reachable only
  // through a mistyped address.
  const sitemap = fs.readFileSync(path.join(docsDir, 'sitemap.xml'), 'utf8');
  const inSitemap = new Set(
    [...sitemap.matchAll(/<loc>https:\/\/beso1227\.github\.io\/Deltempo([^<]*)<\/loc>/g)].map((m) => m[1] || '/')
  );
  for (const file of files) {
    const url = urlOf(file);
    if (url === '/404.html') {
      const html = fs.readFileSync(file, 'utf8');
      if (!/name="robots"[^>]*noindex/i.test(html)) {
        fail(rel, 'the 404 page must ask not to be indexed');
      }
      continue;
    }
    if (!inSitemap.has(url)) fail('sitemap.xml', `${url} is on disk but not in the sitemap`);
  }
  for (const url of inSitemap) {
    if (!files.some((f) => urlOf(f) === url)) fail('sitemap.xml', `${url} is in the sitemap but not on disk`);
  }

  // The AI catalog is also served from the discovery path some agent crawlers
  // look at first. The copy has to be the same file, or the two disagree.
  const wellKnown = path.join(docsDir, '.well-known', 'ai-catalog.json');
  const catalog = path.join(docsDir, 'ai-catalog.json');
  if (!fs.existsSync(wellKnown)) {
    fail('.well-known/ai-catalog.json', 'missing; agent crawlers look for the AI catalog here');
  } else if (!fs.existsSync(catalog)) {
    fail('ai-catalog.json', 'missing');
  } else if (fs.readFileSync(wellKnown, 'utf8') !== fs.readFileSync(catalog, 'utf8')) {
    fail('.well-known/ai-catalog.json', 'the copy differs from ai-catalog.json; they must be the same file');
  }

  // robots.txt
  const robots = fs.readFileSync(path.join(docsDir, 'robots.txt'), 'utf8');
  if (!/^User-agent:\s*\*$/m.test(robots)) fail('robots.txt', 'no rule for every crawler');
  if (/^Disallow:\s*\/$/m.test(robots)) fail('robots.txt', 'the whole site is disallowed');
  if (!robots.includes('Sitemap:')) fail('robots.txt', 'no Sitemap line');

  if (notes.length) {
    console.log('\nNotes (not failures):');
    for (const n of notes) console.log(`  - ${n}`);
  }

  if (problems.length) {
    console.error(`\n${problems.length} SEO problem(s):`);
    for (const p of problems) console.error(`  - ${p}`);
    process.exit(1);
  }

  console.log(`SEO checks passed for ${files.length} pages.`);
}

main();
