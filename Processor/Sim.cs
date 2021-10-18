using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

namespace SpikingDSE
{
    class SimThread : IComparable<SimThread>
    {
        public Process actor;
        public IEnumerator<Command> runnable;
        public int time;

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

    public class Scheduler
    {
        private PriorityQueue<SimThread> running = new PriorityQueue<SimThread>();
        private Dictionary<string, int> channelByName = new Dictionary<string, int>();
        private List<Channel> channelReg = new List<Channel>();
        private List<Process> actors;
        private Environment env;

        public Scheduler(List<Process> actors)
        {
            this.actors = actors;
            this.env = new Environment();
        }

        public void Init()
        {
            foreach (var actor in actors)
            {
                actor.Init(env);
                actor.RegisterPorts(RegisterPort);
                running.Enqueue(new SimThread
                {
                    actor = actor,
                    runnable = actor.Run(),
                    time = 0
                });
            }
        }

        private int RegisterPort(string name, Dir dir)
        {
            // TODO: Do more checking in terms of type and direction, etc
            if (channelByName.ContainsKey(name))
            {
                var channel = channelByName[name];
                return channel;
            }
            else
            {
                var channel = new Channel
                {
                    Message = null,
                    Sender = null,
                    Receiver = null
                };
                channelReg.Add(channel);
                int newId = channelReg.Count;
                channelByName[name] = newId;
                return newId;
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
                    var channel = channelReg[send.Port - 1];
                    channel.Sender = thread;
                    channel.Message = send.Message;
                    if (channel.Sender != null && channel.Receiver != null)
                    {
                        channel.Sender.time = env.Now;
                        channel.Receiver.time = env.Now;
                        running.Enqueue(channel.Sender);
                        running.Enqueue(channel.Receiver);
                        channel.Sender = null;
                        channel.Receiver = null;
                        channel.Message = null;
                    }
                }
                else if (cmd is ReceiveCmd)
                {
                    var recv = cmd as ReceiveCmd;
                    var channel = channelReg[recv.Port - 1];
                    channel.Receiver = thread;
                    if (channel.Sender != null && channel.Receiver != null)
                    {
                        channel.Sender.time = env.Now;
                        channel.Receiver.time = env.Now;
                        running.Enqueue(channel.Sender);
                        running.Enqueue(channel.Receiver);
                        channel.Sender = null;
                        channel.Receiver = null;
                        channel.Message = null;
                    }
                }
            }

            return nrCommands;
        }
    }

    public class Simulation
    {
        public Simulation()
        {

        }

        public void Run()
        {
            var io = new IO(1);
            var core = new Core();
            var actors = new List<Process>() { io, core };

            var scheduler = new Scheduler(actors);
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

    public class Environment
    {
        public SleepCmd Delay(int time)
        {
            return new SleepCmd { Time = time };
        }

        public SendCmd Send(int port, object message)
        {
            return new SendCmd { Port = port, Message = message };
        }

        public ReceiveCmd Receive(int port)
        {
            return new ReceiveCmd { Port = port };
        }

        public object Received
        {
            get; set;
        }

        public int Now { get; set; }
    }

    public class IO : Process
    {
        private int spikesOut = 1;
        private int interval;

        public IO(int interval)
        {
            this.interval = interval;
        }

        public override void RegisterPorts(PortRegister register)
        {
            spikesOut = register("spikes", Dir.Out);
        }

        public override IEnumerator<Command> Run()
        {
            while (true)
            {
                yield return env.Delay(1);
                yield return env.Send(spikesOut, 1);
            }
        }
    }

    public class Core : Process
    {
        private int spikesIn;

        public override void RegisterPorts(PortRegister register)
        {
            spikesIn = register("spikes", Dir.In);
        }

        public override IEnumerator<Command> Run()
        {
            while (true)
            {
                yield return env.Receive(spikesIn);
                var spike = env.Received;

                // Console.WriteLine($"[{env.Now}]: {spike}");
            }
        }
    }

    public enum Dir
    {
        Out,
        In
    }

    public delegate int PortRegister(string name, Dir dir);

    public abstract class Process
    {
        protected Environment env;

        public void Init(Environment env)
        {
            this.env = env;
        }

        public abstract void RegisterPorts(PortRegister register);

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
        public int Port;
        public object Message;
    }

    public class ReceiveCmd : Command
    {
        public int Port;
    }

}