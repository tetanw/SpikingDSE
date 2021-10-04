using System;
using System.Collections.Generic;

namespace SpikingDSE
{

    public struct ActorEntry
    {
        public Actor Actor;
        public List<IReporter> Reporters;
    }

    public class Simulator
    {
        private SNN snn;
        private EventScheduler scheduler;
        private SimOptions opts;
        private int lastID = 0;
        private List<ActorEntry> actorReg = new List<ActorEntry>();
        private List<IReporter> reporterReg = new List<IReporter>();

        public Simulator(SimOptions opts)
        {
            this.opts = opts;

            // make snn
            snn = new SNN();
            // TODO: Broken
            // snn.AddLayer("l0", 2);
            // snn.AddLayer("l1", 1);
            // snn.AddLayer("l2", 1);
        }

        private int CreateActor()
        {
            lastID++;
            actorReg.Add(new ActorEntry { });
            return lastID;
        }

        private int BindActor(int ID, Actor actor, List<IReporter> reporters)
        {
            actor.Init(ID, scheduler);
            var newEntry = new ActorEntry
            {
                Actor = actor,
                Reporters = reporters
            };
            actorReg[ID - 1] = newEntry;
            foreach (var reporter in reporters)
            {
                if (!reporterReg.Contains(reporter))
                {
                    reporterReg.Add(reporter);
                }
            }
            return ID;
        }

        private int createCore(PEConfig config, SimReporter simReporter, string name)
        {
            var inputSource = new TensorFile("res/spike_trace_n_1_0.csv", 0);
            var outputSource = new TensorFile("res/spike_trace_n_2_0.csv", 0);
            // var inputSource = new TraceFile("res/events.trace", true);
            // var outputSource = new TraceFile("res/events.trace", false);
            var inputSpikes = new SpikeBuffer(inputSource);
            var outputSpikes = new SpikeBuffer(outputSource);

            var pe = CreateActor();
            var reporters = new List<IReporter>();
            reporters.Add(new CoreReporter(config, name));
            reporters.Add(simReporter);
            if (opts.ProfPath != null)
            {
                reporters.Add(new ProfReporter(opts.ProfPath));
            }

            BindActor(pe, new PEActor(inputSpikes, outputSpikes, config), reporters);

            return pe;
        }

        public void Simulate()
        {
            scheduler = new EventScheduler();

            // create the ids for the PE
            var simReporter = new SimReporter();
            var config = PEConfig.LoadConfig(opts.HwPath);
            var core1ID = createCore(config, simReporter, "Core 1");

            foreach (var entry in actorReg)
            {
                entry.Actor.Start();
            }
            foreach (var reporter in reporterReg)
            {
                reporter.Start();
            }
            long currentTime = -1;
            while (!scheduler.IsDone())
            {
                var newEvent = scheduler.PopEvent();
                currentTime = newEvent.Time;
                HandleEvent(newEvent);
            }
            foreach (var reporter in reporterReg)
            {
                reporter.End(currentTime);
            }
        }

        private void HandleEvent(Event ev)
        {
            if (ev.TargetID == 0)
            {
                throw new Exception("Event for actor with ID 0. Did you forget to specify a targetID?");
            }

            var entry = actorReg[ev.TargetID - 1];
            if (entry.Actor == null)
            {
                throw new Exception($"No handler for event: ${ev}");
            }
            else
            {
                entry.Actor.CurrentTime = ev.Time;
                entry.Actor.Handle(ev);
                foreach (var reporter in entry.Reporters)
                {
                    reporter.Report(ev);
                }
            }
        }
    }
}