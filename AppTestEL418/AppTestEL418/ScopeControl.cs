// ScopeControl.cs
/*
© 2026 Enzo PERRIER

This code is provided free of charge.

Permission is granted to use this code for personal, non-commercial purposes
only.

The following actions are strictly prohibited:
- Using this code, in whole or in part, for commercial purposes;
- Modifying this code or creating derivative works;
- Redistributing this code, modified or unmodified, without prior written
  permission from the author.

This code is provided "as is", without warranty of any kind, express or implied.
The author shall not be held liable for any damages arising from the use of
this code.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace AppTestEL418
{
    /// <summary>Oscillogramme simple : une courbe valeur = f(temps), autoscale vertical, fenêtre glissante.</summary>
    public class ScopeControl : FrameworkElement
    {
        private const int MaxPoints = 20000;
        private readonly List<Point> points = new List<Point>(); // X = temps (s), Y = valeur

        private string title = "";
        private string unit = "";
        private Brush lineBrush = Brushes.DeepSkyBlue;
        private double windowSeconds = 60;
        private double minSpan = 1.0;

        private static Brush MakeBrush(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        private static readonly Brush GridBrush = MakeBrush(0x2A, 0x2B, 0x30);
        private static readonly Brush TextBrush = MakeBrush(0x88, 0x88, 0x88);
        private static readonly Brush FrameBrush = MakeBrush(0x3A, 0x3B, 0x40);
        private static readonly Pen GridPen = new Pen(GridBrush, 1);
        private static readonly Pen FramePen = new Pen(FrameBrush, 1);

        public ScopeControl()
        {
            ClipToBounds = true;
        }

        public string Title { get => title; set { title = value; InvalidateVisual(); } }
        public string Unit { get => unit; set { unit = value; InvalidateVisual(); } }
        public Brush LineBrush { get => lineBrush; set { lineBrush = value; InvalidateVisual(); } }
        public double WindowSeconds { get => windowSeconds; set { windowSeconds = Math.Max(1, value); InvalidateVisual(); } }
        /// <summary>Étendue verticale minimale (évite de zoomer sur le bruit)</summary>
        public double MinSpan { get => minSpan; set { minSpan = Math.Max(1e-6, value); InvalidateVisual(); } }

        public void AddPoint(double t, double value)
        {
            points.Add(new Point(t, value));
            if (points.Count > MaxPoints)
                points.RemoveRange(0, points.Count - MaxPoints);
            InvalidateVisual();
        }

        public void Clear()
        {
            points.Clear();
            InvalidateVisual();
        }

        private FormattedText MakeText(string s, double size, Brush brush, bool bold = false)
        {
            var face = new Typeface(new FontFamily("Consolas"), FontStyles.Normal,
                                    bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                                     face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static string FormatValue(double v, double span)
        {
            int dec = span < 1 ? 3 : span < 10 ? 2 : span < 100 ? 1 : 0;
            return v.ToString("F" + dec, CultureInfo.InvariantCulture);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 120 || h < 90) return;

            const double left = 62, right = 14, top = 26, bottom = 24;
            var plot = new Rect(left, top, w - left - right, h - top - bottom);

            // --- Plage de temps : la courbe se remplit puis défile ---
            double lastT = points.Count > 0 ? points[points.Count - 1].X : 0;
            double tMax = Math.Max(lastT, windowSeconds);
            double tMin = tMax - windowSeconds;

            int first = points.Count;
            while (first > 0 && points[first - 1].X >= tMin) first--;
            int from = Math.Max(0, first - 1); // un point avant la fenêtre pour la continuité du tracé

            // --- Plage verticale (autoscale) ---
            double yMin, yMax;
            if (points.Count == 0)
            {
                yMin = 0;
                yMax = minSpan;
            }
            else
            {
                yMin = double.MaxValue;
                yMax = double.MinValue;
                for (int i = from; i < points.Count; i++)
                {
                    double y = points[i].Y;
                    if (y < yMin) yMin = y;
                    if (y > yMax) yMax = y;
                }
            }

            double span = yMax - yMin;
            if (span < minSpan)
            {
                double mid = (yMax + yMin) / 2;
                yMin = mid - minSpan / 2;
                yMax = mid + minSpan / 2;
                span = minSpan;
            }
            double pad = span * 0.1;
            yMin -= pad;
            yMax += pad;
            span = yMax - yMin;

            Func<double, double> X = t => plot.Left + (t - tMin) / windowSeconds * plot.Width;
            Func<double, double> Y = v => plot.Bottom - (v - yMin) / span * plot.Height;

            // --- Grille horizontale + graduations Y ---
            for (int i = 0; i <= 4; i++)
            {
                double v = yMin + i * span / 4;
                double y = Y(v);
                dc.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));

                FormattedText lbl = MakeText(FormatValue(v, span), 11, TextBrush);
                dc.DrawText(lbl, new Point(plot.Left - 6 - lbl.Width, y - lbl.Height / 2));
            }

            // --- Grille verticale + graduations temps ---
            for (int i = 0; i <= 6; i++)
            {
                double t = tMin + i * windowSeconds / 6;
                double x = plot.Left + i * plot.Width / 6;
                dc.DrawLine(GridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));

                FormattedText lbl = MakeText(t.ToString("F0", CultureInfo.InvariantCulture) + "s", 11, TextBrush);
                dc.DrawText(lbl, new Point(x - lbl.Width / 2, plot.Bottom + 4));
            }

            dc.DrawRectangle(null, FramePen, plot);

            // --- Courbe ---
            if (points.Count > 0)
            {
                var geo = new StreamGeometry();
                using (StreamGeometryContext ctx = geo.Open())
                {
                    bool started = false;
                    for (int i = from; i < points.Count; i++)
                    {
                        var pt = new Point(X(points[i].X), Y(points[i].Y));
                        if (!started)
                        {
                            ctx.BeginFigure(pt, false, false);
                            started = true;
                        }
                        else
                        {
                            ctx.LineTo(pt, true, true);
                        }
                    }
                }
                geo.Freeze();

                dc.PushClip(new RectangleGeometry(plot));
                dc.DrawGeometry(null, new Pen(lineBrush, 1.8), geo);
                for (int i = from; i < points.Count; i++)
                    dc.DrawEllipse(lineBrush, null, new Point(X(points[i].X), Y(points[i].Y)), 2.5, 2.5);
                dc.Pop();
            }

            // --- Titre + dernière valeur ---
            dc.DrawText(MakeText(title, 12, TextBrush, true), new Point(left, 4));

            if (points.Count > 0)
            {
                string val = FormatValue(points[points.Count - 1].Y, span) + " " + unit;
                FormattedText ft = MakeText(val, 16, lineBrush, true);
                dc.DrawText(ft, new Point(w - right - ft.Width, 2));
            }
        }
    }
}