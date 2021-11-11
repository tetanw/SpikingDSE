using System;
using System.Collections.Generic;

namespace SpikingDSE;

public sealed class XYRouter2 : MeshRouter
{
    private int inputBufferSize;
    private int outputBufferSize;

    public XYRouter2(int x, int y, string name = "", int inputBufferSize = 1, int outputBufferSize = 1)
    {
        this.x = x;
        this.y = y;
        this.Name = name;
        this.inputBufferSize = inputBufferSize;
        this.outputBufferSize = outputBufferSize;
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
                inBuffers[dir] = new FIFO<MeshFlit>(env, inputBufferSize);
                env.Process(InLink(env, inPort, inBuffers[dir], signal));
            }

            var outPort = GetOutputPort(dir);
            if (outPort.IsBound)
            {
                outBuffers[dir] = new FIFO<MeshFlit>(env, outputBufferSize);
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