import { existsSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import GithubSlugger from 'github-slugger';
import { visit } from 'unist-util-visit';
import { repository, branch } from '../site.mjs';

const contentRoot = fileURLToPath(new URL('../src/content/docs/', import.meta.url));
const repositoryRoot = fileURLToPath(new URL('../../', import.meta.url));
const headingIdsByFile = new Map();

function headingText(markdown) {
  return markdown
    .replace(/\s+#+\s*$/, '')
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/`([^`]*)`/g, '$1')
    .replace(/(^|\s)_([^_]+)_(?=\s|$)/g, '$1$2')
    .replace(/[*~]+/g, '');
}

/** The ids GitHub gives the headings and explicit anchors of a Markdown file in this repository. */
function headingIds(file) {
  if (!headingIdsByFile.has(file)) {
    const slugger = new GithubSlugger();
    const ids = new Set();
    let fence = null;
    for (const line of readFileSync(file, 'utf8').split(/\r?\n/)) {
      const delimiter = line.match(/^ {0,3}(`{3,}|~{3,})/);
      if (delimiter) {
        if (!fence) fence = delimiter[1][0];
        else if (delimiter[1][0] === fence) fence = null;
        continue;
      }
      if (fence) continue;
      const heading = line.match(/^ {0,3}#{1,6}\s+(.*)$/);
      if (heading) ids.add(slugger.slug(headingText(heading[1])));
      for (const anchor of line.matchAll(/<a\s+(?:id|name)="([^"]+)"/g)) ids.add(anchor[1]);
    }
    headingIdsByFile.set(file, ids);
  }
  return headingIdsByFile.get(file);
}

export function rewriteLink(url, source, base) {
  if (!url || /^(?:[a-z][a-z\d+.-]*:|\/|#)/i.test(url)) return url;
  const [, pathname, suffix = ''] = url.match(/^([^?#]*)(.*)$/);
  const target = path.resolve(path.dirname(source), decodeURIComponent(pathname));
  if (!existsSync(target)) throw new Error(`Broken documentation link in ${source}: ${url}. Create the target or correct the link.`);
  const contentPath = path.relative(contentRoot, target);
  if (!contentPath.startsWith('..') && /\.mdx?$/.test(contentPath)) {
    const slug = contentPath.replace(/\.mdx?$/, '').replace(/(^|\/)index$/, '').replace(/\/$/, '');
    return `${base}${slug ? `${slug}/` : ''}${suffix}`;
  }
  const repoPath = path.relative(repositoryRoot, target);
  if (repoPath.startsWith('..')) throw new Error(`Documentation link leaves the repository: ${url} in ${source}.`);
  const kind = statSync(target).isDirectory() ? 'tree' : 'blob';
  const fragment = suffix.includes('#') ? decodeURIComponent(suffix.slice(suffix.indexOf('#') + 1)) : '';
  if (fragment && kind === 'blob' && /\.mdx?$/i.test(repoPath) && !headingIds(target).has(fragment)) {
    throw new Error(`Broken heading anchor in ${source}: ${url}. ${repoPath} has no heading with id "${fragment}". Use the id GitHub generates for an existing heading, or remove the fragment.`);
  }
  return `${repository}/${kind}/${branch}/${repoPath.split(path.sep).map(encodeURIComponent).join('/')}${suffix}`;
}

export default function sourceLinks({ base }) {
  return (tree, file) => {
    visit(tree, (node) => {
      if (node.type === 'link' || node.type === 'definition') {
        node.url = rewriteLink(node.url, file.path, base);
      }
    });
  };
}
