import { visit } from 'unist-util-visit';

export default function scrollableContent() {
  return (tree) => {
    visit(tree, (node) => {
      if (node.type === 'element' && node.tagName === 'table') node.properties.tabIndex = 0;
      if (node.type === 'element' && node.tagName === 'pre' &&
          (node.properties.className?.includes('mermaid') ||
           node.children.some((child) => child.properties?.className?.includes('language-mermaid')))) {
        Object.assign(node.properties, { tabIndex: 0, role: 'region', ariaLabel: 'Diagram' });
      }
      if (node.type === 'raw' && node.value.startsWith('<pre class="mermaid">')) {
        node.value = node.value.replace('<pre class="mermaid">', '<pre class="mermaid" tabindex="0" role="region" aria-label="Diagram">');
      }
    });
  };
}
