using System;
using System.Collections.Generic;
using System.Drawing;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        // --- 空間分割四叉樹 (QuadTree) ---
        public class QuadTree
        {
            private const int MAX_OBJECTS = 10;
            private const int MAX_LEVELS = 5;

            private int _level;
            private List<ShapeBase> _objects;
            public RectangleF Bounds { get; private set; } 
            private QuadTree[] _nodes;

            public QuadTree(int level, RectangleF bounds)
            {
                _level = level;
                _objects = new List<ShapeBase>();
                Bounds = bounds;
                _nodes = new QuadTree[4];
            }

            public void Clear()
            {
                _objects.Clear();
                for (int i = 0; i < _nodes.Length; i++)
                {
                    if (_nodes[i] != null)
                    {
                        _nodes[i].Clear();
                        _nodes[i] = null;
                    }
                }
            }

            private void Split()
            {
                float subWidth = Bounds.Width / 2f;
                float subHeight = Bounds.Height / 2f;
                float x = Bounds.X;
                float y = Bounds.Y;

                _nodes[0] = new QuadTree(_level + 1, new RectangleF(x + subWidth, y, subWidth, subHeight));
                _nodes[1] = new QuadTree(_level + 1, new RectangleF(x, y, subWidth, subHeight));
                _nodes[2] = new QuadTree(_level + 1, new RectangleF(x, y + subHeight, subWidth, subHeight));
                _nodes[3] = new QuadTree(_level + 1, new RectangleF(x + subWidth, y + subHeight, subWidth, subHeight));
            }

            private int GetIndex(RectangleF pRect)
            {
                int index = -1;
                double verticalMidpoint = Bounds.X + (Bounds.Width / 2f);
                double horizontalMidpoint = Bounds.Y + (Bounds.Height / 2f);

                bool topQuadrant = (pRect.Y < horizontalMidpoint && pRect.Y + pRect.Height < horizontalMidpoint);
                bool bottomQuadrant = (pRect.Y > horizontalMidpoint);

                if (pRect.X < verticalMidpoint && pRect.X + pRect.Width < verticalMidpoint)
                {
                    if (topQuadrant) index = 1;
                    else if (bottomQuadrant) index = 2;
                }
                else if (pRect.X > verticalMidpoint)
                {
                    if (topQuadrant) index = 0;
                    else if (bottomQuadrant) index = 3;
                }
                return index;
            }

            public void Insert(ShapeBase shape)
            {
                if (_nodes[0] != null)
                {
                    int index = GetIndex(shape.Bounds);
                    if (index != -1)
                    {
                        _nodes[index].Insert(shape);
                        return;
                    }
                }

                _objects.Add(shape);

                if (_objects.Count > MAX_OBJECTS && _level < MAX_LEVELS)
                {
                    if (_nodes[0] == null) Split();

                    int i = 0;
                    while (i < _objects.Count)
                    {
                        int index = GetIndex(_objects[i].Bounds);
                        if (index != -1)
                        {
                            _nodes[index].Insert(_objects[i]);
                            _objects.RemoveAt(i);
                        }
                        else
                        {
                            i++;
                        }
                    }
                }
            }

            public List<ShapeBase> Retrieve(List<ShapeBase> returnObjects, RectangleF pRect)
            {
                int index = GetIndex(pRect);
                if (index != -1 && _nodes[0] != null)
                {
                    _nodes[index].Retrieve(returnObjects, pRect);
                }
                else if (_nodes[0] != null)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (_nodes[i].Bounds.IntersectsWith(pRect))
                        {
                            _nodes[i].Retrieve(returnObjects, pRect);
                        }
                    }
                }

                returnObjects.AddRange(_objects);
                return returnObjects;
            }
        }
    }
}
