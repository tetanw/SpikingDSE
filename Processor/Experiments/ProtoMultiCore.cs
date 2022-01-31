using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MulitCoreHW
{
    private Simulator sim;

    public MeshRouter[,] routers;
    public ProtoController controller;
    public List<Core> cores = new();

    private long interval;
    private int bufferSize;

    public int width, height;

    public MulitCoreHW(Simulator sim, int width, int height, long interval, int bufferSize)
    {
        this.sim = sim;
        this.width = width;
        this.height = height;
        this.interval = interval;
        this.bufferSize = bufferSize;
    }

    public void CreateRouters(MeshUtils.ConstructRouter createRouters)
    {
        routers = MeshUtils.CreateMesh(sim, width, height, createRouters);
    }

    public void AddController(SNN snn, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ProtoController(controllerCoord, 100, snn, 0, interval, name: "controller"));
        sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
        this.controller = controller;
    }

    public void AddCore(ProtoDelayModel delayModel, int size, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new ProtoCore(coreCoord, size, delayModel, name: name, feedbackBufferSize: bufferSize));
        sim.AddChannel(core.output, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, core.input);
        this.cores.Add(core);
    }

    public List<Core> GetPEs()
    {
        var newCores = new List<Core>(cores);
        newCores.Add(controller);
        return newCores;
    }
}

public class SRNN
{
    public SNN snn;
    private InputLayer input;
    private ALIFLayer[] hidden;
    private OutputIFLayer output;

    public SRNN(string folderPath, ISpikeSource spikeSource)
    {
        snn = new SNN();

        this.input = new InputLayer(spikeSource, name: "i");
        snn.AddLayer(input);

        hidden = new ALIFLayer[2];
        hidden[0] = createALIFLayer(folderPath, "i", "h1");
        snn.AddLayer(hidden[0]);
        hidden[1] = createALIFLayer(folderPath, "h1", "h2");
        snn.AddLayer(hidden[1]);

        this.output = createOutputLayer(folderPath);
        snn.AddLayer(output);
    }

    private SRNN() { }

    private ALIFLayer createALIFLayer(string folderPath, string inputName, string name)
    {
        float[] tau_m1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_{name}.csv", headers: true);
        float[] tau_adp1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_adp_{name}.csv", headers: true);
        float[] alpha1 = tau_m1.Transform(WeigthsUtil.Exp);
        float[] rho1 = tau_adp1.Transform(WeigthsUtil.Exp);
        float[] alphaComp1 = alpha1.Transform((_, a) => 1 - a);
        var hidden = new ALIFLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_{inputName}_2_{name}.csv", headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp1)),
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_{name}_2_{name}.csv", headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp1)),
            WeigthsUtil.Read1DFloat($"{folderPath}/bias_{name}.csv", headers: true),
            alpha1,
            rho1,
            0.01f,
            name: $"{name}"
        );
        return hidden;
    }

    private OutputIFLayer createOutputLayer(string folderPath)
    {
        float[] tau_m3 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_o.csv", headers: true);
        float[] alpha3 = tau_m3.Transform(WeigthsUtil.Exp);
        float[] alphaComp3 = alpha3.Transform((_, a) => 1 - a);
        var output = new OutputIFLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_h2_2_o.csv", headers: true).Transform(WeigthsUtil.ScaleWeights(alphaComp3)),
            alpha3,
            threshold: 0.01f,
            name: "output"
        );
        return output;
    }

    public SRNN Copy(ISpikeSource spikeSource)
    {
        var other = new SRNN();
        other.input = new InputLayer(spikeSource, "i");
        other.hidden = new ALIFLayer[2];
        other.hidden[0] = this.hidden[0].Copy();
        other.hidden[1] = this.hidden[1].Copy();
        other.output = this.output.Copy();
        other.snn = new SNN();
        other.snn.AddLayer(other.input);
        other.snn.AddLayer(other.hidden[0]);
        other.snn.AddLayer(other.hidden[1]);
        other.snn.AddLayer(other.output);
        return other;
    }

    public int Prediction() => this.output.Prediction();
}
