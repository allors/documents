import assert from 'node:assert/strict';
import { mkdtemp, mkdir, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { checkLinks } from '../scripts/check-links.mjs';

async function fixture(t, files) {
  const directory = await mkdtemp(path.join(tmpdir(), 'allors-docs-links-'));
  t.after(() => rm(directory, { recursive: true, force: true }));
  for (const [name, content] of Object.entries(files)) {
    const target = path.join(directory, name);
    await mkdir(path.dirname(target), { recursive: true });
    await writeFile(target, content);
  }
  return directory;
}

for (const base of ['/', '/documents/', '/preview/documents/']) {
  test(`accepts HTML, Markdown and canonical links at ${base}`, async (t) => {
    const directory = await fixture(t, {
      'index.html': `<h1 id="documentation">Documentation</h1>
        <link rel="stylesheet" href="${base}assets/site.css">
        <a href="${base}guide/#configuration">Guide</a>
        <a href="https://allors.github.io${base}guide.md#configuration">Markdown</a>`,
      'guide/index.html': `<h1 id="configuration">Configuration</h1>
        <a href="../#documentation">Home</a>
        <a href="https://github.com/allors/documents/blob/main/README.md#configuration">Repository</a>`,
      'index.md': `# Documentation\n[Guide](${base}guide.md#configuration)`,
      'guide.md': '# Guide\n\n[Home](index.md#documentation)',
      'assets/site.css': 'body { color: black; }',
      'llms.txt': `# Documentation\n[Guide](https://allors.github.io${base}guide.md#configuration)`,
    });
    const result = await checkLinks(directory, base);
    assert.deepEqual(result.errors, []);
    assert.equal(result.files, 5);
    assert.equal(result.links, 7);
  });
}

test('reports a local URL that omits the deployment prefix', async (t) => {
  const directory = await fixture(t, {
    'index.html': '<a href="/guide/">Guide</a>',
    'guide/index.html': '<h1>Guide</h1>',
  });
  const { errors } = await checkLinks(directory, '/documents/');
  assert.equal(errors.length, 1);
  assert.match(errors[0], /index\.html.*\/guide\/.*deployment base.*\/documents\//);
});

test('reports missing HTML assets, Markdown targets and canonical index links', async (t) => {
  const directory = await fixture(t, {
    'index.html': '<script src="/documents/missing.js"></script>',
    'index.md': '[Missing](missing.md)',
    'llms.txt': '[Missing](https://allors.github.io/documents/missing/)',
  });
  const { errors } = await checkLinks(directory, '/documents/');
  assert.equal(errors.length, 3);
  assert.ok(errors.every((error) => /does not exist/.test(error)));
  assert.ok(errors.some((error) => /index\.html.*missing\.js/.test(error)));
  assert.ok(errors.some((error) => /index\.md:1.*missing\.md/.test(error)));
  assert.ok(errors.some((error) => /llms\.txt:1.*missing\//.test(error)));
});

test('reports missing HTML and exported Markdown fragments', async (t) => {
  const directory = await fixture(t, {
    'index.html': '<h1 id="documentation">Documentation</h1><a href="#unknown">Unknown</a>',
    'guide/index.html': '<h1 id="configuration">Configuration</h1>',
    'guide.md': '# Guide',
    'llms.txt': '[Guide](https://allors.github.io/documents/guide.md#unknown)',
  });
  const { errors } = await checkLinks(directory, '/documents/');
  assert.equal(errors.length, 2);
  assert.ok(errors.every((error) => /fragment.*unknown.*does not exist/.test(error)));
  assert.ok(errors.some((error) => /guide\/index\.html/.test(error)));
});

test('checks reference links and ignores fenced and inline example links', async (t) => {
  const directory = await fixture(t, {
    'index.html': '<h1 id="documentation">Documentation</h1>',
    'index.md': '# Documentation\n[Valid][home]\n\n[home]: index.md#documentation\n\n'
      + '```markdown\n[Example](missing.md)\n```\n\n`[Example](also-missing.md)`\n',
  });
  const result = await checkLinks(directory, '/documents/');
  assert.deepEqual(result.errors, []);
  assert.equal(result.links, 1);
});

test('supports a complete deployment URL and encoded fragments', async (t) => {
  const directory = await fixture(t, {
    'index.html': '<h1 id="café">Café</h1><a href="https://docs.example.test/manual/#caf%C3%A9">Section</a>',
  });
  const { errors } = await checkLinks(directory, 'https://docs.example.test/manual/');
  assert.deepEqual(errors, []);
});
