using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

namespace SpikingDSE
{
    [DebuggerStepThrough]
    class SimThread : IComparable<SimThread>
    {
        public Process actor;
        public IEnumerator<Command> runnable;
        public int time;
        public object message;

        public int CompareTo([AllowNull] SimThread other)
        {
            return time.CompareTo(other.time);
        }
    }

    class Channel
    {
        public object Message;
        public SimThread Sender;
        public SimThread Receiver;
    }

    [DebuggerStepThrough]
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

        public void AddProcess(Process process)
        {
            processes.Add(process);
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
            foreach (var actor in processes)
            {
                actor.Init(env);
                running.Enqueue(new SimThread
                {
                    actor = actor,
                    runnable = actor.Run(),
                    time = 0
                });
            }
        }

        public int RunUntil(int stopTime)
        {
            int nrCommands = 0;
            while (running.Count > 0)
            {
                nrCommands++;
                var thread = running.Dequeue();
                if (thread.time > stopTime)
                    break;

                env.Now = thread.time;
                env.Received = thread.message;
                bool stillRunning = thread.runnable.MoveNext();
                if (!stillRunning)
                {
                    continue;
                }

                var cmd = thread.runnable.Current;
                if (cmd is SleepCmd)
                {
                    var sleep = cmd as SleepCmd;
                    thread.time += sleep.Time;
                    running.Enqueue(thread);
                }
                else if (cmd is SendCmd)
                {
                    var send = cmd as SendCmd;
                    var channel = channelReg[send.Port.Handle - 1];
                    channel.Sender = thread;
                    channel.Message = send.Message;
                    PollTransmitMessage(channel);
                }
                else if (cmd is ReceiveCmd)
                {
                    var recv = cmd as ReceiveCmd;
                    var channel = channelReg[recv.Port.Handle - 1];
                    channel.Receiver = thread;
                    PollTransmitMessage(channel);
                }
            }

            return nrCommands;
        }

        private void PollTransmitMessage(Channel channel)
        {
            if (channel.Sender != null && channel.Receiver != null)
            {
                channel.Sender.time = env.Now;
                channel.Receiver.time = env.Now;
                channel.Receiver.message = channel.Message;
                running.Enqueue(channel.Sender);
                running.Enqueue(channel.Receiver);
                channel.Sender = null;
                channel.Receiver = null;
                channel.Message = null;
            }
        }
    }

    public class Simulation
    {
        public Simulation()
        {

        }

        public void Run()
        {
            var scheduler = new Scheduler();

            var io = new IO(1);
            scheduler.AddProcess(io);
            var core = new Core();
            scheduler.AddProcess(core);

            scheduler.AddChannel(ref io.spikesOut, ref core.spikesIn);

            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(1_000_000);
            stopwatch.Stop();
            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands:n}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds:n} cmd/s");
            Console.WriteLine($"Time per cmd: {Measurements.FormatSI(stopwatch.Elapsed.TotalSeconds / nrCommands, "s")}");
        }
    }

    [DebuggerStepThrough]
    public class Environment
    {
        public SleepCmd Delay(int time)
        {
            return new SleepCmd { Time = time };
        }

        public SendCmd Send(Port port, object message)
        {
            return new SendCmd { Port = port, Message = message };
        }

        public ReceiveCmd Receive(Port port)
        {
            return new ReceiveCmd { Port = port };
        }

        public object Received
        {
            get; set;
        }

        public int Now { get; set; }
    }

    public class Port
    {
        public int Handle;
    }

    public class IO : Process
    {
        public Port spikesOut = new Port();
        private int interval;

        public IO(int interval)
        {
            this.interval = interval;
        }

        public override IEnumerator<Command> Run()
        {
            int spike = 1;
            while (true)
            {
                yield return env.Delay(1);
                yield return env.Send(spikesOut, spike++);
            }
        }
    }

    public class Core : Process
    {
        public Port spikesIn = new Port { };
        private List<int> buffer = new List<int>();

        public override IEnumerator<Command> Run()
        {
            while (buffer.Count < 10)
            {
                yield return env.Receive(spikesIn);
                var spike = (int)env.Received;
                // buffer.Add(spike);
                // Console.WriteLine($"[{env.Now}]: {spike}");
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

        public abstract IEnumerator<Command> Run();
    }

    public class Command
    {

    }

    public class SleepCmd : Command
    {
        public int Time;
    }

    public class SendCmd : Command
    {
        public Port Port;
        public object Message;
    }

    public class ReceiveCmd : Command
    {
        public Port Port;
    }

}