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
    public class SurveyObject
    {
        public int SamplesNum;
        public int StartPosition;
        public int SampleTicksGap;
        public int MaxGap;
        public int currentSample;
        public int currentAbsolutePos;
        public int currentDist;
        public bool Stop;
        public DateTime StartTime;
        public int ErrorLines;
        public int minDist;
        public int timeoutNum = 0;
        public string FileName;
        public string FolderPath;
        public int currentRelativePos;
    }
    
    public partial class Form1 : Form
    {
        //properties:

        public int CurrentFieldID;
        public SurveyObject mySurveyObj = new SurveyObject();
        private Sigma7Adapter myServo; //It's the new one
        private DistoAdapter myDisto;
        private BoxDrawer boxDraw;
        private BoxDrawer previewDrawer;
        private int supposedPos = 0;
        private bool _DistoConnected = false;
        private bool _ServoConnected = false;
        private bool _ServoOn = false;
        private bool _NowWorking = false;

        private bool NowWorking
        {
            get
            {
                return _NowWorking;
            }
            set
            {
                if (value) //Working...
                {
                    _NowWorking = true;
                    groupBox1.Enabled = false;
                    groupBox2.Enabled = false;
                    groupBox4.Enabled = false;
                    groupBox6.Enabled = false;
                    groupBox11.Enabled = false;
                    groupBox13.Enabled = false;
                    groupBox14.Enabled = false;

                    toolTip1.SetToolTip(btnStartSurvey, "Abort Survey");
                    btnStartSurvey.ImageIndex = 1;
                    btnStopSurvey.Enabled = true;

                    tsProgressbar.Visible = true;
                    tsProgressInfo.Visible = true;
                }
                else //work finished
                {
                    _NowWorking = false;
                    groupBox1.Enabled = true;
                    groupBox2.Enabled = true;
                    groupBox4.Enabled = true;
                    groupBox6.Enabled = true;
                    groupBox11.Enabled = true;
                    groupBox13.Enabled = true;
                    groupBox14.Enabled = true;


                    toolTip1.SetToolTip(btnStartSurvey, "Start Survey");
                    btnStartSurvey.ImageIndex = 0;
                    btnStopSurvey.Enabled = false;

                    tsProgressbar.Visible = false;
                    tsProgressInfo.Visible = false;
                }
            }
        }

        private bool ServoConnected
        {
            get
            {
                return _ServoConnected;
            }
            set
            {
                if (value) //Servo is connected
                {
                    _ServoConnected = true;
                    srvConnectedLbl.Text = "מחובר";
                    srvConnectedPct.Visible = true;
                    srvConnectBtn.Text = "התנתק";
                    srvConStatus.Enabled = true;
                    groupBox11.Enabled = true;
                    groupBox13.Enabled = true;
                }
                else //Servo is disconnected
                {
                    ServoOn = false;
                    _ServoConnected = false;
                    srvConnectedLbl.Text = "מנותק";
                    srvConnectedPct.Visible = false;
                    srvConnectBtn.Text = "התחבר";
                    label51.Text = "-------";
                    srvCOMlbl.Text = "";
                    srvConStatus.Enabled = false;
                    groupBox11.Enabled = false;
                    groupBox13.Enabled = false;
                }
            }
        }

        private bool ServoOn
        {
            get
            {
                if (this.myServo == null) return false;
                if (!ServoConnected) return false;

                return this.myServo.IsOn();
            }
            set
            {
                if (value) //Servo is On
                {
                    tsslOnOff.Text = hndOnOfflbl.Text = "On";
                    srvOnStatus.Enabled = true;
                }
                else //Servo is Off
                {
                    tsslOnOff.Text = hndOnOfflbl.Text = "Off";
                    srvOnStatus.Enabled = false;
                }
            }
        }

        private bool DistoConnected
        {
            get
            {
                return _DistoConnected;
            }
            set
            {
                if (value) //Disto is connected
                {
                    _DistoConnected = true;
                    dstConnectedLbl.Text = "מחובר";
                    dstConnectedPct.Visible = true;
                    dstConnectBtn.Text = "התנתק";
                    //dstOnOffBtn.Enabled = true;
                    dstConStatus.Enabled = true;
                    dstSerialLbl.Text = myDisto.GetSerial();
                    groupBox14.Enabled = true;

                }
                else //Disto is disconnected
                {
                    _DistoConnected = false;
                    dstConnectedLbl.Text = "מנותק";
                    dstConnectedPct.Visible = false;
                    dstConnectBtn.Text = "התחבר";
                    dstCOMlbl.Text = "";
                    //dstOnOffBtn.Enabled = false;
                    dstConStatus.Enabled = false;
                    dstSerialLbl.Text = "";
                    groupBox14.Enabled = false;

                }
            }
        }
    }
}
