using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace SpikingDSE
{
    struct MeshCoord
    {
        public readonly int x;
        public readonly int y;

        public MeshCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
    }

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

    public class Mesh
    {
        private Simulator sim;
        private MeshRouter[,] routers;
        public Actor[,] actors;
        public readonly int width, height;

        public Mesh(Simulator sim, int width, int height, MeshUtils.ConstructRouter construct)
        {
            this.sim = sim;
            this.width = width;
            this.height = height;

            routers = MeshUtils.CreateMesh(sim, width, height, construct);
            actors = new Actor[width, height];
        }

        public T AddActor<T>(int x, int y, [DisallowNull] T actor) where T : Actor
        {
            sim.AddActor(actor);

            actors[x, y] = actor;

            if (actor is Sender)
            {
                var sender = actor as Sender;
                var output = sender.GetOut();
                sim.AddChannel(output, routers[x, y].inLocal);
            }

            if (actor is Receiver)
            {
                var sender = actor as Receiver;
                var input = sender.GetIn();
                sim.AddChannel(routers[x, y].outLocal, input);
            }

            return actor;
        }


    }

    public class MeshFirstFitMapper
    {
        private List<WithLocation<Source>> sources;
        private List<WithLocation<Sink>> sinks;
        private List<WithLocation<Core>> cores;
        private List<WithLocation<NeuronRange>> inputRanges;

        public MeshFirstFitMapper()
        {

        }

        private void SortActors(Mesh mesh)
        {
            sinks = new List<WithLocation<Sink>>();
            sources = new List<WithLocation<Source>>();
            cores = new List<WithLocation<Core>>();

            for (int y = 0; y < mesh.height; y++)
            {
                for (int x = 0; x < mesh.width; x++)
                {
                    var actor = mesh.actors[x, y];
                    if (actor is Sink)
                    {
                        sinks.Add(new WithLocation<Sink>(x, y, actor as Sink));
                    }
                    
                    if (actor is Source)
                    {
                        sources.Add(new WithLocation<Source>(x, y, actor as Source));
                    }

                    if (actor is Core)
                    {
                        cores.Add(new WithLocation<Core>(x, y, actor as Core));
                    }
                }
            }
        }

        public void Compile(Mesh mesh, SNN snn)
        {
            SortActors(mesh);

            if (sources.Count > 1)
                throw new Exception("Can not have more than 1 source");

            if (sinks.Count > 1)
                throw new Exception("Can not have more than 1 sink");

            // Map all source layers
            var (sourceLoc, source) = sources[0];
            source.LoadLayer(snn.inputLayer);

            // Mapp all of the hidden layers to cores
            int lastID = snn.inputLayer.Size;
            var ranges = new List<WithLocation<NeuronRange>>();
            ranges.Add(new WithLocation<NeuronRange>(sourceLoc, new NeuronRange(0, lastID)));
            foreach (var layer in snn.hiddenLayers)
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
                var layer = snn.hiddenLayers[i - 1];
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