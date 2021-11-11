using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SpikingDSE
{
    public class FirstFitMapper
    {
        private SNN snn;
        private List<Core> cores;

        public Action<Core, Layer> OnMappingFound;

        public FirstFitMapper(SNN snn, IEnumerable<Core> cores)
        {
            this.snn = snn;
            this.cores = cores.ToList();
        }

        public void Run()
        {
            foreach (var layer in snn.GetAllLayers())
            {
                bool coreFound = false;
                foreach (var core in cores)
                {
                    if (core.AcceptsLayer(layer))
                    {
                        core.AddLayer(layer);
                        OnMappingFound?.Invoke(core, layer);
                        coreFound = true;
                        break;
                    }
                }

                if (!coreFound)
                {
                    throw new Exception("Could not find core for layer!");
                }
            }
        }
    }

    public interface Core
    {
        public bool AcceptsLayer(Layer layer);
        public void AddLayer(Layer layer);
        public InPort GetIn();
        public OutPort GetOut();
        public object GetLocation();
    }
}