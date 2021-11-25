using System;
using System.Collections.Generic;

namespace SpikingDSE;

public sealed class XYRouter3 : MeshRouter
{
    private int inputBufferSize;
    private int outputBufferSize;
    private int switchDelay;

    public XYRouter3(int x, int y, string name = "", int inputBufferSize = 1, int outputBufferSize = 1, int switchDelay = 1)
    {
        this.x = x;
        this.y = y;
        this.Name = name;
        this.inputBufferSize = inputBufferSize;
        this.outputBufferSize = outputBufferSize;
        this.switchDelay = switchDelay;
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
            int dir = (int)((env.Now % switchDelay) % 5);
            // long syncTime = env.Now;
            int nrDirsChecked = 0;
            while (true)
            {
                inBuffer = inBuffers[dir];
                if (inBuffer != null && inBuffer.Count > 0)
                {
                    var outDir = DetermineOutput(inBuffer.Peek());
                    outBuffer = outBuffers[outDir];
                    if (outBuffer != null && !outBuffer.IsFull)
                    {
                        // Sync up with the right time we do not delay each cycle
                        // for performance reasons
                        // yield return env.SleepUntil(syncTime);

                        // We do not have to wait for buffer space because we know we have it
                        var item = inBuffer.Pop();
                        outBuffer.Push(item);

                        nrDirsChecked = 0;
                    }
                }

                // Standard things to do in each cycle
                dir = (dir + 1) % 5;
                yield return env.Delay(switchDelay);
                nrDirsChecked++;

                // If we checked all 5 directions and found nothing then we can 
                // go to sleep until a next notification
                if (nrDirsChecked == 5)
                {
                    break;
                }
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