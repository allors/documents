import { visit } from 'unist-util-visit';

// An Expressive Code plugin that makes every code block keyboard focusable when it is rendered.
// A code block scrolls horizontally when a line is wider than the page, which depends on the reader's
// fonts, and a scrollable region must be reachable from the keyboard (axe: scrollable-region-focusable).
// Expressive Code's own client script adds the tabindex only after a debounce and an idle callback, which
// a reader or an accessibility check can beat; it removes the attribute again from a block that does not
// scroll, so a block that scrolls keeps the attribute from the start.
export default function focusableCodeBlocks() {
  return {
    name: 'Focusable code blocks',
    hooks: {
      postprocessRenderedBlock: ({ renderData }) => {
        visit(renderData.blockAst, (node) => {
          if (node.type === 'element' && node.tagName === 'pre') node.properties.tabIndex = 0;
        });
      },
    },
  };
}
