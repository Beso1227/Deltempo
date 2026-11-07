#!/usr/bin/env node
/**
 * Query the committed Deltempo knowledge graph (.ua/knowledge-graph.json).
 * Zero dependencies, Node 18+.
 *
 *   node .ua/ua-query.mjs stats
 *   node .ua/ua-query.mjs file CleanupPlanner     path or symbol substring
 *   node .ua/ua-query.mjs layer                   list the 8 layers
 *   node .ua/ua-query.mjs layer core-library      files in one layer
 *   node .ua/ua-query.mjs search "safety shield"  name / tag / summary match
 *   node .ua/ua-query.mjs edges <node id>         in and out edges for a node
 *   node .ua/ua-query.mjs node <node id>          full record for one node
 *   node .ua/ua-query.mjs tour                    the guided reading order
 *
 * Flags: --json   --limit N (default 20)
 */
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.dirname(HERE);
const GRAPH_PATH = path.join(HERE, 'knowledge-graph.json');
const META_PATH = path.join(HERE, 'meta.json');

const argv = process.argv.slice(2);
const flags = { json: false, limit: 20 };
const positional = [];
for (let i = 0; i < argv.length; i += 1) {
  const arg = argv[i];
  if (arg === '--json') flags.json = true;
  else if (arg === '--limit') flags.limit = Number(argv[(i += 1)]) || 20;
  else positional.push(arg);
}
const command = positional[0];
const query = positional.slice(1).join(' ').trim();

function fail(message) {
  console.error(message);
  process.exit(1);
}

function loadGraph() {
  if (!fs.existsSync(GRAPH_PATH)) {
    fail(`No knowledge graph at ${GRAPH_PATH}. Run the /understand pipeline first.`);
  }
  const graph = JSON.parse(fs.readFileSync(GRAPH_PATH, 'utf8'));
  graph.byId = new Map(graph.nodes.map((node) => [node.id, node]));
  graph.layerOf = new Map();
  for (const layer of graph.layers || []) {
    for (const id of layer.nodeIds || []) graph.layerOf.set(id, layer);
  }
  return graph;
}

const clip = (text, max = 220) => {
  const flat = String(text || '').replace(/\s+/g, ' ').trim();
  return flat.length > max ? `${flat.slice(0, max - 1)}…` : flat;
};

function counts(items, key) {
  const out = {};
  for (const item of items) out[item[key]] = (out[item[key]] || 0) + 1;
  return out;
}

function describe(graph, node) {
  const layer = graph.layerOf.get(node.id);
  const lines = [
    `${node.id}  [${node.type}${node.complexity ? `, ${node.complexity}` : ''}]`,
  ];
  if (node.name && node.name !== node.filePath) lines.push(`  name:   ${node.name}`);
  if (node.filePath) lines.push(`  file:   ${node.filePath}`);
  if (layer) lines.push(`  layer:  ${layer.name}`);
  if (node.lineRange) lines.push(`  lines:  ${node.lineRange.join('-')}`);
  if (node.tags && node.tags.length) lines.push(`  tags:   ${node.tags.join(', ')}`);
  lines.push(`  ${clip(node.summary, 400)}`);
  return lines.join('\n');
}

function symbolsOf(graph, fileNode) {
  return graph.edges
    .filter((edge) => edge.source === fileNode.id && edge.type === 'contains')
    .map((edge) => graph.byId.get(edge.target))
    .filter((node) => node && node.type !== 'file');
}

function printEdges(graph, node) {
  const out = graph.edges.filter((edge) => edge.source === node.id);
  const incoming = graph.edges.filter((edge) => edge.target === node.id);
  console.log(`\nout (${out.length})`);
  for (const edge of out.slice(0, flags.limit)) {
    console.log(`  ${edge.type.padEnd(12)} -> ${edge.target}`);
  }
  console.log(`\nin (${incoming.length})`);
  for (const edge of incoming.slice(0, flags.limit)) {
    console.log(`  ${edge.type.padEnd(12)} <- ${edge.source}`);
  }
}

function matchNodes(graph, needle) {
  const term = needle.toLowerCase();
  const scored = [];
  for (const node of graph.nodes) {
    const file = String(node.filePath || '').toLowerCase();
    const id = node.id.toLowerCase();
    const name = String(node.name || '').toLowerCase();
    let score = 0;
    if (id === term || file === term) score = 100;
    else if (name === term) score = 90;
    else if (file.endsWith(`/${term}`) || file === term) score = 80;
    else if (name.includes(term)) score = 60;
    else if (file.includes(term)) score = 50;
    else if (id.includes(term)) score = 40;
    if (score) scored.push({ node, score });
  }
  return scored.sort((a, b) => b.score - a.score || a.node.id.length - b.node.id.length);
}

function commandStats(graph) {
  const meta = fs.existsSync(META_PATH) ? JSON.parse(fs.readFileSync(META_PATH, 'utf8')) : {};
  let head = null;
  try {
    head = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: ROOT, encoding: 'utf8' }).trim();
  } catch {
    // git unavailable: freshness stays unknown rather than guessed
  }
  const built = meta.gitCommitHash || null;
  const status = !head || !built ? 'unknown' : head === built ? 'current' : 'STALE';

  if (flags.json) {
    console.log(
      JSON.stringify(
        {
          nodes: graph.nodes.length,
          edges: graph.edges.length,
          nodeTypes: counts(graph.nodes, 'type'),
          edgeTypes: counts(graph.edges, 'type'),
          layers: (graph.layers || []).map((layer) => ({
            id: layer.id,
            name: layer.name,
            nodes: (layer.nodeIds || []).length,
          })),
          analyzedFiles: meta.analyzedFiles ?? null,
          builtAtCommit: built,
          head,
          status,
        },
        null,
        2,
      ),
    );
    return;
  }

  console.log(`nodes ${graph.nodes.length}   edges ${graph.edges.length}   layers ${(graph.layers || []).length}`);
  console.log(`types ${JSON.stringify(counts(graph.nodes, 'type'))}`);
  console.log(`edges ${JSON.stringify(counts(graph.edges, 'type'))}`);
  console.log(`files analyzed ${meta.analyzedFiles ?? '?'}   built ${built ? built.slice(0, 10) : '?'}   HEAD ${head ? head.slice(0, 10) : '?'}`);
  if (status === 'STALE') {
    console.log('STALE: the graph predates HEAD. Re-run the /understand pipeline to refresh.');
  } else if (status === 'current') {
    console.log('current: graph commit matches HEAD.');
  }
}

function commandFile(graph, needle) {
  if (!needle) fail('usage: ua-query.mjs file <substring>');
  const matches = matchNodes(graph, needle)
    .sort((a, b) => Number(b.node.type === 'file') - Number(a.node.type === 'file') || b.score - a.score)
    .slice(0, flags.limit);
  if (!matches.length) fail(`no node matches "${needle}"`);
  if (flags.json) {
    console.log(JSON.stringify(matches.map((match) => match.node), null, 2));
    return;
  }
  for (const { node } of matches) {
    console.log(`\n${describe(graph, node)}`);
    const symbols = symbolsOf(graph, node);
    if (symbols.length) {
      console.log(`  symbols: ${symbols.slice(0, 12).map((symbol) => symbol.name).join(', ')}${symbols.length > 12 ? ` (+${symbols.length - 12})` : ''}`);
    }
  }
}

function commandLayer(graph, needle) {
  const layers = graph.layers || [];
  if (!needle) {
    for (const layer of layers) {
      console.log(`${layer.id.replace(/^layer:/, '').padEnd(22)} ${String((layer.nodeIds || []).length).padStart(4)}  ${layer.name}`);
    }
    return;
  }
  const term = needle.toLowerCase().replace(/^layer:/, '');
  const layer = layers.find((candidate) => candidate.id.toLowerCase().replace(/^layer:/, '') === term);
  if (!layer) fail(`no layer "${needle}" — run "layer" with no argument to list them`);
  const nodes = (layer.nodeIds || []).map((id) => graph.byId.get(id)).filter(Boolean);
  const files = nodes.filter((node) => node.type === 'file');
  const other = nodes.filter((node) => node.type !== 'file');
  if (flags.json) {
    console.log(JSON.stringify({ ...layer, files: files.map((file) => file.filePath) }, null, 2));
    return;
  }
  console.log(`${layer.name} — ${nodes.length} nodes (${files.length} files, ${other.length} non-file)`);
  console.log(clip(layer.description, 300));
  for (const file of files.sort((a, b) => a.filePath.localeCompare(b.filePath))) {
    console.log(`  ${file.filePath}`);
  }
}

function commandSearch(graph, needle) {
  if (!needle) fail('usage: ua-query.mjs search <text>');
  const term = needle.toLowerCase();
  const hits = graph.nodes
    .filter(
      (node) =>
        String(node.name || '').toLowerCase().includes(term) ||
        String(node.summary || '').toLowerCase().includes(term) ||
        (node.tags || []).some((tag) => String(tag).toLowerCase().includes(term)),
    )
    .slice(0, flags.limit);
  if (!hits.length) fail(`nothing matches "${needle}"`);
  for (const node of hits) {
    console.log(`${node.id}\n  ${clip(node.summary, 160)}`);
  }
}

function commandEdges(graph, needle) {
  if (!needle) fail('usage: ua-query.mjs edges <node id>');
  const node = graph.byId.get(needle) || matchNodes(graph, needle)[0]?.node;
  if (!node) fail(`no node "${needle}"`);
  console.log(describe(graph, node));
  if (flags.json) {
    console.log(
      JSON.stringify(
        {
          out: graph.edges.filter((edge) => edge.source === node.id),
          in: graph.edges.filter((edge) => edge.target === node.id),
        },
        null,
        2,
      ),
    );
    return;
  }
  printEdges(graph, node);
}

function commandTour(graph) {
  const tour = graph.tour || [];
  if (!tour.length) fail('this graph has no tour');
  tour.forEach((step, index) => {
    const ids = step.nodeIds || step.nodes || [];
    console.log(`${index + 1}. ${step.title || step.name || `Step ${index + 1}`}`);
    if (step.description) console.log(`   ${clip(step.description, 240)}`);
    for (const id of ids.slice(0, 4)) {
      const node = graph.byId.get(id);
      console.log(`   - ${id}${node ? `  ${clip(node.summary, 110)}` : ''}`);
    }
  });
}

function main() {
  const graph = loadGraph();
  switch (command) {
    case 'stats':
      return commandStats(graph);
    case 'file':
      return commandFile(graph, query);
    case 'layer':
      return commandLayer(graph, query);
    case 'search':
      return commandSearch(graph, query);
    case 'edges':
      return commandEdges(graph, query);
    case 'node':
      return commandEdges(graph, query);
    case 'tour':
      return commandTour(graph);
    default:
      return fail(
        [
          'usage: node .ua/ua-query.mjs <command> [query] [--json] [--limit N]',
          '  stats | file <substring> | layer [name] | search <text> | edges <id> | node <id> | tour',
        ].join('\n'),
      );
  }
}

main();
