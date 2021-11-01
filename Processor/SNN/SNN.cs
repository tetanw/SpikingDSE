namespace SpikingDSE
{
    public class Layer
    {
        public int startID;
        public int endID;

        public Layer(int start, int end)
        {
            this.startID = start;
            this.endID = end;
        }
    }

    public class ODINLayer
    {
        public int[] pots;
        public int[,] weights;
        public bool[] spikes;


        public ODINLayer(int from, int to, string name = "")
        {
            pots = new int[to];
            weights = new int[from, to];
            this.spikes = new bool[to];
            this.Size = to;
            this.Name = name;
        }

        public int Size { get; }
        public string Name { get; }
    }
}