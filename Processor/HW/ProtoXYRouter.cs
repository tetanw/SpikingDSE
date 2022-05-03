using System;
using System.Collections.Generic;

namespace SpikingDSE;

public sealed class ProtoXYRouter : MeshRouter
{
    private readonly int inputBufferSize;
    private readonly int outputBufferSize;

    public ProtoXYRouter(int x, int y, string name = "", int inputBufferSize = 1, int outputBufferSize = 1)
    {
        this.x = x;
        this.y = y;
        this.Name = name;
        this.inputBufferSize = inputBufferSize;
        this.outputBufferSize = outputBufferSize;
    }

    public override IEnumerable<Event> Run(Simulator env)
    {
        var inBuffers = new Buffer<Packet>[5];
        var outBuffers = new Buffer<Packet>[5];
        var signal = new Signal(env);

        for (int dir = 0; dir < 5; dir++)
        {
            var inPort = GetInputPort(dir);
            if (inPort.IsBound)
            {
                inBuffers[dir] = new Buffer<Packet>(env, inputBufferSize);
                env.Process(InLink(env, inPort, inBuffers[dir], signal));
            }

            var outPort = GetOutputPort(dir);
            if (outPort.IsBound)
            {
                outBuffers[dir] = new Buffer<Packet>(env, outputBufferSize);
                env.Process(OutLink(env, outPort, outBuffers[dir], signal));
            }
        }

        env.Process(Switch(inBuffers, outBuffers, signal));

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

    private IEnumerable<Event> Switch(Buffer<Packet>[] inBuffers, Buffer<Packet>[] outBuffers, Signal signal)
    {
        while (true)
        {
            // 1. Wait for a new packet to arrive at an input
            // or a new packet to be sent from the output
            yield return signal.Wait();

            // 2. If an input was ready then we need to just check whether 
            // that new input needs to be routed. If an output is ready we need
            // to check whether any of the inputs is ready
            Buffer<Packet> inBuffer = null;
            Buffer<Packet> outBuffer = null;
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


    private static IEnumerable<Event> OutLink(Simulator env, OutPort outPort, Buffer<Packet> buffer, Signal signal)
    {
        while (true)
        {
            yield return buffer.RequestRead();
            var flit = buffer.Read();
            yield return env.Send(outPort, flit);
            buffer.ReleaseRead();
            signal.Notify();
        }
    }

    private static IEnumerable<Event> InLink(Simulator env, InPort inPort, Buffer<Packet> buffer, Signal signal)
    {
        while (true)
        {
            yield return buffer.RequestWrite();
            var rcv = env.Receive(inPort);
            yield return rcv;
            buffer.Write((Packet)rcv.Message);
            buffer.ReleaseWrite();
            signal.Notify();
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

    public override double Energy(long now) => 0.0;

    public override double Memory() => 0.0;
}