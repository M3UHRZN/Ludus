using System.Collections.Generic;
using UnityEngine;

public class DungeonGenerator
{
    private readonly DungeonGeneratorSO _config;
    private System.Random _rng;

    public DungeonGenerator(DungeonGeneratorSO config)
    {
        _config = config;
    }

    public DungeonData Generate()
    {
        _rng = _config.useRandomSeed ? new System.Random() : new System.Random(_config.seed);
        var data = new DungeonData();
        Phase1_PlaceStart(data);
        Phase2_GrowMaze(data);
        Phase3_AddLoops(data);
        return data;
    }

    private void Phase1_PlaceStart(DungeonData data)
    {
        data.AddRoom(new RoomNode(Vector2Int.zero, RoomType.Start));
    }

    // Growing Tree algorithm — picking a random active cell guarantees full connectivity
    private void Phase2_GrowMaze(DungeonData data)
    {
        var active = new List<Vector2Int> { Vector2Int.zero };

        while (data.RoomCount < _config.maxRooms && active.Count > 0)
        {
            int idx = _rng.Next(active.Count);
            Vector2Int current = active[idx];

            var freeNeighbors = data.GetFreeNeighbors(current);
            if (freeNeighbors.Count == 0)
            {
                active.RemoveAt(idx);
                continue;
            }

            Vector2Int next = freeNeighbors[_rng.Next(freeNeighbors.Count)];
            data.AddRoom(new RoomNode(next));
            ConnectRooms(data, current, next);
            active.Add(next);
        }
    }

    private void Phase3_AddLoops(DungeonData data)
    {
        foreach (var room in new List<RoomNode>(data.AllRooms))
        {
            foreach (var dir in DirectionHelper.All)
            {
                if (room.HasConnection(dir)) continue;
                var nbCoords = room.Coordinates + DirectionHelper.ToVector(dir);
                if (!data.IsOccupied(nbCoords)) continue;
                if (_rng.NextDouble() < _config.extraConnectionChance)
                    ConnectRooms(data, room.Coordinates, nbCoords);
            }
        }
    }

    private void ConnectRooms(DungeonData data, Vector2Int a, Vector2Int b)
    {
        Vector2Int delta = b - a;
        var dir = delta switch
        {
            { x: 0, y: 1 }  => ConnectionDirection.North,
            { x: 0, y: -1 } => ConnectionDirection.South,
            { x: 1, y: 0 }  => ConnectionDirection.East,
            { x: -1, y: 0 } => ConnectionDirection.West,
            _                => ConnectionDirection.None
        };
        if (dir == ConnectionDirection.None) return;

        data.TryGetRoom(a, out var roomA);
        data.TryGetRoom(b, out var roomB);
        roomA.AddConnection(dir);
        roomB.AddConnection(DirectionHelper.Opposite(dir));
    }
}
