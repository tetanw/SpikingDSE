using System;
using System.Collections.Generic;

namespace SpikingDSE;

public sealed class XYRouter : MeshRouter
{
    private int inputBufferSize;
    private int outputBufferSize;
    private int switchDelay;
    private Buffer<MeshPacket>[] inBuffers;
    private Buffer<MeshPacket>[] outBuffers;
    private CondVar<int[]> condVar;

    public XYRouter(int x, int y, string name = "", int inputBufferSize = 1, int outputBufferSize = 1, int switchDelay = 1)
    {
        this.x = x;
        this.y = y;
        this.Name = name;
        this.inputBufferSize = inputBufferSize;
        this.outputBufferSize = outputBufferSize;
        this.switchDelay = switchDelay;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        inBuffers = new Buffer<MeshPacket>[5];
        outBuffers = new Buffer<MeshPacket>[5];
        condVar = new(env, new int[10]);

        for (int dir = 0; dir < 5; dir++)
        {
            var inPort = GetInputPort(dir);
            if (inPort.IsBound)
            {
                inBuffers[dir] = new Buffer<MeshPacket>(env, inputBufferSize);
                env.Process(InLink(env, dir));
            }

            var outPort = GetOutputPort(dir);
            if (outPort.IsBound)
            {
                outBuffers[dir] = new Buffer<MeshPacket>(env, outputBufferSize);
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

            if (i < 5)
            {
                // Is input event
                OnInputEvent(i);
            }
            else
            {
                // Is output event
                OnOutputEvent(ref lastDir, i - 5);
            }

            condVar.Value[i]--;
            condVar.Update();
        }
    }

    private void OnInputEvent(int dir)
    {
        var inBuffer = inBuffers[dir];
        var packet = inBuffer.Peek();
        int outDir = DetermineOutput(packet);
        var outBuffer = outBuffers[outDir];
        if (!outBuffer.IsFull)
        {
            outBuffer.Push(inBuffer.Pop());
        }
    }

    private void OnOutputEvent(ref int lastDir, int freeDir)
    {
        for (int i = 0; i < 5; i++)
        {
            int inDir = (lastDir + i) % 5;
            var inBuffer = inBuffers[inDir];
            if (inBuffer == null || inBuffer.Count == 0)
                continue;
            var packet = inBuffer.Peek();
            int outDir = DetermineOutput(packet);
            if (outDir == freeDir)
            {
                outBuffers[outDir].Push(inBuffer.Pop());
                lastDir = inDir;
                break;
            }
        }
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

    private IEnumerable<Event> InLink(Simulator env, int dir)
    {
        var inPort = GetInputPort(dir);
        var buffer = inBuffers[dir];

        while (true)
        {
            yield return buffer.RequestWrite();
            var rcv = env.Receive(inPort);
            yield return rcv;
            buffer.Write((MeshPacket)rcv.Message);
            buffer.ReleaseWrite();

            condVar.Value[dir]++;
            condVar.Update();
        }
    }

    private int DetermineOutput(MeshPacket flit)
    {
        int DX = flit.Dest.x - x;
        int DY = flit.Dest.y - y;
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