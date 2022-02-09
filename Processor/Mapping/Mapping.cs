namespace SpikingDSE;

public interface Core
{
    public bool AcceptsLayer(Layer layer);
    public void AddLayer(Layer layer);
    public object GetLocation();
    public string Name();
}