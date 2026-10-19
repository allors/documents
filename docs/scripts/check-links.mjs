import { readFile, readdir, stat } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseHTML } from 'linkedom';
import remarkParse from 'remark-parse';
import { unified } from 'unified';

const defaultOrigin = 'https://allors.github.io';
const markdown = unified().use(remarkParse);

async function publishedFiles(directory) {
  const files = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const filename = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await publishedFiles(filename));
    else if (entry.isFile() && (/\.(html|md)$/.test(entry.name) || entry.name === 'llms.txt')) {
      files.push(filename);
    }
  }
  return files.sort();
}

function visit(node, callback) {
  callback(node);
  for (const child of node.children ?? []) visit(child, callback);
}

function htmlLinks(document) {
  const links = [];
  for (const element of document.querySelectorAll('[href], [src]')) {
    for (const attribute of ['href', 'src']) {
      if (element.hasAttribute(attribute)) {
        links.push({ url: element.getAttribute(attribute), location: `<${element.localName} ${attribute}>` });
      }
    }
  }
  return links;
}

function markdownLinks(content) {
  const tree = markdown.parse(content);
  const definitions = new Map();
  const links = [];
  visit(tree, (node) => {
    if (node.type === 'definition') definitions.set(node.identifier, node.url);
  });
  visit(tree, (node) => {
    const location = `:${node.position?.start.line ?? 1}`;
    if (node.type === 'link' || node.type === 'image') {
      links.push({ url: node.url, location });
    } else if (node.type === 'linkReference' || node.type === 'imageReference') {
      const url = definitions.get(node.identifier);
      if (url !== undefined) links.push({ url, location });
    } else if (node.type === 'html') {
      for (const link of htmlLinks(parseHTML(node.value).document)) links.push({ ...link, location });
    }
  });
  return links;
}

function publicURL(filename, directory, base) {
  let relative = path.relative(directory, filename).split(path.sep).join('/');
  if (relative === 'index.html') relative = '';
  else if (relative.endsWith('/index.html')) relative = relative.slice(0, -'index.html'.length);
  return new URL(relative, base);
}

async function findFile(filename) {
  for (const candidate of [filename, path.join(filename, 'index.html'), `${filename}.html`]) {
    try {
      if ((await stat(candidate)).isFile()) return candidate;
    } catch (error) {
      if (error.code !== 'ENOENT' && error.code !== 'ENOTDIR') throw error;
    }
  }
  return undefined;
}

/** Check published links without making network requests. */
export async function checkLinks(outDir, baseURL = '/documents/') {
  const directory = path.resolve(outDir);
  const base = new URL(baseURL, defaultOrigin);
  if (!base.pathname.endsWith('/')) base.pathname += '/';
  base.search = '';
  base.hash = '';
  const errors = [];
  const relativeTo = (filename) => path.relative(directory, filename).split(path.sep).join('/');
  const documents = new Map();
  const targets = new Map();
  let links = 0;

  async function documentFor(filename) {
    if (!documents.has(filename)) {
      documents.set(filename, parseHTML(await readFile(filename, 'utf8')).document);
    }
    return documents.get(filename);
  }

  async function targetFor(filename) {
    if (!targets.has(filename)) targets.set(filename, await findFile(filename));
    return targets.get(filename);
  }

  let files;
  try {
    files = await publishedFiles(directory);
  } catch (error) {
    if (error.code === 'ENOENT') throw new Error(`Build output does not exist at ${directory}. Run npm run build first.`);
    throw error;
  }

  for (const filename of files) {
    const relative = path.relative(directory, filename).split(path.sep).join('/');
    const sourceURL = publicURL(filename, directory, base);
    const references = filename.endsWith('.html')
      ? htmlLinks(await documentFor(filename))
      : markdownLinks(await readFile(filename, 'utf8'));

    for (const reference of references) {
      const context = `${relative}${reference.location.startsWith(':') ? '' : ' '}${reference.location}: ${JSON.stringify(reference.url)}`;
      let url;
      try {
        url = new URL(reference.url, sourceURL);
      } catch {
        errors.push(`${context} is not a valid URL. Correct the link destination.`);
        continue;
      }
      if (!['http:', 'https:'].includes(url.protocol) || url.host !== base.host) continue;
      links += 1;
      if (!url.pathname.startsWith(base.pathname) && url.pathname !== base.pathname.slice(0, -1)) {
        errors.push(`${context} is outside deployment base ${base.pathname}. Include the deployment base in the local URL.`);
        continue;
      }

      let localPath;
      let fragment;
      try {
        localPath = decodeURIComponent(url.pathname.slice(base.pathname.length));
        fragment = decodeURIComponent(url.hash.slice(1)).split(':~:text=')[0];
      } catch {
        errors.push(`${context} contains invalid percent encoding. Correct the URL encoding.`);
        continue;
      }
      const requested = path.resolve(directory, localPath);
      if (requested !== directory && !requested.startsWith(`${directory}${path.sep}`)) {
        errors.push(`${context} resolves outside the build output. Correct the local path.`);
        continue;
      }
      const target = await targetFor(requested);
      if (!target) {
        errors.push(`${context} target does not exist at ${relativeTo(requested) || 'index.html'}. Correct the link or publish the missing file.`);
        continue;
      }
      if (!fragment || !/\.(html|md)$/.test(target)) continue;

      let htmlTarget = target;
      if (target.endsWith('.md')) {
        const counterpart = target.endsWith(`${path.sep}index.md`)
          ? target.slice(0, -'index.md'.length)
          : target.slice(0, -'.md'.length);
        htmlTarget = await targetFor(counterpart);
        if (!htmlTarget?.endsWith('.html')) {
          errors.push(`${context} has a fragment but its corresponding HTML page does not exist. Publish the HTML page alongside the Markdown export.`);
          continue;
        }
      }
      const document = await documentFor(htmlTarget);
      const anchors = new Set([...document.querySelectorAll('[id], a[name]')]
        .flatMap((element) => [element.getAttribute('id'), element.getAttribute('name')])
        .filter((anchor) => anchor !== null));
      if (!anchors.has(fragment)) {
        errors.push(`${context} fragment #${fragment} does not exist in ${relativeTo(htmlTarget)}. Use an existing heading or anchor ID.`);
      }
    }
  }
  return { files: files.length, links, errors };
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try {
    const base = new URL(process.env.DOCS_BASE || '/documents/', process.env.DOCS_SITE || defaultOrigin);
    const result = await checkLinks(fileURLToPath(new URL('../dist/', import.meta.url)), base);
    for (const error of result.errors) console.error(error);
    console.log(`Checked ${result.links} local links across ${result.files} published files; ${result.errors.length} errors.`);
    if (result.errors.length > 0) process.exitCode = 1;
  } catch (error) {
    console.error(`Link check failed: ${error.message}`);
    process.exitCode = 1;
  }
}
