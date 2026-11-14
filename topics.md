---
layout: page
title: Topics
permalink: /topics/
---

{% for entry in site.data.topics -%}
{%- assign t = entry[0] -%}
{%- assign posts = site.tags[t] -%}
{%- if posts -%}
<h2 id="{{ t }}"><a href="{{ '/topics/' | append: t | append: '/' | relative_url }}">{{ entry[1].name }}</a> <span class="post-meta">{{ posts.size }}</span></h2>
{% include post-list.html posts=posts compact=true %}
{%- endif -%}
{%- endfor -%}
