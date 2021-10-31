namespace SpikingDSE
{
    public class Port
    {
        public int ChannelHandle;
        public bool IsBound;
        public string Name;

        public override string ToString()
        {
            return Name;
        }
    }

    public class InPort : Port
    {

    }

    public class OutPort : Port
    {

    }
}