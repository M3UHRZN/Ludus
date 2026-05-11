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
        if (_config.enableRoomMerging)
            ApplyMerging(data);
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

    // Public for direct testing
    public void ApplyMerging(DungeonData data)
    {
        Phase4_Merge2x2(data);
        Phase4_MergeLinear(data);
    }

    private void Phase4_Merge2x2(DungeonData data)
    {
        foreach (var room in new List<RoomNode>(data.AllRooms))
        {
            if (room.Size != RoomSize.Small_1x1) continue;
            Vector2Int c = room.Coordinates;
            if (!data.TryGetRoom(c,                          out var bl)) continue;
            if (!data.TryGetRoom(c + Vector2Int.right,       out var br)) continue;
            if (!data.TryGetRoom(c + Vector2Int.up,          out var tl)) continue;
            if (!data.TryGetRoom(c + new Vector2Int(1, 1),   out var tr)) continue;

            if (bl.Size != RoomSize.Small_1x1 || br.Size != RoomSize.Small_1x1 ||
                tl.Size != RoomSize.Small_1x1 || tr.Size != RoomSize.Small_1x1) continue;

            if (!bl.HasConnection(ConnectionDirection.East)  || !bl.HasConnection(ConnectionDirection.North)) continue;
            if (!br.HasConnection(ConnectionDirection.West)  || !br.HasConnection(ConnectionDirection.North)) continue;
            if (!tl.HasConnection(ConnectionDirection.East)  || !tl.HasConnection(ConnectionDirection.South)) continue;
            if (!tr.HasConnection(ConnectionDirection.West)  || !tr.HasConnection(ConnectionDirection.South)) continue;

            bl.Size = br.Size = tl.Size = tr.Size = RoomSize.Large_2x2;
        }
    }

    private void Phase4_MergeLinear(DungeonData data)
    {
        foreach (var room in new List<RoomNode>(data.AllRooms))
        {
            if (room.Size != RoomSize.Small_1x1) continue;
            Vector2Int c = room.Coordinates;

            // 1x3 horizontal
            if (data.TryGetRoom(c,                       out var ra) &&
                data.TryGetRoom(c + Vector2Int.right,    out var rb) &&
                data.TryGetRoom(c + new Vector2Int(2,0), out var rc) &&
                ra.Size == RoomSize.Small_1x1 && rb.Size == RoomSize.Small_1x1 && rc.Size == RoomSize.Small_1x1 &&
                ra.HasConnection(ConnectionDirection.East) &&
                rb.HasConnection(ConnectionDirection.West) && rb.HasConnection(ConnectionDirection.East) &&
                rc.HasConnection(ConnectionDirection.West))
            {
                ra.Size = rb.Size = rc.Size = RoomSize.Long_1x3;
                continue;
            }

            // 3x1 vertical
            if (data.TryGetRoom(c,                       out var rd) &&
                data.TryGetRoom(c + Vector2Int.up,       out var re) &&
                data.TryGetRoom(c + new Vector2Int(0,2), out var rf) &&
                rd.Size == RoomSize.Small_1x1 && re.Size == RoomSize.Small_1x1 && rf.Size == RoomSize.Small_1x1 &&
                rd.HasConnection(ConnectionDirection.North) &&
                re.HasConnection(ConnectionDirection.South) && re.HasConnection(ConnectionDirection.North) &&
                rf.HasConnection(ConnectionDirection.South))
            {
                rd.Size = re.Size = rf.Size = RoomSize.Long_3x1;
            }
        }
    }
}
