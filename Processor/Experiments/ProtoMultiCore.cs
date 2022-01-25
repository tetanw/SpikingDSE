using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MulitCoreHW
{
    private Simulator sim;

    public MeshRouter[,] routers;
    public ProtoController controller;
    public List<Core> cores = new();

    public int width, height;

    public MulitCoreHW(Simulator sim, int width, int height)
    {
        this.sim = sim;
        this.width = width;
        this.height = height;
    }

    public void CreateRouters(MeshUtils.ConstructRouter createRouters)
    {
        routers = MeshUtils.CreateMesh(sim, width, height, createRouters);
    }

    public void AddController(SNN snn, int x, int y)
    {
        var controllerCoord = new MeshCoord(x, y);
        var controller = sim.AddActor(new ProtoController(controllerCoord, 100, snn, 0, 1_000_000, name: "controller"));
        sim.AddChannel(controller.spikesOut, routers[x, y].inLocal);
        sim.AddChannel(routers[x, y].outLocal, controller.spikesIn);
        this.controller = controller;
    }

    public void AddCore(ProtoDelayModel delayModel, int size, int x, int y, string name)
    {
        var coreCoord = new MeshCoord(x, y);
        var core = sim.AddActor(new ProtoCore(coreCoord, size, delayModel, name: name));
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

    private float Exp(int index, float value)
    {
        return (float)Math.Exp(-1.0f / value);
    }

    private Func<int, int, float, float> ScaleWeights(float[] beta)
    {
        return (x, y, f) => f * beta[y];
    }

    public SRNN(string folderPath)
    {
        snn = new SNN();

        var input = new InputLayer(new InputTraceFile($"res/shd/input_0.trace", 700), name: "input");
        snn.AddLayer(input);

        float[] tau_m1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_h1_n.csv", headers: true);
        float[] tau_adp1 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_adp_h1_n.csv", headers: true);
        float[] alpha1 = tau_m1.Transform(Exp);
        float[] rho1 = tau_adp1.Transform(Exp);
        float[] alphaComp1 = alpha1.Transform((_, a) => 1 - a);
        var hidden1 = new ALIFLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_i_2_h1_n.csv", headers: true).Transform(ScaleWeights(alphaComp1)),
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_h1_2_h1_n.csv", headers: true).Transform(ScaleWeights(alphaComp1)),
            WeigthsUtil.Read1DFloat($"{folderPath}/bias_h1_n.csv", headers: true),
            alpha1,
            rho1,
            0.01f,
            name: "hidden1"
        );
        snn.AddLayer(hidden1);

        float[] tau_m2 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_h2_n.csv", headers: true);
        float[] tau_adp2 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_adp_h2_n.csv", headers: true);
        float[] alpha2 = tau_m2.Transform(Exp);
        float[] rho2 = tau_adp2.Transform(Exp);
        float[] alphaComp2 = alpha2.Transform((_, a) => 1 - a);
        var hidden2 = new ALIFLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_h1_2_h2_n.csv", headers: true).Transform(ScaleWeights(alphaComp2)),
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_h2_2_h2_n.csv", headers: true).Transform(ScaleWeights(alphaComp2)),
            WeigthsUtil.Read1DFloat($"{folderPath}/bias_h2_n.csv", headers: true),
            alpha2,
            rho2,
            0.01f,
            name: "hidden2"
        );
        snn.AddLayer(hidden2);

        float[] tau_m3 = WeigthsUtil.Read1DFloat($"{folderPath}/tau_m_o_n.csv", headers: true);
        float[] alpha3 = tau_m3.Transform(Exp);
        float[] alphaComp3 = alpha3.Transform((_, a) => 1 - a);
        var output = new IFLayer(
            WeigthsUtil.Read2DFloat($"{folderPath}/weights_h2o_n.csv", headers: true).Transform(ScaleWeights(alphaComp3)),
            alpha3,
            threshold: 0.01f,
            name: "output"
        );
        snn.AddLayer(output);
    }
}
