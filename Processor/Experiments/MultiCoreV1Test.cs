using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MultiCoreV1Test : Experiment
{
    private MultiCoreV1 exp;

    public MultiCoreV1Test()
    {
        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700);
        var srnn = new SRNN("res/snn/best", inputFile);
        this.exp = new MultiCoreV1(sim, true, inputFile.Correct, srnn, 100_000_000, 2048);
    }

    public override void Setup()
    {
        exp.Setup();
    }

    public override void Cleanup()
    {
        exp.Cleanup();
    }
}