using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MultiCoreV1Test : Experiment
{
    private MultiCoreV1 exp;

    public MultiCoreV1Test()
    {
        var srnn = SRNN.Load("res/snn/best", null, 2);
        var mapping = MultiCoreV1Mapping.CreateMapping(new FirstFitMapper(), srnn);
        mapping.PrintReport();

        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700, 100);
        var splittedSRNN = SplittedSRNN.SplitSRNN(srnn, mapping, inputFile);
        this.exp = new MultiCoreV1(sim, true, inputFile.Correct, splittedSRNN, mapping, 1_000_000, 512);
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