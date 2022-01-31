using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public class ProtoMultiCoreDSE : DSEExperiment<ProtoMultiCore>
{
    private SRNN srnn = new SRNN("res/snn/best", null);

    public override IEnumerable<IEnumerable<ProtoMultiCore>> Configs()
    {
        var sizes = Enumerable.Range(1, 64).Where(i => i % 2 == 1);

        foreach (var size in sizes)
        {
            yield return WithBufferSize(size);
        }
    }

    public IEnumerable<ProtoMultiCore> WithBufferSize(int bufferSize)
    {
        for (int i = 0; i < 2264; i++)
        {
            var inputFile = new InputTraceFile($"res/shd/input_{i}.trace", 700);
            var simulator = new Simulator();
            var exp = new ProtoMultiCore(simulator, false, inputFile.Correct, srnn.Copy(inputFile), 100_000_000, bufferSize);
            yield return exp;
        }
    }

    public override void OnConfigCompleted(ProtoMultiCore[] exps)
    {
        int nrCorrect = exps.Count((exp) => exp.prediction == exp.correct);
        Console.WriteLine($"{(float)nrCorrect / exps.Length}");
    }
}