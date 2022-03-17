using System.Collections.Generic;

namespace SpikingDSE;

// TODO: Unfinished
// public class LIF : HiddenLayer
// {
//     private readonly float[] pots;
//     private readonly float[] readout;
//     private readonly bool[] spikes;
//     private readonly float[,] weights;
//     private readonly int offset;
//     private readonly int nrNeurons;
//     private readonly float decay;
//     private readonly float threshold;

//     public override void Forward(int neuron)
//     {
//         for (int i = 0; i < nrNeurons; i++)
//         {
//             pots[i] = weights[i, neuron];
//         }
//     }

//     public override bool IsRecurrent() => false;

//     public override int Offset() => offset;

//     public override IEnumerable<int> Sync()
//     {
//         for (int i = 0; i < nrNeurons; i++)
//         {
//             for (int dst = 0; dst < Size; dst++)
//             {
//                 float pot = pots[dst];

//                 // Readout
//                 readout[dst] = pot;

//                 // Reset
//                 if (spikes[dst])
//                     pot  = 0;

//                 // Threshold
//                 if (pot >= threshold)
//                 {
//                     spikes[dst] = true;
//                     yield return dst;
//                 }
//                 else
//                 {
//                     spikes[dst] = false;
//                 }

//                 // Leakage for next ts
//                 pot *= decay;

//                 // Writeback
//                 pots[dst] = pot;
//             }
//         }
//     }
// }