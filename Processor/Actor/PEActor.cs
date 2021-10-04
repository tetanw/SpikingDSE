using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    public class PEActor : Actor
    {
        private SpikeBuffer inputSpikes;
        private SpikeBuffer outputSpikes;
        private PEConfig config;
        private Scheduler scheduler;
        private int currentNeuronID;

        public PEActor(SpikeBuffer inputSpikes, SpikeBuffer outputSpikes, PEConfig config)
        {
            this.inputSpikes = inputSpikes;
            this.outputSpikes = outputSpikes;
            this.config = config;
            this.scheduler = new Scheduler(config.BufferSize);
        }

        private void StartComputing()
        {
            var neuronID = scheduler.PopNeuron();
            Schedule(new StartComputing()
            {
                TargetID = ThisID,
                Time = CurrentTime,
                NeuronID = neuronID,
            });
        }

        private void StartReceiving()
        {
            Schedule(new StartReceiving()
            {
                Time = CurrentTime,
                TargetID = ThisID
            });
        }

        private void StartSending()
        {
            Schedule(new StartSending()
            {
                TargetID = ThisID,
                Time = CurrentTime,
                Synapse = outputSpikes.PopNeuronSpike()
            });
        }

        private void OnIdle()
        {
            if (scheduler.isSchedulerFull)
            {
                StartComputing();
            }
            else if (!inputSpikes.IsEmpty)
            {
                StartReceiving();
            }
            else if (!scheduler.isSchedulerEmpty)
            {
                StartComputing();
            }
            else if (!outputSpikes.IsEmpty)
            {
                StartSending();
            }
            else if (!inputSpikes.IsDone)
            {
                while (inputSpikes.IsEmpty && outputSpikes.IsEmpty && !inputSpikes.IsDone && !outputSpikes.IsDone)
                {
                    inputSpikes.NextTimestep();
                    outputSpikes.NextTimestep();
                }

                if (!inputSpikes.IsEmpty)
                {
                    StartReceiving();
                }
                else if (!outputSpikes.IsEmpty)
                {
                    StartSending();
                }
            }
        }

        public override void Handle(Event objEv)
        {
            if (objEv is StartReceiving)
            {
                Schedule(new DoneReceiving()
                {
                    TargetID = ThisID,
                    Time = CurrentTime + config.Input.Latency,
                    NeuronID = inputSpikes.PopNeuronSpike(),
                });
            }
            else if (objEv is DoneReceiving)
            {
                var ev = (DoneReceiving)objEv;
                scheduler.PushNeuron(ev.NeuronID);
                OnIdle();
            }
            else if (objEv is StartComputing)
            {
                var ev = (StartComputing)objEv;
                currentNeuronID = ev.NeuronID;
                long memAccessTime = Math.Max(config.MemNeuron.Latency, config.MemSynapse.Latency);
                long duration = (config.Core.ComputeTime + memAccessTime) * config.MaxSynapses;

                Schedule(new DoneComputing()
                {
                    TargetID = ThisID,
                    Time = CurrentTime + duration
                });
            }
            else if (objEv is DoneComputing)
            {
                currentNeuronID = -1;
                OnIdle();
            }
            else if (objEv is StartSending)
            {
                var ev = (StartSending)objEv;
                Schedule(new DoneSending()
                {
                    TargetID = ThisID,
                    Time = CurrentTime + config.Output.Latency,
                    Synapse = ev.Synapse
                });
            }
            else if (objEv is DoneSending)
            {
                OnIdle();
            }
        }

        public override void Start()
        {
            OnIdle();
        }
    }
}