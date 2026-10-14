export type Language = "pl" | "en";
const english: Record<string, string> = {
  "Nowa scena": "New scene",
  "Nowa postać": "New NPC",
  "Nowe miejsce": "New location",
  "Nowa wskazówka": "New clue",
  "Nieprawidłowy plik JSON.": "Invalid JSON file.",
  Scena: "Scene",
  Postać: "NPC",
  Miejsce: "Location",
  Wskazówka: "Clue",
  "Żądanie nie powiodło się": "Request failed",
  "Otwieranie warsztatu…": "Opening the workshop…",
  "Pamięć przeglądarki niedostępna — wyeksportuj swoją pracę.":
    "Browser storage is unavailable — export your work.",
  "Scena nieosiągalna ze sceny początkowej przez leads_to.":
    "Scene is unreachable from the start through leads_to links.",
  "Materiały dla MG — zawierają sekrety.": "GM material — includes secrets.",
  "Sekret MG": "GM secret",
  "Limit 1000 powiązań.": "Maximum of 1000 links.",
  "Zapisano na serwerze.": "Saved to server.",
  "NARZĘDZIA DO OPOWIADANIA HISTORII": "TOOLS FOR TELLING STORIES",
  "Zaloguj się": "Log in",
  "WARSZTAT / PRZYGODA NA JEDNĄ SESJĘ": "WORKSHOP / A ONE-SESSION ADVENTURE",
  "Tytuł przygody": "Adventure title",
  "Śledź powiązania. Odkrywaj luki. Zostaw graczom przestrzeń.":
    "Follow connections. Find gaps. Leave room for your players.",
  "Kopia w tej przeglądarce": "Copy in this browser",
  "Zapisano · wersja ": "Saved · version ",
  "Niezapisane zmiany": "Unsaved changes",
  "Zapisz na koncie": "Save to account",
  "Zapisz zmiany": "Save changes",
  "Twoje konto": "Your account",
  "Zaloguj się do warsztatu": "Log in to the workshop",
  "Zapisane przygody i dostęp MCP.": "Saved adventures and MCP access.",
  "Demo działa bez konta. Konto tworzy administrator serwera.":
    "Try the demo without an account. Accounts are created by the server administrator.",
  "Zapisane przygody": "Saved adventures",
  "Otworzyć inną przygodę i odrzucić niezapisane zmiany?":
    "Open another adventure and discard unsaved changes?",
  "Wybierz przygodę": "Choose an adventure",
  "Wygeneruj / zmień token MCP": "Generate / rotate MCP token",
  Wyloguj: "Log out",
  "Token Bearer — skopiuj i zachowaj prywatnie":
    "Bearer token — copy and keep private",
  Login: "Username",
  Hasło: "Password",
  Zaloguj: "Log in",
  "Zamknij komunikat": "Dismiss message",
  ZAWARTOŚĆ: "CONTENTS",
  "+ Dodaj": "+ Add",
  "Nowa ": "New ",
  "DOBRA PRZYGODA": "A GOOD ADVENTURE",
  "MA WIĘCEJ NIŻ JEDNĄ DROGĘ.": "HAS MORE THAN ONE PATH.",
  "Graf przygody": "Adventure graph",
  "Mapa powiązań": "Connection map",
  "Nowe połączenie": "New link",
  "prowadzi do": "leads to",
  ujawnia: "reveals",
  angażuje: "involves",
  "mieści się w": "located at",
  miejsce: "location",
  "Przeciągnij między punktami, aby połączyć elementy.":
    "Drag between handles to connect elements.",
  SZCZEGÓŁY: "DETAILS",
  Tytuł: "Title",
  "Opis / tekst dla graczy": "Description / player-facing text",
  "Sekret dla prowadzącego": "Game master secret",
  "Ustaw jako początek": "Set as start",
  Powiązania: "Connections",
  "Usuń powiązanie": "Delete link",
  "Usunąć element i jego powiązania?":
    "Delete this element and its connections?",
  "Usuń element": "Delete element",
  "Wybierz element na mapie.": "Select an element on the map.",
  "DRUGIE SPOJRZENIE": "A SECOND LOOK",
  "Sprawdź, zanim poprowadzisz.": "Check before you run the game.",
  "Kontrola struktury wskazuje luki. O jakości historii decydujesz Ty.":
    "Structural checks highlight gaps. You decide what makes a good story.",
  "Zapisz zmiany przed walidacją serwera.":
    "Save your changes before server validation.",
  "Sprawdź strukturę ↗": "Check structure ↗",
  "Eksport Markdown ↓": "Export Markdown ↓",
  "Import JSON": "Import JSON",
  "Plik jest zbyt duży (maks. 300 KB).": "File is too large (maximum 300 KB).",
  "Nieprawidłowy dokument narrative-web/1.":
    "Invalid narrative-web/1 document.",
  "Nieprawidłowy tytuł przygody.": "Invalid adventure title.",
  "Zastąpić bieżącą zawartość importowaną przygodą?":
    "Replace current content with the imported adventure?",
  "Importowana przygoda": "Imported adventure",
  "Brak uwag strukturalnych": "No structural issues found",
  "Warsztat 0.2 / RPG": "Workshop 0.2 / RPG",
  "Nieprawidłowy login lub hasło": "Invalid username or password",
  "Wskazówka ma {count} źródła. Rozważ trzy niezależne drogi odkrycia.":
    "This clue has {count} sources. Consider three independent ways to discover it.",
  "{count} powiązań": "Connections: {count}",
  "{count} sugestie do przejrzenia": "Suggestions to review: {count}",
  "Język interfejsu": "Interface language",
  "Język zmieniono. Treść Twojej przygody pozostaje w języku autora.":
    "Language changed. Your adventure keeps its original content.",
};
const polishErrors: Record<string, string> = {
  "Invalid login fields":
    "Login i hasło muszą być tekstem o poprawnej długości.",
  "Invalid storage text":
    "Tekst zawiera niedozwolony znak lub niepoprawny Unicode.",
  "JSON structure exceeds limits": "Dane JSON mają zbyt złożoną strukturę.",
  "Invalid JSON number": "Dane zawierają niepoprawną liczbę.",
  "Unexpected server response":
    "Serwer zwrócił nieoczekiwaną odpowiedź. Spróbuj ponownie później.",
  "Service dependency unavailable; retry later":
    "Usługa jest chwilowo niedostępna. Spróbuj ponownie później.",
  "Operation ID reused with different content":
    "Ta operacja została już zapisana z inną treścią. Otwórz zapisaną przygodę i porównaj zmiany; możesz wyeksportować lokalny szkic.",
  "Request failed": "Żądanie nie powiodło się",
  "Expected a JSON object": "Oczekiwano obiektu JSON",
  "CSRF check failed": "Sesja wygasła. Odśwież stronę i spróbuj ponownie.",
  "Invalid username or password": "Nieprawidłowy login lub hasło",
  "Run seed_demo":
    "Przykład nie jest dostępny. Administrator musi uruchomić seed_demo.",
  "Provide a UUID id for retry-safe creation":
    "Wymagany jest poprawny identyfikator UUID.",
  "ID unavailable": "Ten identyfikator jest już zajęty.",
  "Scenario quota reached (50)": "Osiągnięto limit 50 przygód.",
  "Invalid title": "Nieprawidłowy tytuł.",
  "Not found": "Nie znaleziono przygody.",
  "Scenario not found": "Nie znaleziono przygody.",
  "Version conflict. Reload before saving; your changes were not applied.":
    "Konflikt wersji. Odśwież dane przed zapisem; Twoje zmiany nie zostały zapisane.",
  "Title must contain 1–200 characters": "Tytuł musi mieć od 1 do 200 znaków.",
  "Expected integer version": "Oczekiwano całkowitego numeru wersji.",
  "Unsupported schema; expected narrative-web/1":
    "Nieobsługiwany format; wymagany narrative-web/1.",
  "Scenario exceeds 256 KB": "Przygoda przekracza limit 256 KB.",
  "Use 1–250 nodes and at most 1000 links":
    "Przygoda musi mieć 1–250 elementów i maksymalnie 1000 powiązań.",
  "Nodes need unique string IDs (1–80 characters)":
    "Elementy muszą mieć unikalne identyfikatory (1–80 znaków).",
  "Invalid node kind or title": "Nieprawidłowy typ lub tytuł elementu.",
  "Coordinates must be finite numbers within ±100000":
    "Współrzędne muszą być liczbami w zakresie ±100000.",
  "Text fields must contain at most 12000 characters":
    "Pole tekstowe może mieć maksymalnie 12000 znaków.",
  "Invalid or duplicate link, or missing endpoint":
    "Nieprawidłowe lub powtórzone powiązanie albo brak elementu docelowego.",
  "Starting node must be a scene": "Element początkowy musi być sceną.",
  "Authentication credentials were not provided.":
    "Zaloguj się, aby wykonać tę operację.",
  "CSRF Failed: CSRF token missing.": "Odśwież stronę i zaloguj się ponownie.",
  "Failed to fetch": "Nie udało się połączyć z serwerem.",
  "Unexpected end of JSON input": "Nieprawidłowy plik JSON.",
};
export function translate(language: Language, text: string): string {
  if (language === "pl" && text === "{count} sugestie do przejrzenia")
    return "Sugestie do przejrzenia: {count}";
  if (language === "pl" && text === "{count} powiązań")
    return "Powiązania: {count}";
  if (
    language === "pl" &&
    text ===
      "Wskazówka ma {count} źródła. Rozważ trzy niezależne drogi odkrycia."
  )
    return "Wskazówka — liczba źródeł: {count}. Rozważ trzy niezależne drogi odkrycia.";
  if (text.startsWith("Request was throttled."))
    return language === "pl"
      ? "Za dużo żądań. Odczekaj chwilę i spróbuj ponownie."
      : "Too many requests. Wait a moment and try again.";
  return language === "en" ? english[text] || text : polishErrors[text] || text;
}
export function initialLanguage(): Language {
  const explicit = new URLSearchParams(location.search).get("lang");
  if (explicit === "pl" || explicit === "en") return explicit;
  try {
    const saved = localStorage.getItem("narrative-framework-language");
    if (saved === "en" || saved === "pl") return saved;
  } catch {}
  return navigator.language.toLowerCase().startsWith("pl") ? "pl" : "en";
}
export function formatIssue(
  language: Language,
  issue: { message: string; code?: string; count?: number },
): string {
  if (issue.code === "clue_sources")
    return translate(
      language,
      "Wskazówka ma {count} źródła. Rozważ trzy niezależne drogi odkrycia.",
    )
      .replace("{count}", String(issue.count))
      .replace("1 sources.", "1 source.");
  if (issue.code === "unreachable_scene")
    return translate(
      language,
      "Scena nieosiągalna ze sceny początkowej przez leads_to.",
    );
  return translate(language, issue.message);
}

const accountEnglish: Record<string, string> = {
  "Załóż konto": "Create account",
  "Mam już konto": "I already have an account",
  "Powtórz hasło": "Confirm password",
  "Demo działa bez konta. Załóż konto, aby zapisywać prywatne przygody.":
    "Try the demo without an account. Create an account to save private adventures.",
  "Wersja testowa. Bez adresu e-mail i automatycznego odzyskiwania hasła. Eksportuj ważne przygody.":
    "Test instance. No email or automatic password recovery. Export important adventures.",
  "Hasło: minimum 12 znaków. Unikaj popularnych haseł i swojego loginu.":
    "Password: at least 12 characters. Avoid common passwords and your username.",
};
Object.assign(english, accountEnglish);

Object.assign(polishErrors, {
  "Registration is disabled": "Rejestracja jest wyłączona.",
  "Log out before creating another account":
    "Wyloguj się przed utworzeniem kolejnego konta.",
  "Use 3–32 letters, numbers, dots, underscores or hyphens":
    "Login: 3–32 znaki, litery A–Z, cyfry, kropki, podkreślenia lub myślniki.",
  "Invalid password fields": "Nieprawidłowe pola hasła.",
  "Passwords do not match": "Hasła nie są jednakowe.",
  "Use at least 12 characters and avoid common, numeric or username-like passwords":
    "Użyj co najmniej 12 znaków. Unikaj popularnych haseł, samych cyfr i podobieństwa do loginu.",
  "This username is already taken": "Ten login jest już zajęty.",
  "CSRF check failed": "Odśwież stronę i spróbuj ponownie.",
});
