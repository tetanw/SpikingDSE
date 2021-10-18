using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;

namespace SpikingDSE
{
    struct SimThread : IComparable<SimThread>
    {
        public Actor actor;
        public IEnumerator<Command> runnable;
        public int time;

        public int CompareTo([AllowNull] SimThread other)
        {
            return time.CompareTo(other.time);
        }
    }

    public class Scheduler
    {
        private PriorityQueue<SimThread> running = new PriorityQueue<SimThread>();
        private List<Actor> actors;
        private Environment env;

        public Scheduler(List<Actor> actors)
        {
            this.actors = actors;
            this.env = new Environment();
        }

        public void Init()
        {
            foreach (var actor in actors)
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

                env.Time = thread.time;
                bool stillRunning = thread.runnable.MoveNext();
                if (thread.runnable.Current is SleepCmd)
                {
                    var sleepCmd = thread.runnable.Current as SleepCmd;
                    thread.time += sleepCmd.TimeMs;
                }
                if (stillRunning)
                {
                    running.Enqueue(thread);
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
            var alice = new Person(1, "Alice");
            var bob = new Person(2, "Bob");
            var actors = new List<Actor>() { alice, bob };

            var scheduler = new Scheduler(actors);
            scheduler.Init();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int nrCommands = scheduler.RunUntil(1_000_000);
            stopwatch.Stop();
            Console.WriteLine($"Running time was: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"Commands handled: {nrCommands}");
            Console.WriteLine($"Performance was about: {nrCommands / stopwatch.Elapsed.TotalSeconds} cmd/s");
        }


    }

    public class Environment
    {
        public Command Sleep(int timeMs)
        {
            return new SleepCmd { TimeMs = timeMs };
        }

        // public Command WaitFor()
        // {

        // }

        // public Command Send(int target, object message)
        // {

        // }

        public int Time { get; set; }
    }

    public class Person : Actor
    {
        private int interval;
        private string name;

        public Person(int interval, string name)
        {
            this.interval = interval;
            this.name = name;
        }

        public override IEnumerator<Command> Run()
        {
            for (; ; )
            {
                yield return env.Sleep(interval);
            }
        }
    }

    public abstract class Actor
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
        public int TimeMs;
    }

    public class WaitFor : Command
    {

    }

    public class Send : Command
    {

    }

}