using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace LaserSurvey
{
    public partial class Form1 : Form
    {
        //TabPage4 Methods:

        private void tabPage4_Enter(object sender, EventArgs e)
        {
            //start local position tracking
            if (this.ServoConnected) this.timer2.Start();
        }

        private void tabPage4_Leave(object sender, EventArgs e)
        {
            //stop local position tracking
            //this.timer2.Stop();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            //Application.DoEvents();

            if (this.myServo.CheckInPosition())
                this.tsslInPos.Text = "InPos";
            else
                this.tsslInPos.Text = "No";

            double dPos = (double)myServo.GetPosition() / 1000;
            tsslPos.Text = label51.Text = dPos.ToString();
            this.ServoOn = this.myServo.IsOn();
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            if (!ServoOn) return;
            try
            {
                myServo.MoveAbsolute((int)nUDpos.Value * 1000);
            }
            catch (Exception ae)
            {
                MessageBox.Show("הפעולה לא הצליחה\n" + ae.Message);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (!ServoOn) return;
            try
            {
                myServo.MoveRelative((int)nUDstep.Value * 1000);
            }
            catch (Exception ae)
            {
                MessageBox.Show("הפעולה לא הצליחה\n" + ae.Message);
            }
        }

        private void revTuneBtn_Click(object sender, EventArgs e)
        {
            if (!ServoOn) return;
            try
            {
                myServo.MoveRelative(0 - (int)nUDtune.Value);
            }
            catch (Exception ae)
            {
                MessageBox.Show("הפעולה לא הצליחה\n" + ae.Message);
            }
        }

        private void fwdTuneBtn_Click(object sender, EventArgs e)
        {
            if (!ServoOn) return;
            try
            {
                myServo.MoveRelative((int)nUDtune.Value);
            }
            catch (Exception ae)
            {
                MessageBox.Show("הפעולה לא הצליחה\n" + ae.Message);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            hndMeasureTB.Enabled = false;
            button5.Enabled = false;
            try
            {
                hndMeasureTB.Text = (myDisto.MeasureOnce() - dstOffsetNum.Value).ToString();
            }
            catch (Exception ae)
            {
                MessageBox.Show("הפעולה לא הצליחה\n" + ae.Message);
            }
            hndMeasureTB.Enabled = true;
            button5.Enabled = true;
        }

    }
}
