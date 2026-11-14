// Crawl the live site and list every same-site URL reachable from the seeds.
// Usage: node crawl.mjs <out-file> [origin]   (origin defaults to the live site)
import { writeFileSync } from "node:fs";

const LIVE = "https://dmiller.github.io";
const ORIGIN = process.argv[3] || LIVE;
const BASE = "/clojure-clr-next/";
const seeds = ["", "feed.xml", "about/", "404.html"].map(s => ORIGIN + BASE + s);

const seen = new Map(); // url -> status
const queue = [...seeds];

const isPage = u => /(\/|\.html|\.xml)$/.test(new URL(u).pathname);

function normalize(href, from) {
  try {
    // Built pages link with absolute live-site URLs; crawl those locally too.
    const u = new URL(href.replace(/&amp;/g, "&").replace(LIVE, ORIGIN), from);
    u.hash = "";
    if (u.origin !== ORIGIN || !u.pathname.startsWith(BASE)) return null;
    return u.origin + u.pathname + u.search;
  } catch { return null; }
}

while (queue.length) {
  const url = queue.shift();
  if (seen.has(url)) continue;
  seen.set(url, "pending");
  let res;
  try {
    res = await fetch(url, { method: isPage(url) ? "GET" : "HEAD", redirect: "manual" });
  } catch (e) { seen.set(url, "ERR " + e.message); continue; }
  seen.set(url, String(res.status));
  if (res.status >= 300 && res.status < 400) {
    const loc = normalize(res.headers.get("location") || "", url);
    if (loc) queue.push(loc);
    continue;
  }
  if (!isPage(url) || res.status !== 200) continue;
  const body = await res.text();
  for (const m of body.matchAll(/(?:href|src)=["']([^"']+)["']|<link>([^<]+)<\/link>|<id>([^<]+)<\/id>/g)) {
    const n = normalize(m[1] || m[2] || m[3], url);
    if (n && !seen.has(n)) queue.push(n);
  }
}

const lines = [...seen].map(([u, s]) => `${s}\t${u.slice(ORIGIN.length)}`).sort((a, b) => a.split("\t")[1].localeCompare(b.split("\t")[1]));
writeFileSync(process.argv[2], lines.join("\n") + "\n");
console.log(`${lines.length} URLs; non-200: ${lines.filter(l => !l.startsWith("200")).length}`);
