/**
 * Cloudflare Worker: Markdown content negotiation for AI agents.
 *
 * When a request carries `Accept: text/markdown`, the Worker looks for a
 * pre-authored `.html.md` companion file at the same path. If found, it
 * returns the markdown with Content-Type: text/markdown and x-markdown-tokens.
 * HTML remains the default for all other requests.
 *
 * Covered pages (have .html.md companion):
 *   nepdatewidget.rajuprasai.com.np - index, features, download, bikram-sambat,
 *     changelog, nepali-calendar-2082/2083/2084
 *   nepdate.rajuprasai.com.np - index, docs, changelog
 *   rajuprasai.com.np - index
 *
 * Pages without a companion fall back to HTML.
 *
 * Note: Regular fetch() from a Worker bypasses Worker route matching and goes
 * directly to origin. Service bindings are the only mechanism that routes a
 * subrequest through another Worker. No recursion guard is needed here.
 */

export default {
  async fetch(request) {
    const url = new URL(request.url);
    let path = url.pathname;

    // .well-known paths are API/discovery files - never HTML pages.
    // Pass through directly; for api-catalog, also fix the Content-Type.
    if (path.startsWith('/.well-known/')) {
      const apiResponse = await fetch(request);
      if (!path.endsWith('/api-catalog') || !apiResponse.ok) {
        return apiResponse;
      }
      const fixedHeaders = new Headers(apiResponse.headers);
      fixedHeaders.set('Content-Type', 'application/linkset+json; profile="https://www.rfc-editor.org/info/rfc9727"');
      return new Response(apiResponse.body, {
        status: apiResponse.status,
        statusText: apiResponse.statusText,
        headers: fixedHeaders,
      });
    }

    // RFC 8288 Link headers for service discovery, keyed by hostname.
    const LINK_HEADERS = {
      'nepdatewidget.rajuprasai.com.np':
        '</.well-known/api-catalog>; rel="api-catalog"; type="application/linkset+json"'
        + ', </.well-known/mcp/server-card.json>; rel="service-desc"; type="application/json"'
        + ', </docs/api-reference.md>; rel="service-doc"; type="text/markdown"'
        + ', </.well-known/agent-skills/index.json>; rel="describedby"; type="application/json"',
      'nepdate.rajuprasai.com.np':
        '</.well-known/api-catalog>; rel="api-catalog"; type="application/linkset+json"'
        + ', </.well-known/mcp/server-card.json>; rel="service-desc"; type="application/json"'
        + ', </docs.html.md>; rel="service-doc"; type="text/markdown"'
        + ', </.well-known/agent-skills/index.json>; rel="describedby"; type="application/json"',
      'rajuprasai.com.np':
        '</.well-known/api-catalog>; rel="api-catalog"; type="application/linkset+json"'
        + ', </.well-known/mcp/server-card.json>; rel="service-desc"; type="application/json"'
        + ', </index.html.md>; rel="service-doc"; type="text/markdown"'
        + ', </.well-known/agent-skills/index.json>; rel="describedby"; type="application/json"',
    };
    const linkHeader = LINK_HEADERS[url.hostname];

    // Normalize path to a concrete .html filename.
    if (path === '/' || path === '') {
      path = '/index.html';
    } else if (path.endsWith('/')) {
      path = path + 'index.html';
    } else {
      const lastSegment = path.split('/').pop();
      if (!lastSegment.includes('.')) {
        // Bare path with no extension - assume HTML page.
        path = path + '.html';
      } else if (!lastSegment.endsWith('.html')) {
        // Static asset (CSS, JS, images, XML, etc.) - pass through unchanged.
        return fetch(request);
      }
      // else: already ends in .html - use as-is.
    }

    const accept = (request.headers.get('Accept') || '').toLowerCase();

    if (accept.includes('text/markdown')) {
      // Build a clean URL for the markdown companion - no query string needed
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
        // Network error or body read failure - fall through to HTML.
      }

      if (markdown !== null) {
        // Rough token estimate: ~4 characters per token (GPT-style tokeniser average).
        const tokenCount = Math.ceil(markdown.length / 4);

        const mdHeaders = {
          'Content-Type': 'text/markdown; charset=utf-8',
          'Vary': 'Accept',
          'x-markdown-tokens': String(tokenCount),
          'Content-Signal': 'ai-train=yes, search=yes, ai-input=yes',
          'Cache-Control': 'public, max-age=3600',
        };
        if (linkHeader) mdHeaders['Link'] = linkHeader;
        return new Response(markdown, { status: 200, headers: mdHeaders });
      }
      // Companion not found or unreadable - fall through to HTML response below.
    }

    // Default: serve HTML and add Vary: Accept so edge caches keep
    // HTML and markdown variants separate.
    const htmlResponse = await fetch(request);
    const headers = new Headers(htmlResponse.headers);
    headers.set('Vary', 'Accept');
    if (linkHeader) headers.set('Link', linkHeader);
    return new Response(htmlResponse.body, {
      status: htmlResponse.status,
      statusText: htmlResponse.statusText,
      headers,
    });
  },
};
