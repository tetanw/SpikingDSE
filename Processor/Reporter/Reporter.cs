using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    public interface IReporter
    {
        public void Report(Event objEv);
        public void Start();
        public void End(long time);
    }
}