using System;
using UnityEngine;

[Flags]
public enum ConnectionDirection
{
    None  = 0,
    North = 1 << 0,
    South = 1 << 1,
    East  = 1 << 2,
    West  = 1 << 3
}

public enum RoomType
{
    Standard,
    Start,
    End,
    Boss
}

public enum RoomSize
{
    Small_1x1,
    Long_1x3,
    Long_3x1,
    Large_2x2
}

public static class DirectionHelper
{
    public static readonly ConnectionDirection[] All =
    {
        ConnectionDirection.North,
        ConnectionDirection.South,
        ConnectionDirection.East,
        ConnectionDirection.West
    };

    public static Vector2Int ToVector(ConnectionDirection dir) => dir switch
    {
        ConnectionDirection.North => Vector2Int.up,
        ConnectionDirection.South => Vector2Int.down,
        ConnectionDirection.East  => Vector2Int.right,
        ConnectionDirection.West  => Vector2Int.left,
        _                         => Vector2Int.zero
    };

    public static ConnectionDirection Opposite(ConnectionDirection dir) => dir switch
    {
        ConnectionDirection.North => ConnectionDirection.South,
        ConnectionDirection.South => ConnectionDirection.North,
        ConnectionDirection.East  => ConnectionDirection.West,
        ConnectionDirection.West  => ConnectionDirection.East,
        _                         => ConnectionDirection.None
    };
}
