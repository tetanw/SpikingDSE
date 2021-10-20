using System.Collections.Generic;

namespace SpikingDSE
{
    public class XYRouter : Process
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
                // TODO: Implement select
                var packet = new XYPacket();

                // 2. Add a delay to simulate processing
                yield return env.Delay(processingDelay);

                // 3. Determine into which output port it goes
                OutPort outPort;
                if (packet.x > 0)
                {
                    // West
                    outPort = outWest;
                }
                else if (packet.x < 0)
                {
                    // East
                    outPort = outEast;
                }
                else if (packet.y > 0)
                {
                    // South
                    outPort = outSouth;
                }
                else if (packet.y < 0)
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

    public class XYPacket
    {
        public int x;
        public int y;
        public object Message;
    }
}