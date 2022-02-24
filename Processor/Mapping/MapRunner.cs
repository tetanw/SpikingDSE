using System.Text.Json;
using System.IO;

namespace SpikingDSE;

public class MapRunner
{
    public MapRunner()
    {

    }

    public void Run()
    {
        var mapper = new FirstFitMapper();
        var hw = HWSpec.Load("./data/mesh-hw.json");
        var snn = SRNN.Load("./res/snn/best", 700, 2);
        var mapping = MultiCoreMapping.CreateMapping(mapper, hw, snn);
        mapping.Save("./data/mapping.json");
    }
}