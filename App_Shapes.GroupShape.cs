using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;

namespace DrawingApp
{
    public partial class App_Shapes
    {
        public class GroupShape : ShapeBase
        {
            [Browsable(false)]
            public List<ShapeBase> Children { get; set; } = new List<ShapeBase>();

            public GroupShape() { }

            public GroupShape(List<ShapeBase> children)
            {
                Children = children;
                NormalizeBounds();
            }

            // [優化 3]：修改群組時，將顏色與格式遞迴同步給所有子圖形
            public override void ApplyFormatFrom(ShapeBase source)
            {
                base.ApplyFormatFrom(source);
                foreach (var child in Children)
                {
                    child.ApplyFormatFrom(source);
                }
            }

            public override void Dispose()
            {
                base.Dispose();
                foreach (var child in Children) child.Dispose();
            }

            public override void Draw(Graphics g)
            {
                foreach (var child in Children)
                {
                    child.DrawWithTransform(g);
                }
            }

            public override void Move(float dx, float dy)
            {
                if (IsLocked) return;
                base.Move(dx, dy);
                foreach (var child in Children)
                {
                    child.Move(dx, dy);
                }
            }

            public override void SetBounds(RectangleF newBounds)
            {
                if (IsLocked) return;
                if (Bounds.Width == 0 || Bounds.Height == 0) return;
                
                float scaleX = newBounds.Width / Bounds.Width;
                float scaleY = newBounds.Height / Bounds.Height;

                foreach (var child in Children)
                {
                    float newChildX = newBounds.X + (child.Bounds.X - Bounds.X) * scaleX;
                    float newChildY = newBounds.Y + (child.Bounds.Y - Bounds.Y) * scaleY;
                    float newChildW = child.Bounds.Width * scaleX;
                    float newChildH = child.Bounds.Height * scaleY;
                    child.SetBounds(new RectangleF(newChildX, newChildY, newChildW, newChildH));
                }
                base.SetBounds(newBounds);
            }

            public override void NormalizeBounds()
            {
                if (Children.Count == 0) return;
                
                float minX = Children.Min(c => c.Bounds.Left);
                float minY = Children.Min(c => c.Bounds.Top);
                float maxX = Children.Max(c => c.Bounds.Right);
                float maxY = Children.Max(c => c.Bounds.Bottom);
                
                Bounds = new RectangleF(minX, minY, maxX - minX, maxY - minY);
            }

            public override bool HitTest(PointF pt)
            {
                return Children.Any(c => c.HitTest(pt));
            }
        }
    }
}
