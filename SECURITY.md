# Security reporting

This is an early public preview. Fixes target the current preview; older tags do not have a separate maintenance promise. Functional/adversarial tests are not a security audit or capacity benchmark.

Please report vulnerabilities privately through [GitHub private vulnerability reporting](https://github.com/AltKorin/NarrativeFramework-OSS/security/advisories/new). Include the affected version, minimal reproduction, expected/actual behavior and whether it involves owner/player isolation, MCP credentials, publication or data loss. Use fictional campaigns and disposable accounts. Do not include real passwords, tokens, campaign notes or personal data.

Do not open a public issue containing an unpatched exploit or exposed credentials. If private reporting is unavailable, contact the maintainer through the [project support page](https://www.buymeacoffee.com/StolemCentrumGier) to arrange a private channel before sending sensitive details. No response-time guarantee is offered.

Self-hosters: generate independent secrets, use HTTPS and secure cookies, restrict trusted proxy addresses, keep dependencies updated and test database restores. Never publish `.env`, backups, tunnel credentials, personal author tokens or Unity `OpenAISettings.asset`. Author MCP reads GM secrets; only session-scoped credentials belong in a player's play client.

## Polski

Podatności zgłaszaj prywatnie przez [GitHub](https://github.com/AltKorin/NarrativeFramework-OSS/security/advisories/new). Podaj wersję i minimalny przykład z fikcyjnymi danymi. Nie wysyłaj prawdziwych sekretów ani nie publikuj niezałatanych exploitów w Issues. Gdy prywatne zgłoszenia są niedostępne, najpierw ustal kanał kontaktu przez stronę wsparcia. Projekt jest w fazie testowej; nie obiecujemy terminu reakcji ani audytu bezpieczeństwa.
