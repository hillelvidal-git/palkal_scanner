using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;

namespace LaserSurvey
{
    public class drawPtInfo
    {
        public string Angle;
        public string Distance;
        public string Status;
    }

    class BoxDrawer
    {
        public Panel drawingPanel;
        public Pen pointsStdPen = new Pen(Color.Blue, 1);
        public Pen pointsErrPen = new Pen(Color.Green, 1);
        public Pen linesStdPen = new Pen(Color.Blue, 1);
        private Graphics g;
        private Point BasePt; //Panel Center
        private System.Collections.ArrayList Vertices = new System.Collections.ArrayList();
        private System.Collections.ArrayList BadVertices = new System.Collections.ArrayList();
        int[,] intPts;
        drawPtInfo[] PtsInfo = new drawPtInfo[501];
        private double BoxScale;
        private double scale = 1;
        internal float zoomVal=1;

        public BoxDrawer(Panel dPanel)
        {
            this.drawingPanel = dPanel;
            BoxScale = (double)drawingPanel.Width / (2*4096);
            this.g = this.drawingPanel.CreateGraphics();
            this.BasePt = new Point(
                this.drawingPanel.Size.Width / 2,
                this.drawingPanel.Size.Height / 2);
            intPts = new int[this.drawingPanel.Size.Width, this.drawingPanel.Size.Height];
        }

        public void Clear()
        {
            this.Vertices.Clear();
            this.BadVertices.Clear();
            this.g.Clear(Color.White);
        }

        public void AddPoint(float x, float y, float ang, float dist, int num, bool valid)
        {
            Point newPT = new Point((int)x, (int)y);

            if (valid)
            {
                this.Vertices.Add(newPT);
                //for (int r = (int)x - 3; r < (int)x + 3; r++)
                //    for (int c = (int)y - 3; c < (int)y + 3; c++)
                //        try
                //        {
                //            this.intPts[r, c] = num;
                //        }
                //        catch { }
                drawPtInfo newInfo = new drawPtInfo();
                newInfo.Angle = (ang * 180 / Math.PI).ToString("0.00");
                newInfo.Distance = ((dist / this.BoxScale) / 10).ToString("0.0");
                newInfo.Status = num.ToString();
                this.PtsInfo[num] = newInfo;
            }
            else //Invalid point
            {
                this.BadVertices.Add(newPT);
                //for (int r = (int)x - 3; r < (int)x + 3; r++)
                //    for (int c = (int)y - 3; c < (int)y + 3; c++)
                //        try
                //        {
                //            this.intPts[r, c] = num;
                //        }
                //        catch { }
                drawPtInfo newInfo = new drawPtInfo();
                newInfo.Angle = (ang * 180 / Math.PI).ToString("0.00");
                newInfo.Distance = "Distance Error";
                newInfo.Status = num.ToString();
                this.PtsInfo[num] = newInfo;
            }
        }

        public void AddPolarPoint(float r, float angle, int num, bool valid)
        {
            r *= (float)this.BoxScale;
            if (!valid) r = 120;

            angle *= (float)(Math.PI / 180);
            float x1, y1;
            x1 = this.BasePt.X - r * (float)Math.Sin(angle);
            y1 = this.BasePt.Y - r * (float)Math.Cos(angle);

            AddPoint(x1, y1, angle, r, num, valid);
        }

        public void AddRadialLine(float angle)
        {
            angle *= (float)(Math.PI / 180);
            float x1, y1, r;
            r = 100;
            x1 = this.BasePt.X - r * (float)Math.Sin(angle);
            y1 = this.BasePt.Y - r * (float)Math.Cos(angle);

            this.linesStdPen.Color = Color.Red;
            this.g.DrawLine(linesStdPen, BasePt, new Point((int)x1, (int)y1));
            this.linesStdPen.Color = Color.Blue;
        }

        public void Redraw()
        {
            //g.ScaleTransform(zoomVal, zoomVal);

            Point[] pts = new Point[Vertices.Count];
            Vertices.CopyTo(pts);

            Point[] Badpts = new Point[BadVertices.Count];
            BadVertices.CopyTo(Badpts);

            try //Draw lines
            {
                this.g.Clear(Color.White);
                this.g.DrawLines(linesStdPen, pts);
            }
            catch { }

            int ptWidth = 1;

            try
            {
                //Draw Valid points
                foreach (Point pt in pts)
                {
                    this.g.FillEllipse(this.pointsStdPen.Brush, pt.X - ptWidth, pt.Y - ptWidth, ptWidth*2, ptWidth*2);
                }

                //Draw Invalid points
                foreach (Point bpt in Badpts)
                {
                    this.g.FillEllipse(this.pointsErrPen.Brush, bpt.X - ptWidth, bpt.Y - ptWidth, ptWidth*2, ptWidth*2);
                }

                //Draw center point
                this.g.FillEllipse(this.pointsErrPen.Brush, this.BasePt.X - 4, this.BasePt.Y - 4, 8, 8);
            }
            catch { }

            //Panel p = sender as Panel;

            // Create pen.
            Pen circlePen = new Pen(Color.Red, 2);
            circlePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            // Create rectangle for ellipse.
            int padding = 5;
            Rectangle rect = new Rectangle(padding, padding, this.drawingPanel.Width - padding - 4, this.drawingPanel.Height - padding - 4);

            // Draw ellipse to screen.
            this.g.DrawEllipse(circlePen, rect);

            // Create pen.
            Pen circlePen2;
            int c = 200;
            

            for (int mm = 500; mm < 4000; mm += 500)
            {                
                circlePen2 = new Pen(Color.FromArgb(c,c,c), 1);
                Rectangle rect2 = new Rectangle((int)(BasePt.X - BoxScale * mm), (int)(BasePt.X - BoxScale * mm), (int)(2 * BoxScale * mm), (int)(2 * BoxScale * mm));
                this.g.DrawEllipse(circlePen2, rect2);
                c -= 15;
            }

        }

        public bool ScaleBox(double absFactor, double[] move)
        {
            try
            {
                absFactor /= this.scale;
                for (int i = 0; i < this.Vertices.Count; i++)
                {
                    Vertices[i] = ScalePoint((Point)this.Vertices[i], absFactor, move);
                }
                this.scale *= absFactor;
                this.Redraw();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private Point ScalePoint(Point pt, double factor, double[] move)
        {
            double x = pt.X * factor + move[0];
            double y = pt.Y * factor + move[1];
            return new Point((int)x, (int)y);
        }

        public string GetPtTip(int x, int y)
        {
            
            return "";
            try
            {
                if (this.intPts[x, y] != 0)
                {
                    drawPtInfo info = PtsInfo[this.intPts[x, y]];
                    string tip = "Num. " + info.Status + ": " + info.Angle + " Deg. , " + info.Distance + " CM";
                    if (tip.Contains("Error")) tip = tip.Remove(tip.Length - 3);
                    return tip;
                }
                else return "";
            }
            catch { return ""; }
        }

        public void ClosePolygon()
        {
            Point newPT = new Point(((Point)Vertices[0]).X, ((Point)Vertices[0]).Y);
            this.Vertices.Add(newPT);
        }
    }
}
