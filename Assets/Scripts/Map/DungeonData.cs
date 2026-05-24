using System.Collections.Generic;
using UnityEngine;

public class DungeonData
{
    private readonly Dictionary<Vector2Int, RoomNode> _rooms = new();
    private readonly List<RoomNode> _roomList = new();

    public int RoomCount => _rooms.Count;
    // List garantili insertion-order — Dictionary.Values değil
    public IReadOnlyList<RoomNode> AllRooms => _roomList;

    public void AddRoom(RoomNode node)
    {
        _rooms[node.Coordinates] = node;
        _roomList.Add(node);
    }

    public bool TryGetRoom(Vector2Int coords, out RoomNode node) =>
        _rooms.TryGetValue(coords, out node);

    public bool IsOccupied(Vector2Int coords) => _rooms.ContainsKey(coords);

    public List<Vector2Int> GetFreeNeighbors(Vector2Int coords)
    {
        var result = new List<Vector2Int>(4);
        foreach (var dir in DirectionHelper.All)
        {
            var neighbor = coords + DirectionHelper.ToVector(dir);
            if (!IsOccupied(neighbor))
                result.Add(neighbor);
        }
        return result;
    }

    public List<(Vector2Int coords, ConnectionDirection direction)> GetOccupiedNeighbors(Vector2Int coords)
    {
        var result = new List<(Vector2Int, ConnectionDirection)>(4);
        foreach (var dir in DirectionHelper.All)
        {
            var neighbor = coords + DirectionHelper.ToVector(dir);
            if (IsOccupied(neighbor))
                result.Add((neighbor, dir));
        }
        return result;
    }
}
