---
layout: page
title: Series
permalink: /series/
---

Multi-part posts, read in order. The most recent series comes first.

{% assign series_names = site.posts | map: "series" | compact | uniq -%}
{%- for s in series_names -%}
{%- assign parts = site.posts | where: "series", s | sort: "series_part" -%}
{%- assign first = parts | first -%}
<h2 id="{{ s | slugify }}">{{ s }}</h2>
<p class="post-meta">{{ parts.size }} parts · <a href="{{ site.data.projects[first.project].url | relative_url }}">{{ site.data.projects[first.project].name }}</a></p>
<ol class="series-parts">
{%- for p in parts -%}
<li><a href="{{ p.url | relative_url }}">{{ p.title | escape }}</a> <span class="post-meta">{{ p.date | date: "%b %-d, %Y" }}</span></li>
{%- endfor -%}
</ol>
{%- endfor -%}
