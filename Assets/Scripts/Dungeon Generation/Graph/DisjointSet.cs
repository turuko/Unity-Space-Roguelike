using System;
using System.Collections.Generic;

namespace Dungeon_Generation.Graph
{
    public class DisjointSet<T>
    {
        private readonly Dictionary<T, T> parent = new Dictionary<T, T>();
        private readonly Dictionary<T, int> rank = new Dictionary<T, int>();

        public DisjointSet(IEnumerable<T> elements)
        {
            foreach (var element in elements)
            {
                parent[element] = element;
                rank[element] = 0;
            }
        }

        public T Find(T x)
        {
            if (!parent.ContainsKey(x))
                throw new ArgumentException("Element not found in the DisjointSet.");

            if (parent[x].Equals(x))
            {
                return x;
            }

            parent[x] = Find(parent[x]);
            return parent[x];
        }

        public void Union(T x, T y)
        {
            var rootX = Find(x);
            var rootY = Find(y);
            
            if (rootX.Equals(rootY))
                return;
            
            if (rank[rootX] < rank[rootY])
            {
                parent[rootX] = rootY;
            }
            else if (rank[rootX] > rank[rootY])
            {
                parent[rootY] = rootX;
            }
            else
            {
                parent[rootY] = rootX;
                rank[rootX]++;
            }
        }
    }
}