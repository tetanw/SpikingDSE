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

    public class Simulation
    {
        public Simulation()
        {

        }

        public void Run()
        {
            RunUntil(10);
        }

        public void RunUntil(int stopTime)
        {
            var alice = new Person(1, "Alice");
            var bob = new Person(2, "Bob");
            var actors = new List<Actor>() { alice, bob };

            Environment env = new Environment();
            foreach (var actor in actors)
            {
                actor.Init(env);
            }

            var threads = new PriorityQueue<SimThread>();
            foreach (var actor in actors)
            {
                threads.Enqueue(new SimThread
                {
                    actor = actor,
                    runnable = actor.Run(),
                    time = 0
                });
            }

            while (threads.Count > 0)
            {
                var thread = threads.Dequeue();
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
                    threads.Enqueue(thread);
                }
            }
        }
    }

    public class Environment
    {
        public Command Sleep(int timeMs)
        {
            return new SleepCmd { TimeMs = timeMs };
        }

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
            for (;;)
            {
                Console.WriteLine($"[{env.Time}]: {name}");
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

}