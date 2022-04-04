using System;
using System.Collections.Generic;

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
    private CondVar<int[]> condVar;

    // Stats
    private double dynamicEnergy = 0.0;

    public XYRouter(int x, int y, MeshSpec spec)
    {
        this.x = x;
        this.y = y;
        this.spec = spec;
        this.Name = $"router({x},{y})";
    }

    public double Energy(long now)
    {
        double staticEnergy = spec.StaticEnergy * (now / spec.Frequency);
        double energy = dynamicEnergy + staticEnergy;
        return energy;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        inBuffers = new Buffer<Packet>[5];
        outBuffers = new Buffer<Packet>[5];
        condVar = new(env, new int[10]);

        for (int dir = 0; dir < 5; dir++)
        {
            var inPort = GetInputPort(dir);
            if (inPort.IsBound)
            {
                inBuffers[dir] = new Buffer<Packet>(env, spec.InputSize);
                long transferDelay = dir == MeshDir.Local ? 0 : spec.TransferDelay;
                env.Process(InLink(env, dir, transferDelay));
            }

            var outPort = GetOutputPort(dir);
            if (outPort.IsBound)
            {
                outBuffers[dir] = new Buffer<Packet>(env, spec.OutputSize);
                env.Process(OutLink(env, dir));
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

    private static bool AnyPortEvent(int[] events)
    {
        return events.Any((v) => v > 0);
    }

    private IEnumerable<Event> Switch(Simulator env)
    {
        int lastDir = 0;

        while (true)
        {
            // Wait until one of the input ports is filled or output port is free
            foreach (var ev in condVar.BlockUntil(AnyPortEvent))
                yield return ev;

            // Find the offending port
            int i = Array.FindIndex(condVar.Value, (v) => v > 0);

            // Update dir
            condVar.Value[i]--;
            condVar.Update();

            int from, to;
            bool routeFound;
            if (i < 5)
            {
                // Is input event
                (routeFound, from, to) = OnInputEvent(env, ref lastDir, i);
            }
            else
            {
                // Is output event
                (routeFound, from, to) = OnOutputEvent(env, ref lastDir, i - 5);
            }

            if (routeFound)
            {
                yield return env.Delay(spec.SwitchDelay);
                outBuffers[to].Push(inBuffers[from].Pop());
            }
        }
    }

    private (bool, int, int) OnInputEvent(Simulator env, ref int lastDir, int dir)
    {
        var inBuffer = inBuffers[dir];
        var packet = inBuffer.Peek();
        int outDir = DetermineOutput(packet);
        var outBuffer = outBuffers[outDir];
        if (!outBuffer.IsFull)
        {
            lastDir = dir;
            OnTransfer?.Invoke(env.Now, dir, outDir);
            return (true, dir, outDir);
        }
        else
        {
            return (false, -1, -1);
        }
    }

    private (bool, int, int) OnOutputEvent(Simulator env, ref int lastDir, int freeDir)
    {
        for (int i = 0; i < 5; i++)
        {
            int inDir = (lastDir + 1 + i) % 5;
            var inBuffer = inBuffers[inDir];
            if (inBuffer == null || inBuffer.Count == 0)
                continue;
            var packet = inBuffer.Peek();
            int outDir = DetermineOutput(packet);
            if (outDir == freeDir)
            {
                lastDir = inDir;
                OnTransfer?.Invoke(env.Now, inDir, outDir);
                return (true, inDir, outDir);
            }
        }
        return (false, -1, -1);
    }

    private IEnumerable<Event> OutLink(Simulator env, int dir)
    {
        var outPort = GetOutputPort(dir);
        var buffer = outBuffers[dir];

        while (true)
        {
            yield return buffer.RequestRead();
            var flit = buffer.Read();
            yield return env.Send(outPort, flit);
            buffer.ReleaseRead();

            condVar.Value[dir + 5]++;
            condVar.Update();
        }
    }

    private IEnumerable<Event> InLink(Simulator env, int dir, long transferDelay)
    {
        var inPort = GetInputPort(dir);
        var buffer = inBuffers[dir];

        while (true)
        {
            yield return buffer.RequestWrite();
            // This symbolises the amount of time for the transfer to take place
            var rcv = env.Receive(inPort, transferTime: transferDelay);
            yield return rcv;
            var packet = (Packet)rcv.Message;
            packet.NrHops++;
            dynamicEnergy += spec.TransferEnergy;
            buffer.Write(packet);
            buffer.ReleaseWrite();

            condVar.Value[dir]++;
            condVar.Update();
        }
    }

    private int DetermineOutput(Packet packet)
    {
        var destCoord = (MeshCoord) packet.Dest;
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
}