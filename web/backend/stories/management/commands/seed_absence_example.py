"""Install a private example under an existing author account, without changing it on retry."""

from django.contrib.auth import get_user_model
from django.core.management.base import BaseCommand, CommandError
from stories.demo import DOCUMENT, DOCUMENT_EN
from stories.models import Scenario


class Command(BaseCommand):
    help = "Create the absent-character example for an existing author (no accounts/tokens created)."

    def add_arguments(self, parser):
        parser.add_argument("--owner", required=True)
        parser.add_argument("--language", choices=["en", "pl"], default="en")

    def handle(self, *args, **options):
        user = (
            get_user_model()
            .objects.filter(username=options["owner"], is_active=True)
            .first()
        )
        if not user:
            raise CommandError("Existing active owner account required")
        english = options["language"] == "en"
        title = (
            "A Night Away — the Missing Keeper"
            if english
            else "Noc poza drużyną — zaginiony latarnik"
        )
        scenario, created = Scenario.objects.get_or_create(
            owner=user,
            title=title,
            is_demo=False,
            defaults={"document": DOCUMENT_EN if english else DOCUMENT},
        )
        self.stdout.write(
            f"{scenario.id} · {'created' if created else 'already exists'}"
        )
