using System.Collections.Generic;

namespace SpikingDSE
{
    public class XYRouter : Actor
    {
        public InPort inNorth;
        public InPort inSouth;
        public InPort inEast;
        public InPort inWest;
        public InPort fromCore;
        public OutPort outNorth;
        public OutPort outSouth;
        public OutPort outEast;
        public OutPort outWest;
        public OutPort toCore;

        public long processingDelay;

        public XYRouter(long processingDelay, string name = "")
        {
            this.name = name;
            this.processingDelay = processingDelay;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                // 1. Monitor the inputs for any packet
                var select = env.Select(inNorth, inEast, inSouth, inWest, fromCore);
                yield return select;
                MeshFlit flit = (MeshFlit)select.Message;

                // 2. Add a delay to simulate processing
                yield return env.Delay(processingDelay);

                // 3. Determine into which output port it goes
                OutPort outPort;
                if (flit.DX > 0)
                {
                    // East
                    flit.DX--;
                    outPort = outEast;
                }
                else if (flit.DX < 0)
                {
                    // West
                    flit.DX++;
                    outPort = outWest;
                }
                else if (flit.DY > 0)
                {
                    // North
                    flit.DY--;
                    outPort = outNorth;
                }
                else if (flit.DY < 0)
                {
                    // South
                    flit.DY++;
                    outPort = outSouth;
                }
                else
                {
                    // Chip
                    outPort = toCore;
                }

                // 4. Send to right port
                yield return env.Send(outPort, flit);
            }
        }
    }

    public class MeshFlit
    {
        public int DX;
        public int DY;
        public object Message;
    }
}