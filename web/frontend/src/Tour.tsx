import { useEffect, useLayoutEffect, useRef, useState } from "react";
import "./tour.css";

/** One highlighted element: `target` is a `data-tour` value; `before` may switch views first. */
export type TourStep = { target: string; title: string; body: string; why?: string; before?: () => void };

const seenKey = (id: string) => `narrative-framework-tour:${id}`;
export const tourSeen = (id: string) => {
  try {
    return localStorage.getItem(seenKey(id)) === "done";
  } catch {
    return false;
  }
};
const markSeen = (id: string) => {
  try {
    localStorage.setItem(seenKey(id), "done");
  } catch {
    /* private mode: the invitation simply shows again */
  }
};

function findTarget(target: string) {
  const el = document.querySelector<HTMLElement>(`[data-tour="${target}"]`);
  return el && el.getClientRects().length ? el : null;
}

/**
 * Spotlight tour: dims the page except the current element and explains what it is and why.
 * Not auto-modal — callers show an invitation first, so first visits are never blocked.
 */
export function Tour({ id, steps, pl, onClose }: { id: string; steps: TourStep[]; pl: boolean; onClose: () => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [index, setIndex] = useState(0);
  const [rect, setRect] = useState<DOMRect | null>(null);
  const card = useRef<HTMLDivElement>(null);
  const step = steps[index];

  const close = () => {
    markSeen(id);
    onClose();
  };

  // Switch views if needed, then wait briefly for the target to render; skip missing targets.
  useEffect(() => {
    let cancelled = false;
    step.before?.();
    let tries = 0;
    const locate = () => {
      if (cancelled) return;
      const el = findTarget(step.target);
      if (el) {
        el.scrollIntoView({ block: "center", behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" });
        setTimeout(() => !cancelled && setRect(el.getBoundingClientRect()), 250);
      } else if (tries++ < 20) {
        setTimeout(locate, 50);
      } else if (index < steps.length - 1) {
        setIndex(index + 1);
      } else {
        close();
      }
    };
    setRect(null);
    locate();
    return () => {
      cancelled = true;
    };
  }, [index]);

  // Follow the target every frame: banners closing, scrolling and view switches move it.
  useEffect(() => {
    let frame = 0;
    let last = "";
    const track = () => {
      const el = findTarget(step.target);
      if (el) {
        const r = el.getBoundingClientRect();
        const key = `${r.top}|${r.left}|${r.width}|${r.height}`;
        if (key !== last) {
          last = key;
          setRect(r);
        }
      }
      frame = requestAnimationFrame(track);
    };
    frame = requestAnimationFrame(track);
    return () => cancelAnimationFrame(frame);
  }, [index]);

  useEffect(() => {
    const keys = (e: KeyboardEvent) => {
      if (e.key === "Escape") close();
      if (e.key === "ArrowRight" && index < steps.length - 1) setIndex(index + 1);
      if (e.key === "ArrowLeft" && index > 0) setIndex(index - 1);
    };
    document.addEventListener("keydown", keys);
    return () => document.removeEventListener("keydown", keys);
  }, [index]);

  useLayoutEffect(() => {
    if (rect) card.current?.focus();
  }, [rect]);

  if (!rect) return <div className="tour-backdrop" aria-hidden="true" />;
  const pad = 6;
  const below = rect.bottom + 220 < innerHeight || rect.top < 240;
  const left = Math.max(12, Math.min(rect.left, innerWidth - 372));
  return (
    <>
      <div className="tour-spot" aria-hidden="true"
        style={{ top: rect.top - pad, left: rect.left - pad, width: rect.width + pad * 2, height: rect.height + pad * 2 }} />
      <div ref={card} className="tour-card" role="dialog" aria-modal="true" aria-labelledby="tour-title" tabIndex={-1}
        style={below ? { top: Math.min(rect.bottom + 14, innerHeight - 220), left } : { bottom: innerHeight - rect.top + 14, left }}>
        <p className="tour-progress">{t(`Krok ${index + 1} z ${steps.length}`, `Step ${index + 1} of ${steps.length}`)}</p>
        <h2 id="tour-title">{step.title}</h2>
        <p>{step.body}</p>
        {step.why && <p className="tour-why"><strong>{t("Dlaczego tak? ", "Why? ")}</strong>{step.why}</p>}
        <div className="tour-actions">
          <button type="button" onClick={close}>{t("Zakończ", "Finish")}</button>
          {index > 0 && <button type="button" onClick={() => setIndex(index - 1)}>{t("Wstecz", "Back")}</button>}
          {index < steps.length - 1
            ? <button type="button" className="primary" onClick={() => setIndex(index + 1)}>{t("Dalej", "Next")}</button>
            : <button type="button" className="primary" onClick={close}>{t("Gotowe", "Done")}</button>}
        </div>
      </div>
    </>
  );
}

/** Non-blocking first-visit invitation; the tour itself starts only on request. */
export function TourInvite({ id, pl, text, onStart }: { id: string; pl: boolean; text: string; onStart: () => void }) {
  const t = (a: string, b: string) => (pl ? a : b);
  const [hidden, setHidden] = useState(() => tourSeen(id));
  if (hidden) return null;
  return (
    <aside className="tour-invite" aria-label={t("Przewodnik", "Guide")}>
      <p>{text}</p>
      <button type="button" className="primary" onClick={() => { setHidden(true); onStart(); }}>{t("Pokaż, jak to działa", "Show me how it works")}</button>
      <button type="button" onClick={() => { markSeen(id); setHidden(true); }}>{t("Nie teraz", "Not now")}</button>
    </aside>
  );
}
