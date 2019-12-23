using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Threading;

namespace LaserSurvey
{
    public partial class Form1 : Form
    {
        private double RoundTicks = 360000;

        //TabPage3 Methods:
        private void DisplayAttribution()
        {
            try
            {
                if (tvProjects.SelectedNode.Level == 3)
                {
                    TreeNode n = tvProjects.SelectedNode;
                    this.CurrentFieldID = Convert.ToInt32(n.Name);
                    tbFieldId.Text = CurrentFieldID.ToString();
                    this.attribution =
                        n.Parent.Parent.Parent.Text + "\\" +
                        n.Parent.Parent.Text + "\\" +
                        n.Parent.Text + "\\" +
                        n.Text;
                    lbBore.Text = nudBoreNum.Value.ToString();
                    lbField.Text = n.Text;
                }
                else
                {
                    this.CurrentFieldID = -1;
                    lbField.Text = "";
                }
            }
            catch
            {
                this.CurrentFieldID = -1;
                lbField.Text = "";
            }
        }

        private void CalculateSteps(object sender, EventArgs e)
        {
            double degrees = (double)(360 / (double)trackBar3.Value);
            double steps = 10000 / (double)trackBar3.Value;
            toolTip1.SetToolTip(pictureBox3, "תזוזה = " + steps.ToString("0.0") + " צעדים, שהם  " + degrees.ToString("0.00") + " מעלות");
        }


        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            trackBar4.Enabled = minDistChkBox.Checked;
        }

        private void srvOnOffBtn_Click(object sender, EventArgs e)
        {
            if (!ServoOn)
            {
                //Switch Servo On
                try
                {
                    myServo.SwitchOn();
                    ServoOn = true;
                }
                catch (Exception e3)
                {
                    MessageBox.Show("Can't Switch Servo On.\n\n" + e3.Message);
                }
            }
            else
            {
                //Switch Servo Off
                try
                {
                    myServo.SwitchOff();
                    ServoOn = false;
                }
                catch (Exception e4)
                {
                    MessageBox.Show("Can't Switch Servo Off.\n\n" + e4.Message);
                }
            }
        }

        private void srvHomeBtn_Click(object sender, EventArgs e)
        {
            if (!ServoOn) return;

            try
            {
                myServo.GoHome();
            }
            catch (Exception ae)
            {
                MessageBox.Show("הפעולה לא הצליחה\n" + ae.Message);
            }

        }

        private void StartBtn_Click(object sender, EventArgs e)
        {
            slog("Now Working? >> " + NowWorking);
            if (NowWorking) //If we have to ABORT a survey:
            {
                this.mySurveyObj.Stop = true;
                return;
            }
            else           //If we have to START a survey:
            {
                string readyMessage = ReadyToSurvey();
                if (readyMessage.StartsWith("OK"))
                {
                    NowWorking = true;
                    this.mySurveyObj.Stop = false;
                    lbSurveyLog.Items.Clear();
                    slog("Executing!");
                    ExecuteSurvey();
                }
                else
                {
                    btnStartSurvey.Enabled = false;
                    MessageBox.Show("לא ניתן עדיין לבצע את הסקר\n" + readyMessage, "ביצוע סקר",
                        MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign);
                    btnStartSurvey.Enabled = true;
                    this.nudOrientaionPt.Focus();
                }
            }
        }

        private string ReadyToSurvey()
        {
            string message = "";
            if (!ServoConnected && !WorkOffline) message += "\nמנוע המיסב איננו מקושר" + " -";
            if (!DistoConnected && !WorkOffline) message += "\nמד הלייזר איננו מקושר" + " -";
            if ((tvProjects.SelectedNode == null) || (tvProjects.SelectedNode.Level != 3)) message += "\nיש לבחור שדה" + " -";
            if (nudOrientaionPt.Text == "") message += "\nיש לבחור את נקודת המכוון" + " -";


            if (message == "")
            {
                message = "OK";

                //Save setting:
                try
                {
                    this.mySurveyObj.SamplesNum = (int)trackBar3.Value;
                    double gap = 360000 / (double)trackBar3.Value;
                    this.mySurveyObj.SampleTicksGap = (int)gap;
                    if (minDistChkBox.Checked)
                        this.mySurveyObj.minDist = (int)trackBar4.Value;
                    else
                        this.mySurveyObj.minDist = 0;
                }
                catch { message = "Setting Error"; }
            }
            return message;
        }

        private bool ExecuteSurvey()
        {
            //Clear former data from dsiplay:
            tbAbsTicks.Text = "";
            tbRelTicks.Text = "";
            tbRelAngle.Text = "";
            tbCurrentDist.Text = "";
            tbFalseSamples.Text = "";

            //Update some labels:
            tsProgressbar.Maximum = this.mySurveyObj.SamplesNum; tsProgressbar.Value = 0;
            tsProgressInfo.Text = "";
            progressBar2.Maximum = this.mySurveyObj.SamplesNum; progressBar2.Value = 0;
            lblBoreNum.Text = nudBoreNum.Value.ToString();
            lblSmaplesSum.Text = this.mySurveyObj.SamplesNum.ToString();
            label32.Text = "0 of " + this.mySurveyObj.SamplesNum.ToString() + " Completed";

            //Check if Servo is Ready
            string ReadyRs = "OK";// myServo.GetReady();
            if (!ReadyRs.StartsWith("OK")) //Servo is not ready
            {
                myServo.SwitchOff();
                this.filesTool.ClearDate();
                MessageBox.Show("מנוע המיסב איננו מוכן\n" + ReadyRs);
                return false;
            }

            //Clear servo alarms
            myServo.ResetAlarms();
            ////refer to current position as 0
            //myServo.SetPositionOffset();
            //Switch Servo ON
            myServo.SwitchOn();
            ServoOn = true;

            this.mySurveyObj.StartPosition = myServo.GetPosition();
            slog("Original Pos: " + mySurveyObj.StartPosition);

            //Switch Disto ON
            myDisto.SwitchOn(true);

            int elevation = (int)trackBar1.Value;

            //Save survey's details
            filesTool.SetSurveyDetails(
                this.attribution,
                this.CurrentFieldID,
                nudBoreNum.Value.ToString(),
                nudOrientaionPt.Value.ToString(),
                (this.trackBar2.Value == 0),
                elevation);

            //Clear Buffers:
            myDisto.ClearBuffer();

            //Start the survey
            this.boxDraw.Clear();
            this.mySurveyObj.currentSample = 0;
            this.mySurveyObj.ErrorLines = 0;
            //tabControl1.SelectedTab = tabControl1.TabPages[2];

            this.myServo.isInPosition = true;

            StartSurveyLoop();
            return true;
        }

        private void StartSurveyLoop()
        {
            timer2.Stop();


            //slog("Starting...");
            slog("Each Step is: " + this.mySurveyObj.SampleTicksGap);

            this.tspbSurvey.Maximum = this.mySurveyObj.SamplesNum + 1;
            this.tspbSurvey.Value = 0;

            this.sample = 0;

            timerLoop.Start();
        }

        int sample, nextPos;

        private void timerLoop_Tick(object sender, EventArgs e)
        {
            timerLoop.Stop();

            //תנאי הפסקה
            if (this.mySurveyObj.Stop)
            {
                slog("Aborted");
                TerminateSurvey();
                return;
            }

            //בדוק האם הסרבו הגיע למקום
            if (!this.myServo.CheckInPosition())
            {
                timerLoop.Start();
                return;
            }

            //קדם את מספר הדגימה
            this.sample++;
            this.mySurveyObj.currentSample = sample;

            slog("");
            slog("---------");
            slog("Sample No. > " + sample);

            //קבל את המיקום העדכני
            int tPos = myServo.GetPosition();
            int relativePos = tPos - mySurveyObj.StartPosition;
            if (relativePos < 0) relativePos += (int)this.RoundTicks;

            this.mySurveyObj.currentAbsolutePos = tPos;
            this.mySurveyObj.currentRelativePos = relativePos;

            slog("Abs. Pos. > " + this.mySurveyObj.currentAbsolutePos);
            slog("Rel. Pos. > " + this.mySurveyObj.currentRelativePos);

            //Display sample description:
            DisplaySmapleBegin(this.mySurveyObj);

            //ביצוע מדידה
            int tMeasure = -1;
            try
            {
                tMeasure = myDisto.MeasureOnce((int)this.nudBTtry.Value);
                //slog("***** Disto...");
                //slog("   Response: " + myDisto.LastResponse);
                slog("Distance > " + tMeasure);

                if (tMeasure == -1)
                {
                    slog("Trying again*******");
                    tMeasure = myDisto.MeasureOnce((int)this.nudBTtry.Value);
                    slog("   Disto...");
                    slog("   Response: " + myDisto.LastResponse);
                    slog("   Distance: " + tMeasure);

                }
            }
            catch (Exception et)
            {
                tMeasure = -1;
                //slog("Disto...");
                //slog("Response: " + myDisto.LastResponse);
                slog("Error Measure: " + et.Message);
            }
            finally
            {
                this.mySurveyObj.currentDist = tMeasure;
            }

            try
            {
                this.tspbSurvey.Value = sample;
                //this.statusStrip1.Refresh();
            }
            catch { }

            //Display sample results:
            DisplaySampleEnd(this.mySurveyObj);

            //בדיקת תנאי עצירה - סיום התהליך
            if (sample == mySurveyObj.SamplesNum)
            {
                TerminateSurvey();
                return;
            }

            //Move to next position:
            nextPos = (int)(sample * mySurveyObj.SampleTicksGap);

            try
            {
                this.supposedPos = nextPos + mySurveyObj.StartPosition;
                slog("Move to >> " + supposedPos);
                if (this.supposedPos < 0)
                {
                    this.supposedPos += (int)this.RoundTicks;
                    slog("Move to >>> " + supposedPos);
                }
                
                if (this.supposedPos > (int)this.RoundTicks)
                {
                    this.supposedPos -= (int)this.RoundTicks;
                    slog("Move to >>>> " + supposedPos);
                }

                myServo.MoveAbsolute(this.supposedPos);
            }
            catch
            {
                //TODO: Handle moving error 
            }
            finally
            {
                timerLoop.Start();
            }
        }



        private void slog(string p)
        {
            this.lbSurveyLog.Items.Insert(0, p);
            this.lbSurveyLog.Refresh();
        }

        private void DisplaySmapleBegin(SurveyObject srvObj)
        {
            int i = srvObj.currentSample;

            //Calculate the relative angle (in degrees)
            double relativePos = mySurveyObj.currentRelativePos;
            double tAngle = (relativePos / 1000);

            //important: in this new version we no more adding minus (-) to upward survey, 
            //but the web dbadapter does this in his turn!

            //Display data in Track TabPage
            tbRelAngle.Text = tAngle.ToString("0.00");
            tbRelTicks.Text = relativePos.ToString();
            tbAbsTicks.Text = srvObj.currentAbsolutePos.ToString();
            tbCurrentDist.Text = "--------";
            lblCurrentSample.Text = i.ToString();
            panel5.Refresh();
            groupBox7.Refresh();

            //Display an orientaion line 
            this.boxDraw.AddRadialLine((float)tAngle);
        }

        private void DisplaySampleEnd(SurveyObject srvObj)
        {
            bool distErr = false;
            int i = srvObj.currentSample;

            //Calculate the distance (in mm)
            int tDistance = srvObj.currentDist - (int)dstOffsetNum.Value;
            if (tDistance <= mySurveyObj.minDist)
            {
                distErr = true;
                mySurveyObj.ErrorLines++;
                this.tbFalseSamples.Text = mySurveyObj.ErrorLines.ToString();
            }

            //Display data in Track TabPage
            if (!distErr) tbCurrentDist.Text = tDistance.ToString();
            else tbCurrentDist.Text = "Error";

            //Add Data to listboxes
            //string newLine = tbRelAngle.Text + ", " + tbCurrentDist.Text+ ", "+ tbRelTicks.Text + ", "+ tbAbsTicks.Text;
            if (!distErr) filesTool.AddSample(tbRelAngle.Text, tbCurrentDist.Text);

            //Display progress information
            this.tsProgressInfo.Text = ((double)i / srvObj.SamplesNum).ToString("P") + " (" + i.ToString() + " of " + srvObj.SamplesNum.ToString() + ") Completed";
            tsProgressbar.Value = i;
            progressBar2.Value = i;

            //Calculate and display Time information כל זה דורש תיקון!!!
            TimeSpan elapsedTime = DateTime.Now - this.mySurveyObj.StartTime;
            ElapsedTimeLbl.Text = elapsedTime.ToString().Substring(0, 8);
            double finishSec = ((double)(elapsedTime.TotalSeconds / i) * (srvObj.SamplesNum - i));
            TimeSpan finishTime = TimeSpan.FromSeconds((int)finishSec);
            FinishTimeLbl.Text = finishTime.ToString().Substring(0, 8);

            //Draw Box
            float drawDist = (float)tDistance;
            this.boxDraw.AddPolarPoint(drawDist, (float)Convert.ToDouble(tbRelAngle.Text), i, !distErr);
            boxDraw.Redraw();
        }

        private void TerminateSurvey()
        {
            //Stop survey timer
            //this.SurveyTimer.Stop();
            //Turn Devices Off
            timerLoop.Stop();
            myServo.SwitchOff();
            ServoOn = false;
            myDisto.SwitchOn(false);

            if (!this.mySurveyObj.Stop) this.boxDraw.ClosePolygon();

            //Write last line
            filesTool.AddFooter();

            if (!this.mySurveyObj.Stop) //Survey Completed Successfully
            {
                //calculate error percentage
                double errPercentage = 100 * this.mySurveyObj.ErrorLines / this.mySurveyObj.SamplesNum;
                string strError = errPercentage.ToString() + "%";

                //Save survey data to file
                if (this.filesTool.WriteSurveyToFile(strError))
                {
                    //Proceed bore number
                    if (nudBoreNum.Value < 1000) nudBoreNum.Value++;
                }
            }
            NowWorking = false;
            timer2.Start();
            return;
        }

        private double GapFinder(double a, double b, double Alpha)
        {
            try
            {
                double cosA = Math.Cos(Alpha);
                double gap2 = (a * a) + (b * b) - (2 * a * b) * cosA;
                return Math.Sqrt(gap2);
            }
            catch { return -1; }
        }
    }
}
