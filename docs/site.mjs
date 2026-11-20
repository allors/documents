import versionJson from '../version.json' with { type: 'json' };

// The documented version follows version.json, which Nerdbank.GitVersioning stamps on the packages:
// a prerelease suffix such as 3.3-alpha means unreleased work on main.
const [release, prerelease] = versionJson.version.split('-');
export const title = 'Allors Documents';
export const description = 'Documentation for template authors, developers and maintainers.';
export const version = prerelease ? `main · ${release} (unreleased)` : `main · ${release}`;
export const repository = 'https://github.com/allors/documents';
export const branch = 'main';
// The root of the repository, which holds this website in docs/. A link from a page to a file that is not
// a page becomes a link to that file on GitHub, by its path from this root.
export const repositoryRoot = new URL('../', import.meta.url);
// The audiences that a page can name, in the order in which the agent index lists them. The content
// schema accepts these names and no others.
export const audiences = ['Everyone', 'Template authors and developers', 'Template authors', 'Developers', 'Maintainers'];
export const site = process.env.DOCS_SITE || 'https://docs.allors.com';
const trimmed = (process.env.DOCS_BASE || '/documents/').split('/').filter(Boolean).join('/');
export const base = trimmed ? `/${trimmed}/` : '/';
// The documentation home is published at the root of the same origin by allors/allors.github.io. The
// sidebar links to it, and the link checker leaves that link to the home's own build.
export const home = new URL('/', site).href;
