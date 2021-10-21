using System.Collections.Generic;

namespace SpikingDSE
{
    public class MeshNI : Process
    {
        public InPort MeshIn;
        public InPort PEIn;
        public OutPort MeshOut;
        public OutPort PEOut;

        public int x, y;

        public MeshNI(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                var select = env.Select(MeshIn, PEIn);
                yield return select;

                if (select.Port == MeshIn)
                {
                    var packet = (MeshFlit) select.Port.Message;
                    yield return env.Send(PEOut, packet.Message);
                }
                else if (select.Port == PEIn)
                {
                    var packet = (Packet) select.Port.Message;
                    var (x, y) = FindLocation(packet.ID);
                    var flit = new MeshFlit
                    {
                        X = x,
                        Y = y,
                        Message = packet.Message
                    };
                    yield return env.Send(MeshOut, packet);
                }
            }
        }

        private (int x, int y) FindLocation(int ID)
        {
            // Do location
            return (0, 0);
        }
    }

    public class Packet
    {
        public int ID;
        public object Message;
    }
}