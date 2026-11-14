// Serve a built _site the way GitHub Pages does, for crawling with crawl.mjs.
// Usage: node serve.mjs [site-dir] [port]   (defaults: _site 4000)
// Mounts the site under /clojure-clr-next/, serves index.html for directories,
// falls back from /foo to /foo.html, and returns 404 for anything else.
import { createServer } from "node:http";
import { readFile, stat } from "node:fs/promises";
import { join, normalize } from "node:path";

const ROOT = process.argv[2] || "_site";
const PORT = Number(process.argv[3] || 4000);
const BASE = "/clojure-clr-next/";

async function isFile(p) {
  try { return (await stat(p)).isFile(); } catch { return false; }
}

async function resolve(path) {
  const p = join(ROOT, normalize(decodeURIComponent(path)));
  for (const c of [p, join(p, "index.html"), p + ".html"]) {
    if (await isFile(c)) return c;
  }
  return null;
}

createServer(async (req, res) => {
  const { pathname } = new URL(req.url, "http://localhost");
  if (!pathname.startsWith(BASE)) { res.writeHead(404).end(); return; }
  const rel = pathname.slice(BASE.length);
  // A directory without a trailing slash redirects, as on GitHub Pages.
  if (rel && !rel.endsWith("/") && await isFile(join(ROOT, rel, "index.html")) && !await isFile(join(ROOT, rel))) {
    res.writeHead(301, { Location: pathname + "/" }).end();
    return;
  }
  const file = await resolve(rel);
  if (!file) { res.writeHead(404).end(); return; }
  const body = await readFile(file);
  const TYPES = { html: "text/html; charset=utf-8", xml: "application/xml", css: "text/css", js: "text/javascript",
    svg: "image/svg+xml", png: "image/png", jpg: "image/jpeg", pdf: "application/pdf", json: "application/json" };
  const type = TYPES[file.split(".").pop().toLowerCase()] || "application/octet-stream";
  res.writeHead(200, { "Content-Type": type }).end(req.method === "HEAD" ? undefined : body);
}).listen(PORT, () => console.log(`Serving ${ROOT} at http://localhost:${PORT}${BASE}`));
