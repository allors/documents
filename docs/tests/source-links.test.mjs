import assert from 'node:assert/strict';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { rewriteLink } from '../integrations/source-links.mjs';

const source = fileURLToPath(new URL('../src/content/docs/internals/releasing.md', import.meta.url));

test('source links become site links at a project or domain root', () => {
  assert.equal(rewriteLink('../tags.md#loops', source, '/documents/'), '/documents/tags/#loops');
  assert.equal(rewriteLink('../index.md', source, '/'), '/');
  assert.equal(rewriteLink('#release', source, '/documents/'), '#release');
});

test('repository references resolve to the documented branch', () => {
  assert.equal(rewriteLink('../../../../../README.md#packages', source, '/documents/'),
    'https://github.com/allors/documents/blob/main/README.md#packages');
  assert.equal(rewriteLink('https://example.com/guide.md', source, '/documents/'), 'https://example.com/guide.md');
});

test('broken source links fail the build with a useful target', () => {
  assert.throws(() => rewriteLink('../missing.md', source, '/documents/'), /missing/);
});

test('repository fragments are checked against the local Markdown headings', () => {
  assert.throws(() => rewriteLink('../../../../../README.md#no-such-heading', source, '/documents/'),
    /no-such-heading.*README/);
  const fixture = 'https://github.com/allors/documents/blob/main/docs/tests/fixtures/anchors.md';
  for (const id of ['added', 'added-1', 'use-rewritelink-in-astro', 'explicit-anchor']) {
    assert.equal(rewriteLink(`../../../../tests/fixtures/anchors.md#${id}`, source, '/documents/'), `${fixture}#${id}`);
  }
  assert.throws(() => rewriteLink('../../../../tests/fixtures/anchors.md#not-a-heading', source, '/documents/'), /not-a-heading/);
});

test('fragments on repository files that are not Markdown are left alone', () => {
  assert.equal(rewriteLink('../../../../../src/Allors.Documents/OpenDocument/OpenDocumentTemplate.cs#L23', source, '/documents/'),
    'https://github.com/allors/documents/blob/main/src/Allors.Documents/OpenDocument/OpenDocumentTemplate.cs#L23');
});
