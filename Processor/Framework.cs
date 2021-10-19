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
        public object message;

        public int CompareTo([AllowNull] SimThread other)
        {
            return Time.CompareTo(other.Time);
        }
    }

    class Channel
    {
        public object Message;
        public long Time;
        public SimThread Sender;
        public SimThread Receiver;
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

        public void AddChannel(ref Port a, ref Port b)
        {
            if (a.Handle != 0 || b.Handle != 0)
            {
                throw new Exception("Port already bound");
            }

            var channel = new Channel
            {
                Message = null,
                Sender = null,
                Receiver = null
            };
            channelReg.Add(channel);
            int newId = channelReg.Count;
            a.Handle = newId;
            b.Handle = newId;
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
                env.Received = thread.message;
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
                    channel.Message = send.Message;
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
                channel.Sender.Time = channel.Time;
                channel.Receiver.Time = channel.Time;
                channel.Receiver.message = channel.Message;
                running.Enqueue(channel.Sender);
                running.Enqueue(channel.Receiver);
                channel.Sender = null;
                channel.Receiver = null;
                channel.Message = null;
            }
        }
    }

    public enum Dir
    {
        Out,
        In
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
        public Port Port;
        public object Message;
        public long Time;
    }

    public class ReceiveCmd : Command
    {
        public Port Port;
    }

    public class Environment
    {
        public Command Delay(int time)
        {
            return new SleepCmd { Time = time };
        }

        public Command SleepUntil(long newTime)
        {
            return new SleepCmd { Time = newTime - Now };
        }

        public Command Send(Port port, object message)
        {
            return new SendCmd { Port = port, Message = message, Time = Now };
        }

        public Command SendAt(Port port, object message, long time)
        {
            return new SendCmd { Port = port, Message = message, Time = time };
        }

        public Command Receive(Port port)
        {
            return new ReceiveCmd { Port = port };
        }

        public bool Ready(Port port)
        {
            // TODO: Implement
            return true;
        }

        public object Received
        {
            get; set;
        }

        public long Now { get; set; }
    }

    public class Port
    {
        public int Handle;
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