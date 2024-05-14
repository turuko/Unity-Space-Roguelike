using System;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using UnityEngine;

namespace Dungeon_Generation.Graph
{
    public class GraphNode<T>
    {
        public T Data { get; }
        public List<GraphEdge<T>> Neighbors { get; }

        public GraphNode(T data)
        {
            Data = data;
            Neighbors = new List<GraphEdge<T>>();
        }

        public void AddNeighbour(GraphNode<T> neighbor, float weight = 1f)
        {
            Neighbors.Add(new GraphEdge<T>(this, neighbor, weight));
            neighbor.Neighbors.Add(new GraphEdge<T>(neighbor, this, weight)); // If the graph is undirected
        }

        public void RemoveNeighbor(GraphNode<T> neighbor)
        {
            Neighbors.RemoveAll(edge => edge.To == neighbor);
            neighbor.Neighbors.RemoveAll(edge => edge.To == this); // If the graph is undirected
        }
    }

    public class GraphEdge<T>
    {
        public GraphNode<T> From { get; }
        public GraphNode<T> To { get; }
        public float Weight { get; }

        public GraphEdge(GraphNode<T> from, GraphNode<T> to, float weight)
        {
            From = from;
            To = to;
            Weight = weight;
        }

        public GraphEdge<T> Reversed()
        {
            return new GraphEdge<T>(To, From, Weight);
        }
    }
    
    public class DungeonGraph<T>
    {
        public List<GraphNode<T>> Nodes { get; }

        public DungeonGraph()
        {
            Nodes = new List<GraphNode<T>>();
        }

        public void AddNode(T position)
        {
            Nodes.Add(new GraphNode<T>(position));
        }

        public void AddEdge(GraphNode<T> nodeA, GraphNode<T> nodeB, float weight = 1f)
        {
            nodeA.AddNeighbour(nodeB, weight);
        }

        public void RemoveEdge(GraphNode<T> nodeA, GraphNode<T> nodeB)
        {
            nodeA.RemoveNeighbor(nodeB);
        }

        public List<GraphEdge<T>> MinimumSpanningTree()
        {
            var mstEdges = new List<GraphEdge<T>>();

            var allEdges = GetAllEdges().OrderBy(e => e.Weight).ToList();

            var disjointSet = new DisjointSet<GraphNode<T>>(Nodes);

            foreach (var edge in allEdges)
            {
                if (disjointSet.Find(edge.From) ! != disjointSet.Find(edge.To))
                {
                    mstEdges.Add(edge);
                    disjointSet.Union(edge.From, edge.To);
                }
            }

            return mstEdges;
        }

        public List<GraphEdge<T>> MstAndAddedEdges(float percent)
        {
            List<GraphEdge<T>> edges = new List<GraphEdge<T>>(MinimumSpanningTree());

            int additionalEdges = Mathf.RoundToInt((GetAllEdges().Count() - MinimumSpanningTree().Count) * percent);

            var shuffledEdges = GetAllEdges().Except(MinimumSpanningTree()).ToList();
            shuffledEdges.Shuffle();

            for (int i = 0; i < additionalEdges && i < shuffledEdges.Count; i++)
            {
                edges.Add(shuffledEdges[i]);
            }

            return edges;
        }

        private IEnumerable<GraphEdge<T>> GetAllEdges()
        {
            var edges = new List<GraphEdge<T>>();

            foreach (var node in Nodes)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    // Only add the edge once to the list
                    if (!edges.Contains(neighbor) && !edges.Contains(neighbor.Reversed()))
                    {
                        edges.Add(neighbor);
                    }
                }
            }

            return edges;
        }
        
        public DungeonGraph<T> CreateNewGraph(List<GraphEdge<T>> edges)
        {
            var newGraph = new DungeonGraph<T>();

            // Add all the nodes from the original graph to the new graph
            foreach (var node in Nodes)
            {
                newGraph.AddNode(node.Data);
            }

            // Add the edges to the new graph
            foreach (var edge in edges)
            {
                // Ensure that both nodes of the edge are present in the new graph
                if (!newGraph.Nodes.Contains(edge.From))
                {
                    newGraph.AddNode(edge.From.Data);
                }

                if (!newGraph.Nodes.Contains(edge.To))
                {
                    newGraph.AddNode(edge.To.Data);
                }

                // Add the edge between the nodes
                newGraph.AddEdge(newGraph.Nodes.Find(n => n.Data.Equals(edge.From.Data)),
                    newGraph.Nodes.Find(n => n.Data.Equals(edge.To.Data)),
                    edge.Weight);
            }

            return newGraph;
        }
        
        public DungeonGraph<U> SelectGraph<U>(Func<T, U> conversionFunc)
        {
            var newGraph = new DungeonGraph<U>();

            // Convert and add all the nodes to the new graph
            foreach (var node in Nodes)
            {
                U convertedData = conversionFunc(node.Data);
                newGraph.AddNode(convertedData);
            }

            // Add the edges to the new graph
            foreach (var node in Nodes)
            {
                foreach (var neighbor in node.Neighbors)
                {
                    // Find the corresponding nodes in the new graph based on the converted data
                    U fromData = conversionFunc(node.Data);
                    U toData = conversionFunc(neighbor.To.Data);
                    GraphNode<U> fromNode = newGraph.Nodes.Find(n => n.Data.Equals(fromData));
                    GraphNode<U> toNode = newGraph.Nodes.Find(n => n.Data.Equals(toData));

                    // Add the edge between the nodes
                    newGraph.AddEdge(fromNode, toNode, neighbor.Weight);
                }
            }

            return newGraph;
        }
    }
}