using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace GoogleDriveDownloader.UI 
{
    public class RubberBandAdorner : Adorner
    {
        private Point _startPoint;
        private Point _endPoint;
        private Pen _rubberBandPen;
        private Brush _rubberBandBrush;

        public RubberBandAdorner(UIElement adornedElement, Point startPoint) : base(adornedElement)
        {
            _startPoint = startPoint;
            _endPoint = startPoint;

            // Сплошная синяя рамка
            _rubberBandPen = new Pen(Brushes.DodgerBlue, 1);

            // Полупрозрачная голубая заливка
            _rubberBandBrush = new SolidColorBrush(Colors.DodgerBlue) { Opacity = 0.2 };
        }

        // Обнов конечную точку и перерис прямоугольник
        public void UpdateSelection(Point endPoint)
        {
            _endPoint = endPoint;
            InvalidateVisual(); // Говорим WPF, что нужно перерисовать
        }

        // Возвращает финальный прямоугольник выделения
        public Rect GetSelectionRect()
        {
            return new Rect(_startPoint, _endPoint);
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            // Рисуем прямоугольник от startP до endP
            dc.DrawRectangle(_rubberBandBrush, _rubberBandPen, GetSelectionRect());
        }
    }
}