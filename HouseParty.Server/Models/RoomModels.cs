namespace HouseParty.Server.Models;

public sealed record RoomCreated(string Id);
public sealed record RoomPlayer(int Number, string Name);
public sealed record RoomJoined(RoomPlayer Player, IReadOnlyList<RoomPlayer> Players);
public sealed record RoomJoinRequest(string ConnectionId, string Name, int? PlayerNumber);
public sealed record SignalRNegotiation(string Url, string AccessToken);
