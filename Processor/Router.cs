using System.Collections.Generic;

namespace SpikingDSE
{
    public class XYRouter : Actor
    {
        public InPort inNorth;
        public InPort inSouth;
        public InPort inEast;
        public InPort inWest;
        public InPort inPE;
        public OutPort outNorth;
        public OutPort outSouth;
        public OutPort outEast;
        public OutPort outWest;
        public OutPort outPE;

        public long processingDelay;

        public XYRouter(long processingDelay)
        {
            this.processingDelay = processingDelay;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                // 1. Monitor the inputs for any packet
                var select = env.Select();
                yield return select;
                MeshFlit packet = (MeshFlit)select.Message;

                // 2. Add a delay to simulate processing
                yield return env.Delay(processingDelay);

                // 3. Determine into which output port it goes
                OutPort outPort;
                if (packet.X > 0)
                {
                    // West
                    outPort = outWest;
                }
                else if (packet.X < 0)
                {
                    // East
                    outPort = outEast;
                }
                else if (packet.Y > 0)
                {
                    // South
                    outPort = outSouth;
                }
                else if (packet.Y < 0)
                {
                    // North
                    outPort = outNorth;
                }
                else
                {
                    // Chip
                    outPort = outPE;
                }

                // 4. Send to right port
                yield return env.Send(outPort, packet);
            }
        }
    }

    public class MeshFlit
    {
        public int X;
        public int Y;
        public object Message;
    }
}