using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    public sealed class Environment
    {
        private Scheduler sched;

        public Environment(Scheduler sched)
        {
            this.sched = sched;
        }

        public SleepEvent Delay(long time)
        {
            return new SleepEvent { Time = time };
        }

        public SleepEvent SleepUntil(long newTime)
        {
            return new SleepEvent { Time = newTime - Now };
        }

        public SendEvent Send(OutPort port, object message)
        {
            if (!port.IsBound)
            {
                throw new Exception("Port is not bound!");
            }
            return new SendEvent { Port = port, Message = message, Time = Now };
        }

        public SendEvent SendAt(OutPort port, object message, long time)
        {
            if (!port.IsBound)
            {
                throw new Exception("Port is not bound!");
            }
            return new SendEvent { Port = port, Message = message, Time = time };
        }

        public ReceiveEvent Receive(InPort port, long waitBefore = 0, bool blockSender = false)
        {
            if (!port.IsBound)
            {
                throw new Exception("Port is not bound!");
            }
            return new ReceiveEvent { Port = port, Time = Now + waitBefore, BlockSender = blockSender };
        }

        public void EndReceive(ReceiveEvent ev)
        {
            // TODO: Test whether this will work
            sched.EndReceive(ev, Now);
        }

        public SelectEvent Select(params InPort[] ports)
        {
            return new SelectEvent { Ports = ports, Time = Now };
        }

        public ProcessWaitEvent Process(IEnumerable<Event> runnable)
        {
            var process = sched.AddProcess(runnable);
            return new ProcessWaitEvent { Process = process };
        }

        public Resource CreateResource(int intial)
        {
            return sched.CreateResource(intial);
        }

        public ResReqEvent RequestResource(Resource resource, int amount)
        {
            return new ResReqEvent { Resource = resource, Amount = amount };
        }

        public void IncreaseResource(Resource resource, int amount)
        {
            sched.Increase(resource, 1);
        }

        public void DecreaseResource(Resource resource, int amount)
        {
            sched.Decrease(resource, 1);
        }

        public void Notify(Signal signal)
        {
            sched.Notify(signal);
        }

        public Signal CreateSignal()
        {
            return sched.CreateSignal();
        }

        public SignalWaitEvent Wait(Signal signal)
        {
            return new SignalWaitEvent { Signal = signal };
        }

        public Process CurrentThread { get; set; }
        public long Now { get; set; }
    }
}