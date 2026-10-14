# First campaign: UX design and validation

[Polski](../pl/first-campaign-ux.md)

The first-use outcome is a GM's own campaign, two entries and a named relationship on a map. A name and one sentence per entry are enough. Markdown, MCP and adventure-editor knowledge are unnecessary.

This design is based on the maintainer's requirements and an interface review, not completed user research. Local `ux-researcher-designer` and `ui-design-system` skills informed the journey, usability study and consistency checks. Existing visual styles are preserved.

The signed-out wiki returns users to campaigns after login/registration through the fixed `next=campaigns` target; invitations retain precedence. The empty state focuses campaign naming. Inline guidance then offers character/place creation with the category selected and title focused, suggests a second related entry, and presents a from → relationship → to form. Saving the connection opens the map. Clicking a node opens its article.

Progress is derived from saved entries and relationships, including after reload. Tips can be hidden/reopened. Guidance does not insert demo data, publish material or block the interface. Players/player previews do not receive editing guidance. Existing relationship controls, graph layout and publication rules remain available.

`web/tests/onboarding_browser.py` checks the real PL/EN journey, login return, focus, title guard, typed entries, reload progress, a custom directed connection, saved map, 390 px layout, hide/reopen and map-to-article navigation. It creates and deletes fictional accounts and sends no GA data.

## Usability study — pending

Ask five first-time GMs: “Prepare a session at an inn. Record its owner and the inn, show that she works there on a map, then change her description.” Start at campaigns and include account creation. Do not name controls or guide actions. Record completion without help, time to map, reversed relationships, lost work and hesitation. Ask what was unclear and whether they would use this before their next session.

Starting targets: 4/5 finish without help; own map within three minutes after login; no lost work or accidental publication. These are proposed targets, not measured results. Browser automation validates behavior, not intuitiveness.
