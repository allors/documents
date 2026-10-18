import versionJson from '../version.json' with { type: 'json' };

// The documented version follows version.json, which Nerdbank.GitVersioning stamps on the packages:
// a prerelease suffix such as 3.3-alpha means unreleased work on main.
const [release, prerelease] = versionJson.version.split('-');
export const version = prerelease ? `main · ${release} (unreleased)` : `main · ${release}`;
export const repository = 'https://github.com/allors/documents';
export const branch = 'main';
export const site = process.env.DOCS_SITE || 'https://allors.github.io';
const trimmed = (process.env.DOCS_BASE || '/documents/').split('/').filter(Boolean).join('/');
export const base = trimmed ? `/${trimmed}/` : '/';
