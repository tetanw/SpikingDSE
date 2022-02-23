using System;
using System.Collections.Generic;

namespace SpikingDSE;

public class ProtoMultiCoreTest
{
    private ProtoMultiCore exp;

    public ProtoMultiCoreTest()
    {
        var inputFile = new InputTraceFile($"res/shd/input_0.trace", 700, 100);
        var srnn = SRNN.Load("res/snn/best", inputFile);
        this.exp = new ProtoMultiCore(true, inputFile.Correct, srnn, 100_000_000, int.MaxValue);
    }

    public void Run()
    {
        exp.Run();
    }
}