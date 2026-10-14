import { useEffect, useState } from "react";
export type WikiAPI = (
  path: string,
  method?: string,
  body?: unknown,
) => Promise<any>;
type T = (pl: string, en: string) => string;
const actionLabels: Record<string, [string, string]> = {
  "wiki.changed": ["Zmieniono materiały", "Material changed"],
  "page.created": ["Utworzono artykuł", "Article created"],
  "page.saved": ["Zapisano artykuł", "Article saved"],
  "page.deleted": ["Usunięto artykuł", "Article deleted"],
  "relation.created": ["Dodano relację", "Relationship created"],
  "relation.deleted": ["Usunięto relację", "Relationship deleted"],
  "campaign.updated": ["Zmieniono kampanię", "Campaign updated"],
  "page.publication": [
    "Zmieniono publikację artykułu",
    "Article publication changed",
  ],
  "relation.publication": [
    "Zmieniono publikację relacji",
    "Relationship publication changed",
  ],
  "invitation.created": ["Utworzono zaproszenie", "Invitation created"],
  "invitation.revoked": ["Unieważniono zaproszenie", "Invitation revoked"],
  "invitation.accepted": ["Przyjęto zaproszenie", "Invitation accepted"],
  "member.removed": ["Usunięto członka", "Member removed"],
  "member.role_changed": ["Zmieniono rolę", "Role changed"],
};
type Member = { id: string; username: string; role: string };
export type Publication = {
  version: number;
  audience?: string;
  recipients?: string[];
  published?: { version: number } | null;
};

type SharedProps = {
  campaignId: string;
  api: WikiAPI;
  t: T;
  run: (fn: () => Promise<void>) => Promise<void>;
  changed: () => Promise<void>;
};

export function PublicationControls({
  campaignId,
  api,
  t,
  run,
  changed,
  resource,
  relation = false,
  dirty = false,
}: SharedProps & {
  resource: Publication & { id: string };
  relation?: boolean;
  dirty?: boolean;
}) {
  const [audience, setAudience] = useState(resource.audience || "gm");
  const [recipients, setRecipients] = useState<string[]>(
    resource.recipients || [],
  );
  const [members, setMembers] = useState<Member[]>([]);
  const [error, setError] = useState("");
  useEffect(() => {
    setAudience(resource.audience || "gm");
    setRecipients(resource.recipients || []);
  }, [resource.version]);
  function loadPlayers() {
    api(`campaigns/${campaignId}/members/`)
      .then((x) =>
        setMembers(x.members.filter((m: Member) => m.role === "player")),
      )
      .catch((e) => setError(e.message));
  }
  const publish = (withdraw: boolean) =>
    run(async () => {
      await api(
        `campaigns/${campaignId}/${relation ? "relations" : "pages"}/${resource.id}/publication/`,
        "POST",
        {
          version: resource.version,
          audience: withdraw ? "gm" : audience,
          recipients: !withdraw && audience === "selected" ? recipients : [],
        },
      );
      await changed();
    });
  return (
    <details
      className="wiki-publication"
      onToggle={(e) => {
        if (e.currentTarget.open) loadPlayers();
      }}
    >
      <summary>
        {relation
          ? t("Udostępnianie relacji", "Relationship sharing")
          : t("Publikacja dla graczy", "Player publication")}
      </summary>
      <p>
        {resource.published
          ? t(
              `Opublikowana wersja: ${resource.published.version}; robocza: ${resource.version}.`,
              `Published version: ${resource.published.version}; draft: ${resource.version}.`,
            )
          : t("Nieopublikowane — tylko dla MG.", "Unpublished — GMs only.")}
      </p>
      <p>
        {t(
          "Zapis roboczy nie zmienia publikacji. Notatki MG nigdy nie są udostępniane. Sprawdź treść i odnośniki przed publikacją.",
          "Saving a draft does not update its publication. GM notes are never shared. Review the text and links before publishing.",
        )}
      </p>
      {error && <p role="alert">{error}</p>}
      <label>
        {t("Odbiorcy", "Audience")}
        <select
          aria-label={t("Odbiorcy", "Audience")}
          value={audience}
          onChange={(e) => setAudience(e.target.value)}
        >
          <option value="gm">{t("Tylko MG", "GMs only")}</option>
          <option value="all">
            {t("Wszyscy gracze kampanii", "All campaign players")}
          </option>
          <option value="selected">
            {t("Wybrani gracze", "Selected players")}
          </option>
        </select>
      </label>
      {audience === "selected" && (
        <fieldset>
          <legend>{t("Wybierz graczy", "Choose players")}</legend>
          {members.map((m) => (
            <label className="wiki-check" key={m.id}>
              <input
                type="checkbox"
                checked={recipients.includes(m.id)}
                onChange={(e) =>
                  setRecipients(
                    e.target.checked
                      ? [...recipients, m.id]
                      : recipients.filter((x) => x !== m.id),
                  )
                }
              />
              {m.username}
            </label>
          ))}
          {!members.length && (
            <p>{t("Najpierw zaproś graczy.", "Invite players first.")}</p>
          )}
        </fieldset>
      )}
      {dirty && (
        <p>{t("Najpierw zapisz artykuł.", "Save the article first.")}</p>
      )}
      <div className="wiki-sharing-actions">
        <button
          disabled={
            dirty ||
            audience === "gm" ||
            (audience === "selected" && !recipients.length)
          }
          onClick={() => publish(false)}
        >
          {relation
            ? t("Opublikuj relację", "Publish relationship")
            : t("Opublikuj dla graczy", "Publish for players")}
        </button>
        <button disabled={!resource.published} onClick={() => publish(true)}>
          {t("Wycofaj publikację", "Withdraw publication")}
        </button>
      </div>
    </details>
  );
}

export function CampaignSharing({
  campaignId,
  version,
  owner,
  readonly,
  preview,
  onPreview,
  api,
  t,
  run,
  changed,
}: SharedProps & {
  version: number;
  owner: boolean;
  readonly: boolean;
  preview: string;
  onPreview: (id: string) => void;
}) {
  const [members, setMembers] = useState<Member[]>([]);
  const [invites, setInvites] = useState<any[]>([]);
  const [role, setRole] = useState("player");
  const [link, setLink] = useState("");
  const [error, setError] = useState("");
  const [audit, setAudit] = useState<any[] | null>(null);
  async function reload() {
    try {
      setMembers((await api(`campaigns/${campaignId}/members/`)).members);
      setInvites(
        owner && !readonly
          ? await api(`campaigns/${campaignId}/invitations/`)
          : [],
      );
      setError("");
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    }
  }
  useEffect(() => {
    setLink("");
  }, [campaignId]);
  useEffect(() => {
    setAudit(null);
    void reload();
  }, [campaignId, version, owner, readonly]);
  return (
    <section className="wiki-sharing">
      <label>
        {t("Podgląd jako gracz", "Preview as player")}
        <select
          aria-label={t("Podgląd jako gracz", "Preview as player")}
          value={preview}
          onChange={(e) => onPreview(e.target.value)}
        >
          <option value="">{t("Widok MG", "GM view")}</option>
          {members
            .filter((m) => m.role === "player")
            .map((m) => (
              <option value={m.id} key={m.id}>
                {m.username}
              </option>
            ))}
        </select>
      </label>
      {preview && (
        <p>
          {t(
            "Podgląd pokazuje tylko opublikowane materiały wybranego gracza.",
            "Preview shows only the selected player's published material.",
          )}
        </p>
      )}
      {!readonly && (
        <details>
          <summary>
            {t("Członkowie i zaproszenia", "Members and invitations")}
          </summary>
          {error && <p role="alert">{error}</p>}
          <p>
            {t(
              "Współprowadzący widzą wszystkie szkice i sekrety MG. Gracze tylko publikacje dla nich.",
              "Co-GMs see all drafts and GM secrets. Players see only their publications.",
            )}
          </p>
          {members.map((m) => (
            <div className="wiki-member" key={m.id}>
              <strong>{m.username}</strong>
              <span>
                {m.role === "gm"
                  ? t("Współprowadzący", "Co-GM")
                  : t("Gracz", "Player")}
              </span>
              {owner && (
                <>
                  <select
                    aria-label={t(`Rola: ${m.username}`, `Role: ${m.username}`)}
                    value={m.role}
                    onChange={(e) => {
                      const value = e.target.value;
                      if (
                        confirm(
                          t(
                            "Zmienić rolę? Współprowadzący mają dostęp do wszystkich sekretów. Zmiana usuwa indywidualne przydziały artykułów.",
                            "Change role? Co-GMs have access to all secrets. Changing role clears individual article grants.",
                          ),
                        )
                      )
                        run(async () => {
                          await api(
                            `campaigns/${campaignId}/members/${m.id}/`,
                            "PATCH",
                            { role: value, version },
                          );
                          await changed();
                          await reload();
                        });
                    }}
                  >
                    <option value="player">{t("Gracz", "Player")}</option>
                    <option value="gm">{t("Współprowadzący", "Co-GM")}</option>
                  </select>
                  <button
                    onClick={() => {
                      if (
                        confirm(
                          t(
                            "Odebrać dostęp do kampanii? Pobranych wcześniej materiałów nie można cofnąć.",
                            "Remove campaign access? Previously downloaded material cannot be retracted.",
                          ),
                        )
                      )
                        run(async () => {
                          await api(
                            `campaigns/${campaignId}/members/${m.id}/`,
                            "DELETE",
                          );
                          await changed();
                          await reload();
                        });
                    }}
                  >
                    {t("Usuń członka", "Remove member")}
                  </button>
                </>
              )}
            </div>
          ))}
          {owner && (
            <>
              <label>
                {t("Rola w zaproszeniu", "Invitation role")}
                <select
                  aria-label={t("Rola w zaproszeniu", "Invitation role")}
                  value={role}
                  onChange={(e) => setRole(e.target.value)}
                >
                  <option value="player">{t("Gracz", "Player")}</option>
                  <option value="gm">
                    {t(
                      "Współprowadzący — wszystkie sekrety",
                      "Co-GM — all secrets",
                    )}
                  </option>
                </select>
              </label>
              <p>
                {t(
                  "Link jest ważny 7 dni; użyje go pierwsze akceptujące konto. Przekaż go prywatnie właściwej osobie.",
                  "The link expires in 7 days; the first account accepting it joins. Send it privately to the intended person.",
                )}
              </p>
              <button
                onClick={() =>
                  run(async () => {
                    if (
                      role === "gm" &&
                      !confirm(
                        t(
                          "Zaproszony współprowadzący zobaczy wszystkie sekrety MG. Kontynuować?",
                          "The invited co-GM will see all GM secrets. Continue?",
                        ),
                      )
                    )
                      return;
                    const i = await api(
                      `campaigns/${campaignId}/invitations/`,
                      "POST",
                      { role },
                    );
                    setLink(`${location.origin}/campaigns/#invite=${i.token}`);
                    await changed();
                    await reload();
                  })
                }
              >
                {t("Utwórz zaproszenie", "Create invitation")}
              </button>
              {link && (
                <label>
                  {t("Skopiuj link zaproszenia", "Copy invitation link")}
                  <input
                    aria-label={t(
                      "Skopiuj link zaproszenia",
                      "Copy invitation link",
                    )}
                    readOnly
                    value={link}
                    onFocus={(e) => e.target.select()}
                  />
                </label>
              )}
              {invites.map((i) => (
                <div className="wiki-invitation" key={i.id}>
                  <span>
                    {i.role === "gm"
                      ? t("Współprowadzący", "Co-GM")
                      : t("Gracz", "Player")}{" "}
                    ·{" "}
                    {i.accepted
                      ? `${t("Przyjęte przez", "Accepted by")} ${i.accepted_by || "—"}`
                      : i.revoked
                        ? t("Unieważnione", "Revoked")
                        : new Date(i.expires_at).toLocaleString()}
                  </span>
                  {!i.accepted && !i.revoked && (
                    <button
                      onClick={() =>
                        run(async () => {
                          await api(
                            `campaigns/${campaignId}/invitations/${i.id}/`,
                            "DELETE",
                          );
                          await changed();
                          await reload();
                        })
                      }
                    >
                      {t("Unieważnij zaproszenie", "Revoke invitation")}
                    </button>
                  )}
                </div>
              ))}
            </>
          )}
          <button
            onClick={() =>
              run(async () =>
                setAudit(await api(`campaigns/${campaignId}/audit/`)),
              )
            }
          >
            {t("Pokaż dziennik dostępu", "Show access log")}
          </button>
          {audit && (
            <ul className="wiki-audit">
              {audit.map((e, i) => (
                <li key={i}>
                  {e.actor || "—"} ·{" "}
                  {actionLabels[e.action]
                    ? t(...actionLabels[e.action])
                    : t("Zmiana kampanii", "Campaign changed")}{" "}
                  · v{e.version}
                </li>
              ))}
            </ul>
          )}
        </details>
      )}
    </section>
  );
}
