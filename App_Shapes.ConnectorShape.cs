using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using Newtonsoft.Json;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        public class ConnectorShape : ShapeBase
        {
            [Browsable(false)] public override Color FillColor { get; set; } = Color.Transparent;
            [Browsable(false)] public override float RotationAngle { get; set; } = 0f;
            [Browsable(false)] public override string Text { get; set; } = "";
            // 【修正6】: 字型預設改為標楷體
            [Browsable(false)] public override string FontName { get; set; } = "標楷體";
            [Browsable(false)] public override float FontSize { get; set; } = 12f;
            [Browsable(false)] public override Color FontColor { get; set; } = Color.Black;
            [Browsable(false)] public override bool FontBold { get; set; } = false;
            [Browsable(false)] public override bool FontItalic { get; set; } = false;
            [Browsable(false)] public override bool FontUnderline { get; set; } = false;
            [Browsable(false)] public override TextAlign TextAlignment { get; set; } = TextAlign.MiddleCenter;

            [Browsable(false)] public Guid SourceId { get; set; }
            [Browsable(false)] public Guid TargetId { get; set; }
            
            [Category("5. 連線屬性")]
            [DisplayName("起點錨點")]
            public AnchorPosition SourceAnchor { get; set; } = AnchorPosition.Auto;
            
            [Category("5. 連線屬性")]
            [DisplayName("終點錨點")]
            public AnchorPosition TargetAnchor { get; set; } = AnchorPosition.Auto;

            [Browsable(false)] public PointF StartPt { get; set; }
            [Browsable(false)] public PointF EndPt { get; set; }
            
            private bool _hasArrow;
            [Category("5. 連線屬性")]
            [DisplayName("顯示箭頭")]
            public bool HasArrow 
            { 
                get => _hasArrow; 
                set { if (_hasArrow != value) { _hasArrow = value; InvalidatePen(); } } 
            }
            
            [Category("5. 連線屬性")]
            [DisplayName("直角折線")]
            public bool IsOrthogonal { get; set; }

            [Category("5. 連線屬性")]
            [DisplayName("開啟交錯跳線")]
            [Description("當連線與其他連線交錯時，是否自動繪製跳線半圓弧。")]
            public bool EnableLineJumps { get; set; } = true;

            [JsonIgnore] 
            [Browsable(false)] 
            public PointF[] CachedPath => _cachedPath;
            private PointF[] _cachedPath;

            public ConnectorShape() { }
            public ConnectorShape(PointF start, Color color, bool arrow, bool orthogonal = false) : base(start, color)
            {
                StartPt = start; EndPt = start; HasArrow = arrow; IsOrthogonal = orthogonal;
            }
            
            public override void UpdateEndPoint(PointF pt) { if (!IsLocked) EndPt = pt; }
            
            public override bool HitTest(PointF pt) 
            { 
                if (_cachedPath == null || _cachedPath.Length < 2) return false;
                for (int i = 0; i < _cachedPath.Length - 1; i++)
                {
                    if (DistancePointToSegment(pt, _cachedPath[i], _cachedPath[i+1]) < 8f) return true;
                }
                return false; 
            } 

            public override HandlePosition HitTestHandle(PointF pt)
            {
                if (!IsSelected || IsLocked || _cachedPath == null || _cachedPath.Length < 2) return HandlePosition.None;

                float s = 10;
                PointF start = _cachedPath[0];
                PointF end = _cachedPath[_cachedPath.Length - 1];

                if (new RectangleF(start.X - s, start.Y - s, s * 2, s * 2).Contains(pt)) return HandlePosition.StartPoint;
                if (new RectangleF(end.X - s, end.Y - s, s * 2, s * 2).Contains(pt)) return HandlePosition.EndPoint;

                return HandlePosition.None;
            }

            public override void Move(float dx, float dy)
            {
                if (IsLocked) return;
                StartPt = new PointF(StartPt.X + dx, StartPt.Y + dy);
                EndPt = new PointF(EndPt.X + dx, EndPt.Y + dy);
            }

            private float DistancePointToSegment(PointF pt, PointF p1, PointF p2)
            {
                float l2 = (p1.X - p2.X)*(p1.X - p2.X) + (p1.Y - p2.Y)*(p1.Y - p2.Y);
                if (l2 == 0) return (float)Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
                float t = Math.Max(0, Math.Min(1, ((pt.X - p1.X) * (p2.X - p1.X) + (pt.Y - p1.Y) * (p2.Y - p1.Y)) / l2));
                PointF projection = new PointF(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
                return (float)Math.Sqrt(Math.Pow(pt.X - projection.X, 2) + Math.Pow(pt.Y - projection.Y, 2));
            }

            private bool GetIntersection(PointF A, PointF B, PointF C, PointF D, out PointF intersection)
            {
                intersection = PointF.Empty;
                float den = (B.X - A.X) * (D.Y - C.Y) - (B.Y - A.Y) * (D.X - C.X);
                if (Math.Abs(den) < 0.001f) return false; 
                
                float num1 = (A.Y - C.Y) * (D.X - C.X) - (A.X - C.X) * (D.Y - C.Y);
                float num2 = (A.Y - C.Y) * (B.X - A.X) - (A.X - C.X) * (B.Y - A.Y);
                
                float r = num1 / den;
                float s = num2 / den;
                
                if (r > 0.05f && r < 0.95f && s > 0.05f && s < 0.95f) 
                {
                    intersection = new PointF(A.X + r * (B.X - A.X), A.Y + r * (B.Y - A.Y));
                    return true;
                }
                return false;
            }

            private PointF[] CalculateOrthogonalPath(PointF p1, PointF p2, IEnumerable<ShapeBase> allShapes, QuadTree quadTree)
            {
                if (Math.Abs(p1.X - p2.X) < 20 && Math.Abs(p1.Y - p2.Y) < 20)
                {
                    return new PointF[] { p1, new PointF(p1.X, p2.Y), p2 };
                }

                List<RectangleF> obstacles = new List<RectangleF>();
                
                float minX = Math.Min(p1.X, p2.X) - 100;
                float minY = Math.Min(p1.Y, p2.Y) - 100;
                float maxX = Math.Max(p1.X, p2.X) + 100;
                float maxY = Math.Max(p1.Y, p2.Y) + 100;
                RectangleF searchArea = new RectangleF(minX, minY, maxX - minX, maxY - minY);

                List<ShapeBase> nearbyShapes = new List<ShapeBase>();
                if (quadTree != null)
                {
                    quadTree.Retrieve(nearbyShapes, searchArea);
                }
                else if (allShapes != null)
                {
                    nearbyShapes = allShapes.ToList();
                }

                foreach (var s in nearbyShapes)
                {
                    if (s is ConnectorShape || s.Id == this.SourceId || s.Id == this.TargetId) continue;
                    RectangleF obs = s.Bounds;
                    obs.Inflate(15, 15);
                    obstacles.Add(obs);
                }

                List<float> xCoords = new List<float> { p1.X, p2.X, p1.X - 30, p1.X + 30, p2.X - 30, p2.X + 30 };
                List<float> yCoords = new List<float> { p1.Y, p2.Y, p1.Y - 30, p1.Y + 30, p2.Y - 30, p2.Y + 30 };

                foreach (var obs in obstacles)
                {
                    xCoords.Add(obs.Left); xCoords.Add(obs.Right);
                    yCoords.Add(obs.Top); yCoords.Add(obs.Bottom);
                }

                xCoords = xCoords.Distinct().OrderBy(x => x).ToList();
                yCoords = yCoords.Distinct().OrderBy(y => y).ToList();

                if (xCoords.Count * yCoords.Count > 1000) 
                {
                    return BasicOrthogonalPath(p1, p2);
                }

                PointF startNode = GetClosestNode(p1, xCoords, yCoords);
                PointF endNode = GetClosestNode(p2, xCoords, yCoords);

                var openSet = new List<PointF> { startNode };
                var cameFrom = new Dictionary<PointF, PointF>();
                var gScore = new Dictionary<PointF, float> { [startNode] = 0 };
                var fScore = new Dictionary<PointF, float> { [startNode] = Heuristic(startNode, endNode) };

                while (openSet.Count > 0)
                {
                    PointF current = openSet.OrderBy(n => fScore.ContainsKey(n) ? fScore[n] : float.MaxValue).First();
                    if (current == endNode) return ReconstructPath(cameFrom, current, p1, p2);

                    openSet.Remove(current);

                    foreach (var neighbor in GetNeighbors(current, xCoords, yCoords))
                    {
                        if (LineIntersectsObstacles(current, neighbor, obstacles)) continue;

                        float penalty = 0;
                        if (cameFrom.ContainsKey(current))
                        {
                            PointF prev = cameFrom[current];
                            bool wasHorizontal = Math.Abs(current.Y - prev.Y) < 1f;
                            bool isHorizontal = Math.Abs(neighbor.Y - current.Y) < 1f;
                            if (wasHorizontal != isHorizontal) penalty = 50f; 
                        }

                        float tentativeG = gScore[current] + Heuristic(current, neighbor) + penalty;

                        if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                        {
                            cameFrom[neighbor] = current;
                            gScore[neighbor] = tentativeG;
                            fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, endNode);
                            if (!openSet.Contains(neighbor)) openSet.Add(neighbor);
                        }
                    }
                }

                return BasicOrthogonalPath(p1, p2);
            }

            private PointF[] BasicOrthogonalPath(PointF p1, PointF p2)
            {
                float midX = p1.X + (p2.X - p1.X) / 2;
                float midY = p1.Y + (p2.Y - p1.Y) / 2;
                
                if (Math.Abs(p2.X - p1.X) > Math.Abs(p2.Y - p1.Y))
                    return new PointF[] { p1, new PointF(midX, p1.Y), new PointF(midX, p2.Y), p2 };
                else
                    return new PointF[] { p1, new PointF(p1.X, midY), new PointF(p2.X, midY), p2 };
            }

            private PointF GetClosestNode(PointF p, List<float> xCoords, List<float> yCoords)
            {
                float closeX = xCoords.OrderBy(x => Math.Abs(x - p.X)).First();
                float closeY = yCoords.OrderBy(y => Math.Abs(y - p.Y)).First();
                return new PointF(closeX, closeY);
            }

            private float Heuristic(PointF a, PointF b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);

            private IEnumerable<PointF> GetNeighbors(PointF p, List<float> xCoords, List<float> yCoords)
            {
                int xi = xCoords.IndexOf(p.X);
                int yi = yCoords.IndexOf(p.Y);
                if (xi > 0) yield return new PointF(xCoords[xi - 1], p.Y);
                if (xi < xCoords.Count - 1) yield return new PointF(xCoords[xi + 1], p.Y);
                if (yi > 0) yield return new PointF(p.X, yCoords[yi - 1]);
                if (yi < yCoords.Count - 1) yield return new PointF(p.X, yCoords[yi + 1]);
            }

            private bool LineIntersectsObstacles(PointF p1, PointF p2, List<RectangleF> obstacles)
            {
                float minX = Math.Min(p1.X, p2.X);
                float maxX = Math.Max(p1.X, p2.X);
                float minY = Math.Min(p1.Y, p2.Y);
                float maxY = Math.Max(p1.Y, p2.Y);

                foreach (var obs in obstacles)
                {
                    if (p1.X == p2.X) 
                    {
                        if (p1.X > obs.Left && p1.X < obs.Right && minY < obs.Bottom && maxY > obs.Top) return true;
                    }
                    else if (p1.Y == p2.Y) 
                    {
                        if (p1.Y > obs.Top && p1.Y < obs.Bottom && minX < obs.Right && maxX > obs.Left) return true;
                    }
                }
                return false;
            }

            private PointF[] ReconstructPath(Dictionary<PointF, PointF> cameFrom, PointF current, PointF start, PointF end)
            {
                List<PointF> path = new List<PointF> { end };
                while (cameFrom.ContainsKey(current))
                {
                    path.Add(current);
                    current = cameFrom[current];
                }
                path.Add(start);
                path.Reverse();

                List<PointF> cleanPath = new List<PointF> { path[0] };
                for (int i = 1; i < path.Count - 1; i++)
                {
                    PointF prev = cleanPath.Last();
                    PointF next = path[i + 1];
                    if (prev.X != next.X && prev.Y != next.Y)
                    {
                        cleanPath.Add(path[i]);
                    }
                }
                cleanPath.Add(path.Last());

                return cleanPath.ToArray();
            }

            public void DrawDynamic(Graphics g, PointF p1, PointF p2, IEnumerable<ShapeBase> allShapes = null, bool isFastMode = false, QuadTree quadTree = null)
            {
                PointF[] pts;
                if (IsOrthogonal)
                {
                    if (isFastMode) pts = BasicOrthogonalPath(p1, p2);
                    else pts = CalculateOrthogonalPath(p1, p2, allShapes, quadTree);
                }
                else
                {
                    pts = new PointF[] { p1, p2 };
                }
                _cachedPath = pts;

                if (pts.Length < 2) return; 

                float totalDist = Distance(pts[pts.Length - 2], pts[pts.Length - 1]);
                if (totalDist < 0.5f) return;

                Pen basePen = GetCachedPen();

                using (GraphicsPath mainPath = new GraphicsPath())
                {
                    if (EnableLineJumps && allShapes != null && !isFastMode)
                    {
                        for (int i = 0; i < pts.Length - 1; i++)
                        {
                            PointF segStart = pts[i];
                            PointF segEnd = pts[i + 1];
                            List<PointF> intersections = new List<PointF>();

                            List<ShapeBase> checkShapes = allShapes.ToList();
                            if (quadTree != null)
                            {
                                checkShapes.Clear();
                                float minX = Math.Min(segStart.X, segEnd.X) - 10;
                                float minY = Math.Min(segStart.Y, segEnd.Y) - 10;
                                float maxX = Math.Max(segStart.X, segEnd.X) + 10;
                                float maxY = Math.Max(segStart.Y, segEnd.Y) + 10;
                                quadTree.Retrieve(checkShapes, new RectangleF(minX, minY, maxX - minX, maxY - minY));
                            }

                            foreach (var other in checkShapes)
                            {
                                if (other is ConnectorShape otherConn && otherConn != this && otherConn.CachedPath != null && other.Id.CompareTo(this.Id) < 0)
                                {
                                    for (int j = 0; j < otherConn.CachedPath.Length - 1; j++)
                                    {
                                        if (GetIntersection(segStart, segEnd, otherConn.CachedPath[j], otherConn.CachedPath[j + 1], out PointF intersectPt))
                                        {
                                            intersections.Add(intersectPt);
                                        }
                                    }
                                }
                            }

                            if (intersections.Count == 0)
                            {
                                mainPath.AddLine(segStart, segEnd);
                            }
                            else
                            {
                                // 【修正1】: 改為以 Bezier 曲線繪製完美的跳線弧度，解決斜線跳線扭曲問題
                                intersections = intersections.OrderBy(pt => Distance(segStart, pt)).ToList();
                                PointF currentPt = segStart;
                                float jumpRadius = 6f; 

                                float segmentLength = Distance(segStart, segEnd);
                                float dx = segEnd.X - segStart.X;
                                float dy = segEnd.Y - segStart.Y;
                                float nx = -dy / segmentLength; 
                                float ny = dx / segmentLength;  

                                foreach (var ipt in intersections)
                                {
                                    float distToIntersect = Distance(currentPt, ipt);
                                    if (distToIntersect > jumpRadius)
                                    {
                                        float ratioPre = (Distance(segStart, ipt) - jumpRadius) / segmentLength;
                                        PointF preJump = new PointF(segStart.X + ratioPre * dx, segStart.Y + ratioPre * dy);
                                        mainPath.AddLine(currentPt, preJump);
                                        
                                        float ratioPost = (Distance(segStart, ipt) + jumpRadius) / segmentLength;
                                        PointF postJump = new PointF(segStart.X + ratioPost * dx, segStart.Y + ratioPost * dy);
                                        
                                        // 用法線向量推出控制點來畫貝茲半圓
                                        PointF controlPt = new PointF(ipt.X + nx * jumpRadius * 1.5f, ipt.Y + ny * jumpRadius * 1.5f);
                                        mainPath.AddBezier(preJump, controlPt, controlPt, postJump);

                                        currentPt = postJump;
                                    }
                                }
                                mainPath.AddLine(currentPt, segEnd);
                            }
                        }
                    }
                    else
                    {
                        if (pts.Length > 2) mainPath.AddLines(pts);
                        else mainPath.AddLine(p1, p2);
                    }

                    if (HasArrow && totalDist > 5f)
                    {
                        using (Pen arrowPen = new Pen(basePen.Color, basePen.Width) { DashStyle = basePen.DashStyle })
                        {
                            arrowPen.CustomEndCap = new AdjustableArrowCap(5, 5, true);
                            g.DrawPath(arrowPen, mainPath);
                        }
                    }
                    else
                    {
                        g.DrawPath(basePen, mainPath);
                    }
                }
            }

            public void DrawDynamic(Graphics g, PointF p1, PointF p2) => DrawDynamic(g, p1, p2, null, false, null);
            public override void Draw(Graphics g) { }

            public override void DrawSelection(Graphics g)
            {
                if (!IsSelected || _cachedPath == null || _cachedPath.Length < 2) return;
                
                Color ptColor = IsLocked ? Color.LightGray : Color.White;
                Color borderColor = IsLocked ? Color.Gray : Color.DodgerBlue;

                using (Brush b = new SolidBrush(ptColor))
                using (Pen p = new Pen(borderColor))
                {
                    foreach (var pt in _cachedPath)
                    {
                        g.FillRectangle(b, pt.X - 3, pt.Y - 3, 6, 6);
                        g.DrawRectangle(p, pt.X - 3, pt.Y - 3, 6, 6);
                    }
                }

                if (!IsLocked)
                {
                    PointF start = _cachedPath[0];
                    PointF end = _cachedPath[_cachedPath.Length - 1];

                    g.FillEllipse(Brushes.Yellow, start.X - 5, start.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.Red, start.X - 5, start.Y - 5, 10, 10);

                    g.FillEllipse(Brushes.Yellow, end.X - 5, end.Y - 5, 10, 10);
                    g.DrawEllipse(Pens.Red, end.X - 5, end.Y - 5, 10, 10);
                }
            }
        }
    }
}
