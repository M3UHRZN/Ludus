/// <summary>
/// Run sonlandığında ExtractionService tarafından (Rpc→Everyone) yayınlanır.
/// ExtractionUIController bu event'i dinleyip MISSION COMPLETE/FAILED panelini doldurur.
/// </summary>
public struct RunResultEvent
{
    public int Gross;
    public int Penalty;
    public int Net;
    public int RescuedAlive;
    public int RescuedCorpses;
    public int Abandoned;
    public SessionEndReason Reason; // Escaped | TimeUp | AllDead(=wipe)
}
