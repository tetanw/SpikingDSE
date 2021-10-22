using System.Collections.Generic;

namespace SpikingDSE
{
    public class MeshNI : Actor
    {
        public InPort FromMesh;
        public InPort FromCore;
        public OutPort ToMesh;
        public OutPort ToCore;

        public int srcX, srcY;

        public MeshNI(int x, int y, string name = "")
        {
            this.name = name;
            this.srcX = x;
            this.srcY = y;
        }

        public override IEnumerable<Command> Run()
        {
            while (true)
            {
                var select = env.Select(FromMesh, FromCore);
                yield return select;

                if (select.Port == FromMesh)
                {
                    var packet = (MeshFlit) select.Message;
                    yield return env.Send(ToCore, packet.Message);
                }
                else if (select.Port == FromCore)
                {
                    var packet = (Packet) select.Message;
                    var (x, y) = FindLocation(packet.ID);
                    var flit = new MeshFlit
                    {
                        DX = x,
                        DY = y,
                        Message = packet.Message
                    };
                    yield return env.Send(ToMesh, flit);
                }
            }
        }

        private (int x, int y) FindLocation(int ID)
        {
            var (destX, destY) = (1, 0);
            return (destX - srcX, destY - srcY);
        }
    }

    public class Packet
    {
        public int ID;
        public object Message;
    }
}