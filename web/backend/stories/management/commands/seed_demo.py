from django.core.management.base import BaseCommand
from stories.models import Scenario
from stories.demo import DOCUMENT


class Command(BaseCommand):
    def handle(self, *args, **kwargs):
        Scenario.objects.get_or_create(
            is_demo=True, defaults={"title": "Dzwon pod wodą", "document": DOCUMENT}
        )
        self.stdout.write("Demo ready")
