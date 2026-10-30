from django.db import migrations, models


class Migration(migrations.Migration):
    dependencies = [("stories", "0008_character_drafts")]

    # db_default keeps inserts from the previous release working during rollout (see 0006).
    operations = [
        migrations.AddField(model_name="campaign", name="appearance",
                            field=models.CharField(max_length=20, default="fantasy", db_default="fantasy")),
        migrations.AddField(model_name="campaign", name="page_template",
                            field=models.CharField(max_length=20, default="fantasy", db_default="fantasy")),
    ]
