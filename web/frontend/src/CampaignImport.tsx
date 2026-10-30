import { useRef, useState } from "react";
import { newUUID } from "./uuid";

type Template = {
  schema: string;
  title: string;
  summary?: string;
  pages: { kind: string }[];
  events: unknown[];
  relations?: unknown[];
};
export default function CampaignImport({ pl, request }: {
  pl: boolean;
  request: (path: string, method?: string, body?: unknown) => Promise<any>;
}) {
  const t = (a: string, b: string) => pl ? a : b;
  const [template, setTemplate] = useState<Template | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const receipt = useRef(newUUID());
  const sequence = useRef(0);
  async function read(file?: File) {
    const current = ++sequence.current;
    setTemplate(null); setError(""); receipt.current = newUUID();
    if (!file) return;
    try {
      if (file.size > 450000) throw Error(t("Plik jest za duży (limit 450 KB).", "File too large (450 KB limit)."));
      const value = JSON.parse(await file.text());
      if (current !== sequence.current) return;
      if (value?.schema !== "narrative-campaign-template/1" || typeof value.title !== "string" || (value.summary !== undefined && typeof value.summary !== "string") || !Array.isArray(value.pages) || !value.pages.every((p:unknown) => p && typeof p === "object" && "kind" in p && typeof p.kind === "string") || !Array.isArray(value.events)) {
        throw Error(t("Wybierz szablon kampanii narrative-campaign-template/1. PDF i eksport wiki mają inny format.", "Choose a narrative-campaign-template/1 template. PDF and wiki exports use different formats."));
      }
      setTemplate(value);
    } catch (e) {
      if (current === sequence.current) setError(e instanceof SyntaxError ? t("Niepoprawny plik JSON.", "Invalid JSON file.") : (e as Error).message);
    }
  }
  async function create() {
    if (!template || busy) return;
    setBusy(true); setError("");
    try {
      const campaign = await request("campaigns/import-template/", "POST", {id:receipt.current, document:template});
      location.assign(`/campaigns/?lang=${pl ? "pl" : "en"}&campaign=${encodeURIComponent(campaign.id)}`);
    } catch (e) {
      setError(t("Nie utworzono kampanii: ", "Campaign not created: ") + (e as Error).message);
      setBusy(false);
    }
  }
  return <section className="wh-campaign-import" aria-labelledby="campaign-import-title">
    <h2 id="campaign-import-title">{t("Masz przykładową kampanię?", "Have a sample campaign?")}</h2>
    <p>{t("Wczytaj szablon JSON z postaciami, statystykami, miejscami i wydarzeniami. Powstanie nowa, prywatna kampania na Twoim koncie.", "Load a JSON template with characters, stats, places and events. It creates a new private campaign in your account.")}</p>
    <label>{t("Plik kampanii (.json)", "Campaign file (.json)")}<input type="file" accept=".json,application/json" disabled={busy} onChange={e=>void read(e.target.files?.[0])}/></label>
    {error && <p role="alert">{error}</p>}
    {template && <div className="wh-import-preview">
      <h3>{template.title}</h3><p>{template.summary}</p>
      <p>{template.pages.filter(p=>p.kind === "pc").length} {t("postaci graczy", "player characters")} · {template.pages.filter(p=>p.kind === "npc").length} NPC · {template.pages.filter(p=>p.kind === "location").length} {t("miejsc", "places")} · {template.events.length} {t("wydarzeń", "events")}</p>
      <p>{t("Materiały i statystyki będą widoczne tylko dla Ciebie i zaproszonych MG. Opisy możesz później opublikować graczom.", "Material and stats will be visible only to you and invited GMs. You can publish descriptions to players later.")}</p>
      <button className="primary" disabled={busy} onClick={()=>void create()}>{busy?t("Tworzenie kampanii…", "Creating campaign…"):t("Utwórz kampanię z pliku", "Create campaign from file")}</button>
    </div>}
  </section>;
}
