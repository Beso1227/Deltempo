// @ts-nocheck
// Install canary for the only npm dependency this repository declares.
//
// Deltempo itself is a .NET application; this package exists so the OpenCode
// plugin surface is pinned and reviewable. The check therefore only has to
// prove that the declared floor is what is actually installed and that its
// entry point is a real, non-empty file.
const assert = require('node:assert/strict');
const test = require('node:test');
const fs = require('node:fs');
const path = require('node:path');

const DEPENDENCY = '@plannotator/opencode';

function parseVersion(value) {
  const match = /^(\d+)\.(\d+)\.(\d+)/.exec(value);
  assert.ok(match, `unparseable version "${value}"`);
  return match.slice(1, 4).map(Number);
}

function compare(left, right) {
  const a = parseVersion(left);
  const b = parseVersion(right);
  for (let index = 0; index < a.length; index += 1) {
    if (a[index] !== b[index]) return a[index] < b[index] ? -1 : 1;
  }
  return 0;
}

test('OpenCode plugin dependency is installed and satisfies the declared range', () => {
  const entryPath = require.resolve(DEPENDENCY);
  assert.ok(fs.statSync(entryPath).size > 0, `entry point is empty: ${entryPath}`);

  // The package has an "exports" map with only ".", so "../package.json" is not
  // resolvable; walk up from the resolved entry point instead.
  let packageRoot = path.dirname(entryPath);
  while (!fs.existsSync(path.join(packageRoot, 'package.json'))) {
    const parent = path.dirname(packageRoot);
    assert.notEqual(parent, packageRoot, `no package.json above ${entryPath}`);
    packageRoot = parent;
  }

  const installed = JSON.parse(
    fs.readFileSync(path.join(packageRoot, 'package.json'), 'utf8'),
  );
  const declared = require('../package.json').dependencies[DEPENDENCY];
  const floor = declared.replace(/^[~^]/, '');

  assert.equal(installed.name, DEPENDENCY);
  assert.ok(
    compare(installed.version, floor) >= 0,
    `installed ${installed.version} is below the declared range "${declared}"`,
  );
});
