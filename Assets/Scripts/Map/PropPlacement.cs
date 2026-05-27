// PropPlacer.SelectPlacements çıktısı: hangi anchor'a hangi palette girdisi,
// hangi yaw ve ölçekle yerleşecek. Index tabanlı → GameObject bağımsız, test edilebilir.
public struct PropPlacement
{
    public int AnchorIndex;
    public int EntryIndex;
    public float Yaw;
    public float Scale;
}
