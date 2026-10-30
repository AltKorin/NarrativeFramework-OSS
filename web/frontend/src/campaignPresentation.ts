export type Presentation = "cyberpunk" | "fantasy";

function legacyTemplate(kind: string, pl: boolean): string {
  if (kind === "timeline")
    return pl
      ? "## Oś czasu kampanii\n\nWpisuj wydarzenia w kolejności chronologicznej. Używaj dat świata gry, numerów sesji lub czasu względnego (np. dzień 3, świt).\n\n| Kiedy | Co się wydarza | Kto / gdzie | Status | Konsekwencje |\n| --- | --- | --- | --- | --- |\n| Sesja 1, dzień 1, świt | | | Planowane | |\n\n## Warunki i terminy\n\n- Jeśli gracze nie zareagują do…: …\n- Po wydarzeniu… następuje…\n"
      : "## Campaign timeline\n\nKeep events in chronological order. Use in-world dates, session numbers or relative time (e.g. day 3, dawn).\n\n| When | What happens | Who / where | Status | Consequences |\n| --- | --- | --- | --- | --- |\n| Session 1, day 1, dawn | | | Planned | |\n\n## Triggers and deadlines\n\n- Unless the players act by…: …\n- After event… comes…\n";
  if (kind !== "npc" && kind !== "pc") return "";
  const player = kind === "pc";
  return pl
    ? `## ${player ? "Karta postaci gracza" : "Karta NPC"}\n\n- ${player ? "Gracz" : "Rola w kampanii"}: \n- System RPG: \n- Pochodzenie / profesja: \n- Opis i wygląd: \n- Cel i motywacja: \n\n## Statystyki\n\nAktualne wartości prowadź w karcie w widoku „Postacie”. Tutaj opisz zasady i dodatkowe uwagi do systemu RPG.\n\n## Umiejętności i zdolności\n\n- \n\n## Ekwipunek\n\n- \n\n## Relacje i historia\n\n- \n`
    : `## ${player ? "Player character sheet" : "NPC sheet"}\n\n- ${player ? "Player" : "Campaign role"}: \n- RPG system: \n- Background / profession: \n- Description and appearance: \n- Goal and motivation: \n\n## Statistics\n\nTrack current values in the sheet in the “Characters” view. Use this section for RPG system rules and additional notes.\n\n## Skills and abilities\n\n- \n\n## Equipment\n\n- \n\n## Relationships and history\n\n- \n`;
}

// Templates describe content only: live statistics stay in the campaign ruleset.
export function entryTemplate(
  kind: string,
  pl: boolean,
  profile: Presentation = "fantasy",
): string {
  const t = (polish: string, english: string) => (pl ? polish : english);
  if (kind === "timeline") return legacyTemplate(kind, pl);
  if (profile === "fantasy" && (kind === "pc" || kind === "npc"))
    return legacyTemplate(kind, pl);
  const cyber = profile === "cyberpunk";
  const character = kind === "pc" || kind === "npc";
  const sections: Record<string, string[]> = {
    pc: [
      t("Tożsamość i wygląd", "Identity and appearance"),
      t("Umiejętności i specjalizacja", "Skills and specialty"),
      t("Sprzęt i cybernetyka", "Equipment and cyberware"),
      t("Kontakty i zobowiązania", "Contacts and obligations"),
      t("Historia i motywacja", "History and motivation"),
    ],
    npc: [
      t("Wygląd i sposób mówienia", "Appearance and voice"),
      t("Cel i metody", "Goal and methods"),
      t("Zasoby i cybernetyka", "Resources and cyberware"),
      t("Kontakty i wpływy", "Contacts and influence"),
    ],
    location: cyber
      ? [
          t("Dostęp i ochrona", "Access and security"),
          t("Ludzie i frakcje", "People and factions"),
          t("Usługi i infrastruktura", "Services and infrastructure"),
          t("Zagrożenia i tropy", "Threats and leads"),
        ]
      : [
          t("Opis i atmosfera", "Description and atmosphere"),
          t("Mieszkańcy", "Inhabitants"),
          t("Ważne miejsca", "Landmarks"),
          t("Pogłoski i zagrożenia", "Rumors and threats"),
        ],
    organization: cyber
      ? [
          t("Operacje i zasoby", "Operations and resources"),
          t("Hierarchia i kontakty", "Hierarchy and contacts"),
          t("Terytorium i wpływy", "Territory and influence"),
          t("Konflikty i kontrakty", "Conflicts and contracts"),
        ]
      : [
          t("Cel i tradycje", "Purpose and traditions"),
          t("Przywódcy i członkowie", "Leaders and members"),
          t("Siedziba i zasoby", "Seat and resources"),
          t("Sojusze i konflikty", "Alliances and conflicts"),
        ],
    item: cyber
      ? [
          t("Specyfikacja", "Specifications"),
          t("Modyfikacje", "Modifications"),
          t("Pochodzenie i właściciel", "Origin and owner"),
        ]
      : [
          t("Wygląd i właściwości", "Appearance and properties"),
          t("Pochodzenie", "Origin"),
          t("Właściciel i znaczenie", "Owner and significance"),
        ],
    lore: [
      t("Najważniejsze fakty", "Key facts"),
      t("Kontekst", "Context"),
      t("Powiązania", "Connections"),
    ],
    journal: [
      t("Przebieg sesji", "Session recap"),
      t("Decyzje i konsekwencje", "Decisions and consequences"),
      t("Otwarte wątki", "Open threads"),
    ],
    scene: [
      t("Sytuacja początkowa", "Opening situation"),
      t("Uczestnicy i cele", "Participants and goals"),
      t("Przeszkody", "Obstacles"),
      t("Możliwe następstwa", "Possible outcomes"),
    ],
    clue: [
      t("Co można odkryć", "What can be discovered"),
      t("Gdzie i w jaki sposób", "Where and how"),
      t("Dokąd prowadzi trop", "Where the lead points"),
    ],
  };
  if (!sections[kind]) return "";
  const fields = character
    ? [
        t("Pseudonim", "Handle"),
        t("Rola", "Role"),
        t("Afiliacja", "Affiliation"),
        t("Status", "Status"),
      ]
    : [
        t("Typ", "Type"),
        t("Miejsce / obszar", "Location / area"),
        t("Status", "Status"),
      ];
  const heading = character
    ? t("Dossier postaci", "Character dossier")
    : t("Informacje podstawowe", "At a glance");
  const intro = cyber
    ? `## ${heading}\n\n| ${t("Pole", "Field")} | ${t("Wartość", "Value")} |\n| --- | --- |\n${fields.map((field) => `| ${field} | |`).join("\n")}\n`
    : `## ${heading}\n\n${fields.map((field) => `- ${field}: `).join("\n")}\n`;
  const stats = character
    ? `\n## ${t("Statystyki", "Statistics")}\n\n${t("Aktualne wartości prowadź w widoku „Postacie”, zgodnie z pakietem zasad kampanii.", "Track current values in the Characters view using the campaign rules pack.")}\n`
    : "";
  return (
    intro +
    stats +
    sections[kind].map((heading) => `\n## ${heading}\n\n- \n`).join("")
  );
}
