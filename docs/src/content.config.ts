import { defineCollection } from 'astro:content';
import { z } from 'astro/zod';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({
    loader: docsLoader(),
    schema: docsSchema({
      extend: z.object({
        description: z.string().min(1),
        audience: z.enum(['Template authors', 'Developers', 'Maintainers', 'Template authors and developers', 'Everyone']),
        kind: z.enum(['Tutorial', 'Explanation', 'How-to guide', 'Reference', 'Index', 'Decision record']),
        status: z.enum(['Current', 'Planned', 'Historical']),
      }),
    }),
  }),
};
