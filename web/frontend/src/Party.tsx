import { useEffect, useState } from "react";
import "./party.css";

type Skill = { label: string; value: number; tier: string | null };
type Card = { id: string; name: string | null; status: string; axes: string; skills: Skill[]; focus: string };
type Tile = { member: string; display: string; account?: string; mine: boolean; character: Card | null; drafts: Card[] };
type Request = (path: string, method?: string, body?: unknown) => Promise<any>;

const STATUS: Record<string, [string, string]> = {
  draft: ["w przygotowaniu", "in progress"], submitted: ["czeka na prowadzącego", "waiting for the GM"],
  returned: ["do poprawy", "returned"], approved: ["zatwierdzona", "approved"],
};

export const partyLabel = (cyber: boolean, pl: boolean) => (cyber ? (pl ? "Ekipa" : "Crew") : pl ? "Drużyna" : "Party");

// Who plays whom and what they are good at; the player's own tile edits the one-sentence job in the party.
export default function Party({ campaignId, pl, cyber, request, onCreate }: {
  campaignId: string; pl: boolean; cyber: boolean; request: Request; onCreate: () => void;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [tiles, setTiles] = useState<Tile[] | null>(null);
  const [error, setError] = useState("");
  const load = () => request(`campaigns/${campaignId}/party/`).then((d) => setTiles(d.players)).catch((e) => setError(String(e.message || e)));
  useEffect(() => { load(); }, [campaignId]);
  if (error) return <p className="builder-warning">{error}</p>;
  if (!tiles) return <p>{t("Wczytuję…", "Loading…")}</p>;
  return (
    <section className="party" aria-label={partyLabel(cyber, pl)}>
      <h2>{partyLabel(cyber, pl)}</h2>
      {tiles.length === 0 && <p>{t("W kampanii nie ma jeszcze graczy. Zaproś ich w ustawieniach udostępniania.", "No players yet. Invite them from sharing.")}</p>}
      <div className="party-tiles">
        {tiles.map((tile) => (
          <article key={tile.member} className="party-tile">
            <p className="party-account">{tile.display}{tile.account && <small> · {t("login", "login")}: {tile.account}</small>}</p>
            {tile.mine && <DisplayName key={tile.display} tile={tile} pl={pl}
              save={(name) => request(`campaigns/${campaignId}/party/`, "POST", { display_name: name }).then((d) => setTiles(d.players))} />}
            {tile.character ? <CharacterCard card={tile.character} pl={pl} />
              : <p>{t("Jeszcze bez postaci.", "No character yet.")}</p>}
            {tile.mine && (tile.character
              ? <Focus key={tile.character.id + tile.character.focus} card={tile.character} pl={pl}
                  save={(text) => request(`campaigns/${campaignId}/characters/${tile.character!.id}/`).then((d) =>
                    request(`campaigns/${campaignId}/characters/${tile.character!.id}/`, "POST", { version: d.version, action: "set_focus", text })).then(load)} />
              : <button type="button" className="primary" onClick={onCreate}>{t("Stwórz postać", "Create a character")}</button>)}
            {tile.drafts.length > 0 && (
              <details className="party-drafts">
                <summary>{t(`Szkice (${tile.drafts.length})`, `Drafts (${tile.drafts.length})`)}</summary>
                {tile.drafts.map((d) => <CharacterCard key={d.id} card={d} pl={pl} />)}
              </details>
            )}
          </article>
        ))}
      </div>
    </section>
  );
}

function CharacterCard({ card, pl }: { card: Card; pl: boolean }) {
  const t = (a: string, b: string) => (pl ? a : b);
  return (
    <div className="party-card">
      <h3>{card.name || t("Bez imienia", "Unnamed")}</h3>
      <p className="party-meta">{[card.axes, STATUS[card.status]?.[pl ? 0 : 1] || card.status].filter(Boolean).join(" · ")}</p>
      {card.skills.length > 0 && (
        <ul className="party-skills">
          {card.skills.map((s) => <li key={s.label}>{s.label} <strong>{s.value}</strong>{s.tier && ` · ${s.tier}`}</li>)}
        </ul>
      )}
      {card.focus && <p className="party-focus">„{card.focus}”</p>}
    </div>
  );
}

function DisplayName({ tile, pl, save }: { tile: Tile; pl: boolean; save: (name: string) => Promise<unknown> }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [name, setName] = useState(/^Gracz \d+$/.test(tile.display) ? "" : tile.display);
  const [state, setState] = useState("");
  return (
    <form className="party-focus-form" onSubmit={(e) => {
      e.preventDefault();
      setState("…");
      save(name).then(() => setState(t("Zapisano.", "Saved."))).catch((err) => setState(String(err.message || err)));
    }}>
      <label>{t("Jak widzą cię inni w drużynie", "How the party sees you")}
        <input value={name} maxLength={40} placeholder={tile.display} onChange={(e) => setName(e.target.value)} />
      </label>
      <button type="submit">{t("Zapisz nazwę", "Save name")}</button>
      {state && <small role="status">{state}</small>}
    </form>
  );
}

function Focus({ card, pl, save }: { card: Card; pl: boolean; save: (text: string) => Promise<unknown> }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [text, setText] = useState(card.focus);
  const [state, setState] = useState("");
  return (
    <form className="party-focus-form" onSubmit={(e) => {
      e.preventDefault();
      setState("…");
      save(text).then(() => setState(t("Zapisano.", "Saved."))).catch((err) => setState(String(err.message || err)));
    }}>
      <label>{t("W drużynie odpowiadam za…", "In the party I handle…")}
        <input value={text} maxLength={200} onChange={(e) => setText(e.target.value)} />
      </label>
      <button type="submit" disabled={text === card.focus}>{t("Zapisz", "Save")}</button>
      {state && <small role="status">{state}</small>}
    </form>
  );
}
