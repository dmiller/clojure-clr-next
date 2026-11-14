# Generates one page per topic (post tag) at /topics/<tag>/, using _layouts/topic.html.
# Display names come from _data/topics.yml.
module ClojureClrNext
  class TopicPage < Jekyll::PageWithoutAFile
    def initialize(site, tag)
      super(site, site.source, File.join("topics", tag), "index.html")
      info = (site.data["topics"] || {})[tag] || {}
      self.data = {
        "layout" => "topic",
        "topic"  => tag,
        "title"  => info["name"] || tag,
      }
    end
  end

  class TopicPageGenerator < Jekyll::Generator
    safe true
    priority :low

    def generate(site)
      site.tags.each_key do |tag|
        site.pages << TopicPage.new(site, tag)
      end
    end
  end
end
