using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace SpikingDSE
{
    class SimThread : IComparable<SimThread>
    {
        public Process Process;
        public IEnumerator<Command> Runnable;
        public long Time;

        public int CompareTo([AllowNull] SimThread other)
        {
            return Time.CompareTo(other.Time);
        }
    }

    class Channel
    {
        public long Time;
        public SimThread Sender;
        public OutPort OutPort;
        public SimThread Receiver;
        public InPort InPort;
    }

    public class Scheduler
    {
        private PriorityQueue<SimThread> running = new PriorityQueue<SimThread>();
        private List<Channel> channelReg = new List<Channel>();
        private List<Process> processes = new List<Process>();
        private Environment env;

        public Scheduler()
        {
            this.env = new Environment();
        }

        public T AddProcess<T>(T process) where T : Process
        {
            processes.Add(process);
            return process;
        }

        public void AddChannel(ref InPort inPort, ref OutPort outPort)
        {
            if (inPort != null|| outPort != null)
            {
                throw new Exception("Port already bound");
            }

            inPort = new InPort();
            outPort = new OutPort();
            var channel = new Channel
            {
                Sender = null,
                Receiver = null,
                OutPort = outPort,
                InPort = inPort
                
            };
            channelReg.Add(channel);
            int newId = channelReg.Count;
            inPort.Handle = newId;
            outPort.Handle = newId;
        }

        public void Init()
        {
            foreach (var process in processes)
            {
                process.Init(env);
                running.Enqueue(new SimThread
                {
                    Process = process,
                    Runnable = process.Run().GetEnumerator(),
                    Time = 0
                });
            }
        }

        public int RunUntil(int stopTime, int stopCmds)
        {
            int nrCommands = 0;
            while (running.Count > 0 && nrCommands < stopCmds)
            {
                nrCommands++;
                var thread = running.Dequeue();
                if (thread.Time > stopTime)
                    break;

                env.Now = thread.Time;
                var runnable = thread.Runnable;
                bool stillRunning = runnable.MoveNext();
                if (!stillRunning)
                {
                    continue;
                }

                var cmd = runnable.Current;
                if (cmd is SleepCmd)
                {
                    var sleep = cmd as SleepCmd;
                    thread.Time += sleep.Time;
                    running.Enqueue(thread);
                }
                else if (cmd is SendCmd)
                {
                    var send = cmd as SendCmd;
                    var channel = channelReg[send.Port.Handle - 1];
                    channel.Sender = thread;
                    channel.OutPort.Message = send.Message;
                    channel.InPort.Ready = true;
                    channel.Time = send.Time;
                    PollTransmitMessage(channel);
                }
                else if (cmd is ReceiveCmd)
                {
                    var recv = cmd as ReceiveCmd;
                    var channel = channelReg[recv.Port.Handle - 1];
                    channel.Receiver = thread;
                    PollTransmitMessage(channel);
                }
                else
                {
                    throw new Exception("Unknown command: " + cmd);
                }
            }

            return nrCommands;
        }

        private void PollTransmitMessage(Channel channel)
        {
            if (channel.Sender != null && channel.Receiver != null)
            {
                channel.InPort.Ready = false;
                channel.InPort.Message = channel.OutPort.Message; // FIXME: Maybe clean after each run?
                channel.OutPort.Message = null;

                // Handle threads
                channel.Sender.Time = channel.Time;
                channel.Receiver.Time = channel.Time;
                running.Enqueue(channel.Sender);
                running.Enqueue(channel.Receiver);
                channel.Sender = null;
                channel.Receiver = null;
            }
        }
    }

    public abstract class Process
    {
        protected Environment env;

        public void Init(Environment env)
        {
            this.env = env;
        }

        public abstract IEnumerable<Command> Run();
        public void Finish() {} 
    }

    public class Command
    {

    }

    public class SleepCmd : Command
    {
        public long Time;
    }

    public class SendCmd : Command
    {
        public OutPort Port;
        public object Message;
        public long Time;
    }

    public class ReceiveCmd : Command
    {
        public InPort Port;
    }

    public class Environment
    {
        public Command Delay(long time)
        {
            return new SleepCmd { Time = time };
        }

        public Command SleepUntil(long newTime)
        {
            return new SleepCmd { Time = newTime - Now };
        }

        public Command Send(OutPort port, object message)
        {
            return new SendCmd { Port = port, Message = message, Time = Now };
        }

        public Command SendAt(OutPort port, object message, long time)
        {
            return new SendCmd { Port = port, Message = message, Time = time };
        }

        public Command Receive(InPort port)
        {
            return new ReceiveCmd { Port = port };
        }

        public long Now { get; set; }
    }

    public class InPort
    {
        public int Handle;
        public bool Ready;
        public object Message;
    }

    public class OutPort
    {
        public int Handle;
        public object Message;
    }

    public class Weights
    {
        public static double[,] ReadFromCSV(string path)
        {
            double[,] weights = null;
            int currentLine = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                double[] numbers = line.Split(",").Select(t => double.Parse(t)).ToArray();
                if (weights == null)
                {
                    weights = new double[numbers.Length, numbers.Length];
                }

                for (int i = 0; i < numbers.Length; i++)
                {
                    weights[currentLine, i] = numbers[i]; 
                }
                currentLine++;
            }

            return weights;
        }
    }
}