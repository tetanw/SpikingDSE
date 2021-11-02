using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    public class MeshFlit
    {
        public int SrcX;
        public int SrcY;
        public int DestX;
        public int DestY;
        public object Message;

        public override string ToString()
        {
            return $"MeshFlit {{ Route: ({SrcX}, {SrcY}) -> ({DestX}, {DestY}), Message: {Message} }}";
        }
    }

    public class MeshDir
    {
        public const int North = 0;
        public const int East = 1;
        public const int South = 2;
        public const int West = 3;
        public const int Local = 4;
    }

    public class XYRouter : MeshRouter
    {
        public long processingDelay;

        public XYRouter(int x, int y, long processingDelay, string name = "")
        {
            this.Name = name;
            this.x = x;
            this.y = y;
            this.processingDelay = processingDelay;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            while (true)
            {
                // 1. Monitor the inputs for any packet
                var select = env.Select(inNorth, inEast, inSouth, inWest, inLocal);
                yield return select;
                MeshFlit flit = (MeshFlit)select.Message;

                // 2. Add a delay to simulate processing
                yield return env.Delay(processingDelay);

                // 3. Determine into which output port it goes
                int DX = flit.DestX - x;
                int DY = flit.DestY - y;
                OutPort outPort;
                if (DX > 0)
                {
                    // East
                    outPort = outEast;
                }
                else if (DX < 0)
                {
                    outPort = outWest;
                }
                else if (DY > 0)
                {
                    // North
                    outPort = outNorth;
                }
                else if (DY < 0)
                {
                    // South
                    outPort = outSouth;
                }
                else
                {
                    // Chip
                    outPort = outLocal;
                }

                // 4. Send to right port
                yield return env.Send(outPort, flit);
            }
        }
    }

    public abstract class MeshRouter : Actor
    {
        protected int x, y;

        public InPort inNorth = new InPort();
        public InPort inSouth = new InPort();
        public InPort inEast = new InPort();
        public InPort inWest = new InPort();
        public InPort inLocal = new InPort();
        public OutPort outNorth = new OutPort();
        public OutPort outSouth = new OutPort();
        public OutPort outEast = new OutPort();
        public OutPort outWest = new OutPort();
        public OutPort outLocal = new OutPort();
    }

    public class XYRouter2 : MeshRouter
    {
        public XYRouter2(int x, int y, string name = "")
        {
            this.Name = name;
            this.x = x;
            this.y = y;
        }

        public override IEnumerable<Event> Run(Environment env)
        {
            var inBuffers = new FIFO<MeshFlit>[5];
            var outBuffers = new FIFO<MeshFlit>[5];
            var signal = env.CreateSignal();

            for (int dir = 0; dir < 5; dir++)
            {
                var inPort = GetInputPort(dir);
                if (inPort.IsBound)
                {
                    inBuffers[dir] = new FIFO<MeshFlit>(env, 1);
                    env.Process(InLink(env, inPort, inBuffers[dir], signal));
                }

                var outPort = GetOutputPort(dir);
                if (outPort.IsBound)
                {
                    outBuffers[dir] = new FIFO<MeshFlit>(env, 1);
                    env.Process(OutLink(env, outPort, outBuffers[dir], signal));
                }
            }

            env.Process(Switch(env, inBuffers, outBuffers, signal));

            yield break;
        }

        private OutPort GetOutputPort(int dir)
        {
            if (dir == MeshDir.North) return outNorth;
            else if (dir == MeshDir.East) return outEast;
            else if (dir == MeshDir.South) return outSouth;
            else if (dir == MeshDir.West) return outWest;
            else if (dir == MeshDir.Local) return outLocal;
            else throw new Exception("Unknown direction");
        }

        private InPort GetInputPort(int dir)
        {
            if (dir == MeshDir.North) return inNorth;
            else if (dir == MeshDir.East) return inEast;
            else if (dir == MeshDir.South) return inSouth;
            else if (dir == MeshDir.West) return inWest;
            else if (dir == MeshDir.Local) return inLocal;
            else throw new Exception("Unknown direction");
        }

        private IEnumerable<Event> Switch(Environment env, FIFO<MeshFlit>[] inBuffers, FIFO<MeshFlit>[] outBuffers, Signal signal)
        {
            while (true)
            {
                // 1. Wait for a new packet to arrive at an input
                // or a new packet to be sent from the output
                yield return env.Wait(signal);

                // 2. If an input was ready then we need to just check whether 
                // that new input needs to be routed. If an output is ready we need
                // to check whether any of the inputs is ready
                FIFO<MeshFlit> inBuffer = null;
                FIFO<MeshFlit> outBuffer = null;
                while (true)
                {
                    bool routeFound = false;
                    for (int dir = 0; dir < 5; dir++)
                    {
                        inBuffer = inBuffers[dir];
                        if (inBuffer == null || inBuffer.Count == 0)
                            continue;

                        var outDir = DetermineOutput(inBuffer.Peek());
                        outBuffer = outBuffers[outDir];
                        if (outBuffer != null && !outBuffer.IsFull)
                        {
                            routeFound = true;
                            break;
                        }
                    }

                    // 3. If we can not find any routeable flit than we can stop
                    if (!routeFound)
                    {
                        break;
                    }

                    // 4. Transfer from input buffer to output buffer
                    var item = inBuffer.Pop();
                    outBuffer.Push(item);
                }
            }
        }


        private IEnumerable<Event> OutLink(Environment env, OutPort outPort, FIFO<MeshFlit> buffer, Signal signal)
        {
            while (true)
            {
                yield return buffer.RequestRead();
                var flit = buffer.Read();
                yield return env.Send(outPort, flit);
                buffer.ReleaseRead();
                env.Notify(signal);
            }
        }

        private IEnumerable<Event> InLink(Environment env, InPort inPort, FIFO<MeshFlit> buffer, Signal signal)
        {
            while (true)
            {
                yield return buffer.RequestWrite();
                var rcv = env.Receive(inPort);
                yield return rcv;
                buffer.Write((MeshFlit)rcv.Message);
                buffer.ReleaseWrite();
                env.Notify(signal);
            }
        }

        private int DetermineOutput(MeshFlit flit)
        {
            int DX = flit.DestX - x;
            int DY = flit.DestY - y;
            if (DX > 0)
            {
                // East
                return MeshDir.East;
            }
            if (DX < 0)
            {
                // West
                return MeshDir.West;
            }
            else if (DY > 0)
            {
                // North
                return MeshDir.North;
            }
            else if (DY < 0)
            {
                // South
                return MeshDir.South;
            }
            else
            {
                // Chip
                return MeshDir.Local;
            }
        }
    }

    public class MeshUtils
    {
        public delegate MeshRouter ConstructRouter(int x, int y);

        public static MeshRouter[,] CreateMesh(Simulator sim, int width, int height, ConstructRouter constructRouter)
        {
            MeshRouter[,] routers = new MeshRouter[width, height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    routers[x, y] = sim.AddActor(constructRouter(x, y));
                }
            }

            ConnectRouters(sim, routers);
            return routers;
        }

        public static void ConnectRouters(Simulator sim, MeshRouter[,] routers)
        {
            int width = routers.GetLength(0);
            int height = routers.GetLength(1);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // wire up west side if possible
                    if (x > 0)
                    {
                        sim.AddChannel(routers[x, y].outWest, routers[x - 1, y].inEast);
                    }

                    // wire up east side if possible
                    if (x < width - 1)
                    {
                        sim.AddChannel(routers[x, y].outEast, routers[x + 1, y].inWest);
                    }

                    // wire up south side if possible
                    if (y > 0)
                    {
                        sim.AddChannel(routers[x, y].outSouth, routers[x, y - 1].inNorth);
                    }

                    // wire up north side if possible
                    if (y < height - 1)
                    {
                        sim.AddChannel(routers[x, y].outNorth, routers[x, y + 1].inSouth);
                    }
                }
            }
        }
    }
}