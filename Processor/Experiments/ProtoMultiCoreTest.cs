using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class ProtoMultiCoreTest : Experiment
{
    private ProtoMultiCore exp;

    public ProtoMultiCoreTest()
    {
        var inputFile = new InputTraceFile($"res/shd/input_521.trace", 700);
        var srnn = SRNN.Load("res/snn/best", inputFile);
        this.exp = new ProtoMultiCore(sim, true, inputFile.Correct, srnn, 100_000_000, int.MaxValue);
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