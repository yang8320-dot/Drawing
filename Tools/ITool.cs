using System.Drawing;
using System.Windows.Forms;

namespace DrawingApp.Tools
{
    /// <summary>
    /// 定義畫布工具的統一介面。
    /// 透過實作這個介面，可以將不同工具 (如游標、畫筆、連線) 的互動邏輯完全獨立。
    /// </summary>
    public interface ITool
    {
        void OnMouseDown(App_CanvasControl canvas, MouseEventArgs e, PointF realPt);
        void OnMouseMove(App_CanvasControl canvas, MouseEventArgs e, PointF realPt);
        void OnMouseUp(App_CanvasControl canvas, MouseEventArgs e, PointF realPt);
        
        /// <summary>
        /// 提供工具額外渲染的機會 (例如繪製拖曳中的虛線框、懸停提示點等)
        /// </summary>
        void OnPaint(App_CanvasControl canvas, Graphics g);

        /// <summary>
        /// 處理鍵盤事件 (例如 ESC 取消動作)
        /// 回傳 true 表示已處理，不需傳遞給基底
        /// </summary>
        bool OnKeyDown(App_CanvasControl canvas, Keys keyData);

        /// <summary>
        /// 當工具被切換 (啟用/停用) 時觸發，用來清理殘留狀態
        /// </summary>
        void OnToolDeactivated(App_CanvasControl canvas);
    }
}
