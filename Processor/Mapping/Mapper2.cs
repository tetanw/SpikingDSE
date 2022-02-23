namespace SpikingDSE;

public class MapRunner
{
    public MapRunner()
    {

    }

    public void Run()
    {
        // var hw = JsonSerializer.Deserialize<Specs.HW>(File.ReadAllText("./data/mesh-hw.json"));
        var hw = HWSpec.Load("./data/mesh-hw.json");

        // var mapper = new Mapper();
    }
}