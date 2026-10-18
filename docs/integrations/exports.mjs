import { mkdir, readdir, readFile, writeFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseHTML } from 'linkedom';
import TurndownService from 'turndown';
import { gfm } from 'turndown-plugin-gfm';

const metadataFields = ['title', 'description', 'audience', 'kind', 'status', 'version', 'slug'];

async function htmlFiles(dir) {
  const files = [];
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) files.push(...await htmlFiles(path));
    else if (entry.isFile() && entry.name.endsWith('.html')) files.push(path);
  }
  return files.sort();
}

function readMetadata(document, path) {
  const element = document.querySelector('script[type="application/json"][data-documentation]');
  if (!element) return undefined;
  let metadata;
  try {
    metadata = JSON.parse(element.textContent);
  } catch (error) {
    throw new Error(`${path}: documentation metadata must be valid JSON.`, { cause: error });
  }
  if (!metadata || typeof metadata !== 'object' || Array.isArray(metadata)) {
    throw new Error(`${path}: documentation metadata must be a JSON object with page fields.`);
  }
  for (const field of metadataFields) {
    if (typeof metadata[field] !== 'string' || (field !== 'slug' && !metadata[field].trim())) {
      throw new Error(`${path}: supply a non-empty documentation ${field} in the content metadata.`);
    }
  }
  if (metadata.slug && (!/^[\w.-]+(?:\/[\w.-]+)*$/.test(metadata.slug) || metadata.slug.split('/').some((part) => part === '.' || part === '..'))) {
    throw new Error(`${path}: documentation slug must be a relative path without traversal segments.`);
  }
  return metadata;
}

function routeKey(url) {
  return url.pathname.replace(/\/(?:index\.html)?$/, '').replace(/\.html$/, '');
}

function exportUrl(metadata, root) {
  return new URL(`${metadata.slug || 'index'}.md`, root);
}

function pageUrl(metadata, root) {
  return new URL(metadata.slug ? `${metadata.slug}/` : '', root);
}

function fence(content, language = '') {
  const longest = Math.max(2, ...Array.from(content.matchAll(/`+/g), ([match]) => match.length));
  const marker = '`'.repeat(longest + 1);
  return `\n\n${marker}${language}\n${content.replace(/\n$/, '')}\n${marker}\n\n`;
}

function markdownConverter() {
  const converter = new TurndownService({ headingStyle: 'atx', codeBlockStyle: 'fenced', bulletListMarker: '-' });
  converter.use(gfm);
  converter.addRule('headingAnchors', {
    filter: ['h1', 'h2', 'h3', 'h4', 'h5', 'h6'],
    replacement: (content, node) => {
      const id = node.getAttribute('id');
      const escaped = id?.replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
      const anchor = id ? `<a id="${escaped}"></a>\n\n` : '';
      return `\n\n${anchor}${'#'.repeat(Number(node.nodeName[1]))} ${content.trim()}\n\n`;
    },
  });
  converter.addRule('sourceCode', {
    filter: 'pre',
    replacement: (_content, node) => {
      if (node.classList.contains('mermaid')) return fence(node.textContent, 'mermaid');
      const code = node.querySelector('code');
      const language = node.getAttribute('data-language') || code?.className.match(/(?:^|\s)language-(\S+)/)?.[1] || '';
      const lines = node.querySelectorAll('.ec-line');
      const source = lines.length ? Array.from(lines, (line) => line.textContent).join('\n') : (code || node).textContent;
      return fence(source, language);
    },
  });
  return converter;
}

function prepareContent(document, metadata, root, targets) {
  const content = document.querySelector('.sl-markdown-content');
  const name = metadata.slug || 'index';
  if (!content) throw new Error(`${name}: provide .sl-markdown-content so the Markdown export includes the page explanation.`);

  for (const element of content.querySelectorAll('script, style, button, input, select, textarea, .copy, .sl-anchor-link, [data-documentation-export-ignore], svg[aria-hidden="true"]')) {
    element.remove();
  }
  for (const interactive of content.querySelectorAll('[data-documentation-interactive], astro-island')) {
    const explanation = Array.from(interactive.querySelectorAll('p, li, table, dl, pre, details, figcaption'), (element) => element.textContent.trim()).join(' ').trim();
    if (!explanation || /^(?:loading|please (?:enable|turn on) javascript|javascript (?:is )?required)[.!…\s]*$/i.test(explanation)) {
      throw new Error(`${name}: render a static explanation inside the interactive component from the same content or scenario data before exporting it.`);
    }
  }
  for (const drawing of content.querySelectorAll('svg')) {
    const labelledBy = (drawing.getAttribute('aria-labelledby') || '').split(/\s+/).filter(Boolean).map((id) => document.getElementById(id)?.textContent.trim()).filter(Boolean);
    const descriptions = [...new Set([
      ...Array.from(drawing.querySelectorAll('title, desc'), (element) => element.textContent.trim()),
      drawing.getAttribute('aria-label') || '',
      ...labelledBy,
    ].filter(Boolean))];
    if (descriptions.length) {
      const explanation = document.createElement('p');
      explanation.textContent = descriptions.join(' ');
      drawing.replaceWith(explanation);
    } else if (drawing.closest('figure')?.querySelector('figcaption')?.textContent.trim()) {
      drawing.remove();
    } else {
      throw new Error(`${name}: give the SVG a title and description or a figure caption so its explanation survives the Markdown export.`);
    }
  }
  for (const element of content.querySelectorAll('a[href], img[src]')) {
    const attribute = element.tagName === 'A' ? 'href' : 'src';
    const value = element.getAttribute(attribute);
    if (!value || value.startsWith('#') || /^(?:mailto:|tel:|data:)/i.test(value)) continue;
    const url = new URL(value, pageUrl(metadata, root));
    const target = url.origin === root.origin ? targets.get(routeKey(url)) : undefined;
    if (target && attribute === 'href') {
      const output = exportUrl(target, root);
      output.search = url.search;
      output.hash = url.hash;
      element.setAttribute(attribute, output.href);
    } else {
      element.setAttribute(attribute, url.href);
    }
  }
  return content;
}

function createIndex(pages, root, converter) {
  const versions = [...new Set(pages.map(({ metadata }) => metadata.version))];
  const lines = [
    '# Allors Documents documentation',
    '',
    '> Human documentation for template authors, developers and maintainers, exported from the same content as the website.',
    '',
    `Documented version: ${versions.join('; ')}. Use documentation from the relevant checkout when working on another branch or version.`,
    '',
    'The links below point to Markdown exports. Audience, kind and status describe each page independently. This index follows the llms.txt discovery proposal; repository instructions remain in AGENTS.md.',
  ];
  const audiences = [...new Set(pages.map(({ metadata }) => metadata.audience))];
  const order = ['Everyone', 'Template authors and developers', 'Template authors', 'Developers', 'Maintainers'];
  audiences.sort((a, b) => (order.includes(a) ? order.indexOf(a) : order.length) - (order.includes(b) ? order.indexOf(b) : order.length) || a.localeCompare(b));
  for (const audience of audiences) {
    lines.push('', `## ${audience}`, '');
    for (const { metadata } of pages.filter((page) => page.metadata.audience === audience).sort((a, b) => a.metadata.slug.localeCompare(b.metadata.slug))) {
      const description = metadata.description.replace(/\s+/g, ' ');
      lines.push(`- [${converter.escape(metadata.title)}](${exportUrl(metadata, root)}): ${description} Audience: ${metadata.audience}; Kind: ${metadata.kind}; Status: ${metadata.status}; Version: ${metadata.version}.`);
    }
  }
  return `${lines.join('\n')}\n`;
}

export async function exportDocumentation({ dir, site, base = '/' }) {
  if (!site) throw new Error('Set Astro site before building documentation Markdown exports.');
  const root = new URL(`/${base.replace(/^\/+|\/+$/g, '')}/`.replace(/^\/\/$/, '/'), site);
  const pages = [];
  const slugs = new Set();
  for (const path of await htmlFiles(dir)) {
    const { document } = parseHTML(await readFile(path, 'utf8'));
    const metadata = readMetadata(document, path);
    if (!metadata) continue;
    const output = metadata.slug || 'index';
    if (slugs.has(output)) throw new Error(`Duplicate documentation slug ${output}: each page needs a distinct Markdown export path.`);
    slugs.add(output);
    pages.push({ document, metadata });
  }
  if (!pages.length) throw new Error('No documentation metadata found: render the data-documentation JSON script on every documentation page.');
  const targets = new Map(pages.map(({ metadata }) => [routeKey(pageUrl(metadata, root)), metadata]));
  const converter = markdownConverter();
  const outputs = pages.map(({ document, metadata }) => {
    const content = prepareContent(document, metadata, root, targets);
    const frontmatter = metadataFields.map((field) => `${field}: ${JSON.stringify(metadata[field])}`).join('\n');
    const markdown = converter.turndown(content.outerHTML);
    return {
      path: join(dir, `${metadata.slug || 'index'}.md`),
      body: `---\n${frontmatter}\nurl: ${JSON.stringify(pageUrl(metadata, root).href)}\n---\n\n# ${converter.escape(metadata.title)}\n\n${markdown}\n`,
    };
  });
  outputs.push({ path: join(dir, 'llms.txt'), body: createIndex(pages, root, converter) });
  for (const { path, body } of outputs) {
    await mkdir(dirname(path), { recursive: true });
    await writeFile(path, body);
  }
  return pages.length;
}

export default function documentationExports() {
  let config;
  return {
    name: 'allors-documentation-exports',
    hooks: {
      'astro:config:done': ({ config: resolvedConfig }) => { config = resolvedConfig; },
      'astro:build:done': async ({ dir, logger }) => {
        const count = await exportDocumentation({ dir: fileURLToPath(dir), site: config.site, base: config.base });
        logger.info(`Exported ${count} documentation pages and llms.txt.`);
      },
    },
  };
}
