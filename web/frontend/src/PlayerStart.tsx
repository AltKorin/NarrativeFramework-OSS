import { useEffect, useState } from "react";
import type { Request } from "./campaignRuntimeTypes";
import "./player-start.css";

export type PlayerDraft = { id: string; status: string; name: string | null; page?: string | null };

/** The saved character state determines the next action, including after a new login. */
export function PlayerNextStep({ drafts, hasRules, pl, busy = false, onCreate, onDraft, onPage, onRefresh }: {
  drafts: PlayerDraft[]; hasRules: boolean; pl: boolean; busy?: boolean;
  onCreate: () => void; onDraft: (id: string) => void; onPage: (id: string) => void; onRefresh: () => void;
}) {
  const t = (a: string, b: string) => pl ? a : b;
  const approved = drafts.find(d => d.status === "approved" && d.page);
  const returned = drafts.find(d => d.status === "returned");
  const submitted = drafts.find(d => d.status === "submitted");
  const started = drafts.find(d => d.status === "draft");
  const next = approved || returned || submitted || started;
  const title = approved ? t("Twoja postać jest gotowa", "Your character is ready")
    : returned ? t("Postać wymaga poprawy", "Your character needs changes")
    : submitted ? t("Czekamy na prowadzącego", "Waiting for the GM")
    : started ? t("Dokończ swoją postać", "Continue your character")
    : hasRules ? t("Zacznij od swojej postaci", "Start with your character")
    : t("Czekamy na zasady kampanii", "Waiting for campaign rules");
  const text = approved ? t("Otwórz kartę i przygotuj się do sesji.", "Open your sheet and get ready to play.")
    : returned ? t("Prowadzący zostawił uwagę. Otwórz postać, popraw ją i wyślij ponownie.", "The GM left a note. Open your character, make changes and send it again.")
    : submitted ? t("Postać została wysłana. Tutaj sprawdzisz, czy prowadzący ją zatwierdził lub poprosił o zmianę.", "Your character was sent. Check here for approval or a request for changes.")
    : started ? t("Twoje zapisane wybory czekają. Możesz do nich wrócić i kontynuować.", "Your saved choices are here. Return to them and continue.")
    : hasRules ? t("Nie musisz znać zasad. Kreator przeprowadzi cię przez kolejne wybory.", "You do not need to know the rules. The builder will guide you through each choice.")
    : t("Prowadzący przygotowuje zasady tworzenia postaci. Na razie nie musisz niczego wybierać.", "The GM is preparing character creation rules. There is nothing you need to choose yet.");
  const label = approved ? t("Otwórz kartę", "Open sheet") : returned ? t("Popraw postać", "Fix character")
    : submitted ? t("Sprawdź decyzję", "Check for a decision") : started ? t("Kontynuuj", "Continue")
    : hasRules ? t("Stwórz postać", "Create character") : t("Sprawdź ponownie", "Check again");
  return <section className="player-next" aria-labelledby="player-next-title" data-tour="player-path">
    <h2 id="player-next-title">{title}</h2>
    {next?.name && <p className="player-next-name">{next.name}</p>}
    <p>{text}</p>
    <button className="primary" disabled={busy} onClick={() => {
      if (approved?.page) onPage(approved.page);
      else if (returned || (!submitted && started)) onDraft((returned || started)!.id);
      else if (submitted || !hasRules) onRefresh();
      else onCreate();
    }}>{label}</button>
  </section>;
}

export default function PlayerStart({ campaignId, summary, pl, request, onCreate, onDraft, onPage }: {
  campaignId: string; summary: string; pl: boolean; request: Request;
  onCreate: () => void; onDraft: (id: string) => void; onPage: (id: string) => void;
}) {
  const t = (a: string, b: string) => pl ? a : b;
  const [state, setState] = useState<{ drafts: PlayerDraft[]; hasRules: boolean } | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(true);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    let active = true;
    setBusy(true); setError("");
    const base = `campaigns/${campaignId}/`;
    Promise.all([
      request(base + "characters/"),
      request(base + "ruleset/").catch((e: Error) => {
        if (e.message === "Campaign has no ruleset") return null;
        throw e;
      }),
    ]).then(([drafts, rules]) => {
      if (active) setState({ drafts, hasRules: !!rules });
    }).catch(e => { if (active) setError(e.message); })
      .finally(() => { if (active) setBusy(false); });
    return () => { active = false; };
  }, [campaignId, revision]);
  return <section className="player-start" aria-label={t("Początek gry", "Getting started")}>
    {summary && (summary.length <= 280 ? <p className="player-intro">{summary}</p> :
      <details className="player-intro"><summary>{t("Wprowadzenie do kampanii", "Campaign introduction")}</summary><p>{summary}</p></details>)}
    {error ? <div role="alert"><p>{t("Nie udało się sprawdzić postępu. Spróbuj ponownie.", "Could not check your progress. Please try again.")}</p>
      <button disabled={busy} onClick={() => setRevision(r => r + 1)}>{t("Spróbuj ponownie", "Try again")}</button></div>
      : state ? <PlayerNextStep {...state} pl={pl} busy={busy} onCreate={onCreate} onDraft={onDraft} onPage={onPage} onRefresh={() => setRevision(r => r + 1)} />
      : <p role="status">{t("Sprawdzam, na czym skończyłeś…", "Checking where you left off…")}</p>}
  </section>;
}
