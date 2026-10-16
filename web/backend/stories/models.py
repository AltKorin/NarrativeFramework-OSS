from django.conf import settings
from django.db import models
import uuid


class Scenario(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    owner = models.ForeignKey(
        settings.AUTH_USER_MODEL, on_delete=models.CASCADE, null=True
    )
    title = models.CharField(max_length=200)
    document = models.JSONField(default=dict)
    creation_fingerprint = models.CharField(
        max_length=64, blank=True, default="", editable=False
    )
    version = models.PositiveIntegerField(default=1)
    is_demo = models.BooleanField(default=False)
    updated_at = models.DateTimeField(auto_now=True)


class PlaySession(models.Model):
    """A frozen assignment, independent of subsequent edits to its source scenario."""

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    gm = models.ForeignKey(
        settings.AUTH_USER_MODEL, on_delete=models.CASCADE, related_name="managed_plays"
    )
    player = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        on_delete=models.CASCADE,
        related_name="assigned_plays",
    )
    scenario = models.ForeignKey(Scenario, on_delete=models.SET_NULL, null=True)
    source_version = models.PositiveIntegerField()
    title = models.CharField(max_length=200)
    snapshot = models.JSONField()
    mode = models.CharField(
        max_length=12,
        choices=[("downtime", "Downtime"), ("absence", "Absence"), ("solo", "Solo")],
    )
    language = models.CharField(max_length=2, default="en")
    character = models.TextField()
    brief = models.TextField()
    instructions = models.TextField()
    boundaries = models.TextField()
    return_condition = models.TextField()
    gm_notes = models.TextField(blank=True)
    auto_approve = models.JSONField(default=list)
    current_scene = models.CharField(max_length=80)
    memory_markdown = models.TextField(blank=True)
    summary = models.TextField(blank=True)
    status = models.CharField(
        max_length=12,
        default="active",
        choices=[("active", "Active"), ("completed", "Completed")],
    )
    version = models.PositiveIntegerField(default=1)
    access_digest = models.CharField(
        max_length=64, null=True, unique=True, editable=False
    )
    access_expires_at = models.DateTimeField(null=True, editable=False)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)


class PlayEvent(models.Model):
    """Operation receipt and ordered journal entry, committed with session state."""

    session = models.ForeignKey(
        PlaySession, on_delete=models.CASCADE, related_name="events"
    )
    operation_id = models.UUIDField()
    kind = models.CharField(max_length=12)
    version = models.PositiveIntegerField()
    fingerprint = models.CharField(max_length=64)
    payload = models.JSONField()
    receipt = models.JSONField()
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        ordering = ["version"]
        constraints = [
            models.UniqueConstraint(
                fields=["session", "operation_id"], name="unique_play_operation"
            )
        ]


class PlayConsequence(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    session = models.ForeignKey(
        PlaySession, on_delete=models.CASCADE, related_name="consequences"
    )
    event = models.ForeignKey(PlayEvent, on_delete=models.CASCADE)
    category = models.CharField(max_length=16)
    description = models.TextField()
    status = models.CharField(max_length=10, default="pending")
    review_note = models.TextField(blank=True)


class Campaign(models.Model):
    """Campaign world with role-scoped members; independent of play snapshots."""

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    owner = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.CASCADE)
    title = models.CharField(max_length=200)
    summary = models.TextField(blank=True)
    runtime = models.JSONField(default=dict, blank=True)
    version = models.PositiveIntegerField(default=1)
    creation_fingerprint = models.CharField(max_length=64, editable=False)
    updated_at = models.DateTimeField(auto_now=True)


class WikiPage(models.Model):
    audience = models.CharField(max_length=10, default="gm", db_default="gm")
    published = models.JSONField(null=True, default=None)
    recipients = models.ManyToManyField("CampaignMember", blank=True, related_name="granted_pages")

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    campaign = models.ForeignKey(
        Campaign, on_delete=models.CASCADE, related_name="pages"
    )
    title = models.CharField(max_length=200)
    kind = models.CharField(max_length=20, default="lore")
    body = models.TextField(blank=True)
    gm_notes = models.TextField(blank=True)
    version = models.PositiveIntegerField(default=1)
    creation_fingerprint = models.CharField(max_length=64, editable=False)
    updated_at = models.DateTimeField(auto_now=True)


class WikiRelation(models.Model):
    version = models.PositiveIntegerField(default=1, db_default=1)
    audience = models.CharField(max_length=10, default="gm", db_default="gm")
    published = models.JSONField(null=True, default=None)
    recipients = models.ManyToManyField("CampaignMember", blank=True, related_name="granted_relations")

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    campaign = models.ForeignKey(
        Campaign, on_delete=models.CASCADE, related_name="relations"
    )
    source = models.ForeignKey(
        WikiPage, on_delete=models.CASCADE, related_name="outgoing"
    )
    target = models.ForeignKey(
        WikiPage, on_delete=models.CASCADE, related_name="incoming"
    )
    label = models.CharField(max_length=80)

    class Meta:
        constraints = [
            models.UniqueConstraint(
                fields=["source", "target", "label"], name="unique_wiki_relation"
            )
        ]


class CampaignMember(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    campaign = models.ForeignKey(Campaign, on_delete=models.CASCADE, related_name="members")
    user = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.CASCADE)
    role = models.CharField(max_length=10, choices=[("gm", "Co-GM"), ("player", "Player")])

    class Meta:
        constraints = [models.UniqueConstraint(fields=["campaign", "user"], name="unique_campaign_member")]


class CampaignInvitation(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    campaign = models.ForeignKey(Campaign, on_delete=models.CASCADE, related_name="invitations")
    digest = models.CharField(max_length=64, unique=True, editable=False)
    role = models.CharField(max_length=10)
    expires_at = models.DateTimeField()
    revoked = models.BooleanField(default=False)
    accepted_by = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.SET_NULL, null=True)
    accepted_at = models.DateTimeField(null=True)
    created_at = models.DateTimeField(auto_now_add=True)


class CampaignAuditEvent(models.Model):
    campaign = models.ForeignKey(Campaign, on_delete=models.CASCADE, related_name="audit_events")
    actor = models.ForeignKey(settings.AUTH_USER_MODEL, on_delete=models.SET_NULL, null=True)
    action = models.CharField(max_length=40)
    resource_id = models.CharField(max_length=64, blank=True)
    version = models.PositiveIntegerField()
    created_at = models.DateTimeField(auto_now_add=True)
