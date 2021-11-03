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

        public WithLocation(MeshCoord coord, T value)
        {
            this.Coord = coord;
            this.Value = value;
        }

        public readonly MeshCoord Coord;
        public readonly T Value;

        public void Deconstruct(out MeshCoord coord, out T value)
        {
            coord = this.Coord;
            value = this.Value;
        }

        public override string ToString()
        {
            return $"Coord: {Coord}, Value: {Value}";
        }
    }

    public struct NeuronRange
    {
        public readonly int Start;
        public readonly int End;

        public NeuronRange(int start, int end)
        {
            this.Start = start;
            this.End = end;
        }

        public bool Contains(int number)
        {
            return number >= Start && number < End;
        }

        public override string ToString()
        {
            return $"[{Start}, {End})";
        }
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
        private List<WithLocation<NeuronRange>> inputRanges;

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
            var (sourceLoc, source) = sources[0];
            source.LoadLayer(inputLayer);

            // Mapp all of the hidden layers to cores
            int lastID = inputLayer.Size;
            var ranges = new List<WithLocation<NeuronRange>>();
            ranges.Add(new WithLocation<NeuronRange>(sourceLoc, new NeuronRange(0, lastID)));
            foreach (var layer in hiddenLayers)
            {
                bool layerFound = false;
                foreach (var (coreLoc, core) in cores)
                {
                    if (core.AcceptsLayer(layer))
                    {
                        var neuronRange = new NeuronRange(lastID, lastID + layer.Size);
                        layer.SetNeuronRange(neuronRange);
                        ranges.Add(new WithLocation<NeuronRange>(coreLoc, neuronRange));
                        lastID += layer.Size;
                        core.AddLayer(layer);
                        layerFound = true;
                        break;
                    }
                }

                if (!layerFound)
                {
                    throw new Exception($"Could not find core for layer {layer}");
                }
            }

            inputRanges = new List<WithLocation<NeuronRange>>();
            for (int i = 1; i < ranges.Count; i++)
            {
                var layer = hiddenLayers[i - 1];
                var prevRange = ranges[i - 1].Value;
                var curLoc = ranges[i].Coord;
                layer.SetInputRange(prevRange);
                inputRanges.Add(new WithLocation<NeuronRange>(curLoc, prevRange));
            }

            foreach (var (_, sink) in sinks)
            {
                sink.LoadInTransformer(GetSpikeFromFlit);
            }

            foreach (var (_, core) in cores)
            {
                core.LoadInTransformer(GetSpikeFromFlit);
            }
            var sinkLoc = sinks[0].Coord;
            foreach (var (coord, core) in cores)
            {
                core.LoadOutTransformer(SpikeToFlit(sinkLoc, coord));
            }
            source.LoadOutTransformer(SpikeToFlit(sinkLoc, sourceLoc));
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
                foreach (var (loc, range) in inputRanges)
                {
                    if (range.Contains(neuron))
                    {
                        destCoord = loc;
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