#!/bin/sh
# Build the site into _site the way the GitHub workflow does: Ruby 3.3, the
# Gemfile, JEKYLL_ENV=production. Runs in Docker, so no local Ruby is needed.
# Run from Git Bash in the repository root:  sh _tools/build.sh
#
# The build runs on a copy of the source with CRLF line endings converted to LF,
# matching the workflow's Linux checkout. (With CRLF, Jekyll misses the blank
# line that ends a post's excerpt, so the front page shows whole posts.)
# Installed gems are cached in the Docker volume "jekyll-gems".
MSYS_NO_PATHCONV=1 docker run --rm \
  -v "$(pwd -W 2>/dev/null || pwd):/srv/site" \
  -v jekyll-gems:/usr/local/bundle \
  ruby:3.3 sh -c '
    set -e
    mkdir /tmp/s
    cd /srv/site
    tar --exclude=.git --exclude=_site -cf - . | tar -xf - -C /tmp/s
    cd /tmp/s
    find . -type f \( -name "*.md" -o -name "*.markdown" -o -name "*.html" -o -name "*.yml" \) -exec sed -i "s/\r\$//" {} +
    bundle install --quiet
    rm -rf /srv/site/_site
    JEKYLL_ENV=production bundle exec jekyll build --quiet -d /srv/site/_site 2>&1 | grep -v -i -E "deprecat|sass|color\.|math\.div|[╷│╵]|^ *$|@import|root stylesheet|More info|Suggestion|Recommendation|migrator|repetitive|verbose" || true
    echo "Built $(find /srv/site/_site -type f | wc -l) files into _site"
  '
