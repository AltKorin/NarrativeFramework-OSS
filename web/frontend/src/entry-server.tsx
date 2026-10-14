// Build-time HTML only: no Node server is needed in the deployed image.
import { renderToString } from "react-dom/server";
import Landing from "./Landing";
import type { Language } from "./i18n";
export function render(
  language: Language,
  config: {
    applicationUrl: string;
    repositoryUrl: string | null;
    supportUrl: string | null;
  },
) {
  return renderToString(<Landing initialLang={language} siteConfig={config} />);
}

import Blog from "./Blog";
import { posts, postPath, postBody } from "./posts";
export { posts, postPath, postBody };
export function renderBlog(language: Language, slug = "") {
  return renderToString(<Blog initialLang={language} slug={slug} />);
}
