import assert from 'node:assert/strict';
import { mkdtemp, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { exportDocumentation } from '../integrations/exports.mjs';

const metadata = {
  title: 'Domains',
  description: 'How domains inherit behaviour.',
  audience: 'Template authors',
  kind: 'Explanation',
  status: 'Current',
  version: 'main · 3.3 (unreleased)',
  slug: 'domains',
};

function page(data, content) {
  return `<!doctype html><html><head><title>${data.title}</title></head><body>
    <nav>Navigation is not documentation.</nav>
    <script type="application/json" data-documentation>${JSON.stringify(data)}</script>
    <main><h1>${data.title}</h1><div class="sl-markdown-content">${content}</div></main>
    <footer>Footer is not documentation.</footer></body></html>`;
}

async function fixture(t, files) {
  const dir = await mkdtemp(join(tmpdir(), 'allors-docs-exports-'));
  t.after(() => rm(dir, { recursive: true, force: true }));
  for (const [name, content] of Object.entries(files)) {
    await mkdir(join(dir, name, '..'), { recursive: true });
    await writeFile(join(dir, name), content);
  }
  return dir;
}

test('exports rendered prose, metadata, tables, code and static component explanations', async (t) => {
  const dir = await fixture(t, {
    'domains/index.html': page(metadata, `
      <p>The order of the domains determines the hooks.</p>
      <table><thead><tr><th>End</th><th>Meaning</th></tr></thead>
        <tbody><tr><td>Role</td><td>Forward, writable</td></tr></tbody></table>
      <div class="expressive-code"><figure><figcaption>example.cs</figcaption>
        <pre data-language="csharp"><code><div class="ec-line">var session = workspace.CreateSession();</div><div class="ec-line">session.Reset();</div></code></pre>
        <div class="copy"><button>Copy code</button></div></figure></div>
      <section data-documentation-interactive><h2>Reset example</h2>
        <button>Next step</button><p>Reset restores the accepted editing baseline.</p>
        <details><summary>All scenario steps</summary><ol><li>Accept the object.</li><li>Edit its role.</li><li>Reset restores the role.</li></ol></details>
      </section>`),
  });
  await exportDocumentation({ dir, site: 'https://example.org', base: '/documents/' });
  const output = await readFile(join(dir, 'domains.md'), 'utf8');
  for (const field of ['title', 'description', 'audience', 'kind', 'status', 'version', 'slug']) {
    assert.ok(output.includes(`${field}: ${JSON.stringify(metadata[field])}`), field);
  }
  assert.match(output, /# Domains/);
  assert.match(output, /\| Role \| Forward, writable \|/);
  assert.match(output, /```csharp\nvar session = workspace\.CreateSession\(\);\nsession\.Reset\(\);\n```/);
  assert.match(output, /Reset restores the accepted editing baseline/);
  assert.match(output, /All scenario steps/);
  assert.match(output, /Reset restores the role/);
  assert.doesNotMatch(output, /Navigation is not|Footer is not|Copy code|Next step|<script/);
});

test('rewrites links to known Markdown exports at a deployment base and keeps anchors', async (t) => {
  const dir = await fixture(t, {
    'index.html': page({ ...metadata, title: 'Documentation', slug: '' }, '<p>Start here.</p>'),
    'domains/index.html': page(metadata, `
      <p><a href="../internals/state/#reset">Relative</a>
      <a href="/documents/internals/state/?example=1#reset">Absolute path</a>
      <a href="https://example.org/documents/internals/state/#reset">Canonical</a>
      <a href="/documents/">Home</a><a href="#order">Same page</a>
      <a href="https://other.org/domains/">External</a>
      <a href="/documents/assets/diagram.svg">Drawing</a></p>`),
    'internals/state/index.html': page({ ...metadata, slug: 'internals/state', audience: 'Maintainers' }, '<h2 id="reset">Reset</h2>'),
  });
  await exportDocumentation({ dir, site: 'https://example.org', base: '/documents' });
  const output = await readFile(join(dir, 'domains.md'), 'utf8');
  assert.match(output, /\[Relative\]\(https:\/\/example\.org\/documents\/internals\/state\.md#reset\)/);
  assert.match(output, /\[Absolute path\]\(https:\/\/example\.org\/documents\/internals\/state\.md\?example=1#reset\)/);
  assert.match(output, /\[Canonical\]\(https:\/\/example\.org\/documents\/internals\/state\.md#reset\)/);
  assert.match(output, /\[Home\]\(https:\/\/example\.org\/documents\/index\.md\)/);
  assert.match(output, /\[Same page\]\(#order\)/);
  assert.match(output, /\[External\]\(https:\/\/other\.org\/domains\/\)/);
  assert.match(output, /\[Drawing\]\(https:\/\/example\.org\/documents\/assets\/diagram\.svg\)/);
});

test('creates a metadata-driven index with shared, user and maintainer pages, excluding non-doc pages', async (t) => {
  const dir = await fixture(t, {
    'index.html': page({ ...metadata, title: 'Documentation', slug: '', audience: 'Everyone', kind: 'Index' }, '<p>Start.</p>'),
    'domains/index.html': page(metadata, '<p>Domains.</p>'),
    'internals/plan/index.html': page({ ...metadata, title: 'Plan', slug: 'internals/plan', audience: 'Maintainers', kind: 'Decision record', status: 'Planned' }, '<p>Planned.</p>'),
    '404.html': '<html><main><h1>Not found</h1></main></html>',
  });
  await exportDocumentation({ dir, site: 'https://example.org', base: '/' });
  const output = await readFile(join(dir, 'llms.txt'), 'utf8');
  assert.match(output, /## Everyone/);
  assert.match(output, /## Template authors/);
  assert.match(output, /## Maintainers/);
  assert.match(output, /\[Documentation\]\(https:\/\/example\.org\/index\.md\)/);
  assert.match(output, /\[Plan\]\(https:\/\/example\.org\/internals\/plan\.md\)/);
  assert.match(output, /How domains inherit behaviour/);
  assert.match(output, /Audience: Maintainers; Kind: Decision record; Status: Planned; Version: main · 3\.3 \(unreleased\)/);
  assert.doesNotMatch(output, /404|Not found/);
});

test('preserves SVG textual equivalents and details without retaining drawing markup', async (t) => {
  const dir = await fixture(t, {
    'domains/index.html': page(metadata, `<figure><svg role="img"><title>Domain inheritance</title><desc>Core extends System.</desc><path d="M0 0" /></svg><figcaption>Arrows point from the extending domain to its parent.</figcaption></figure>`),
  });
  await exportDocumentation({ dir, site: 'https://example.org', base: '/' });
  const output = await readFile(join(dir, 'domains.md'), 'utf8');
  assert.match(output, /Domain inheritance/);
  assert.match(output, /Core extends System/);
  assert.match(output, /Arrows point/);
  assert.doesNotMatch(output, /<svg|<path/);
});

test('preserves the Mermaid source emitted by astro-mermaid, including HTML labels and long fences', async (t) => {
  const dir = await fixture(t, {
    'domains/index.html': page(metadata, '<pre class="mermaid">flowchart BT\n  Core[&quot;Core&lt;br/&gt;host&quot;] --&gt; System\n  Note[&quot;```&quot;]</pre>'),
  });
  await exportDocumentation({ dir, site: 'https://example.org', base: '/documents/' });
  const output = await readFile(join(dir, 'domains.md'), 'utf8');
  assert.match(output, /````mermaid\nflowchart BT\n  Core\["Core<br\/>host"\] --> System\n  Note\["```"\]\n````/);
});

test('fails with an actionable message for an interactive placeholder with no static explanation', async (t) => {
  const dir = await fixture(t, {
    'domains/index.html': page(metadata, '<section data-documentation-interactive><button>Start</button><canvas></canvas></section>'),
  });
  await assert.rejects(exportDocumentation({ dir, site: 'https://example.org', base: '/' }), /domains.*static.*explanation/i);
});

test('fails instead of exporting incomplete metadata or conflicting page slugs', async (t) => {
  const dir = await fixture(t, {
    'domains/index.html': page({ ...metadata, version: '' }, '<p>Domains.</p>'),
  });
  await assert.rejects(exportDocumentation({ dir, site: 'https://example.org', base: '/' }), /version/i);
  await writeFile(join(dir, 'domains/index.html'), page(metadata, '<p>Domains.</p>'));
  await writeFile(join(dir, 'duplicate.html'), page(metadata, '<p>Duplicate.</p>'));
  await assert.rejects(exportDocumentation({ dir, site: 'https://example.org', base: '/' }), /duplicate.*domains/i);
});

test('preserves rendered heading anchors when they differ from Markdown heading text', async (t) => {
  const dir = await fixture(t, {
    'domains/index.html': page(metadata, '<p><a href="#stable-contract">The contract</a></p><h2 id="stable-contract">Editing baseline <a class="sl-anchor-link" href="#stable-contract"><span>Section titled Editing baseline</span></a></h2><p>Reset restores the accepted record.</p>'),
  });
  await exportDocumentation({ dir, site: 'https://example.org', base: '/' });
  const output = await readFile(join(dir, 'domains.md'), 'utf8');
  assert.match(output, /\[The contract\]\(#stable-contract\)/);
  assert.match(output, /<a id="stable-contract"><\/a>\n\n## Editing baseline/);
  assert.doesNotMatch(output, /Section titled/);
});
