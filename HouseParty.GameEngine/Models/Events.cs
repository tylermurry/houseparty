using System.Text.Json.Serialization;

namespace HouseParty.GameEngine.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ControlObjectEvent), nameof(ControlObjectEvent))]
[JsonDerivedType(typeof(ReleaseObjectEvent), nameof(ReleaseObjectEvent))]
[JsonDerivedType(typeof(RevokeObjectEvent), nameof(RevokeObjectEvent))]
[JsonDerivedType(typeof(SetActivePlayerEvent), nameof(SetActivePlayerEvent))]
[JsonDerivedType(typeof(ReleaseActivePlayerEvent), nameof(ReleaseActivePlayerEvent))]
[JsonDerivedType(typeof(RevokeActivePlayerEvent), nameof(RevokeActivePlayerEvent))]
[JsonDerivedType(typeof(ClaimRoleEvent), nameof(ClaimRoleEvent))]
[JsonDerivedType(typeof(ReleaseRoleEvent), nameof(ReleaseRoleEvent))]
[JsonDerivedType(typeof(RevokeRoleEvent), nameof(RevokeRoleEvent))]
public abstract record GameEvent
{
    public long Sequence { get; set; }
    public abstract string Name { get; }
    public string PlayerId { get; init; } = string.Empty;
    public long Timestamp { get; init; }
}

public sealed record ControlObjectEvent(string ObjectId) : GameEvent { public override string Name => nameof(ControlObjectEvent); }
public sealed record ReleaseObjectEvent(string ObjectId) : GameEvent { public override string Name => nameof(ReleaseObjectEvent); }
public sealed record RevokeObjectEvent(string ObjectId) : GameEvent { public override string Name => nameof(RevokeObjectEvent); }
public sealed record SetActivePlayerEvent(string ActivePlayerId) : GameEvent { public override string Name => nameof(SetActivePlayerEvent); }
public sealed record ReleaseActivePlayerEvent : GameEvent { public override string Name => nameof(ReleaseActivePlayerEvent); }
public sealed record RevokeActivePlayerEvent : GameEvent { public override string Name => nameof(RevokeActivePlayerEvent); }
public sealed record ClaimRoleEvent(string RoleHolderId) : GameEvent { public override string Name => nameof(ClaimRoleEvent); }
public sealed record ReleaseRoleEvent : GameEvent { public override string Name => nameof(ReleaseRoleEvent); }
public sealed record RevokeRoleEvent : GameEvent { public override string Name => nameof(RevokeRoleEvent); }
