import versionJson from '../version.json' with { type: 'json' };

// The documented version follows version.json, which Nerdbank.GitVersioning stamps on the packages:
// a prerelease suffix such as 3.3-alpha means unreleased work on main.
const [release, prerelease] = versionJson.version.split('-');
export const version = prerelease ? `main · ${release} (unreleased)` : `main · ${release}`;
export const repository = 'https://github.com/allors/documents';
export const branch = 'main';
export const site = process.env.DOCS_SITE || 'https://docs.allors.com';
const trimmed = (process.env.DOCS_BASE || '/documents/').split('/').filter(Boolean).join('/');
export const base = trimmed ? `/${trimmed}/` : '/';
// The documentation home is published at the root of the same origin by allors/allors.github.io. The
// sidebar links to it, and the link checker leaves that link to the home's own build.
export const home = new URL('/', site).href;
