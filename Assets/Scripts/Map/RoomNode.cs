using UnityEngine;

public class RoomNode
{
    public Vector2Int Coordinates { get; }
    public ConnectionDirection Connections { get; private set; }
    public RoomType Type { get; set; }
    public RoomSize Size { get; set; }
    public int MergeGroupId { get; set; } = -1;

    public RoomNode(Vector2Int coordinates, RoomType type = RoomType.Standard)
    {
        Coordinates = coordinates;
        Type = type;
        Connections = ConnectionDirection.None;
        Size = RoomSize.Small_1x1;
    }

    public void AddConnection(ConnectionDirection direction) => Connections |= direction;
    public void RemoveConnection(ConnectionDirection direction) => Connections &= ~direction;
    public bool HasConnection(ConnectionDirection direction) => (Connections & direction) != 0;
}
