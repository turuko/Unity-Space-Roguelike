using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungeon_Generation.QuadTree
{
    public class TreeNode<T>
    {
        public Rect BoundingBox { get; private set; }
        public List<T> Data { get; private set; }
        public TreeNode<T>[] Children { get; set; }

        public TreeNode(Rect boundingBox)
        {
            BoundingBox = boundingBox;
            Data = new List<T>();
            Children = null;
        }
    }
    
    public class QuadTree<T>
    {
        private TreeNode<T> root;
        private int maxCapacity;

        public QuadTree(Rect boundingBox, int maxCapacity)
        {
            root = new TreeNode<T>(boundingBox);
            this.maxCapacity = maxCapacity;
        }

        public void Insert(T item, Vector2 position)
        {
            if (root.BoundingBox.Contains(position))
            {
                InsertRecursive(root, item, position);
            }
        }

        private void InsertRecursive(TreeNode<T> node, T item, Vector2 position)
        {
            if (node.Data.Count < maxCapacity && node.Children == null)
            {
                node.Data.Add(item);
            }
            else
            {
                if (node.Children == null)
                {
                    SubdivideNode(node);
                }

                foreach (var child in node.Children)
                {
                    if (child.BoundingBox.Contains(position))
                    {
                        InsertRecursive(child, item, position);
                        break;
                    }
                }
            }
        }

        private void SubdivideNode(TreeNode<T> node)
        {
            float subWidth = node.BoundingBox.width / 2f;
            float subHeight = node.BoundingBox.height / 2f;
            float x = node.BoundingBox.x;
            float y = node.BoundingBox.y;
            
            node.Children = new TreeNode<T>[4];
            node.Children[0] = new TreeNode<T>(new Rect(x, y, subWidth, subHeight));
            node.Children[1] = new TreeNode<T>(new Rect(x + subWidth, y, subWidth, subHeight));
            node.Children[2] = new TreeNode<T>(new Rect(x, y + subHeight, subWidth, subHeight));
            node.Children[3] = new TreeNode<T>(new Rect(x + subWidth, y + subHeight, subWidth, subHeight));
            
            foreach (var item in node.Data)
            {
                Vector2 itemPosition = GetPositionFromItem(item); // Implement this method based on your item type
                foreach (var childNode in node.Children)
                {
                    if (childNode.BoundingBox.Contains(itemPosition))
                    {
                        childNode.Data.Add(item);
                        break;
                    }
                }
            }

            node.Data.Clear();
        }
        
        private Vector2 GetPositionFromItem(T item)
        {
            // Return the position (Vector2) of the item in your dungeon generation
            // For example, if your item is a Cell with position information, return the position of the cell.
            // Modify this method according to your specific implementation.
            if (item is Cell cell)
            {
                return cell.center;
            }

            throw new ArgumentException("Item is not of type Cell");
        }
        
        public List<T> QueryRange(Rect range)
        {
            List<T> result = new List<T>();
            QueryRangeRecursive(root, range, result);
            return result;
        }

        private void QueryRangeRecursive(TreeNode<T> node, Rect range, List<T> result)
        {
            if (node.BoundingBox.Overlaps(range))
            {
                foreach (var item in node.Data)
                {
                    Vector2 itemPosition = GetPositionFromItem(item); // Implement this method based on your item type
                    if (range.Contains(itemPosition))
                    {
                        result.Add(item);
                    }
                }

                if (node.Children != null)
                {
                    foreach (var childNode in node.Children)
                    {
                        QueryRangeRecursive(childNode, range, result);
                    }
                }
            }
        }
        
        public List<T> GetAllData()
        {
            List<T> allData = new List<T>();
            GetAllDataRecursive(root, allData);
            return allData;
        }

        private void GetAllDataRecursive(TreeNode<T> node, List<T> dataList)
        {
            if (node == null)
                return;

            // Add data from this node
            dataList.AddRange(node.Data);

            // Recursively get data from child nodes
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    GetAllDataRecursive(child, dataList);
                }
            }
        }
    }
}