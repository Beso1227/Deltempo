#!/usr/bin/env node
/**
 * Render the README download chart from the GitHub releases API.
 *
 * WHY THIS EXISTS
 * ---------------
 * The READMEs used to carry a shields.io badge with the download total. A badge
 * is a number, not a shape: it cannot show whether downloads are accelerating,
 * which release carried them, or how far the last release pulled away from the
 * ones before it. This script turns the same public API into a filled curve that
 * is committed to `docs/` and referenced by all nine READMEs, so the picture is
 * real data rather than decoration.
 *
 * WHAT IS PLOTTED
 * ---------------
 * The cumulative download count after each published release, oldest first, with
 * every release as a point on the curve and its own download count underneath.
 * The curve is monotone, so it only ever rises - a falling line would mean the
 * GitHub counts went backwards, which is a bug, not a story.
 *
 * Downloads are counted across every asset of a release, which is exactly what
 * shields.io's `github/downloads` total counts, so the headline number and the
 * curve never disagree.
 *
 * OUTPUT
 * ------
 * docs/downloads-chart-light.svg and docs/downloads-chart-dark.svg. Two files
 * because a README cannot restyle one image per theme; each carries a palette
 * that reads on GitHub's light and dark backgrounds. No <style>, no <script>,
 * no external fonts or images: GitHub renders SVGs through its camo proxy, which
 * strips all of those anyway.
 *
 * USAGE
 * -----
 *   node scripts/generate_downloads_chart.js
 *   node scripts/generate_downloads_chart.js --input releases.json   # offline
 *
 * GITHUB_TOKEN is used when present (higher API rate limit in CI).
 */

'use strict';

const fs = require('node:fs');
const path = require('node:path');

const REPO = 'Beso1227/Deltempo';
const ROOT = path.resolve(__dirname, '..');
const OUT = {
  light: path.join(ROOT, 'docs', 'downloads-chart-light.svg'),
  dark: path.join(ROOT, 'docs', 'downloads-chart-dark.svg'),
};

const THEMES = {
  light: {
    text: '#16212B',
    muted: '#6B7A8F',
    grid: '#16212B',
    gridOpacity: 0.14,
    curve: '#8B5CF6',
    area: '#8B5CF6',
    marker: '#F59E0B',
    dotFill: '#FFFFFF',
  },
  dark: {
    text: '#E6EDF3',
    muted: '#8B98A9',
    grid: '#E6EDF3',
    gridOpacity: 0.16,
    curve: '#A78BFA',
    area: '#8B5CF6',
    marker: '#FBBF24',
    dotFill: '#0D1117',
  },
};

// Geometry, in the 1000x300 viewBox. LEFT/RIGHT are inset far enough that the
// first and last release labels, which are centred on their point, stay inside
// the canvas instead of being cut in half.
const W = 1000;
const H = 300;
const LEFT = 58;
const RIGHT = 942;
const HEADER_BASELINE = 50;
const CAPTION_BASELINE = 68;
const PLOT_TOP = 84;
const BASELINE = 236;
const TAG_BASELINE = 258;
const DATE_BASELINE = 274;

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

function escapeXml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function round(value) {
  return Math.round(value * 10) / 10;
}

function formatCount(value) {
  return new Intl.NumberFormat('en-US').format(value);
}

function formatDate(iso) {
  const date = new Date(iso);
  return `${date.getUTCDate()} ${MONTHS[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
}

async function fetchReleases() {
  const headers = {
    accept: 'application/vnd.github+json',
    'user-agent': 'deltempo-downloads-chart',
  };
  if (process.env.GITHUB_TOKEN) {
    headers.authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
  }

  const all = [];
  for (let page = 1; page <= 10; page += 1) {
    const url = `https://api.github.com/repos/${REPO}/releases?per_page=100&page=${page}`;
    const response = await fetch(url, { headers });
    if (!response.ok) {
      throw new Error(`GitHub API returned ${response.status} ${response.statusText} for ${url}`);
    }
    const batch = await response.json();
    if (!Array.isArray(batch)) {
      throw new Error('GitHub API returned an unexpected payload');
    }
    all.push(...batch);
    if (batch.length < 100) break;
  }
  return all;
}

/** Published, non-draft releases, oldest first, with their total asset downloads. */
function summarize(releases) {
  return releases
    .filter((release) => !release.draft && release.published_at)
    .map((release) => ({
      tag: release.tag_name,
      published: release.published_at,
      downloads: (release.assets || []).reduce(
        (sum, asset) => sum + (Number(asset.download_count) || 0),
        0,
      ),
    }))
    .sort((a, b) => new Date(a.published) - new Date(b.published));
}

function toPoints(releases) {
  let running = 0;
  return releases.map((release) => {
    running += release.downloads;
    return { ...release, cumulative: running };
  });
}

/**
 * Fritsch-Carlson monotone cubic interpolation. A plain Catmull-Rom spline
 * overshoots between unevenly sized steps, which on a cumulative curve draws a
 * dip that never happened. The monotone variant cannot overshoot, so the shape
 * stays honest while still being a smooth curve rather than a polyline.
 */
function monotoneSegments(points) {
  const n = points.length;
  if (n < 2) return '';

  const dx = [];
  const slope = [];
  for (let i = 0; i < n - 1; i += 1) {
    dx[i] = points[i + 1].x - points[i].x;
    slope[i] = (points[i + 1].y - points[i].y) / dx[i];
  }

  const tangent = new Array(n);
  tangent[0] = slope[0];
  tangent[n - 1] = slope[n - 2];
  for (let i = 1; i < n - 1; i += 1) {
    if (slope[i - 1] * slope[i] <= 0) {
      tangent[i] = 0;
    } else {
      const w1 = 2 * dx[i] + dx[i - 1];
      const w2 = dx[i] + 2 * dx[i - 1];
      tangent[i] = (w1 + w2) / (w1 / slope[i - 1] + w2 / slope[i]);
    }
  }

  let segments = '';
  for (let i = 0; i < n - 1; i += 1) {
    const c1x = points[i].x + dx[i] / 3;
    const c1y = points[i].y + (tangent[i] * dx[i]) / 3;
    const c2x = points[i + 1].x - dx[i] / 3;
    const c2y = points[i + 1].y - (tangent[i + 1] * dx[i]) / 3;
    segments += ` C ${round(c1x)} ${round(c1y)} ${round(c2x)} ${round(c2y)} ${round(points[i + 1].x)} ${round(points[i + 1].y)}`;
  }
  return segments;
}

function render(series, themeName) {
  const theme = THEMES[themeName];
  const total = series.length === 0 ? 0 : series[series.length - 1].cumulative;
  const peak = Math.max(total, 1);

  const span = RIGHT - LEFT;
  const step = series.length > 1 ? span / (series.length - 1) : 0;
  const points = series.map((entry, index) => ({
    ...entry,
    x: LEFT + step * index,
    y: BASELINE - (entry.cumulative / peak) * (BASELINE - PLOT_TOP),
  }));

  const segments = points.length > 1 ? monotoneSegments(points) : '';
  const first = points.length > 0 ? points[0] : null;
  const last = points.length > 0 ? points[points.length - 1] : null;

  const curve = first ? `M ${round(first.x)} ${round(first.y)}${segments}` : '';
  const area = first
    ? `M ${round(first.x)} ${BASELINE} L ${round(first.x)} ${round(first.y)}${segments} L ${round(last.x)} ${BASELINE} Z`
    : '';

  const latest = series.length > 0 ? series[series.length - 1] : null;

  const gridlines = [0.33, 0.66].map((fraction) => {
    const y = round(BASELINE - fraction * (BASELINE - PLOT_TOP));
    return `<line x1="${LEFT}" y1="${y}" x2="${RIGHT}" y2="${y}" stroke="${theme.grid}" stroke-opacity="${theme.gridOpacity}" stroke-width="1" stroke-dasharray="2 6" />`;
  });

  const guides = points
    .slice(0, -1)
    .map(
      (point) =>
        `<line x1="${round(point.x)}" y1="${round(point.y)}" x2="${round(point.x)}" y2="${BASELINE}" stroke="${theme.grid}" stroke-opacity="${theme.gridOpacity * 0.7}" stroke-width="1" stroke-dasharray="2 6" />`,
    );

  const dots = points.map((point, index) => {
    const isLatest = index === points.length - 1;
    return `<circle cx="${round(point.x)}" cy="${round(point.y)}" r="${isLatest ? 5.5 : 4}" fill="${theme.dotFill}" stroke="${theme.curve}" stroke-width="${isLatest ? 3 : 2.5}" />`;
  });

  const marker =
    points.length > 0
      ? `<line x1="${round(points[points.length - 1].x)}" y1="${PLOT_TOP}" x2="${round(points[points.length - 1].x)}" y2="${BASELINE}" stroke="${theme.marker}" stroke-width="2.5" stroke-opacity="0.9" />`
      : '';

  const floor = `<line x1="${LEFT}" y1="${BASELINE}" x2="${RIGHT}" y2="${BASELINE}" stroke="${theme.grid}" stroke-opacity="${theme.gridOpacity * 1.4}" stroke-width="1" />`;

  const labels = points
    .map((point, index) => {
      const isLatest = index === points.length - 1;
      return (
        `<text x="${round(point.x)}" y="${TAG_BASELINE}" text-anchor="middle" font-size="11" font-weight="${isLatest ? 700 : 400}" fill="${theme.text}" fill-opacity="${isLatest ? 1 : 0.72}">${escapeXml(point.tag)}</text>` +
        `<text x="${round(point.x)}" y="${DATE_BASELINE}" text-anchor="middle" font-size="9.5" fill="${theme.muted}">${escapeXml(formatDate(point.published))}</text>`
      );
    })
    .join('');

  // The per-release count sits above its point, so the flat early stretch reads
  // as "almost nobody downloaded those" rather than as a broken chart. The last
  // release is already spelled out in the top-right annotation.
  const perRelease = points
    .slice(0, -1)
    .map(
      (point) =>
        `<text x="${round(point.x)}" y="${round(point.y) - 11}" text-anchor="middle" font-size="9.5" font-weight="600" fill="${theme.muted}">${formatCount(point.downloads)}</text>`,
    )
    .join('\n    ');

  const title = `${formatCount(total)} downloads across ${series.length} release${series.length === 1 ? '' : 's'}`;
  const description = latest
    ? `Cumulative Deltempo downloads after each GitHub release, ${formatDate(series[0].published)} to ${formatDate(latest.published)}. The latest release, ${latest.tag}, has ${formatCount(latest.downloads)} downloads.`
    : 'No published releases yet.';

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" role="img" aria-labelledby="chart-title chart-desc" data-total="${total}" data-releases="${series.length}">
  <title id="chart-title">${escapeXml(title)}</title>
  <desc id="chart-desc">${escapeXml(description)}</desc>
  <defs>
    <linearGradient id="area" x1="0" y1="${PLOT_TOP}" x2="0" y2="${BASELINE}" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="${theme.area}" stop-opacity="0.42" />
      <stop offset="1" stop-color="${theme.area}" stop-opacity="0.03" />
    </linearGradient>
  </defs>
  <g font-family="'Segoe UI', -apple-system, BlinkMacSystemFont, 'Helvetica Neue', Arial, sans-serif">
    <text x="${LEFT}" y="${HEADER_BASELINE}" font-size="46" font-weight="700" fill="${theme.text}" letter-spacing="-1">${formatCount(total)}</text>
    <text x="${LEFT}" y="${CAPTION_BASELINE}" font-size="10" font-weight="600" fill="${theme.muted}" letter-spacing="1.9">TOTAL DOWNLOADS &#183; ${series.length} RELEASE${series.length === 1 ? '' : 'S'}</text>
    <text x="${RIGHT}" y="${HEADER_BASELINE}" text-anchor="end" font-size="13" font-weight="700" fill="${theme.text}" fill-opacity="0.85">${latest ? escapeXml(latest.tag) : ''}</text>
    <text x="${RIGHT}" y="${CAPTION_BASELINE}" text-anchor="end" font-size="10" font-weight="600" fill="${theme.muted}" letter-spacing="1.4">${latest ? `${formatCount(latest.downloads)} IN THIS RELEASE` : ''}</text>
    ${gridlines.join('\n    ')}
    ${floor}
    ${guides.join('\n    ')}
    ${area ? `<path d="${area}" fill="url(#area)" />` : ''}
    ${curve ? `<path d="${curve}" fill="none" stroke="${theme.curve}" stroke-width="4" stroke-linecap="round" stroke-linejoin="round" />` : ''}
    ${marker}
    ${dots.join('\n    ')}
    ${perRelease}
    ${labels}
  </g>
</svg>
`;
}

async function main() {
  const inputIndex = process.argv.indexOf('--input');
  const releases =
    inputIndex > -1
      ? JSON.parse(fs.readFileSync(process.argv[inputIndex + 1], 'utf8'))
      : await fetchReleases();

  const series = toPoints(summarize(releases));
  if (series.length === 0) {
    throw new Error('No published releases to plot; refusing to overwrite the chart with an empty one.');
  }

  const total = series[series.length - 1].cumulative;

  // GitHub counts only ever rise. A lower total means a truncated API response,
  // not news, so keep the chart that is already published.
  if (fs.existsSync(OUT.light)) {
    const previous = fs.readFileSync(OUT.light, 'utf8').match(/data-total="(\d+)"/);
    if (previous && total < Number(previous[1])) {
      console.log(
        `Refusing to write: the API reports ${total} downloads but the chart already shows ${previous[1]}.`,
      );
      return;
    }
  }

  fs.mkdirSync(path.dirname(OUT.light), { recursive: true });
  fs.writeFileSync(OUT.light, render(series, 'light'), 'utf8');
  fs.writeFileSync(OUT.dark, render(series, 'dark'), 'utf8');
  console.log(
    `Wrote ${path.relative(ROOT, OUT.light)} and ${path.relative(ROOT, OUT.dark)}: ${formatCount(total)} downloads, ${series.length} releases.`,
  );
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
