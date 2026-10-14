import React, { Suspense, lazy } from "react";
import { createRoot } from "react-dom/client";
const Workshop = lazy(() => import("./Workshop"));
const CampaignWiki = lazy(() => import("./CampaignWiki"));
const inWiki = /^\/campaigns(?:\/|$)/.test(location.pathname);
const Landing = lazy(() => import("./Landing"));
const inWorkshop = /^\/workshop(?:\/|$)/.test(location.pathname);
createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <Suspense fallback={<p role="status">Narrative Framework…</p>}>
      {inWiki ? <CampaignWiki /> : inWorkshop ? <Workshop /> : <Landing />}
    </Suspense>
  </React.StrictMode>,
);
