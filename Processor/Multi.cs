using System.Collections.Generic;

namespace SpikingDSE
{
    public interface Locator<T>
    {
        public T Locate(int packetID);
    }

    public class MeshNI : Actor
    {
        public InPort FromMesh;
        public InPort FromCore;
        public OutPort ToMesh;
        public OutPort ToCore;

        private Locator<(int x, int y)> locator;
        public int srcX, srcY;

        public MeshNI(int x, int y, Locator<(int x, int y)> locator, string name = "")
        {
            this.srcX = x;
            this.srcY = y;
            this.locator = locator;
            this.name = name;
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
                    var (destX, destY) = locator.Locate(packet.ID);
                    var flit = new MeshFlit
                    {
                        DX = destX - srcX,
                        DY = destY - srcY,
                        Message = packet.Message
                    };
                    yield return env.Send(ToMesh, flit);
                }
            }
        }
    }

    public class Packet
    {
        public int ID;
        public object Message;
    }
}