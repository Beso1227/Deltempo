#!/usr/bin/env node
/**
 * The machine-readable context an agent reads, and the version that says
 * which copy of it a link points at.
 *
 * WHY A VERSION AT ALL
 * --------------------
 * A crawler that has already read llms.txt keeps the copy it read. The only
 * thing that tells it the copy is stale is a URL that changed - which is the
 * same lesson the favicon work established: the URL is the cache key.
 * Without a version query a changed llms.txt is still served from the same
 * URL, and an agent keeps answering from the old one. The documentation
 * changes far more often than a release does, so this is not a rare case.
 *
 * WHY IT IS DERIVED, NOT MAINTAINED
 * ---------------------------------
 * The site's other cache-buster is a release number, which is right for the
 * app's assets and wrong here: these files describe the documentation, and
 * the documentation moves on its own schedule. A version that only moved on
 * release would leave a stale copy in place for weeks.
 *
 * So the version is a hash of the files' own contents. It moves exactly when
 * they move - which is the property a cache key needs - and it cannot drift,
 * because nothing has to remember to bump it.
 *
 * The hash is taken over the text with any version query removed, so writing
 * the version into the files does not change the version. Without that the
 * value would depend on itself and never settle.
 *
 * Line endings are normalised before hashing, because git checks this
 * repository out with CRLF on Windows and LF everywhere else: the same file
 * must not have two different versions, or CI and the deployed site would
 * disagree about what "the current version" is.
 *
 * USAGE
 * -----
 *   const agentContext = require('./agent_context');
 *   const version = agentContext.computeContextVersion(docsDir);
 */
'use strict';

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

/**
 * The files that describe the site to an agent.
 *
 * The catalog is listed once even though it is published twice - at the
 * project path and at the discovery path - because the two copies are the
 * same document and must not drift apart.
 */
const CONTEXT_FILES = ['llms.txt', 'llms-full.txt', 'ai-catalog.json'];

/**
 * A URL that points at one of the context files, with its version query.
 *
 * The scheme is required so that a bare mention of a path in prose (the
 * discovery path, for instance) is not rewritten into something that looks
 * like a link but is not one.
 */
const CONTEXT_URL = /(https?:\/\/[^\s)"'<>]*?\b(?:llms\.txt|llms-full\.txt|ai-catalog\.json))(\?v=[0-9a-z]+)?/g;

/**
 * The line ending the file already uses.
 *
 * Git checks this repository out with CRLF on Windows and LF everywhere else,
 * so a script that writes LF unconditionally leaves a Windows checkout with
 * mixed line endings, and then reports the file it just wrote as stale.
 */
function lineEnding(text) {
  const crlf = (text.match(/\r\n/g) || []).length;
  const lf = (text.match(/\n/g) || []).length - crlf;
  return crlf > lf ? '\r\n' : '\n';
}

/**
 * The text as it is hashed: line endings normalised and any version query on
 * a context URL removed.
 *
 * Removing the query is what makes the version a fixed point. The version
 * goes INTO these files, so hashing the files as they are would make the
 * version depend on itself.
 */
function canonical(text) {
  return text
    .replace(/\r\n/g, '\n')
    .replace(/(https?:\/\/[^\s)"'<>]*?\b(?:llms\.txt|llms-full\.txt|ai-catalog\.json))\?v=[0-9a-z]+/g, '$1');
}

/**
 * The version of the context files, as a short hex string.
 *
 * The files are hashed in a fixed order and each is preceded by its name, so
 * that moving content between two files changes the version.
 */
function computeContextVersion(docsDir) {
  const hash = crypto.createHash('sha256');
  for (const name of CONTEXT_FILES) {
    const file = path.join(docsDir, name);
    if (!fs.existsSync(file)) continue;
    hash.update(name);
    hash.update('\n');
    hash.update(canonical(fs.readFileSync(file, 'utf8')));
    hash.update('\n');
  }
  return hash.digest('hex').slice(0, 12);
}

/**
 * Point every context URL in a text at the given version.
 *
 * This is what makes a changed file a changed URL: the link in the page, and
 * the link in llms.txt to the fuller context, both name the version they
 * mean, so a crawler that fetches them gets the current copy rather than
 * whatever it kept.
 */
function applyVersionToContextUrls(text, version) {
  return text.replace(CONTEXT_URL, (match, url) => `${url}?v=${version}`);
}

module.exports = {
  CONTEXT_FILES,
  applyVersionToContextUrls,
  canonical,
  computeContextVersion,
  lineEnding,
};
