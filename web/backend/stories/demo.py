DOCUMENT = {
    "schema": "narrative-web/1",
    "start": "harbor",
    "nodes": [
        {
            "id": "harbor",
            "kind": "scene",
            "title": "Dzwon pod wodą",
            "text": "O północy z zatopionej kaplicy dobiega bicie dzwonu. Rano znika latarnik. W porcie czeka jego córka, Mira.",
            "secret": "Dzwon odpowiada na rytuał odprawiany w starej solni.",
            "x": 40,
            "y": 140,
        },
        {
            "id": "market",
            "kind": "scene",
            "title": "Targ milczących rybaków",
            "text": "Rybacy sprzedają połów w zupełnej ciszy. Ktoś wykupił cały zapas soli.",
            "secret": "Sprzedawczyni widziała wozy jadące do solni.",
            "x": 350,
            "y": 40,
        },
        {
            "id": "salt",
            "kind": "scene",
            "title": "Stara solnia",
            "text": "Ślady wozów kończą się przy zaryglowanej bramie. W środku płonie zielone światło.",
            "secret": "Latarnik żyje. Próbuje przerwać rytuał, który sam rozpoczął.",
            "x": 670,
            "y": 140,
        },
        {
            "id": "mira",
            "kind": "npc",
            "title": "Mira, córka latarnika",
            "text": "Chce odzyskać ojca. Zna przejście do solni przez kanał odpływowy.",
            "secret": "Ukryła dziennik ojca, bo boi się oskarżeń.",
            "x": 40,
            "y": 390,
        },
        {
            "id": "ledger",
            "kind": "clue",
            "title": "Sól tłumi głos dzwonu",
            "text": "Notatki latarnika i stare pieśni wskazują, że krąg soli może przerwać rytuał.",
            "secret": "Zniszczenie dzwonu uwolni to, co jest pod kaplicą.",
            "x": 670,
            "y": 390,
        },
        {
            "id": "chapel",
            "kind": "location",
            "title": "Zatopiona kaplica",
            "text": "Przy odpływie odsłaniają się schody. Na kamieniach świeże znaki ochronne.",
            "secret": "Kaplica jest pieczęcią, nie siedzibą potwora.",
            "x": 350,
            "y": 390,
        },
    ],
    "edges": [
        {"id": "e1", "source": "harbor", "target": "market", "kind": "leads_to"},
        {"id": "e2", "source": "market", "target": "salt", "kind": "leads_to"},
        {"id": "e3", "source": "harbor", "target": "salt", "kind": "leads_to"},
        {"id": "e4", "source": "harbor", "target": "mira", "kind": "involves"},
        {"id": "e5", "source": "mira", "target": "ledger", "kind": "reveals"},
        {"id": "e6", "source": "salt", "target": "ledger", "kind": "reveals"},
        {"id": "e7", "source": "harbor", "target": "chapel", "kind": "located_at"},
    ],
}


# Content translations keep stable IDs and graph structure; authored stories are never auto-translated.
from copy import deepcopy

DOCUMENT_EN = deepcopy(DOCUMENT)
EN_CONTENT = {
    "harbor": (
        "The Bell Beneath the Water",
        "At midnight, a bell rings from the submerged chapel. By morning the lighthouse keeper has vanished. His daughter Mira waits at the harbor.",
        "The bell responds to a ritual being performed in the old saltworks.",
    ),
    "market": (
        "The Silent Fish Market",
        "The fishers sell their catch in complete silence. Someone has bought every sack of salt.",
        "A vendor saw carts heading toward the saltworks.",
    ),
    "salt": (
        "The Old Saltworks",
        "Cart tracks end at a barred gate. A green light burns inside.",
        "The lighthouse keeper is alive. He is trying to stop the ritual he started.",
    ),
    "mira": (
        "Mira, the Keeper’s Daughter",
        "She wants her father back. She knows a way into the saltworks through the drainage channel.",
        "She hid her father’s journal because she fears he will be blamed.",
    ),
    "ledger": (
        "Salt Silences the Bell",
        "The keeper’s notes and old songs suggest that a circle of salt can interrupt the ritual.",
        "Destroying the bell will release what lies beneath the chapel.",
    ),
    "chapel": (
        "The Submerged Chapel",
        "At low tide, steps emerge from the water. Fresh protective marks cover the stones.",
        "The chapel is a seal, not the creature’s home.",
    ),
}
for node in DOCUMENT_EN["nodes"]:
    node["title"], node["text"], node["secret"] = EN_CONTENT[node["id"]]
