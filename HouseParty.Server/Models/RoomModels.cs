namespace HouseParty.Server.Models;

public sealed record RoomCreated(string Id);
public sealed record RoomStatePayload(string Id, int Counter);
public sealed record RoomJoined(int Counter);
public sealed record RoomCounterUpdated(int Counter);
public sealed record RoomJoinRequest(string ConnectionId);
public sealed record SignalRNegotiation(string Url, string AccessToken);
