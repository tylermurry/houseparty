using System.Text.Json.Serialization;

namespace HouseParty.GameEngine.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ControlObjectEvent), nameof(ControlObjectEvent))]
[JsonDerivedType(typeof(ReleaseObjectEvent), nameof(ReleaseObjectEvent))]
[JsonDerivedType(typeof(RevokeObjectEvent), nameof(RevokeObjectEvent))]
[JsonDerivedType(typeof(ClaimRoleEvent), nameof(ClaimRoleEvent))]
[JsonDerivedType(typeof(ReleaseRoleEvent), nameof(ReleaseRoleEvent))]
[JsonDerivedType(typeof(RevokeRoleEvent), nameof(RevokeRoleEvent))]
[JsonDerivedType(typeof(SubmitActionEvent), nameof(SubmitActionEvent))]
public abstract record GameEvent
{
    public long Sequence { get; set; }
    public abstract string Name { get; }
    public string PlayerId { get; init; } = string.Empty;
    public long Timestamp { get; init; }
}

// Exclusive Operation Events
public sealed record ControlObjectEvent(string ObjectId) : GameEvent { public override string Name => nameof(ControlObjectEvent); }
public sealed record ReleaseObjectEvent(string ObjectId) : GameEvent { public override string Name => nameof(ReleaseObjectEvent); }
public sealed record RevokeObjectEvent(string ObjectId) : GameEvent { public override string Name => nameof(RevokeObjectEvent); }
public sealed record ClaimRoleEvent(string RoleHolderId) : GameEvent { public override string Name => nameof(ClaimRoleEvent); }
public sealed record ReleaseRoleEvent : GameEvent { public override string Name => nameof(ReleaseRoleEvent); }
public sealed record RevokeRoleEvent : GameEvent { public override string Name => nameof(RevokeRoleEvent); }

// Contested Operation Events
public sealed record SubmitActionEvent(string action) : GameEvent { public override string Name => nameof(SubmitActionEvent); }
