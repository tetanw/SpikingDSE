using System;
using System.Collections.Generic;

namespace SpikingDSE
{
    struct WithLocation<T>
    {
        public WithLocation(int x, int y, T value)
        {
            this.Coord = new MeshCoord(x, y);
            this.Value = value;
        }

        public MeshCoord Coord;
        public T Value;
    }

    public class MeshMapping
    {
        private MeshRouter[,] routers;
        private Actor[,] actors;
        private Simulator sim;
        private InputLayer inputLayer;
        private List<HiddenLayer> hiddenLayers;
        private List<WithLocation<Source>> sources;
        private List<WithLocation<Sink>> sinks;
        private List<WithLocation<Core>> cores;
        private List<WithLocation<HiddenLayer>> mappedLayers;

        public MeshMapping(Simulator sim)
        {
            this.sim = sim;
        }

        public void CreateMesh(int width, int height)
        {
            routers = MeshUtils.CreateMesh(sim, width, height, (x, y) => new XYRouter2(x, y, name: $"router({x},{y})"));
            actors = new Actor[width, height];
        }

        public void AddInputLayer(InputLayer inputLayer)
        {
            this.inputLayer = inputLayer;
        }

        public void AddHiddenLayer(HiddenLayer hiddenLayer)
        {
            if (hiddenLayers == null)
                hiddenLayers = new List<HiddenLayer>();

            hiddenLayers.Add(hiddenLayer);
        }

        public void AddSource<T>(int x, int y, T source) where T : Actor, Source
        {
            if (sources == null)
                sources = new List<WithLocation<Source>>();
            sources.Add(new WithLocation<Source>(x, y, source));
            sim.AddActor(source);

            actors[x, y] = source;
            var output = source.GetOut();
            sim.AddChannel(output, routers[x, y].inLocal);
        }

        public void AddSink<T>(int x, int y, T sink) where T : Actor, Sink
        {
            if (sinks == null)
                sinks = new List<WithLocation<Sink>>();
            sinks.Add(new WithLocation<Sink>(x, y, sink));
            sim.AddActor(sink);

            actors[x, y] = sink;
            var input = sink.GetIn();
            sim.AddChannel(routers[x, y].outLocal, input);
        }

        public void AddCore<T>(int x, int y, T core) where T : Actor, Core
        {
            if (cores == null)
                cores = new List<WithLocation<Core>>();
            cores.Add(new WithLocation<Core>(x, y, core));
            sim.AddActor(core);

            actors[x, y] = core;
            var coreIn = core.GetIn();
            var coreOut = core.GetOut();
            sim.AddChannel(coreOut, routers[x, y].inLocal);
            sim.AddChannel(routers[x, y].outLocal, coreIn);
        }

        public void Compile()
        {
            if (sources.Count > 1)
                throw new Exception("Can not have more than 1 source");

            if (sinks.Count > 1)
                throw new Exception("Can not have more than 1 sink");

            // Map all source layers
            sources[0].Value.LoadLayer(inputLayer);

            // Mapp all of the hidden layers to cores
            mappedLayers = new List<WithLocation<HiddenLayer>>();
            int lastID = inputLayer.Size;
            foreach (var layer in hiddenLayers)
            {
                foreach (var core in cores)
                {
                    if (core.Value.AcceptsLayer(layer))
                    {
                        lastID = layer.SetBaseID(lastID);
                        core.Value.AddLayer(layer);
                        mappedLayers.Add(new WithLocation<HiddenLayer>(core.Coord.x, core.Coord.y, layer));
                        break;
                    }

                    throw new Exception($"Could not find core for layer {layer}");
                }
            }

            foreach (var sink in sinks)
            {
                sink.Value.LoadInTransformer(GetSpikeFromFlit);
            }
            foreach (var core in cores)
            {
                core.Value.LoadInTransformer(GetSpikeFromFlit);
            }

            var sinkLoc = sinks[0].Coord;
            foreach (var core in cores)
            {
                core.Value.LoadOutTransformer(SpikeToFlit(sinkLoc, core.Coord));
            }
            foreach (var source in sources)
            {
                source.Value.LoadOutTransformer(SpikeToFlit(sinkLoc, source.Coord));
            }

        }

        private int GetSpikeFromFlit(object obj)
        {
            var flit = (MeshFlit)obj;
            return (int)flit.Message;
        }

        private Func<int, object> SpikeToFlit(MeshCoord sinkLoc, MeshCoord srcCoord)
        {
            return (int neuron) =>
            {
                MeshCoord destCoord = sinkLoc;
                foreach (var layer in mappedLayers)
                {
                    if (layer.Value.Accepts(neuron))
                    {
                        destCoord = layer.Coord;
                        break;
                    }
                }

                return new MeshFlit
                {
                    SrcX = srcCoord.x,
                    SrcY = srcCoord.y,
                    Message = neuron,
                    DestX = destCoord.x,
                    DestY = destCoord.y
                };
            };
        }
    }

    public interface Core : Sender, Receiver
    {
        public bool AcceptsLayer(Layer layer);
        public void AddLayer(Layer layer);
    }

    public interface Source : Sender
    {
        public void LoadLayer(InputLayer inputLayer);
    }

    public interface Sink : Receiver
    {

    }

    public interface Receiver
    {
        public InPort GetIn();
        public void LoadInTransformer(Func<object, int> inTransformer);
    }

    public interface Sender
    {
        public OutPort GetOut();
        public void LoadOutTransformer(Func<int, object> outTransformer);

    }
}