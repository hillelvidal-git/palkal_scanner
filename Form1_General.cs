using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.IO.Ports;
using System.Configuration;
using System.Management;

namespace LaserSurvey
{
    public partial class Form1 : Form
    {
        bool StopComSearch;
        FilesAdapter filesTool;
        string attribution;
        bool bAvoidZeros = true;

        public Form1()
        {
            filesTool = new FilesAdapter();
            filesTool.LoadSetting();
            InitializeComponent();
            SyncData();

            InitializeLaserSurvey();
            DisplayAttribution();
        }

        string lastServoCOM, lastDistoCOM;

        private void InitializeLaserSurvey()
        {
            this.boxDraw = new BoxDrawer(drawPanel);
            this.previewDrawer = new BoxDrawer(pnlPreview);
            filesTool.LoadSetting();

            dstOffsetNum.Value = filesTool.setting.DistoOffsetMm;

            if (filesTool.setting.UpwardSurvey) trackBar2.Value = 0;
            else trackBar2.Value = 1;
            trackBar2_ValueChanged(new object(), new EventArgs());

            trackBar3.Value = filesTool.setting.MovesNum;
            trackBar3_ValueChanged(new object(), new EventArgs());

            nudServoRpm.Value = filesTool.setting.ServoRpm;

            this.lastServoCOM = filesTool.setting.servoCOM;
            this.lastDistoCOM = filesTool.setting.distoCOM;

            try
            {
                string[] att = filesTool.LoadLastField();
                tvProjects.SelectedNode = tvProjects.Nodes[att[0]].Nodes[att[1]].Nodes[att[2]].Nodes[att[3]];
                tvProjects.SelectedNode.Parent.Parent.Parent.Expand();
                tvProjects.SelectedNode.Parent.Parent.Expand();
                tvProjects.SelectedNode.Parent.Expand();
            }
            catch { }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Switch Devices Off
            try
            {
                myServo.SwitchOff();
                myDisto.SwitchOn(false);
            }
            catch { }

            //Save User's Prefferences
            filesTool.SaveSetting(new string[]
            {
            "DistoOffset: "+dstOffsetNum.Value,
            "UpwardSurvey: "+ (trackBar2.Value == 0),
            "ServoRpm: "+ (int)nudServoRpm.Value,
            "MovesNum: "+ (int)trackBar3.Value,
            "servoCOM: "+this.lastServoCOM,
            "distoCOM: "+this.lastDistoCOM
            });

        }

        private void svoConnectBtn_Click(object sender, EventArgs e)
        {
            if (!ServoConnected) //Connect to Servo
            {
                this.myServo = new Sigma7Adapter("192.168.1.1", 502);
                if (myServo.Connect())
                {
                    myServo.DoHoming();
                    ServoConnected = true;
                    if (this.ServoConnected) this.timer2.Start();
                }
                else
                {
                    MessageBox.Show("סרבו לא נמצא");
                    srvConnectBtn.Text = "התחבר";
                }
            }
            else //Disconnect from Servo
            {
                if (myServo.Disconnect())
                {
                    myServo = null;
                    ServoConnected = false;
                }
                else MessageBox.Show("Disconnection failed");
            }
        }

        private List<string> GetPortsNames(string firstPort)
        {
            List<string> ports = new List<string>();

            ports.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            ports.Remove(firstPort);
            ports.Insert(0, firstPort);
            return ports;
        }


        private void dstConnectBtn_Click(object sender, EventArgs e)
        {
            string message;
            if (!DistoConnected)
            {
                this.StopComSearch = false;
                this.btnStopSearchDisto.Visible = true;

                dstConnectBtn.Text = "מחפש...";
                dstConnectBtn.Refresh();

                //Search for disto
                string res = ""; string dstPort = "";
                DistoAdapter da;
                string fix;

                List<string> comPorts;
                if (chkDistoCom.Checked) comPorts = new List<string>() { "COM" + nudDistoCom.Value.ToString() };
                else comPorts = GetPortsNames(this.lastDistoCOM);

                foreach (string port in comPorts)
                {
                    fix = FixPortName(port);
                    dstCOMlbl.Text = "Trying: " + fix;

                    //תן אפשרות לעצור את החיפוש
                    //Application.DoEvents();
                    if (this.StopComSearch) break;

                    try
                    {
                        da = new DistoAdapter(fix);
                        res = da.Connect();
                        if (res.StartsWith("OK"))
                        {
                            dstConnectBtn.Refresh();
                            dstPort = fix;
                            myDisto = da;
                            DistoConnected = true;
                            break;
                        }
                        else da.Disconnect();

                    }
                    catch
                    {
                    }

                }
                if (dstPort == "")
                {
                    dstCOMlbl.Text = "";
                    MessageBox.Show("דיסטו לא נמצא");
                    dstConnectBtn.Text = "התחבר";
                }
                else
                {
                    dstCOMlbl.Text = dstPort;
                    this.lastDistoCOM = dstPort;
                }
            }

            else
            {
                //Disconnect from Disto
                message = myDisto.Disconnect();
                if (message.StartsWith("OK"))
                {
                    myDisto = null;
                    DistoConnected = false;
                }
                else MessageBox.Show(message);
            }
            this.btnStopSearchDisto.Visible = false;

        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return) SelectNextControl((Control)sender, true, true, true, true);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (NowWorking) StartBtn_Click(new object(), new EventArgs());
        }

        private void drawPanel_Paint(object sender, PaintEventArgs e)
        {
            this.boxDraw.Redraw();
        }

        private void drawPanel_MouseMove(object sender, MouseEventArgs e)
        {
            string tip = boxDraw.GetPtTip(e.X, e.Y);
            if (tip != "")
            {
                if (tip.Contains("Error")) this.ptInfoToolTip.BackColor = Color.LightGray;
                else this.ptInfoToolTip.BackColor = Color.LightSkyBlue;
                this.ptInfoToolTip.Show(tip, drawPanel, e.X, e.Y, 2000);
            }
        }

        private string FixPortName(string port)
        {
            int temp;
            string fix = port;
            for (int i = 3; i < fix.Length; i++)
            {
                if (!int.TryParse(fix.Substring(i, 1), out temp))
                    return fix.Substring(0, i);
            }
            return fix;
        }

        private void btnRefreshServoSpeed_Click(object sender, EventArgs e)
        {
            if (this.ServoConnected)
            {
                //Update servo speed:
                this.myServo.SetSpeed((int)this.nudServoRpm.Value);
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            try
            {
                if (tvProjects.SelectedNode.Level == 3) //שדה
                    filesTool.SaveLastField(tvProjects.SelectedNode);
            }
            catch { }

            DisplayAttribution();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            this.WorkOffline = true;
        }
        private bool WorkOffline;

        private void UpdateNewsNum()
        {
            int news = filesTool.GetNewsNumber();

            if (news > 0)
                this.lblNewSurveys.Text = "קיימות " + news.ToString() + " סריקות חדשות";
            else
                this.lblNewSurveys.Text = "לא קיימות סריקות חדשות";

            lblNewSurveys.Visible = true;
        }

        private void txtOrientaionPt_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(nudOrientaionPt.Text))
                nudOrientaionPt.BackColor = Color.FromKnownColor(KnownColor.Highlight);
            else
                nudOrientaionPt.BackColor = Color.FromKnownColor(KnownColor.Window);
        }

        private void nUDpos_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
                button2_Click_1(new object(), new EventArgs());
        }

        private void nUDstep_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
                button3_Click(new object(), new EventArgs());
        }

        private void tabPage5_Enter(object sender, EventArgs e)
        {
            LoadSurveysDiary();
        }

        private void LoadSurveysDiary()
        {
            lvDiary.Items.Clear();
            string[] details;
            string[] files; string msg;
            filesTool.GetNewSurveys(out files, out msg);
            foreach (string filename in files)
            {
                if (!filesTool.GetSurveyDetails(filename, out details)) continue;
                AddFileToDiary(details, filename);
            }
            UpdateNewsNum();
        }

        private void AddFileToDiary(string[] details, string filename)
        {
            try
            {
                string att = details[0];
                bool groupExists = false;

                string fieldName = att;
                ListViewGroup myGroup;

                ListViewItem itm = new ListViewItem();
                itm.Tag = filename;

                foreach (ListViewGroup grp in lvDiary.Groups)
                    if (grp.Name == fieldName)
                    {
                        myGroup = grp;
                        itm.Group = myGroup;
                        groupExists = true;
                        break;
                    }

                if (!groupExists)
                {
                    myGroup = new ListViewGroup(fieldName, "Field " + fieldName);
                    lvDiary.Groups.Add(myGroup);
                    itm.Group = myGroup;
                }

                itm.Text = details[1];
                itm.ToolTipText = "OR: " + details[2] + "   [" + details[4] + "]";
                if (details[3] == "True") itm.ImageIndex = 0; else itm.ImageIndex = 1;

                lvDiary.Items.Add(itm);
            }
            catch
            {
                //...
            }
        }

        private void button8_Click_1(object sender, EventArgs e)
        {
            this.StopComSearch = true;
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            SyncData();
        }

        private void SyncData()
        {
            SyncWizard sw = new SyncWizard(this.filesTool, this.tvProjects.Nodes, this.lblTreeTime);
            sw.ShowDialog();

            LoadSurveysDiary();
        }

        private void nudBoreNum_ValueChanged(object sender, EventArgs e)
        {
            lbBore.Text = nudBoreNum.Value.ToString();
        }

        private void trackBar4_ValueChanged(object sender, EventArgs e)
        {
            label59.Text = trackBar4.Value.ToString() + "mm";
        }

        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {
            //כלפי התקרה או הרצפה
            bool upFlag = (trackBar2.Value == 0);
            pictUp.Visible = upFlag;
            pictDown.Visible = !upFlag;

            //צבע את הבחירה
            if (upFlag)
            {
                lblUp.ForeColor = Color.Blue;
                lblDown.ForeColor = Color.FromKnownColor(KnownColor.ControlText);
            }
            else
            {
                lblDown.ForeColor = Color.Blue;
                lblUp.ForeColor = Color.FromKnownColor(KnownColor.ControlText);
            }

        }

        private void trackBar3_ValueChanged(object sender, EventArgs e)
        {
            lblSamples.Text = trackBar3.Value.ToString();
            CalculateSteps(new object(), new EventArgs());
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            switch (trackBar1.Value)
            {
                case 0: lblElevation.Text = "תחתונה"; break;
                case 1: lblElevation.Text = "אמצעית"; break;
                case 2: lblElevation.Text = "עליונה"; break;
            }
        }

        private void מחקסריקהToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lvDiary.SelectedItems.Count != 1) return;

            //delete file
            string filename = lvDiary.SelectedItems[0].Tag.ToString();
            if (MessageBox.Show("האם אתה בטוח שברצונך למחוק את קובץ הסריקה?\n\n" + filename, "מחיקת סריקה", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, MessageBoxOptions.RightAlign) == System.Windows.Forms.DialogResult.Yes)
            {
                filesTool.DeleteSurvey(filename);
                lvDiary.SelectedItems[0].Remove();
                UpdateNewsNum();
            }
        }

        private void cmsDiary_Opening(object sender, CancelEventArgs e)
        {
            if (lvDiary.SelectedItems.Count != 1) e.Cancel = true;
        }

        private void הפוךכיווןסריקהToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lvDiary.SelectedItems.Count != 1) return;

            //invert survey
            string filename = lvDiary.SelectedItems[0].Tag.ToString();
            if (MessageBox.Show("האם אתה בטוח שברצונך להפוך את כיוון הסריקה?\n\n" + filename, "היפוך סריקה", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, MessageBoxOptions.RightAlign) == System.Windows.Forms.DialogResult.Yes)
            {
                filesTool.InvertSurvey(filename);
                string[] details;
                filesTool.GetSurveyDetails(filename, out details);
                if (details[3] == "True") lvDiary.SelectedItems[0].ImageIndex = 0; else lvDiary.SelectedItems[0].ImageIndex = 1;
            }
        }

        private void שנהמספרקדחToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lvDiary.SelectedItems.Count != 1) return;
            lvDiary.LabelEdit = true;
            lvDiary.SelectedItems[0].BeginEdit();
        }

        private void lvDiary_AfterLabelEdit(object sender, LabelEditEventArgs e)
        {
            string msg = "";
            ListView lv = (ListView)sender;
            try
            {
                string newname = e.Label;
                string filename = lvDiary.SelectedItems[0].Tag.ToString();
                if (!filesTool.RenameSurvey(filename, newname)) throw new Exception("לא ניתן לשנות את שם הסריקה. נסה שם אחר");
                msg = "שם הסריקה השתנה בהצלחה";
            }
            catch (Exception ee)
            {
                msg = ee.Message;
                e.CancelEdit = true;
            }
            finally
            {
                lv.LabelEdit = false;
                MessageBox.Show(msg);
            }
        }

        private void lvDiary_ItemActivate(object sender, EventArgs e)
        {
            if (lvDiary.SelectedItems.Count != 1) return;
            string filename = lvDiary.SelectedItems[0].Tag.ToString();
            string line = "";
            float r, a;
            int i = 0;

            try
            {
                this.previewDrawer.Clear();
                this.label8.Text = "Loading Survey Preview...";
                using (StreamReader sr = new StreamReader(filename))
                {
                    //skip file header
                    do
                    { line = sr.ReadLine(); } while (line != "DATA:");

                    do
                    {
                        line = sr.ReadLine();
                        if (ReadPreviewLine(line, out r, out a))
                            this.previewDrawer.AddPolarPoint(r, a, i++, true);
                    } while (!sr.EndOfStream);
                }
                this.previewDrawer.Redraw();
                this.previewDrawer.ClosePolygon();
                label8.Text = "Good samples: " + i;
            }
            catch
            {
                this.label8.Text = "Cannot Draw Survey";
            }

        }

        private bool ReadPreviewLine(string line, out float r, out float a)
        {
            try
            {
                int c = line.IndexOf(",");
                string strAngle = line.Substring(0, c);
                string strDist = line.Substring(c + 1);
                r = (float)Convert.ToDouble(strDist);
                a = (float)Convert.ToDouble(strAngle);
                return true;
            }
            catch
            {
                r = a = -1;
                return false;
            }
        }

        private void hScrollBar1_ValueChanged(object sender, EventArgs e)
        {
            double percent = hScrollBar1.Value;
            percent /= 100;
            this.previewDrawer.ScaleBox(percent, new double[] { 0, 0 });
            this.label10.Text = hScrollBar1.Value + "%";
        }

        string[] mbedFiles;
        string mbedDrive;
        private void BtnLookForFiles_Click(object sender, EventArgs e)
        {
            lbDrive.Text = "";
            lbFilesFound.Text = "";

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable)
                {
                    if (drive.VolumeLabel == "MBED_PALKAL")
                    {
                        mbedDrive = drive.Name;
                        lbDrive.Text = mbedDrive;
                        mbedFiles = FindMbedSurveys(drive.Name);
                        lbFilesFound.Text = mbedFiles.Length.ToString();
                        break;
                    }
                }
            }
        }

        private string[] FindMbedSurveys(string drive)
        {
            return Directory.GetFiles(drive, "*.PLK");
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            if (mbedFiles.Length == 0)
            {
                lbImportResults.Text = "לא נמצאו סריקות";
                return;
            }

            int good = 0;
            int bad = 0;
            string dest;

            foreach (string f in mbedFiles)
            {
                try
                {
                    dest = f.Replace(mbedDrive, filesTool.newsFolder + "\\").Replace(".PLK", ".dat");
                    File.Move(f, dest);
                    good++;
                }
                catch (Exception ee)
                {
                    Console.WriteLine("Import Error: " + ee.Message);
                    bad++;
                }
            }
            lbImportResults.Text = "יובאו בהצלחה:  " + good + "  סריקות.";
            if (bad > 0) lbImportResults.Text += "נכשלו:  " + bad + "  סריקות.";

            BtnLookForFiles_Click(new object(), new EventArgs());
        }


        private void btnServoResetAlarms_Click(object sender, EventArgs e)
        {
            try
            {
                this.myServo.ResetAlarms();
            }
            catch
            {

            }
        }






    }   //Class

    public sealed class MyListBox : ListBox
    {
        public MyListBox(int iheight)
        {
            ItemHeight = iheight;
        }
        public override int ItemHeight { get; set; }
    }

}       //Namespace