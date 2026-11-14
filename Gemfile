source "https://rubygems.org"
# Hello! This is where you manage which Jekyll version is used to run.
# When you want to use a different version, change it below, save the
# file and run `bundle install`. Run Jekyll with `bundle exec`, like so:
#
#     bundle exec jekyll serve
#
# This will help ensure the proper Jekyll version is running.
# Happy Jekylling!
gem "jekyll", "~> 4.4"
# Pinned to the version the old github-pages build used, so the switch to this
# Gemfile doesn't change the site's look. Unpin when redesigning.
gem "minima", "= 2.5.1"
# The site is built by .github/workflows/jekyll-gh-pages.yml with this Gemfile,
# not with the github-pages gem.
group :jekyll_plugins do
  gem "jekyll-feed", "~> 0.12"
  # The github-pages build turned these on implicitly. The site depends on them:
  # for example, .md files without front matter (data/, docs/) become pages.
  gem "jekyll-optional-front-matter"
  gem "jekyll-titles-from-headings"
  gem "jekyll-relative-links"
  gem "jekyll-default-layout"
  gem "jekyll-readme-index"
end

# Windows and JRuby does not include zoneinfo files, so bundle the tzinfo-data gem
# and associated library.
platforms :mingw, :x64_mingw, :mswin, :jruby do
  gem "tzinfo", ">= 1", "< 3"
  gem "tzinfo-data"
end

# Performance-booster for watching directories on Windows
gem "wdm", "~> 0.1.1", :platforms => [:mingw, :x64_mingw, :mswin]

# Lock `http_parser.rb` gem to `v0.6.x` on JRuby builds since newer versions of the gem
# do not have a Java counterpart.
gem "http_parser.rb", "~> 0.6.0", :platforms => [:jruby]
