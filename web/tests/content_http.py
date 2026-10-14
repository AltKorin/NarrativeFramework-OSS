"""Search-visible HTML, real status codes and feeds, with no browser JavaScript."""

import json
import os
from html.parser import HTMLParser
from urllib.error import HTTPError
from urllib.request import urlopen
from urllib.parse import urlparse
from xml.etree import ElementTree as ET

base = os.environ.get("SITE_TEST_BASE", "http://localhost:8080").rstrip("/")
origin = "https://narrativeframework.com"


class Document(HTMLParser):
    def __init__(self, html):
        super().__init__()
        self.links = []
        self.meta = {}
        self.headings = 0
        self.structured = []
        self.current = None
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "link":
            self.links.append(attrs)
        if tag == "meta":
            self.meta[attrs.get("name") or attrs.get("property")] = attrs.get("content")
        if tag == "h1":
            self.headings += 1
        if tag == "script" and attrs.get("type") == "application/ld+json":
            self.current = ""

    def handle_data(self, data):
        if self.current is not None:
            self.current += data

    def handle_endtag(self, tag):
        if tag == "script" and self.current is not None:
            self.structured.append(json.loads(self.current))
            self.current = None


def get(path):
    with urlopen(base + path, timeout=20) as r:
        return r.status, r.read().decode(), r.headers


_, raw, _ = get("/sitemap.xml")
root = ET.fromstring(raw)
ns = {
    "s": "http://www.sitemaps.org/schemas/sitemap/0.9",
    "x": "http://www.w3.org/1999/xhtml",
}
urls = root.findall("s:url", ns)
assert len(urls) == 11
paths = []
for url in urls:
    location = url.find("s:loc", ns).text
    assert location.startswith(origin + "/") and not any(
        x in location for x in ["/api/", "/campaigns", "/workshop", "?", "#"]
    )
    path = urlparse(location).path
    paths.append(path)
    status, html, headers = get(path)
    assert status == 200
    doc = Document(html)
    assert doc.headings == 1, (path, doc.headings)
    assert (
        doc.meta.get("description")
        and doc.meta.get("og:title")
        and doc.meta.get("og:image")
    )
    assert any(
        x.get("rel") == "canonical" and x.get("href") == location for x in doc.links
    ), path
    langs = {x.get("hreflang"): x.get("href") for x in doc.links if x.get("hreflang")}
    assert set(langs) == {"en", "pl", "x-default"}, path
    for target in langs.values():
        assert get(urlparse(target).path)[0] == 200
    css = [x["href"] for x in doc.links if x.get("rel") == "stylesheet"]
    assert css and all(get(x)[0] == 200 for x in css), (path, css)
    if "/blog/" in path and path not in ["/pl/blog/", "/en/blog/"]:
        data = doc.structured[0]
        assert (
            isinstance(data, list)
            and data[0]["@type"] == "BlogPosting"
            and data[1]["@type"] == "BreadcrumbList"
        )
        assert data[0]["mainEntityOfPage"]["@id"] == location
        assert data[0]["headline"] in html
        assert "<h2 id=" in html
    assert "<link" in html and '<div id="root">' in html
assert len(set(paths)) == 11
for lang in ("pl", "en"):
    _, raw, headers = get(f"/{lang}/feed.xml")
    feed = ET.fromstring(raw)
    assert len(feed.findall("./channel/item")) == 3
    for item in feed.findall("./channel/item"):
        assert item.find("link").text.startswith(f"{origin}/{lang}/blog/")
    for route in ("/workshop/", "/campaigns/"):
        _, html, _ = get(route)
        assert Document(html).meta["robots"] == "noindex,nofollow"
for path in [
    "/pl/blog/not-a-real-article/",
    "/en/blog/not-a-real-article/",
    "/this-page-does-not-exist/",
    "/missing.css",
]:
    try:
        get(path)
        raise AssertionError("Soft 404: " + path)
    except HTTPError as error:
        assert error.code == 404, (path, error.code)
_, robots, _ = get("/robots.txt")
assert f"Sitemap: {origin}/sitemap.xml" in robots
for prefix in ["/api/", "/campaigns", "/workshop"]:
    assert "Disallow: " + prefix in robots
print(
    "PASS: 11 static public pages, article/breadcrumb JSON-LD, canonical/hreflang, CSS without JS, XML sitemap/RSS, private noindex and real 404s"
)
