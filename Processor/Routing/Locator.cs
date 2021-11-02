using System.Collections.Generic;
using System;

namespace SpikingDSE
{
    public struct MeshCoord
    {
        public readonly int x, y;

        public MeshCoord(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public void Deconstruct(out int x, out int y)
        {
            x = this.x;
            y = this.y;
        }

        public override string ToString()
        {
            return $"({x},{y})";
        }
    }
}