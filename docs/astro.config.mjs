import { defineConfig } from 'astro/config';
import { unified } from '@astrojs/markdown-remark';
import starlight from '@astrojs/starlight';
import mermaid from 'astro-mermaid';
import sourceLinks from './integrations/source-links.mjs';
import scrollableContent from './integrations/scrollable-content.mjs';
import focusableCodeBlocks from './integrations/focusable-code-blocks.mjs';
import documentationExports from './integrations/exports.mjs';
import { site, base, repository } from './site.mjs';

export default defineConfig({
  site,
  base,
  trailingSlash: 'always',
  markdown: {
    processor: unified({ remarkPlugins: [[sourceLinks, { base }]], rehypePlugins: [scrollableContent] }),
  },
  integrations: [
    mermaid({ autoTheme: true, enableLog: false, mermaidConfig: { securityLevel: 'strict' } }),
    starlight({
      title: 'Allors Documents',
      favicon: '/favicon.png',
      description: 'Documentation for template authors, developers and maintainers.',
      social: [{ icon: 'github', label: 'GitHub', href: repository }],
      editLink: { baseUrl: `${repository}/edit/main/docs/` },
      components: { PageTitle: './src/components/PageTitle.astro' },
      customCss: ['./src/styles/documentation.css'],
      expressiveCode: { plugins: [focusableCodeBlocks()] },
      sidebar: [
        { label: 'Documentation', slug: '' },
        { label: 'For template authors', items: [
          { slug: 'write-a-template' }, { slug: 'tags' }, { slug: 'formatting' }, { slug: 'pictures' }, { slug: 'maps' }, { slug: 'findings' },
        ] },
        { label: 'For developers', items: [
          { slug: 'render-your-first-document' }, { slug: 'validate-templates' }, { slug: 'resolve-your-own-model' }, { slug: 'renderers' }, { slug: 'api' }, { slug: 'design' },
        ] },
        { label: 'Terminology', slug: 'terminology' },
        { label: 'For maintainers', items: [{ autogenerate: { directory: 'internals' } }] },
      ],
    }),
    documentationExports(),
  ],
});
