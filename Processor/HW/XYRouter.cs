using System;
using System.Collections.Generic;
using System.Linq;

namespace SpikingDSE;

public sealed class XYRouter : MeshRouter
{
    public delegate void Transfer(long now, int from, int to);
    public delegate void Blocking(long now);

    public Transfer OnTransfer;
    public Blocking OnBlocking;

    private readonly MeshSpec spec;
    private Buffer<Packet>[] inBuffers;
    private Buffer<Packet>[] outBuffers;
    private bool eventsReady = false;
    private Condition anEventReady;

    // Stats
    private double dynamicEnergy = 0.0;
    public long[] inBusy = new long[5];
    public long[] outBusy = new long[5];
    public long switchBusy = 0;
    public long nrHops = 0;
    private readonly Dictionary<string, int> Operations = new();

    public XYRouter(int x, int y, MeshSpec spec)
    {
        this.x = x;
        this.y = y;
        this.spec = spec;
        Name = $"router({x}_{y})";
    }

    public override double Energy(long now)
    {
        return 0.0;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        inBuffers = new Buffer<Packet>[5];
        outBuffers = new Buffer<Packet>[5];
        anEventReady = new(env, () => eventsReady);

        for (int dir = 0; dir < 5; dir++)
        {
            var inPort = GetInputPort(dir);
            if (inPort.IsBound)
            {
                inBuffers[dir] = new Buffer<Packet>(env, spec.InputSize);
                int transferDelay = dir == MeshDir.Local ? spec.InputDelay : spec.TransferDelay;
                env.Process(InLink(env, dir, transferDelay));
            }

            var outPort = GetOutputPort(dir);
            if (outPort.IsBound)
            {
                outBuffers[dir] = new Buffer<Packet>(env, spec.OutputSize);
                int transferDelay = dir == MeshDir.Local ? spec.OutputDelay : 0;
                env.Process(OutLink(env, dir, transferDelay));
            }
        }

        env.Process(Switch(env));

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

    private IEnumerable<Event> Switch(Simulator env)
    {
        int lastDir = 0;

        while (true)
        {
            // Wait until one of the input ports is filled or output port is free
            foreach (var ev in anEventReady.Wait())
                yield return ev;
            eventsReady = false;

            long before = env.Now;
            bool noRouteFound = false;
            while (!noRouteFound)
            {
                for (int j = 0; j < 5; j++)
                {
                    int inDir = (lastDir + 1 + j) % 5;
                    var inBuffer = inBuffers[inDir];
                    if (inBuffer == null || inBuffer.Count == 0)
                        continue;
                    var packet = inBuffer.Peek();
                    int outDir = DetermineOutput(packet);
                    if (!outBuffers[outDir].IsFull)
                    {
                        lastDir = inDir;
                        yield return env.Delay(spec.SwitchDelay);
                        OnTransfer?.Invoke(env.Now, inDir, outDir);
                        outBuffers[outDir].Push(inBuffers[inDir].Pop());
                        Operations.AddCount("Sub", 2);
                        Operations.AddCount("Cmp", 2);
                        break;
                    }
                }
                noRouteFound = true;
            }

            switchBusy += env.Now - before;
        }
    }

    private IEnumerable<Event> OutLink(Simulator env, int dir, int transferDelay)
    {
        var outPort = GetOutputPort(dir);
        var buffer = outBuffers[dir];

        while (true)
        {
            yield return buffer.RequestRead();
            var flit = buffer.Read();
            long before = env.Now;
            yield return env.Send(outPort, flit, transferTime: transferDelay);
            outBusy[dir] += env.Now - before;
            buffer.ReleaseRead();

            if (dir != MeshDir.Local)
                nrHops++;

            eventsReady = true;
            anEventReady.Update();
        }
    }

    private IEnumerable<Event> InLink(Simulator env, int dir, int transferDelay)
    {
        var inPort = GetInputPort(dir);
        var buffer = inBuffers[dir];

        while (true)
        {
            yield return buffer.RequestWrite();
            // This symbolises the amount of time for the transfer to take place
            var rcv = env.Receive(inPort, transferTime: transferDelay);
            yield return rcv;
            inBusy[dir] += env.Now - rcv.StartedReceiving;
            var packet = (Packet)rcv.Message;
            packet.NrHops++;
            dynamicEnergy += spec.TransferEnergy;
            buffer.Write(packet);
            buffer.ReleaseWrite();

            eventsReady = true;
            anEventReady.Update();
        }
    }

    private int DetermineOutput(Packet packet)
    {
        var destCoord = (MeshCoord)packet.Dest;
        int DX = destCoord.X - x;
        int DY = destCoord.Y - y;
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

    public override double Memory()
    {
        double inputBuffers = spec.PacketSize * spec.InputSize * 5;
        double outputBuffers = spec.PacketSize * spec.OutputSize * 5;

        return inputBuffers + outputBuffers;
    }
}