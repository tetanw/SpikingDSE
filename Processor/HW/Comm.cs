namespace SpikingDSE;

public abstract class Comm : Actor
{
    public abstract double Memory();
    public abstract string Report(bool header);
}