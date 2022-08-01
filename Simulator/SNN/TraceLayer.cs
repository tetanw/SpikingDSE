using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

// public class TraceLayer : HiddenLayer
// {
//     private readonly ISpikeSource spikeSource;
//     private readonly int offset;
//     private readonly bool isRecurrent;
//     public int TS { get; private set; }

//     public TraceLayer(ISpikeSource spikeSource, bool isRecurrent, int offset)
//     {
//         this.spikeSource = spikeSource;
//         this.isRecurrent = isRecurrent;
//         this.offset = offset;
//         TS = 0;
//     }

//     public override void Forward(int _) { }

//     public override IEnumerable<int> Sync()
//     {
//         TS++;
//         bool spikesRemaining = spikeSource.NextTimestep();
//         if (!spikesRemaining)
//             return Enumerable.Empty<int>();

//         return spikeSource.NeuronSpikes();
//     }

//     public override bool IsRecurrent() => isRecurrent;

//     public override int Offset() => offset;
// }