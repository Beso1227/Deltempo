/*
 * Live download chart for https://beso1227.github.io/Deltempo/downloads/
 *
 * Reads the public GitHub Releases API, sums the download_count of every asset
 * on every published release, and draws the cumulative curve (or per-release
 * bars) in SVG. The same numbers are rendered as an accessible table.
 *
 * There is no analytics here. The page asks GitHub for public counters and
 * sends nothing about the visitor. If the API is rate-limited or unreachable,
 * it falls back to `downloads.json`, the snapshot the Pages deploy writes from
 * the same API.
 *
 * GitHub's counters are not a realtime firehose: the API refreshes them on a
 * short delay, so the page re-checks every two minutes while the tab is
 * visible instead of pretending a socket exists.
 */
(function () {
  'use strict';

  var REPO = 'Beso1227/Deltempo';
  var API_ROOT = 'https://api.github.com/repos/' + REPO + '/releases';
  var SNAPSHOT_URL = '../downloads.json';
  var POLL_MS = 120000;
  var MANUAL_COOLDOWN_MS = 20000;
  var RELATIVE_TICK_MS = 5000;

  var SVG_NS = 'http://www.w3.org/2000/svg'; // DevSkim: ignore DS137138 (SVG XML namespace - a fixed spec identifier, never fetched)

  var numberFormat = new Intl.NumberFormat('en-US');
  var dateFormat = new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });
  var timeFormat = new Intl.DateTimeFormat(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });

  var el = {
    total: document.getElementById('dlTotal'),
    releases: document.getElementById('dlReleases'),
    latestValue: document.getElementById('dlLatestValue'),
    latestLabel: document.getElementById('dlLatestLabel'),
    newSince: document.getElementById('dlNewSince'),
    status: document.getElementById('dlStatus'),
    statusText: document.getElementById('dlStatusText'),
    updated: document.getElementById('dlUpdated'),
    refresh: document.getElementById('dlRefresh'),
    liveStatus: document.getElementById('dlLiveStatus'),
    notice: document.getElementById('dlNotice'),
    chart: document.getElementById('dlChart'),
    chartContent: document.getElementById('dlChartContent'),
    chartTitle: document.getElementById('dlChartTitle'),
    chartDesc: document.getElementById('dlChartDesc'),
    chartSub: document.getElementById('dlChartSub'),
    chartHeading: document.getElementById('dlChartHeading'),
    chartStage: document.getElementById('dlChartStage'),
    tooltip: document.getElementById('dlTooltip'),
    tooltipTag: document.getElementById('dlTooltipTag'),
    tooltipDate: document.getElementById('dlTooltipDate'),
    tooltipRelease: document.getElementById('dlTooltipRelease'),
    tooltipCumulative: document.getElementById('dlTooltipCumulative'),
    tooltipShare: document.getElementById('dlTooltipShare'),
    tableBody: document.getElementById('dlTableBody'),
    tableSummary: document.getElementById('dlTableSummary'),
    modeButtons: Array.prototype.slice.call(document.querySelectorAll('[data-chart-mode]')),
  };

  var state = {
    releases: [],
    mode: 'cumulative',
    source: 'none',
    fetchedAt: 0,
    snapshotAt: 0,
    total: 0,
    baseline: null,
    lastError: null,
    hasData: false,
    loading: false,
    lastAttempt: 0,
    online: navigator.onLine !== false,
    timer: null,
    tickTimer: null,
    geometry: null,
    activeIndex: -1,
    pinned: false,
    resizeTimer: null,
  };

  function formatCount(value) {
    return numberFormat.format(Math.max(0, Math.round(Number(value) || 0)));
  }

  function formatDate(iso) {
    var date = new Date(iso);
    if (isNaN(date.getTime())) return 'Unknown date';
    return dateFormat.format(date);
  }

  function formatFileDate(iso) {
    var date = new Date(iso);
    if (isNaN(date.getTime())) return 'unknown date';
    return timeFormat.format(date) + ' on ' + dateFormat.format(date);
  }

  function plural(value, one, many) {
    return value === 1 ? one : many;
  }

  function niceStep(raw) {
    if (!isFinite(raw) || raw <= 0) return 1;
    var exponent = Math.floor(Math.log10(raw));
    var fraction = raw / Math.pow(10, exponent);
    var multiplier = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
    return multiplier * Math.pow(10, exponent);
  }

  function axisFor(maxValue) {
    var target = Math.max(1, maxValue);
    var step = niceStep(target / 5);
    var top = Math.ceil(target / step) * step;
    if (top === target) top += step;
    var ticks = [];
    for (var value = 0; value <= top + step * 0.001; value += step) {
      ticks.push(value);
    }
    return { top: top, ticks: ticks };
  }

  function clamp(value, low, high) {
    return Math.max(low, Math.min(high, value));
  }

  function svgElement(name, attributes) {
    var node = document.createElementNS(SVG_NS, name);
    if (attributes) {
      Object.keys(attributes).forEach(function (key) {
        node.setAttribute(key, attributes[key]);
      });
    }
    return node;
  }

  function textElement(attributes, value) {
    var node = svgElement('text', attributes);
    node.textContent = value;
    return node;
  }

  function normalizeDownloads(value) {
    var number = Number(value);
    return isFinite(number) && number > 0 ? Math.floor(number) : 0;
  }

  function normalizeApiRelease(release) {
    var assets = Array.isArray(release.assets) ? release.assets : [];
    return {
      tag: String(release.tag_name || release.name || 'unnamed'),
      published: String(release.published_at || ''),
      downloads: assets.reduce(function (sum, asset) {
        return sum + normalizeDownloads(asset.download_count);
      }, 0),
      assets: assets.map(function (asset) {
        return {
          name: String(asset.name || 'asset'),
          downloads: normalizeDownloads(asset.download_count),
        };
      }),
    };
  }

  function sortAndValidate(releases) {
    return releases
      .filter(function (release) {
        return release.published && !isNaN(new Date(release.published).getTime());
      })
      .sort(function (a, b) {
        return new Date(a.published) - new Date(b.published);
      });
  }

  function normalizeApiPayload(rows) {
    if (!Array.isArray(rows)) throw new Error('Unexpected GitHub API payload');
    var releases = sortAndValidate(
      rows
        .filter(function (release) {
          return release && !release.draft && release.published_at;
        })
        .map(normalizeApiRelease),
    );
    if (releases.length === 0) throw new Error('No published releases');
    return releases;
  }

  function normalizeSnapshot(payload) {
    if (!payload || !Array.isArray(payload.releases)) {
      throw new Error('Snapshot is missing its release list');
    }
    var releases = sortAndValidate(
      payload.releases.map(function (release) {
        return {
          tag: String(release.tag || 'unnamed'),
          published: String(release.published || ''),
          downloads: normalizeDownloads(release.downloads),
          assets: Array.isArray(release.assets)
            ? release.assets.map(function (asset) {
                return {
                  name: String(asset.name || 'asset'),
                  downloads: normalizeDownloads(asset.downloads),
                };
              })
            : [],
        };
      }),
    );
    if (releases.length === 0) throw new Error('Snapshot has no published releases');
    return releases;
  }

  function withCumulative(releases) {
    var running = 0;
    return releases.map(function (release) {
      running += release.downloads;
      return Object.assign({}, release, { cumulative: running });
    });
  }

  async function fetchApiReleases() {
    var all = [];
    for (var page = 1; page <= 10; page += 1) {
      var response = await fetch(API_ROOT + '?per_page=100&page=' + page, {
        method: 'GET',
        mode: 'cors',
        credentials: 'omit',
        cache: 'no-cache',
        headers: { accept: 'application/vnd.github+json' },
      });
      if (!response.ok) {
        throw new Error('GitHub API returned ' + response.status);
      }
      var batch = await response.json();
      if (!Array.isArray(batch)) throw new Error('Unexpected GitHub API payload');
      all = all.concat(batch);
      if (batch.length < 100) break;
    }
    return normalizeApiPayload(all);
  }

  async function fetchSnapshot() {
    var response = await fetch(SNAPSHOT_URL, {
      method: 'GET',
      mode: 'same-origin',
      credentials: 'omit',
      cache: 'no-store',
    });
    if (!response.ok) throw new Error('Snapshot returned ' + response.status);
    var payload = await response.json();
    return {
      releases: normalizeSnapshot(payload),
      generatedAt: payload.generatedAt ? String(payload.generatedAt) : '',
    };
  }

  function setLiveMessage(message) {
    if (!el.liveStatus) return;
    el.liveStatus.textContent = '';
    window.setTimeout(function () {
      el.liveStatus.textContent = message;
    }, 60);
  }

  function applyData(releases, source, snapshotAt) {
    var previousTotal = state.total;
    var hadData = state.hasData;
    var series = withCumulative(releases);
    var total = series.length ? series[series.length - 1].cumulative : 0;

    state.releases = series;
    state.total = total;
    state.source = source;
    state.snapshotAt = snapshotAt || 0;
    state.fetchedAt = Date.now();
    state.hasData = true;
    if (state.baseline === null) state.baseline = total;

    renderStats();
    renderChart();
    renderTable();
    renderStatus();

    if (hadData && total > previousTotal) {
      setLiveMessage(
        '+' + formatCount(total - previousTotal) + ' ' +
          plural(total - previousTotal, 'download', 'downloads') +
          ' since the last check. ' + formatCount(total) + ' in total.',
      );
    } else if (!hadData) {
      setLiveMessage(
        'Live download data loaded: ' + formatCount(total) + ' ' +
          plural(total, 'download', 'downloads') + ' across ' +
          series.length + ' ' + plural(series.length, 'release', 'releases') + '.',
      );
    }
  }

  function renderStats() {
    var latest = state.releases.length ? state.releases[state.releases.length - 1] : null;
    if (el.total) el.total.textContent = formatCount(state.total);
    if (el.releases) el.releases.textContent = formatCount(state.releases.length);
    if (el.latestValue) el.latestValue.textContent = latest ? formatCount(latest.downloads) : '—';
    if (el.latestLabel) {
      el.latestLabel.textContent = latest ? 'LATEST · ' + latest.tag : 'LATEST RELEASE';
    }
    if (el.newSince) {
      var delta = Math.max(0, state.total - (state.baseline || 0));
      el.newSince.textContent = delta > 0 ? '+' + formatCount(delta) : '0';
      el.newSince.classList.toggle('is-positive', delta > 0);
    }
  }

  function renderStatus() {
    var status = 'live';
    var label = 'Live · GitHub API';
    var note = '';

    if (!state.hasData) {
      status = state.loading ? 'checking' : 'error';
      label = state.loading ? 'Connecting…' : (state.online ? 'Unable to load' : 'Offline');
    } else if (!state.online) {
      status = 'error';
      label = 'Offline · showing last data';
    } else if (state.lastError && state.source === 'snapshot') {
      status = 'snapshot';
      label = 'Snapshot · live API unavailable';
      note = 'GitHub\u2019s public API is rate-limited or unreachable from this browser. ' +
        'Showing the snapshot published with the site' +
        (state.snapshotAt ? ' on ' + formatFileDate(state.snapshotAt) : '') +
        '. It refreshes on the next site deploy.';
    } else if (state.lastError) {
      status = 'stale';
      label = 'Reconnecting… · last check failed';
      note = 'The last GitHub API check failed. Still showing data from ' +
        (state.fetchedAt ? timeFormat.format(new Date(state.fetchedAt)) : 'the last successful check') +
        '; the page retries automatically.';
    } else if (state.source === 'snapshot') {
      status = 'snapshot';
      label = 'Snapshot · live API unavailable';
      note = 'Showing the snapshot published with the site' +
        (state.snapshotAt ? ' on ' + formatFileDate(state.snapshotAt) : '') + '.';
    }

    if (el.status) el.status.setAttribute('data-state', status);
    if (el.statusText) el.statusText.textContent = label;
    if (el.notice) {
      el.notice.textContent = note;
      el.notice.hidden = !note;
    }
    updateUpdatedLabel();
    updateRefreshButton();
  }

  function relativeTime(milliseconds) {
    var seconds = Math.max(0, Math.round(milliseconds / 1000));
    if (seconds < 10) return 'just now';
    if (seconds < 60) return seconds + 's ago';
    var minutes = Math.floor(seconds / 60);
    if (minutes < 60) return minutes + 'm ago';
    var hours = Math.floor(minutes / 60);
    if (hours < 24) return hours + 'h ago';
    return Math.floor(hours / 24) + 'd ago';
  }

  function updateUpdatedLabel() {
    if (!el.updated) return;
    if (!state.hasData) {
      el.updated.textContent = state.loading ? 'Reading the public releases API…' : 'No data yet';
      return;
    }
    if (!state.online) {
      el.updated.textContent = 'Offline · last data ' + relativeTime(Date.now() - state.fetchedAt);
      return;
    }
    el.updated.textContent = 'Checked ' + relativeTime(Date.now() - state.fetchedAt) +
      ' · refreshes every 2 minutes';
  }

  function updateRefreshButton() {
    if (!el.refresh) return;
    var cooldown = Math.max(0, state.lastAttempt + MANUAL_COOLDOWN_MS - Date.now());
    var busy = state.loading;
    el.refresh.disabled = busy || cooldown > 0;
    el.refresh.classList.toggle('is-loading', busy);
    el.refresh.title = busy
      ? 'Checking GitHub now'
      : cooldown > 0
        ? 'Next manual check in ' + Math.ceil(cooldown / 1000) + 's'
        : 'Check GitHub now';
  }

  function renderChart() {
    if (!el.chartContent) return;
    if (!state.hasData) {
      el.chartContent.replaceChildren();
      return;
    }

    var series = state.releases;
    var mode = state.mode;
    var stageWidth = el.chartStage ? el.chartStage.clientWidth || 900 : 900;
    var compact = stageWidth < 620;
    var viewW = compact ? 560 : 1000;
    var viewH = compact ? 430 : 400;
    var plotLeft = compact ? 64 : 76;
    var plotRight = viewW - (compact ? 14 : 28);
    var plotTop = compact ? 24 : 30;
    var plotBottom = viewH - (compact ? 104 : 82);
    var values = series.map(function (release) {
      return mode === 'cumulative' ? release.cumulative : release.downloads;
    });
    var axis = axisFor(Math.max.apply(null, values));
    var plotWidth = plotRight - plotLeft;
    var plotHeight = plotBottom - plotTop;
    var pointX = [];
    var pointY = [];

    el.chart.setAttribute('viewBox', '0 0 ' + viewW + ' ' + viewH);

    series.forEach(function (release, index) {
      var x = series.length === 1
        ? plotLeft + plotWidth / 2
        : plotLeft + (plotWidth * index) / (series.length - 1);
      pointX.push(x);
      pointY.push(plotBottom - (values[index] / axis.top) * plotHeight);
    });

    state.geometry = {
      xs: pointX,
      ys: pointY,
      values: values,
      axisTop: axis.top,
      plotWidth: plotWidth,
      width: viewW,
      height: viewH,
    };

    var fragment = document.createDocumentFragment();

    var gradient = svgElement('linearGradient', {
      id: 'dlAreaGradient',
      x1: '0',
      y1: plotTop,
      x2: '0',
      y2: plotBottom,
      gradientUnits: 'userSpaceOnUse',
    });
    gradient.appendChild(svgElement('stop', { offset: '0', 'stop-color': '#00F2B0', 'stop-opacity': '0.38' }));
    gradient.appendChild(svgElement('stop', { offset: '1', 'stop-color': '#00F2B0', 'stop-opacity': '0.02' }));
    var barGradient = svgElement('linearGradient', {
      id: 'dlBarGradient',
      x1: '0',
      y1: '0',
      x2: '0',
      y2: '1',
    });
    barGradient.appendChild(svgElement('stop', { offset: '0', 'stop-color': '#00F2B0', 'stop-opacity': '0.95' }));
    barGradient.appendChild(svgElement('stop', { offset: '1', 'stop-color': '#8B5CF6', 'stop-opacity': '0.35' }));
    var defs = svgElement('defs');
    defs.appendChild(gradient);
    defs.appendChild(barGradient);
    fragment.appendChild(defs);

    // Horizontal grid and y-axis ticks.
    var grid = svgElement('g', { class: 'dl-grid' });
    axis.ticks.forEach(function (tick) {
      var y = plotBottom - (tick / axis.top) * plotHeight;
      grid.appendChild(svgElement('line', {
        x1: plotLeft,
        y1: y,
        x2: plotRight,
        y2: y,
      }));
    });
    var floor = svgElement('line', {
      x1: plotLeft,
      y1: plotBottom,
      x2: plotRight,
      y2: plotBottom,
      class: 'dl-floor',
    });
    grid.appendChild(floor);
    fragment.appendChild(grid);

    var axisLabels = svgElement('g', { class: 'dl-axis' });
    axis.ticks.forEach(function (tick) {
      var y = plotBottom - (tick / axis.top) * plotHeight;
      axisLabels.appendChild(textElement({
        x: plotLeft - 12,
        y: y + 4,
        'text-anchor': 'end',
      }, formatCount(tick)));
    });
    fragment.appendChild(axisLabels);

    var highlight = svgElement('g', { class: 'dl-highlight', hidden: 'hidden' });
    var crosshair = svgElement('line', {
      class: 'dl-crosshair',
      x1: plotLeft,
      y1: plotTop,
      x2: plotLeft,
      y2: plotBottom,
    });
    highlight.appendChild(crosshair);
    fragment.appendChild(highlight);

    if (mode === 'cumulative') {
      var pathData = monotonePath(pointX, pointY);
      var areaData = 'M ' + round(pointX[0]) + ' ' + plotBottom +
        ' L ' + round(pointX[0]) + ' ' + round(pointY[0]) + ' ' +
        pathData.replace(/^M\s+[^\s]+\s+[^\s]+\s*/, '') +
        ' L ' + round(pointX[pointX.length - 1]) + ' ' + plotBottom + ' Z';
      fragment.appendChild(svgElement('path', {
        d: areaData,
        class: 'dl-area',
        fill: 'url(#dlAreaGradient)',
      }));
      if (series.length > 1) {
        fragment.appendChild(svgElement('path', {
          d: pathData,
          class: 'dl-curve',
        }));
      }

      fragment.appendChild(svgElement('line', {
        x1: pointX[pointX.length - 1],
        y1: plotTop,
        x2: pointX[pointX.length - 1],
        y2: plotBottom,
        class: 'dl-latest-marker',
      }));

      var points = svgElement('g', { class: 'dl-points' });
      series.forEach(function (release, index) {
        var isLatest = index === series.length - 1;
        var group = svgElement('g', {
          class: 'dl-point' + (isLatest ? ' is-latest' : ''),
          'data-index': String(index),
          tabindex: '0',
          role: 'img',
          'aria-label': pointLabel(release, index, series.length),
        });
        group.appendChild(svgElement('circle', {
          class: 'dl-dot-hit',
          cx: pointX[index],
          cy: pointY[index],
          r: 16,
        }));
        group.appendChild(svgElement('circle', {
          class: 'dl-dot-ring',
          cx: pointX[index],
          cy: pointY[index],
          r: isLatest ? 6.5 : 5,
        }));
        points.appendChild(group);
      });
      fragment.appendChild(points);
    } else {
      var barWidth = Math.min(54, (plotWidth / series.length) * 0.56);
      var bars = svgElement('g', { class: 'dl-bars' });
      series.forEach(function (release, index) {
        var height = Math.max(0, plotBottom - pointY[index]);
        var x = pointX[index] - barWidth / 2;
        var group = svgElement('g', {
          class: 'dl-bar-group' + (index === series.length - 1 ? ' is-latest' : ''),
          'data-index': String(index),
          tabindex: '0',
          role: 'img',
          'aria-label': pointLabel(release, index, series.length),
        });
        group.appendChild(svgElement('rect', {
          class: 'dl-bar-hit',
          x: pointX[index] - barWidth / 2 - 6,
          y: plotTop,
          width: barWidth + 12,
          height: plotHeight,
        }));
        if (height > 0) {
          group.appendChild(svgElement('rect', {
            class: 'dl-bar',
            x: x,
            y: pointY[index],
            width: barWidth,
            height: height,
            rx: Math.min(6, barWidth / 3),
            fill: 'url(#dlBarGradient)',
          }));
        }
        bars.appendChild(group);
      });
      fragment.appendChild(bars);
    }

    // X labels: first and last always, the rest thinned to the available width.
    var labels = svgElement('g', { class: 'dl-x-axis' });
    var width = el.chartStage ? el.chartStage.clientWidth || 900 : 900;
    var budget = Math.max(3, Math.min(12, Math.floor(width / 96)));
    var stride = Math.max(1, Math.ceil(series.length / budget));
    series.forEach(function (release, index) {
      if (index !== 0 && index !== series.length - 1 && index % stride !== 0) return;
      labels.appendChild(textElement({
        x: pointX[index],
        y: plotBottom + (compact ? 30 : 26),
        'text-anchor': 'middle',
        class: 'dl-tag',
      }, release.tag));
      labels.appendChild(textElement({
        x: pointX[index],
        y: plotBottom + (compact ? 52 : 44),
        'text-anchor': 'middle',
        class: 'dl-date',
      }, formatDate(release.published)));
    });
    fragment.appendChild(labels);

    el.chartContent.replaceChildren(fragment);
    if (el.chartHeading) {
      el.chartHeading.textContent = mode === 'cumulative'
        ? 'Cumulative downloads'
        : 'Downloads per release';
    }
    el.chartSub.textContent = mode === 'cumulative'
      ? 'The curve after each published release, oldest first.'
      : 'Downloads counted inside each release, independent of the others.';
    el.chartTitle.textContent = mode === 'cumulative'
      ? formatCount(state.total) + ' downloads across ' + series.length + ' ' +
        plural(series.length, 'release', 'releases')
      : 'Downloads in each of ' + series.length + ' ' + plural(series.length, 'release', 'releases');
    el.chartDesc.textContent = mode === 'cumulative'
      ? 'Cumulative Deltempo downloads after each published GitHub release, from ' +
        formatDate(series[0].published) + ' to ' + formatDate(series[series.length - 1].published) + '.'
      : 'Downloads counted in each published Deltempo GitHub release, from ' +
        formatDate(series[0].published) + ' to ' + formatDate(series[series.length - 1].published) + '.';

    if (state.activeIndex >= 0 && state.activeIndex < series.length) {
      setActive(state.activeIndex, false);
    } else {
      hideTooltip();
    }
  }

  function pointLabel(release, index, length) {
    var position = index === length - 1 ? 'latest release' : 'release ' + (index + 1) + ' of ' + length;
    return release.tag + ', ' + position + ', published ' + formatDate(release.published) +
      ', ' + formatCount(release.downloads) + ' downloads in this release, ' +
      formatCount(release.cumulative) + ' cumulative.';
  }

  function round(value) {
    return Math.round(value * 10) / 10;
  }

  /** Fritsch-Carlson monotone cubic path: smooth, but cannot dip below a step. */
  function monotonePath(xs, ys) {
    var n = xs.length;
    if (n === 0) return '';
    if (n === 1) return 'M ' + round(xs[0]) + ' ' + round(ys[0]);
    if (n === 2) {
      return 'M ' + round(xs[0]) + ' ' + round(ys[0]) +
        ' L ' + round(xs[1]) + ' ' + round(ys[1]);
    }

    var dx = [];
    var slope = [];
    var i;
    for (i = 0; i < n - 1; i += 1) {
      dx[i] = xs[i + 1] - xs[i];
      slope[i] = (ys[i + 1] - ys[i]) / dx[i];
    }

    var tangent = new Array(n);
    tangent[0] = slope[0];
    tangent[n - 1] = slope[n - 2];
    for (i = 1; i < n - 1; i += 1) {
      if (slope[i - 1] * slope[i] <= 0) {
        tangent[i] = 0;
      } else {
        var w1 = 2 * dx[i] + dx[i - 1];
        var w2 = dx[i] + 2 * dx[i - 1];
        tangent[i] = (w1 + w2) / (w1 / slope[i - 1] + w2 / slope[i]);
      }
    }

    var path = 'M ' + round(xs[0]) + ' ' + round(ys[0]);
    for (i = 0; i < n - 1; i += 1) {
      var c1x = xs[i] + dx[i] / 3;
      var c1y = ys[i] + (tangent[i] * dx[i]) / 3;
      var c2x = xs[i + 1] - dx[i] / 3;
      var c2y = ys[i + 1] - (tangent[i + 1] * dx[i]) / 3;
      path += ' C ' + round(c1x) + ' ' + round(c1y) + ' ' +
        round(c2x) + ' ' + round(c2y) + ' ' +
        round(xs[i + 1]) + ' ' + round(ys[i + 1]);
    }
    return path;
  }

  function renderTable() {
    if (!el.tableBody) return;
    var total = state.total || 1;
    var rows = state.releases.map(function (release, index) {
      var row = document.createElement('tr');
      var tag = document.createElement('th');
      tag.scope = 'row';
      tag.textContent = release.tag;
      if (index === state.releases.length - 1) tag.classList.add('is-latest');
      row.appendChild(tag);
      row.appendChild(cell(formatDate(release.published)));
      row.appendChild(cell(formatCount(release.downloads), true));
      row.appendChild(cell(formatCount(release.cumulative), true));
      row.appendChild(cell(Math.round((release.downloads / total) * 1000) / 10 + '%', true));
      return row;
    });
    el.tableBody.replaceChildren.apply(el.tableBody, rows);
    if (el.tableSummary) {
      el.tableSummary.textContent = 'All ' + state.releases.length + ' ' +
        plural(state.releases.length, 'release', 'releases') + ' as a table';
    }
  }

  function cell(value, numeric) {
    var node = document.createElement('td');
    node.textContent = value;
    if (numeric) node.className = 'num';
    return node;
  }

  function positionTooltip(index) {
    if (!state.geometry || !el.tooltip || !el.chartStage) return;
    var geometry = state.geometry;
    var stage = el.chartStage.getBoundingClientRect();
    if (!stage.width || !stage.height) return;

    var x = (geometry.xs[index] / geometry.width) * stage.width;
    var y = (geometry.ys[index] / geometry.height) * stage.height;
    el.tooltip.hidden = false;

    var width = el.tooltip.offsetWidth || 220;
    var height = el.tooltip.offsetHeight || 120;
    var left = clamp(x, width / 2 + 8, stage.width - width / 2 - 8);
    var below = y < height + 24;

    el.tooltip.style.left = left + 'px';
    el.tooltip.style.top = y + 'px';
    el.tooltip.classList.toggle('is-below', below);
  }

  function setActive(index, scrollIntoView) {
    if (!state.hasData || index < 0 || index >= state.releases.length) return;
    state.activeIndex = index;
    var release = state.releases[index];

    var groups = el.chart.querySelectorAll('[data-index]');
    Array.prototype.forEach.call(groups, function (group) {
      group.classList.toggle('is-active', Number(group.getAttribute('data-index')) === index);
    });

    var highlight = el.chart.querySelector('.dl-highlight');
    if (highlight) {
      highlight.removeAttribute('hidden');
      var line = highlight.querySelector('.dl-crosshair');
      if (line) {
        var x = state.geometry.xs[index];
        line.setAttribute('x1', x);
        line.setAttribute('x2', x);
      }
    }

    var total = state.total || 1;
    if (el.tooltipTag) el.tooltipTag.textContent = release.tag;
    if (el.tooltipDate) el.tooltipDate.textContent = formatDate(release.published);
    if (el.tooltipRelease) el.tooltipRelease.textContent = formatCount(release.downloads);
    if (el.tooltipCumulative) el.tooltipCumulative.textContent = formatCount(release.cumulative);
    if (el.tooltipShare) {
      el.tooltipShare.textContent = (Math.round((release.downloads / total) * 1000) / 10) + '%';
    }
    positionTooltip(index);

    if (scrollIntoView) {
      var target = el.chart.querySelector('[data-index="' + index + '"]');
      if (target && document.activeElement !== target) target.focus({ preventScroll: true });
    }
  }

  function hideTooltip() {
    state.activeIndex = -1;
    state.pinned = false;
    if (el.tooltip) el.tooltip.hidden = true;
    if (!el.chart) return;
    Array.prototype.forEach.call(el.chart.querySelectorAll('[data-index]'), function (group) {
      group.classList.remove('is-active');
    });
    var highlight = el.chart.querySelector('.dl-highlight');
    if (highlight) highlight.setAttribute('hidden', 'hidden');
  }

  function indexFromClientX(clientX) {
    if (!state.geometry || !el.chart) return -1;
    var rect = el.chart.getBoundingClientRect();
    if (!rect.width) return -1;
    var x = ((clientX - rect.left) / rect.width) * state.geometry.width;
    var best = 0;
    var bestDistance = Infinity;
    state.geometry.xs.forEach(function (pointX, index) {
      var distance = Math.abs(pointX - x);
      if (distance < bestDistance) {
        bestDistance = distance;
        best = index;
      }
    });
    return best;
  }

  function wireChart() {
    if (!el.chart) return;

    el.chart.addEventListener('pointermove', function (event) {
      if (event.pointerType === 'touch' && !state.pinned) return;
      var index = indexFromClientX(event.clientX);
      if (index >= 0) setActive(index, false);
    });

    el.chart.addEventListener('pointerdown', function (event) {
      var index = indexFromClientX(event.clientX);
      if (index < 0) return;
      state.pinned = true;
      setActive(index, false);
    });

    el.chart.addEventListener('pointerleave', function (event) {
      if (event.pointerType === 'touch') return;
      if (!el.chart.contains(document.activeElement)) hideTooltip();
    });

    el.chart.addEventListener('pointercancel', hideTooltip);

    el.chart.addEventListener('focusin', function (event) {
      var target = event.target.closest ? event.target.closest('[data-index]') : null;
      if (!target) return;
      setActive(Number(target.getAttribute('data-index')), false);
    });

    el.chart.addEventListener('focusout', function (event) {
      if (event.relatedTarget && el.chart.contains(event.relatedTarget)) return;
      hideTooltip();
    });

    el.chart.addEventListener('keydown', function (event) {
      if (state.activeIndex < 0) return;
      var next = -1;
      if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
        next = Math.min(state.releases.length - 1, state.activeIndex + 1);
      } else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
        next = Math.max(0, state.activeIndex - 1);
      } else if (event.key === 'Home') {
        next = 0;
      } else if (event.key === 'End') {
        next = state.releases.length - 1;
      } else if (event.key === 'Escape') {
        hideTooltip();
        return;
      }
      if (next >= 0) {
        event.preventDefault();
        setActive(next, true);
      }
    });

    document.addEventListener('pointerdown', function (event) {
      if (state.pinned && el.chart && !el.chart.contains(event.target)) {
        hideTooltip();
      }
    });
  }

  function wireModes() {
    el.modeButtons.forEach(function (button) {
      button.addEventListener('click', function () {
        var mode = button.getAttribute('data-chart-mode');
        if (!mode || mode === state.mode) return;
        state.mode = mode;
        el.modeButtons.forEach(function (other) {
          other.setAttribute('aria-pressed', other === button ? 'true' : 'false');
        });
        hideTooltip();
        renderChart();
      });
    });
  }

  async function refresh(reason) {
    if (state.loading) return;
    state.loading = true;
    state.lastAttempt = Date.now();
    renderStatus();

    try {
      var releases = await fetchApiReleases();
      state.lastError = null;
      applyData(releases, 'api', 0);
    } catch (error) {
      state.lastError = error;
      if (state.hasData) {
        // Never replace fresher live data with an older deploy snapshot.
        renderStatus();
      } else {
        try {
          var snapshot = await fetchSnapshot();
          applyData(snapshot.releases, 'snapshot', snapshot.generatedAt);
        } catch (snapshotError) {
          renderStatus();
          setLiveMessage('Download data could not be loaded. The table and chart will appear when GitHub is reachable.');
        }
      }
    } finally {
      state.loading = false;
      renderStatus();
      updateUpdatedLabel();
    }
  }

  function schedulePolling() {
    window.clearInterval(state.timer);
    state.timer = window.setInterval(function () {
      if (document.visibilityState === 'visible' && state.online) refresh('poll');
    }, POLL_MS);

    window.clearInterval(state.tickTimer);
    state.tickTimer = window.setInterval(function () {
      updateUpdatedLabel();
      updateRefreshButton();
    }, RELATIVE_TICK_MS);
  }

  function wireEnvironment() {
    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState !== 'visible') return;
      if (!state.online) return;
      if (Date.now() - state.fetchedAt > MANUAL_COOLDOWN_MS) refresh('visible');
      else updateUpdatedLabel();
    });

    window.addEventListener('online', function () {
      state.online = true;
      setLiveMessage('Connection restored. Checking GitHub again.');
      refresh('online');
    });

    window.addEventListener('offline', function () {
      state.online = false;
      renderStatus();
    });

    if (el.refresh) {
      el.refresh.addEventListener('click', function () {
        if (el.refresh.disabled) return;
        refresh('manual');
      });
    }

    if (window.ResizeObserver && el.chartStage) {
      var observer = new ResizeObserver(function () {
        window.clearTimeout(state.resizeTimer);
        state.resizeTimer = window.setTimeout(function () {
          if (state.hasData) renderChart();
        }, 140);
      });
      observer.observe(el.chartStage);
    } else {
      window.addEventListener('resize', function () {
        window.clearTimeout(state.resizeTimer);
        state.resizeTimer = window.setTimeout(function () {
          if (state.hasData) renderChart();
        }, 180);
      });
    }
  }

  function init() {
    wireChart();
    wireModes();
    wireEnvironment();
    schedulePolling();
    renderStatus();
    refresh('initial');
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
