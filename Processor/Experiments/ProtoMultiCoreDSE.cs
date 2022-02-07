using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class ProtoMultiCoreDSE : DSEExperiment<ProtoMultiCore>
{
    private int size = 2264;
    private SRNN srnn = null;
    private int nrCorrect = 0;
    private int curBufferSize = -1;

    public ProtoMultiCoreDSE()
    {
        this.srnn = SRNN.Load("res/snn/best", null);
    }

    public override IEnumerable<IEnumerable<ProtoMultiCore>> Configs()
    {
        var sizes = Enumerable.Range(1, 64).Where(i => i % 2 == 1);

        foreach (var size in sizes)
        {
            curBufferSize = size;
            yield return WithBufferSize(size);
        }
    }

    public IEnumerable<ProtoMultiCore> WithBufferSize(int bufferSize)
    {
        for (int i = 0; i < size; i++)
        {
            var inputFile = new InputTraceFile($"res/shd/input_{i}.trace", 700, 100);
            var simulator = new Simulator();
            var exp = new ProtoMultiCore(simulator, false, inputFile.Correct, srnn.Copy(inputFile), 100_000_000, bufferSize);
            yield return exp;
        }
    }

    public override void OnConfigCompleted(TimeSpan runningTime)
    {
        var acc = (float)nrCorrect / size;
        Console.WriteLine($"{curBufferSize};{acc};{(int)runningTime.TotalMilliseconds}ms");
        nrCorrect = 0;
    }

    public override void OnExpCompleted(ProtoMultiCore exp)
    {
        if (exp.prediction == exp.correct)
        {
            nrCorrect++;
        }
    }
}