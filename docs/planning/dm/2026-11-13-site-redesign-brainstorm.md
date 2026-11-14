# Site Redesign Brainstorm - 2026-11-13

## Context

This branch of the ClojureCLR.Next project repository drives the Github Pages I use for blogging about the ClojureCLR.Next project.
It has become larger than I initially anticipated.  Also, in addition to blogs about the ClojureCLR.Next, I have extended the topics covered to include the mainline ClojureCLR project.

The current site is done as a completely vanilla Jekyll setup with the minima theme, relying on the default layouts and styles provided by the theme.  Among other things, this means that the front page is just a reverse chronological list of blog posts.  There are no topical lists to help the user focus on specific areas of interest.  And it has gotten quite long, with over 50 posts published to date.

## Goals for a Site Redesign

My primary goal is to improve the usability and organization of the site, making it easier for visitors to find and engage with content related to both the ClojureCLR.Next and mainline ClojureCLR projects.

A much less significant goal is to modernize the visual design and improve the overall aesthetic appeal of the site.  If the overall look and feel stayed the same, I would be okay with that.  However, it is okay to give some consideration to updating the design to make it more visually appealing and contemporary.

## Potential Improvements

- Introduce topical lists or categories to help users focus on specific areas of interest.
- Improve navigation to make it easier for visitors to find content related to both ClojureCLR.Next and mainline ClojureCLR.
- Consider updating the visual design to make the site more visually appealing and contemporary.
- Enhance search functionality to allow users to quickly locate relevant posts.

## Requirements

- First and foremost, all existing content must be preserved and remain accessible under their current URLs.  There are public links to many of the posts, and these must continue to work without any broken links.  If any changes to URLs are necessary, proper redirects must be implemented to maintain link integrity.
- The site must support categorization and tagging of posts to facilitate topical navigation.
- It is permissible to update the metadata on existing posts to accommodate categorization, tagging, and other organizational improvements.
- Navigation should be intuitive and allow easy access to both ClojureCLR.Next and mainline ClojureCLR content.

## Secondary Requirements

- The visual design should be modern and aesthetically pleasing, while maintaining readability and usability.
- Search functionality must be robust and allow users to quickly find relevant content.
- The site should be responsive and accessible on various devices and screen sizes.
- Performance should be optimized to ensure fast loading times and a smooth user experience.
- Accessibility standards should be met to make the site usable for visitors with disabilities.

## Findings from a review of the current site

### Post URLs are built from the `categories` field

`_config.yml` sets no `permalink`, so Jekyll uses its default, `/:categories/:year/:month/:day/:title:output_ext`.
All 52 posts have `categories: general`, which gives URLs like `/clojure-clr-next/general/2023/01/06/clojureclr-reconsidered.html`.
Changing a post's `categories` would change its public URL.

Mitigation:

- First, set `permalink: /general/:year/:month/:day/:title:output_ext` in `_config.yml`. That freezes every current URL.
- Put the new organization in other front-matter fields (for example `tags`, `project:`, `series:`) rather than in `categories`. Then no redirects are needed.

### Production is not built the way the Gemfile suggests

The Gemfile asks for Jekyll 4.3, but `.github/workflows/jekyll-gh-pages.yml` uses `actions/jekyll-build-pages@v1`.
That action builds with GitHub's own locked-down setup (the `github-pages` gem): Jekyll 3.x and a short approved list of plugins.
So a local preview can differ from production, and plugins not on the list (for example `jekyll-archives`, which makes tag pages automatically) won't run there.

Options:

- (a) Stay inside GitHub's approved setup. Build tag and topic pages by hand with Liquid templates, and add `jekyll-redirect-from` and `jekyll-sitemap`.
- (b) Switch to a standard Ruby/Bundler workflow (`ruby/setup-ruby` + `bundle exec jekyll build`). Then the Gemfile is what actually runs, and any plugin or build step is available, including the Pagefind search tool.

Leaning toward (b): it's a small change to the workflow file, and it makes local and production builds match.

### One post's URL date doesn't match its filename

`2024-06-24-corrigendum-static-initialization.md` has `date: 2024-06-18` in its front matter.
Its live URL uses `/2024/06/18/`, and we need to keep it that way.

### What the content already gives us

- Series are already there:
  - Persistent vectors (4 parts)
  - Micro-benchmarks (3 parts)
  - PersistentHashMap (5 parts)
  - STM (3 parts)
  - Circular reasoning (2 parts)
  - C4 (about 11 posts)

  "Part N of M" links with previous/next on each post would be an easy, useful win.
- Two ways to group posts:
  - By project: ClojureCLR.Next or mainline ClojureCLR.
  - By subject: data structures, the compiler, numbers, performance, concurrency, types and interop.

### Step zero: a URL safety net

Before making any changes, build the current site and save its full list of output URLs, including `feed.xml`, `/about/` and the page numbers.
Check every later build against that list, so a broken URL gets caught before it ships.

## Open questions

1. Which posts are about mainline ClojureCLR? Likely candidates: qualified methods, typename syntax, AOT compilation, maybe runtime async. Is C4 a Next topic or a mainline one?
   - **Answer:** Mostly right. Stringulation is also mainline. C4 analyzes the existing compiler (mostly no F# code), so it goes under mainline ClojureCLR. Per-post assignments are being worked out in `2026-11-13-post-inventory.md`.
2. OK to switch the deploy workflow to option (b)?
   - **Answer:** Either is fine. Going with option (b), the Bundler-based workflow, so local and production builds match and any plugin or build step (such as Pagefind) is available.
3. Front page: a curated landing page (intro, the series, topics, recent posts), or keep the reverse-chronological list with topic navigation added?
   - **Answer:** A curated landing page: intro, the series, topics, recent posts.
4. Should the drafts in `_drafts/` follow the new tagging scheme as well?
   - **Answer:** Yes.

## Plan

1. **Record the current URLs.** Get the full list of URLs the live site serves now (built by the current workflow) and save it as the baseline.
2. **Fix the URL format in the config.** Set `permalink: /general/:year/:month/:day/:title:output_ext` in `_config.yml`. Rebuild and check against the baseline.
3. **Switch the deploy workflow to Bundler (option b).** Rebuild and check against the baseline again.
4. **Add the new front-matter fields** (`project`, `series`, `tags`) to every post and draft, using the marked-up `2026-11-13-post-inventory.md`.
5. **Build the new pages and navigation:** the curated front page, a page per topic and per project, "Part N of M" links within each series, and the nav bar.
6. **Add search with Pagefind.**
7. **Light visual updates**, last and optional.

### Step 1 result (2026-11-13)

Done by crawling the live site rather than rebuilding it locally, since the live site is the source of truth.
The crawler is `_tools/url-check/crawl.mjs`; the baseline is `_tools/url-check/baseline-2026-11-13.txt`.
`_tools/` is ignored by Jekyll, so neither file gets published.
How to check a build is in "Checking a build" below.

A local build of the current source, using the same Docker image as the old workflow (`ghcr.io/actions/jekyll-build-pages:v1.0.13`), matched the live crawl. The only differences were the three broken links (fixed since) and the new post-inventory page.

- 92 URLs: the 52 posts, home, about, 404, `feed.xml`, `data/links.html`, two pages under `docs/`, and 22 assets.
- Some posts link to posts without the `.html` extension (for example `.../2025/10/16/tag-youre-int`). GitHub Pages serves these automatically as long as the `.html` file exists.
- `docs/` is published as part of the site, so these planning docs are publicly visible (for example `/docs/planning/dm/2026-11-13-site-redesign-brainstorm.html`). Consider excluding `docs/planning` in step 2.
- Three broken links already in posts (not caused by this work):
  - `2025-10-16-tag-youre-int.md`: the PDF link has a stray `"` (`expr-analysis.pdf")`).
  - `2025-09-01-AST-me-anything.md`: the image path is `xpr-type-dependencies.dot.png`; the file is `expr-type-dependencies.dot.png`.
  - `2023-02-05-reductionism.md`: the `.` after the `post_url` tag ends up inside the link (`laziness-and-chunking.html.`).

### Step 2 result (2026-11-13)

Added `permalink: /general/:year/:month/:day/:title:output_ext` to `_config.yml`.
The rebuild gave the same 110 files and the same 92 crawled URLs.
The only change in content was the build timestamp in `feed.xml`.

Also excluded `docs/planning/` (these notes) and `src/` (two error logs were being published from it).
Removing the planning docs also removed them from minima's header nav, where they had appeared on every page.
In the github-pages build, setting `exclude` drops the default exclusions, which briefly published `Gemfile` and `Gemfile.lock`. The defaults are now restated in `_config.yml`.
The new reference lists are `_tools/url-check/expected-urls.txt` (90 URLs) and `_tools/url-check/expected-files.txt` (104 files).

### Step 3 result (2026-11-13)

Switched to Jekyll 4.4 with Bundler:

- `Gemfile`:
  - Jekyll `~> 4.4`.
  - minima pinned to `2.5.1`, the version the old build used.
  - Added the plugins the github-pages build turned on implicitly: `jekyll-optional-front-matter`, `jekyll-titles-from-headings`, `jekyll-relative-links`, `jekyll-default-layout`, `jekyll-readme-index`. Without `jekyll-optional-front-matter`, `data/links.md` and `docs/*.md` would stop being pages.
- `Gemfile.lock`: updated, with the `x86_64-linux` platform added so the workflow can install from it.
- `_config.yml`: `sass: sourcemap: never` and `style: compressed`, to match the old CSS output.
- Posts: Jekyll 4's `post_url` adds the base URL itself. The 91 uses of `{{site.baseurl}}{% post_url ... %}` in 34 posts and drafts produced `/clojure-clr-next/clojure-clr-next/...` links, so they are now plain `{% post_url ... %}`.
- Workflow: `.github/workflows/jekyll-gh-pages.yml` now uses `ruby/setup-ruby` and `bundle exec jekyll build` instead of `actions/jekyll-build-pages`.

Compared against the live site:

- Same 104 files and 90 URLs as the expected lists; all return 200.
- Page HTML: across all 57 pages, the only differences are:
  - the generator version
  - small Jekyll SEO tag changes (for example, `article:modified_time` added)
  - the planning-doc nav links
  - the three link fixes

  Code highlighting and post content are unchanged.
- `feed.xml`: only the generator version, the timestamp and one link fix differ.
- `main.css`: the same rules. The newer Sass compiler writes some values differently (for example, `hsl(0,0%,24.67%)` for `#3f3f3f`) and splits one rule in two.
- A clean, frozen `bundle install` from `Gemfile.lock` on Linux (what the workflow does) works.

Line endings: about 25 posts have CRLF line endings in the Windows working copy.
With CRLF, Jekyll can't find the end of the first paragraph, so the excerpt shown on the front page becomes the whole post.
The workflow's Linux checkout has LF, so the live site is unaffected, and `_tools/build.sh` converts to LF before building.
A plain local `jekyll serve` on Windows would show the problem.

Every step that changes the build ends with a check against the expected lists. Steps 1–3 didn't depend on the inventory markup.

## Checking a build

1. Build into `_site` the way the workflow does (Docker, from Git Bash in the repo root): `sh _tools/build.sh`.
2. Serve it the way GitHub Pages does: `node _tools/url-check/serve.mjs _site 4000`.
3. Crawl it: `node _tools/url-check/crawl.mjs out.txt http://localhost:4000`, then diff `out.txt` against `_tools/url-check/expected-urls.txt`.
4. Diff the list of files in `_site` against `_tools/url-check/expected-files.txt`. This catches pages nothing links to:

   ```bash
   (cd _site && find . -type f | sed 's#^\./##' | sort) | diff _tools/url-check/expected-files.txt -
   ```

`_tools/url-check/baseline-2026-11-13.txt` is the original crawl of the live site, kept for reference.

## The final word

Functionality trumps aesthetics.  While a modern and visually appealing design is desirable, it should never come at the expense of usability, accessibility, or the preservation of existing content.