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

                // 【修正崩潰】：絕對不要修改 CachedPen 的 CustomEndCap，改用獨立的 Pen 繪製
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
                                intersections = intersections.OrderBy(pt => Distance(segStart, pt)).ToList();
                                PointF currentPt = segStart;
                                float jumpRadius = 6f;

                                foreach (var ipt in intersections)
                                {
                                    if (Distance(currentPt, ipt) > jumpRadius)
                                    {
                                        float ratio = (Distance(segStart, ipt) - jumpRadius) / Distance(segStart, segEnd);
                                        PointF preJump = new PointF(segStart.X + ratio * (segEnd.X - segStart.X), segStart.Y + ratio * (segEnd.Y - segStart.Y));
                                        mainPath.AddLine(currentPt, preJump);
                                        
                                        bool isHorizontal = Math.Abs(segStart.Y - segEnd.Y) < 1f;
                                        if (isHorizontal)
                                        {
                                            float sweep = segStart.X < segEnd.X ? 180 : -180;
                                            mainPath.AddArc(ipt.X - jumpRadius, ipt.Y - jumpRadius, jumpRadius * 2, jumpRadius * 2, segStart.X < segEnd.X ? 180 : 0, sweep);
                                        }
                                        else
                                        {
                                            float sweep = segStart.Y < segEnd.Y ? 180 : -180;
                                            mainPath.AddArc(ipt.X - jumpRadius, ipt.Y - jumpRadius, jumpRadius * 2, jumpRadius * 2, segStart.Y < segEnd.Y ? 270 : 90, sweep);
                                        }

                                        ratio = (Distance(segStart, ipt) + jumpRadius) / Distance(segStart, segEnd);
                                        currentPt = new PointF(segStart.X + ratio * (segEnd.X - segStart.X), segStart.Y + ratio * (segEnd.Y - segStart.Y));
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

                    // 繪製：如果有箭頭，建立一個全新的暫存 Pen 來畫，安全且不崩潰
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
