using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class MultiCoreV1Test
{
    private int correct;
    private MultiCoreV1 exp;

    public MultiCoreV1Test()
    {
        var srnn = SRNN.Load("res/snn/best", 700, 2);
        var mapping = MultiCoreV1Mapping.CreateMapping(new FirstFitMapper(), srnn);
        mapping.PrintReport();

        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700, 100);
        this.correct = inputFile.Correct;
        var splittedSRNN = SplittedSRNN.SplitSRNN(srnn, mapping);
        this.exp = new MultiCoreV1(inputFile, "res/multi-core/v1", splittedSRNN, mapping, 100_000, 512);
    }

    public void Run()
    {
        exp.Run();
        Console.WriteLine($"Predicted: {exp.Predict()}, Truth: {correct}");
    }
}