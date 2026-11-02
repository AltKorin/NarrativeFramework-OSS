import { useEffect, useRef, useState } from "react";
import { newUUID } from "./uuid";

type Request = (path: string, method?: string, body?: unknown) => Promise<any>;
type Usage = { cover: boolean; pages: string[]; ruleset: boolean };
type Img = { id: string; caption: string; gallery: "gm" | "players"; width: number; height: number; size: number; usage?: Usage };
type PageMeta = { id: string; title: string; version: number; image?: string | null };

export const imageUrl = (campaignId: string, id: string) => `/api/campaigns/${campaignId}/images/${id}/file/`;

// Native <dialog>: Escape, focus trap and backdrop come from the browser; no lightbox library.
export function ZoomImage({ src, caption, className, pl }: { src: string; caption?: string; className?: string; pl: boolean }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const label = caption || (pl ? "Obraz" : "Image");
  return (
    <>
      <button type="button" className={`zoom-trigger ${className || ""}`} aria-label={`${pl ? "Powiększ" : "Enlarge"}: ${label}`}
        onClick={() => dialog.current?.showModal()}>
        <img src={src} alt={caption || ""} loading="lazy" />
      </button>
      <dialog ref={dialog} className="zoom-dialog" aria-label={label} onClick={() => dialog.current?.close()}>
        <img src={src} alt={caption || ""} />
        {caption && <p>{caption}</p>}
        <button type="button" onClick={() => dialog.current?.close()}>{pl ? "Zamknij" : "Close"}</button>
      </dialog>
    </>
  );
}

export default function Gallery({ campaignId, gm, pl, request, csrf, pages, cover, onChanged }: {
  campaignId: string;
  gm: boolean;
  pl: boolean;
  request: Request;
  csrf: () => string;
  pages: PageMeta[];
  cover: string | null | undefined;
  onChanged: () => Promise<void>;
}) {
  const t = (a: string, b: string) => (pl ? a : b);
  const base = `campaigns/${campaignId}/`;
  const [items, setItems] = useState<Img[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [caption, setCaption] = useState("");
  const [shared, setShared] = useState(false);

  async function run(fn: () => Promise<void>, done = "", changed = true) {
    if (busy) return;
    setBusy(true);
    setNotice("");
    try {
      await fn();
      if (changed) await onChanged();
      setItems(await request(base + "images/"));
      setNotice(done);
    } catch (e) {
      setNotice((e as Error).message);
    } finally {
      setBusy(false);
    }
  }
  useEffect(() => {
    void run(async () => undefined, "", false);
  }, [campaignId]);

  async function upload() {
    if (!file) return;
    const form = new FormData();
    form.append("id", newUUID());
    form.append("file", file);
    form.append("caption", caption);
    form.append("gallery", shared ? "players" : "gm");
    const r = await fetch(`/api/${base}images/`, { method: "POST", body: form, headers: { "X-CSRFToken": csrf() } });
    const result = await r.json().catch(() => ({}));
    if (!r.ok) throw Error(result.error || (r.status === 413 ? t("Plik jest za duży (limit 8 MB).", "File too large (8 MB limit).") : "Upload failed"));
    setFile(null);
    setCaption("");
  }

  const pageTitle = (id: string) => pages.find((p) => p.id === id)?.title || "?";
  return (
    <section className="campaign-gallery" aria-labelledby="gallery-title">
      <h2 id="gallery-title">{t("Galeria", "Gallery")}</h2>
      <p className="gallery-privacy">
        {t("Obrazy są prywatne dla tej kampanii: nie mają publicznych adresów, a gracze widzą tylko to, co im udostępnisz.",
          "Images are private to this campaign: no public links, and players only see what you share.")}
      </p>
      {notice && <p role="status">{notice}</p>}
      {gm && (
        <form className="gallery-upload" onSubmit={(e) => { e.preventDefault(); void run(upload, t("Obraz dodany.", "Image added.")); }}>
          <label>{t("Obraz (PNG, JPEG, WebP, GIF, do 8 MB)", "Image (PNG, JPEG, WebP, GIF, up to 8 MB)")}
            <input type="file" accept="image/png,image/jpeg,image/webp,image/gif" disabled={busy}
              onChange={(e) => setFile(e.target.files?.[0] || null)} />
          </label>
          <label>{t("Podpis", "Caption")}
            <input value={caption} maxLength={200} onChange={(e) => setCaption(e.target.value)} />
          </label>
          <label className="gallery-check">
            <input type="checkbox" checked={shared} onChange={(e) => setShared(e.target.checked)} />
            {t("Pokaż graczom w galerii", "Show to players in the gallery")}
          </label>
          <p className="gallery-hint">{t("Wgrywaj tylko obrazy, do których masz prawo. Serwer zmniejsza obraz i usuwa metadane (np. lokalizację).",
            "Only upload images you have the right to use. The server downsizes images and strips metadata (e.g. location).")}</p>
          <button className="primary" disabled={busy || !file}>{t("Dodaj obraz", "Add image")}</button>
        </form>
      )}
      {items === null ? <p>{t("Wczytywanie…", "Loading…")}</p> : items.length === 0 ? (
        <p>{gm ? t("Galeria jest pusta.", "The gallery is empty.") : t("Prowadzący nie udostępnił jeszcze obrazów.", "The GM has not shared any images yet.")}</p>
      ) : (
        <ul className="gallery-grid">
          {items.map((img) => (
            <li key={img.id}>
              <figure>
                <ZoomImage src={imageUrl(campaignId, img.id)} caption={img.caption} pl={pl} />
                {img.caption && <figcaption>{img.caption}</figcaption>}
              </figure>
              {gm && img.usage && (
                <div className="gallery-tools">
                  <p className="gallery-usage">
                    {img.gallery === "players" ? t("Widoczny w galerii graczy", "In the players' gallery") : t("Tylko MG", "GM only")}
                    {img.usage.cover && ` · ${t("okładka", "cover")}`}
                    {img.usage.ruleset && ` · ${t("zasady", "rules")}`}
                    {img.usage.pages.length > 0 && ` · ${img.usage.pages.map(pageTitle).join(", ")}`}
                  </p>
                  <label>{t("Podpis", "Caption")}
                    <input defaultValue={img.caption} maxLength={200} disabled={busy}
                      onBlur={(e) => e.target.value !== img.caption && run(() => request(`${base}images/${img.id}/`, "PATCH", { caption: e.target.value }), t("Podpis zapisany.", "Caption saved."))} />
                  </label>
                  <button disabled={busy} onClick={() => run(() => request(`${base}images/${img.id}/`, "PATCH", { gallery: img.gallery === "players" ? "gm" : "players" }))}>
                    {img.gallery === "players" ? t("Ukryj przed graczami", "Hide from players") : t("Pokaż graczom", "Show to players")}
                  </button>
                  <button disabled={busy} onClick={() => run(async () => { await request(`${base}cover/`, "PUT", { image: cover === img.id ? null : img.id }); })}>
                    {cover === img.id ? t("Usuń z okładki", "Remove as cover") : t("Ustaw jako okładkę", "Set as cover")}
                  </button>
                  <label>{t("Ilustracja strony", "Page illustration")}
                    <select value="" disabled={busy} onChange={(e) => {
                      const page = pages.find((p) => p.id === e.target.value);
                      if (page) void run(async () => { await request(`${base}pages/${page.id}/image/`, "PUT", { version: page.version, image: img.id }); },
                        t(`Dołączono do „${page.title}”. Gracze zobaczą ilustrację po ponownej publikacji strony.`, `Attached to “${page.title}”. Players see it after the page is published again.`));
                    }}>
                      <option value="">{t("Dołącz do strony…", "Attach to page…")}</option>
                      {pages.map((p) => <option key={p.id} value={p.id}>{p.title}{p.image === img.id ? " ✓" : ""}</option>)}
                    </select>
                  </label>
                  <button disabled={busy || img.usage.ruleset} title={img.usage.ruleset ? t("Używany w zasadach kampanii", "Used by the campaign rules") : undefined}
                    onClick={() => {
                      const effects = [img.usage!.cover && t("okładkę", "the cover"), img.usage!.pages.length && t(`${img.usage!.pages.length} ilustracji stron`, `${img.usage!.pages.length} page illustrations`)].filter(Boolean).join(t(" i ", " and "));
                      if (confirm(t(`Usunąć obraz?${effects ? ` Odepnie też ${effects}.` : ""}`, `Delete the image?${effects ? ` It also removes ${effects}.` : ""}`)))
                        void run(async () => { await request(`${base}images/${img.id}/`, "DELETE"); }, t("Obraz usunięty.", "Image deleted."));
                    }}>
                    {t("Usuń", "Delete")}
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
