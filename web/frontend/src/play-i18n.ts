import type { Language } from "./i18n";
const en = {
  eyebrow: "BETWEEN SESSIONS",
  title: "A story of your own.",
  intro:
    "Prepare an individual adventure, let an agent run it, and bring its consequences back to the table.",
  login: "Log in to prepare or open an assigned adventure.",
  loginButton: "Log in",
  prepare: "Assign this adventure",
  saveFirst: "Save your adventure before assigning it.",
  empty: "No individual adventures yet.",
  refresh: "Refresh",
  mode: "Activity",
  downtime: "Downtime",
  absence: "Absent character",
  solo: "Solo",
  player: "Player username",
  character: "Character",
  language: "Adventure language",
  brief: "Opening situation",
  instructions: "Agent instructions",
  boundaries: "Limits of improvisation",
  returnCondition: "Return condition",
  gmNotes: "Private GM notes",
  sharedWarning:
    "The player can inspect everything published to their agent, including these instructions. Keep unrevealed information in Private GM notes or the original scenario secrets.",
  publish: "Materials for the agent",
  publishHelp:
    "Select public node descriptions to copy. The starting scene is required. Secret fields and unselected nodes are excluded.",
  autoApprove: "Pre-approved consequence categories",
  approvalHelp:
    "Leave unchecked to require a human GM decision. This records approval within the activity; it does not modify a campaign or verify narrative compliance.",
  resource: "Resources",
  relationship: "Relationships",
  discovery: "Discoveries",
  narrative: "Narrative developments",
  create: "Create assignment",
  cancel: "Cancel",
  choose: "Choose an activity",
  managed: "GM",
  assigned: "Player",
  active: "Active",
  completed: "Completed",
  version: "Version",
  sourceVersion: "Scenario version at assignment",
  accessTitle: "Connect your agent",
  accessHelp:
    "Use this session-scoped token at /play/mcp. It cannot call author tools. A new token revokes the previous one; tokens expire after 14 days.",
  generate: "Generate / rotate play token",
  revoke: "Revoke play token",
  token: "Private play token — copy now",
  expires: "Expires",
  endpoint: "Player MCP endpoint",
  prompt: "Starter message for your MCP client",
  starter:
    "Run this individual adventure with me. Call get_play_context first, follow its boundaries, and ask what my character does. Save resolved actions with commit_play_turn. When we finish, call finish_play_session. Do not treat pending consequences as approved.",
  memory: "Working memory",
  noMemory: "The agent has not saved a turn yet.",
  recap: "Closing recap",
  noRecap: "The activity has not been finished yet.",
  effects: "Consequences",
  noEffects: "No consequences recorded yet.",
  pending: "Needs GM decision",
  approved: "Approved",
  rejected: "Rejected",
  approve: "Approve",
  reject: "Reject",
  reviewNote: "Decision note (visible to the player)",
  report: "Download Markdown report",
  journal: "Play journal",
  previous: "Previous",
  next: "Next",
  noJournal: "No resolved turns yet.",
  action: "Player action",
  narration: "Narration",
  recordWarning:
    "Memory, narration and recap are play records. Only approved consequences are accepted within this assignment. Nothing is automatically merged into the main campaign.",
  created: "Assignment created.",
  tokenRevoked: "Play token revoked.",
  reviewed: "Decision saved.",
  loadError: "Could not load activities.",
  requestError: "The request failed. Refresh the activity before retrying.",
  sample: "Use example briefing",
  status: "Status",
  date: "Date",
  turn: "Turn",
  finish: "Finished",
  examples: "Example: absent from the party",
  sampleCharacter: "Arin — a scout returning to the party at dawn.",
  sampleBrief:
    "While your companions explored the ruins, you stayed in the harbor. Mira asks for help finding her father, the lighthouse keeper. What do you do?",
  sampleInstructions:
    "Run a short investigation with conversation and a meaningful choice. Follow the player’s ideas. Do not force the listed scene links. Describe consequences honestly and submit lasting changes for approval.",
  sampleBoundaries:
    "No permanent injury, character death, rare rewards or changes to the party’s main quest. Any lasting reward, new ally or important discovery needs GM approval.",
  sampleReturn:
    "By dawn the character can rejoin the party at the harbor. End with a recap separating played events from proposed campaign consequences.",
  sampleNotes:
    "Private reminder: reconcile the report with what the party actually did during the main session. Do not publish the scenario’s unrevealed secrets.",
  detail: "Activity details",
  nodes: "Published elements",
  gmNotesLabel: "Visible only to the human GM",
  instructionsLabel: "Visible to the player and their agent",
  retryCreate:
    "Creation may have succeeded. Refresh the activity list before changing and resubmitting this assignment.",
};
const pl: Record<keyof typeof en, string> = {
  eyebrow: "MIĘDZY SESJAMI",
  title: "Własny fragment historii.",
  intro:
    "Przygotuj indywidualną przygodę, oddaj jej prowadzenie agentowi i wróć z konsekwencjami do wspólnej gry.",
  login: "Zaloguj się, aby przygotować lub otworzyć przypisaną przygodę.",
  loginButton: "Zaloguj się",
  prepare: "Przypisz tę przygodę",
  saveFirst: "Zapisz przygodę przed jej przypisaniem.",
  empty: "Nie ma jeszcze indywidualnych przygód.",
  refresh: "Odśwież",
  mode: "Rodzaj aktywności",
  downtime: "Downtime",
  absence: "Nieobecna postać",
  solo: "Solo",
  player: "Login gracza",
  character: "Postać",
  language: "Język przygody",
  brief: "Sytuacja początkowa",
  instructions: "Instrukcje dla agenta",
  boundaries: "Granice improwizacji",
  returnCondition: "Warunek powrotu",
  gmNotes: "Prywatne notatki MG",
  sharedWarning:
    "Gracz może odczytać wszystko opublikowane dla jego agenta, także te instrukcje. Nieujawnione informacje umieść w prywatnych notatkach MG lub sekretach oryginalnego scenariusza.",
  publish: "Materiały dla agenta",
  publishHelp:
    "Wybierz opisy elementów do skopiowania. Scena początkowa jest wymagana. Pola sekretów i niewybrane elementy zostaną pominięte.",
  autoApprove: "Z góry zatwierdzone kategorie konsekwencji",
  approvalHelp:
    "Pozostaw niezaznaczone, aby wymagać decyzji MG. Zatwierdzenie dotyczy tej aktywności; nie zmienia kampanii i nie weryfikuje treści narracji.",
  resource: "Zasoby",
  relationship: "Relacje",
  discovery: "Odkrycia",
  narrative: "Rozwój fabuły",
  create: "Utwórz przypisanie",
  cancel: "Anuluj",
  choose: "Wybierz aktywność",
  managed: "MG",
  assigned: "Gracz",
  active: "W toku",
  completed: "Zakończona",
  version: "Wersja",
  sourceVersion: "Wersja scenariusza przy przypisaniu",
  accessTitle: "Podłącz swojego agenta",
  accessHelp:
    "Użyj tokena tej rozgrywki pod /play/mcp. Nie pozwala wywołać narzędzi autora. Nowy token unieważnia poprzedni; tokeny wygasają po 14 dniach.",
  generate: "Wygeneruj / zmień token rozgrywki",
  revoke: "Unieważnij token rozgrywki",
  token: "Prywatny token rozgrywki — skopiuj teraz",
  expires: "Wygasa",
  endpoint: "Adres MCP gracza",
  prompt: "Wiadomość początkowa do klienta MCP",
  starter:
    "Poprowadź ze mną tę indywidualną przygodę. Najpierw wywołaj get_play_context, przestrzegaj otrzymanych granic i pytaj, co robi moja postać. Zapisuj rozstrzygnięte działania przez commit_play_turn. Na koniec wywołaj finish_play_session. Nie traktuj oczekujących konsekwencji jako zatwierdzonych.",
  memory: "Pamięć robocza",
  noMemory: "Agent nie zapisał jeszcze żadnej tury.",
  recap: "Podsumowanie końcowe",
  noRecap: "Aktywność nie została jeszcze zakończona.",
  effects: "Konsekwencje",
  noEffects: "Nie zapisano jeszcze konsekwencji.",
  pending: "Do decyzji MG",
  approved: "Zatwierdzona",
  rejected: "Odrzucona",
  approve: "Zatwierdź",
  reject: "Odrzuć",
  reviewNote: "Uzasadnienie decyzji (widoczne dla gracza)",
  report: "Pobierz raport Markdown",
  journal: "Dziennik rozgrywki",
  previous: "Poprzednie",
  next: "Następne",
  noJournal: "Nie ma jeszcze rozstrzygniętych tur.",
  action: "Działanie gracza",
  narration: "Narracja",
  recordWarning:
    "Pamięć, narracja i podsumowanie są zapisem gry. Tylko zatwierdzone konsekwencje są przyjęte w ramach tej aktywności. Nic nie trafia automatycznie do głównej kampanii.",
  created: "Utworzono przypisanie.",
  tokenRevoked: "Unieważniono token rozgrywki.",
  reviewed: "Zapisano decyzję.",
  loadError: "Nie udało się pobrać aktywności.",
  requestError:
    "Żądanie nie powiodło się. Odśwież aktywność przed ponowieniem.",
  sample: "Wstaw przykładowe wprowadzenie",
  status: "Status",
  date: "Data",
  turn: "Tura",
  finish: "Zakończenie",
  examples: "Przykład: nieobecność w drużynie",
  sampleCharacter: "Arin — zwiadowca wracający do drużyny o świcie.",
  sampleBrief:
    "Gdy Twoi towarzysze badali ruiny, zostałeś w porcie. Mira prosi o pomoc w odnalezieniu ojca, latarnika. Co robisz?",
  sampleInstructions:
    "Poprowadź krótkie śledztwo z rozmową i znaczącym wyborem. Reaguj na pomysły gracza, nie wymuszaj przejść zapisanych w grafie. Opisuj skutki uczciwie i zgłaszaj trwałe zmiany do zatwierdzenia.",
  sampleBoundaries:
    "Bez trwałych obrażeń, śmierci postaci, rzadkich nagród i zmiany głównego zadania drużyny. Trwała nagroda, nowy sojusznik lub istotne odkrycie wymagają decyzji MG.",
  sampleReturn:
    "O świcie postać może wrócić do drużyny w porcie. Zakończ podsumowaniem oddzielającym przebieg gry od proponowanych konsekwencji dla kampanii.",
  sampleNotes:
    "Prywatne przypomnienie: porównaj raport z wydarzeniami głównej sesji. Nie publikuj nieodkrytych sekretów scenariusza.",
  detail: "Szczegóły aktywności",
  nodes: "Opublikowane elementy",
  gmNotesLabel: "Widoczne tylko dla człowieka pełniącego rolę MG",
  instructionsLabel: "Widoczne dla gracza i jego agenta",
  retryCreate:
    "Przypisanie mogło już powstać. Odśwież listę aktywności przed zmianą i ponownym wysłaniem formularza.",
};
export type PlayTextKey = keyof typeof en;
export const playText = (language: Language, key: PlayTextKey) =>
  (language === "pl" ? pl : en)[key];
const errors: Record<string, string> = {
  "Play session not found": "Nie znaleziono rozgrywki.",
  "Player account not found": "Nie znaleziono aktywnego konta gracza.",
  "Operation ID reused with different content":
    "Ten identyfikator operacji został już użyty z inną treścią.",
  "Publish valid unique nodes including the starting scene":
    "Wybierz poprawne elementy, w tym scenę początkową.",
  "Only pending consequences from this session can be reviewed":
    "Można rozpatrywać tylko oczekujące konsekwencje z tej rozgrywki.",
};
export function playError(language: Language, error: unknown) {
  const message = error instanceof Error ? error.message : String(error);
  return language === "pl"
    ? errors[message] ||
        (/^(Invalid |At most |Decision |Review |Duplicate )/.test(message)
          ? "Nieprawidłowe dane. Sprawdź formularz i spróbuj ponownie."
          : message)
    : message;
}
