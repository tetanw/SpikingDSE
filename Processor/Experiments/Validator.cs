using System;

namespace SpikingDSE;

public class Validator
{
    public void Run()
    {
        var srnn = SRNN.Load("res/snn/best", null);

        for (int i = 0; i < 2264; i++)
        {
            var inputFile1 = new InputTraceFile($"res/shd/input_{i}.trace", 700);
            var srnn1 = new SplittedSRNN(srnn, inputFile1, 64);
            var exp1 = new MultiCoreV1(new Simulator(), false, inputFile1.Correct, srnn1, 100_000_000, 16_384);
            exp1.Run();

            var inputFile2 = new InputTraceFile($"res/shd/input_{i}.trace", 700);
            var srnn2 = srnn.Copy(inputFile2);
            var exp2 = new ProtoMultiCore(new Simulator(), false, inputFile2.Correct, srnn2, 100_000_000, 16_384);
            exp2.Run();

            if (!AreSame(srnn1.output.Pots, srnn2.output.Pots))
            {
                Console.WriteLine($"[{i}] FAILED");
                // break;
            }
            else
            {
                // Console.WriteLine($"[{i}] PASSED");
            }
        }
    }

    private bool AreSame(float[] a, float[] b)
    {
        for (int j = 0; j < 20; j++)
        {
            if (Math.Abs(a[j] - b[j]) >= 0.01)
            {
                return false;
            }    
        }

        return true;
    }
}