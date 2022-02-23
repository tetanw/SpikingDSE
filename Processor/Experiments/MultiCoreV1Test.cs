using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MultiCoreV1Test
{
    private MultiCoreV1 exp;

    public MultiCoreV1Test()
    {
        var srnn = SRNN.Load("res/snn/best", null, 2);
        var mapping = MultiCoreV1Mapping.CreateMapping(new FirstFitMapper(), srnn);
        mapping.PrintReport();

        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700, 100);
        var splittedSRNN = SplittedSRNN.SplitSRNN(srnn, mapping, inputFile);
        exp = new MultiCoreV1(true, "res/multi-core/v1", inputFile.Correct, splittedSRNN, mapping, 100_000, 512);
    }

    public void Run()
    {
        exp.Run();
    }
}