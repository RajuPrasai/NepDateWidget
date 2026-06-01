/**
 * Cloudflare Worker: Markdown content negotiation for AI agents.
 *
 * When a request carries `Accept: text/markdown`, the Worker looks for a
 * pre-authored `.html.md` companion file at the same path. If found, it
 * returns the markdown with Content-Type: text/markdown and x-markdown-tokens.
 * HTML remains the default for all other requests.
 *
 * Covered pages (have .html.md companion):
 *   index, features, download, bikram-sambat, changelog,
 *   nepali-calendar-2082/2083/2084
 *
 * Pages without a companion (gallery, privacy, 404) fall back to HTML.
 *
 * Note: Regular fetch() from a Worker bypasses Worker route matching and goes
 * directly to origin. Service bindings are the only mechanism that routes a
 * subrequest through another Worker. No recursion guard is needed here.
 */

export default {
  async fetch(request) {
    const url = new URL(request.url);
    let path = url.pathname;

    // Normalize path to a concrete .html filename.
    if (path === '/' || path === '') {
      path = '/index.html';
    } else if (path.endsWith('/')) {
      path = path + 'index.html';
    } else {
      const lastSegment = path.split('/').pop();
      if (!lastSegment.includes('.')) {
        // Bare path with no extension — assume HTML page.
        path = path + '.html';
      } else if (!lastSegment.endsWith('.html')) {
        // Static asset (CSS, JS, images, XML, etc.) — pass through unchanged.
        return fetch(request);
      }
      // else: already ends in .html — use as-is.
    }

    const accept = (request.headers.get('Accept') || '').toLowerCase();

    if (accept.includes('text/markdown')) {
      // Build a clean URL for the markdown companion — no query string needed
      // since GitHub Pages serves static files regardless of query parameters.
      const mdUrl = new URL(url.origin + path + '.md');

      const mdReq = new Request(mdUrl.toString(), {
        headers: { Accept: 'text/plain' },
      });

      let markdown = null;
      try {
        const mdResponse = await fetch(mdReq);
        if (mdResponse.ok) {
          markdown = await mdResponse.text();
        }
      } catch {
        // Network error or body read failure — fall through to HTML.
      }

      if (markdown !== null) {
        // Rough token estimate: ~4 characters per token (GPT-style tokeniser average).
        const tokenCount = Math.ceil(markdown.length / 4);

        return new Response(markdown, {
          status: 200,
          headers: {
            'Content-Type': 'text/markdown; charset=utf-8',
            'Vary': 'Accept',
            'x-markdown-tokens': String(tokenCount),
            'Content-Signal': 'ai-train=yes, search=yes, ai-input=yes',
            'Cache-Control': 'public, max-age=3600',
          },
        });
      }
      // Companion not found or unreadable — fall through to HTML response below.
    }

    // Default: serve HTML and add Vary: Accept so edge caches keep
    // HTML and markdown variants separate.
    const htmlResponse = await fetch(request);
    const headers = new Headers(htmlResponse.headers);
    headers.set('Vary', 'Accept');
    return new Response(htmlResponse.body, {
      status: htmlResponse.status,
      statusText: htmlResponse.statusText,
      headers,
    });
  },
};
