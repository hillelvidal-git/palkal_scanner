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
using System.Threading;

namespace LaserSurvey
{
    public partial class Form1 : Form
    {
        bool StopComSearch;
        FilesAdapter filesTool;
        string attribution;
        bool bAvoidZeros = true;
        //NewScannerBt bt;
        serial_adapter bt;
        List<string> btTransferLines;
        int transferCount;
        string lastServoCOM, lastDistoCOM;
        string stt_status = "";
        int surveyTotalSmaples;
        bool gui_prev_status = false;

        public Form1()
        {
            filesTool = new FilesAdapter();
            filesTool.LoadSetting();
            InitializeComponent();
            InitializeBoxCanvas();

            chkShowOldGUI_CheckedChanged(new object(), new EventArgs());

            SyncData();

            InitializeLaserSurvey();
            DisplayAttribution();

            

            filesTool.parse_done += actParseDone;
        }

        private void InitializeBoxCanvas()
        {
            //this.WindowState = FormWindowState.Maximized;
            pnlBoxCanvas.Size = new Size(640, 640);
            //int w = Screen.GetWorkingArea(this).Width;
            //float mid = ( w- 640) / 2;
            //pnlBoxCanvas.Location = new Point((int)mid, pnlBoxCanvas.Location.Y);
            int pad = lvDiary.Location.X + lvDiary.Width;
            pnlBoxCanvas.Location = new Point(this.ClientSize.Width / 2 - pnlBoxCanvas.Size.Width / 2 + pad / 2, this.ClientSize.Height / 2 - pnlBoxCanvas.Size.Height / 2);

            pnlBoxCanvas.Anchor = AnchorStyles.None;
        }

        private void pnlBoxCanvas_Paint(object sender, PaintEventArgs e)
        {
            Panel p = sender as Panel;

            // Create pen.
            Pen circlePen = new Pen(Color.Red, 2);
            circlePen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            // Create rectangle for ellipse.
            int padding = 5;
            Rectangle rect = new Rectangle(padding, padding, p.Width - padding - 4, p.Height - padding - 4);

            // Draw ellipse to screen.
            e.Graphics.DrawEllipse(circlePen, rect);
        }

        private void actParseDone(bool ok, string msg)
        {

            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                if (ok)
                {
                    msg = "������ ������:\n" + msg;
                    tt("saving files succeeded.");
                }
                else
                {
                    msg = "������ �����:\n" + msg;
                    tt("saving files failed!");
                }

                tt(msg);
                MessageBox.Show(msg);
                timerHideLb.Start();
            });
        }

        private void actBtDataRead(string s)
        {
            Console.WriteLine(">>> " + s);
            if (bt.isTransfering)
            {
                transferTOcount = 0;
                BtTransferDataRecieved(s);
                return;
            }

            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                lstBT.Items.Insert(0, s);
            });

            if (s.Contains("<<empty:ok>>"))
            {
                timerResetTransfer.Stop();
                tt("deleting mbed data succeeded.");
                if (IsHandleCreated) Invoke((MethodInvoker)delegate
                {
                    tt("parsing raw data...");
                    filesTool.ParseBtData();
                });
            }

            //bt.printf("\r\n<stt>%d,%d,%f</stt>",_srv.status, _srv.i, battery_voltage);
            string opening_tag = "<stt>";
            string closing_tag = "</stt>";
            try
            {
                if (s.Contains(opening_tag) && s.Contains(closing_tag))
                {
                    int k1 = s.IndexOf(opening_tag) + opening_tag.Length;
                    int k2 = s.IndexOf(closing_tag, k1);
                    string raw_status = s.Substring(k1, k2 - k1);
                    Console.WriteLine("Raw Status >> " + raw_status);

                    string[] words = raw_status.Split(',');

                    if (IsHandleCreated) Invoke((MethodInvoker)delegate
                    {

                        try
                        {
                            ///////////
                            //BATTERY//
                            ///////////
                            double b = Convert.ToDouble(words[2]);
                            if (b <= 16) pBatteryVoltage.BackColor = Color.Red;
                            else if (b <= 17) pBatteryVoltage.BackColor = Color.Orange;
                            else if (b <= 18.2) pBatteryVoltage.BackColor = Color.Yellow;
                            else pBatteryVoltage.BackColor = Color.Lime;

                            b = (b - 15) / 6;
                            tbBatteryV.Text = b.ToString("P");

                            ///////////
                            //STATUS///
                            ///////////
                            stt_status = words[0];
                            Console.WriteLine("Status >> " + stt_status);
                            if (stt_status == "0")
                            {
                                if (IsHandleCreated) Invoke((MethodInvoker)delegate
                                {
                                    pbScanning.Visible = false;
                                    lbScannerStatus.Text = "����� ����";
                                    pPbBack.Visible = false;
                                });
                            }
                            else if (stt_status == "1")
                            {
                                ///////////
                                //SURVEY///
                                ///////////
                                string srv_status = words[1];
                                Console.WriteLine("Survey >> " + srv_status);

                                if (IsHandleCreated) Invoke((MethodInvoker)delegate
                                {
                                    pbScanning.Visible = true;
                                    lbScannerStatus.Text = "����� ������: ����� ��' " + srv_status;
                                    pPbBack.Visible = true;
                                    try
                                    {
                                        double v = Convert.ToDouble(srv_status);
                                        v /= surveyTotalSmaples;
                                        v *= pPbBack.Width;
                                        pPbFore.Width = (int)v;
                                    }
                                    catch { }
                                });
                            }

                        }
                        catch
                        {

                        }
                    });

                }
            }
            catch { }




            //<<sample_is>>180<<sample_end>>

            if (stt_status == "1")
            {
                try
                {
                    if (s.Contains("<<sample_is>>") && s.Contains("<<sample_end>>"))
                    {
                        int k1 = s.IndexOf("<<sample_is>>") + 13;
                        int k2 = s.IndexOf("<<sample_end>>", k1);



                    }
                }
                catch { }
            }

        }

        private void actBtConnectionChanged(bool connected)
        {

            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                pnl_conn.BackColor = connected ? Color.Lime : Color.Silver;
                if (connected)
                {
                    //was_disconnected = false;
                    bt.Send("hv:BAT,1,");
                    bt.Send("hv:BAT,1,");
                    btnConnectNewBt.Text = "�����";
                    lbBtStatus.Text = "����� �����";

                }
                else if (!connected)
                {
                    //was_disconnected = true;
                    btnConnectNewBt.Text = "�����";
                    lbBtStatus.Text = "����� �����";
                }

                pBatteryVoltage.Visible = connected;
                pnlJog.Visible = connected;
            });
        }

        private void InitializeLaserSurvey()
        {
            this.boxDraw = new BoxDrawer(pnlBoxDraw);
            this.previewDrawer = new BoxDrawer(pnlBoxDraw);
            filesTool.LoadSetting();

            dstOffsetNum.Value = filesTool.setting.DistoOffsetMm;
            tbPipe.Value = filesTool.setting.DistoOffsetMm;

            if (filesTool.setting.UpwardSurvey)
            {
                trackBar2.Value = 0;
                cmbDirection.SelectedIndex = 0;
            }
            else
            {
                trackBar2.Value = 1;
                cmbDirection.SelectedIndex = 1;
            }

            trackBar2_ValueChanged(new object(), new EventArgs());

            trackBar3.Value = filesTool.setting.MovesNum;
            trackBar3_ValueChanged(new object(), new EventArgs());

            nudServoRpm.Value = filesTool.setting.ServoRpm;

            this.lastServoCOM = filesTool.setting.servoCOM;
            this.lastDistoCOM = filesTool.setting.distoCOM;
            tbNewBtCom.Text = filesTool.setting.distoCOM;

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
            
            
            filesTool.parse_done -= actParseDone;

            //Switch Devices Off
            try
            {
                if (bt != null)
                {
                    bt.actUpdateState -= actBtDataRead; 
                    bt?.Die();
                }

            }
            catch { }

            try
            {
                myServo?.SwitchOff();
            }
            catch { }
            try
            {
                myDisto?.SwitchOn(false);
            }
            catch { }


            //Save User's Prefferences
            filesTool.SaveSetting(new string[]
            {
            "DistoOffset: " + tbPipe.Value, //dstOffsetNum.Value,
            "UpwardSurvey: " + (cmbDirection.SelectedIndex == 0),
            "ServoRpm: " + (int)nudServoRpm.Value,
            "MovesNum: "+ (int)trackBar3.Value,
            "servoCOM: "+this.lastServoCOM,
            "distoCOM: "+ tbNewBtCom.Text // this.lastDistoCOM
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
                    MessageBox.Show("���� �� ����");
                    srvConnectBtn.Text = "�����";
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

                dstConnectBtn.Text = "����...";
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

                    //�� ������ ����� �� ������
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
                    MessageBox.Show("����� �� ����");
                    dstConnectBtn.Text = "�����";
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
                if (tvProjects.SelectedNode.Level == 3) //���
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
                this.lblNewSurveys.Text = "������ " + news.ToString() + " ������ �����";
            else
                this.lblNewSurveys.Text = "�� ������ ������ �����";

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
            //���� ����� �� �����
            bool upFlag = (trackBar2.Value == 0);
            pictUp.Visible = upFlag;
            pictDown.Visible = !upFlag;

            //��� �� ������
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
                case 0: lblElevation.Text = "������"; break;
                case 1: lblElevation.Text = "������"; break;
                case 2: lblElevation.Text = "������"; break;
            }
        }

        private void ��������ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lvDiary.SelectedItems.Count != 1) return;

            //delete file
            string filename = lvDiary.SelectedItems[0].Tag.ToString();
            if (MessageBox.Show("��� ��� ���� ������� ����� �� ���� ������?\n\n" + filename, "����� �����", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, MessageBoxOptions.RightAlign) == System.Windows.Forms.DialogResult.Yes)
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

        private void ��������������ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (lvDiary.SelectedItems.Count != 1) return;

            //invert survey
            string filename = lvDiary.SelectedItems[0].Tag.ToString();
            if (MessageBox.Show("��� ��� ���� ������� ����� �� ����� ������?\n\n" + filename, "����� �����", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2, MessageBoxOptions.RightAlign) == System.Windows.Forms.DialogResult.Yes)
            {
                filesTool.InvertSurvey(filename);
                string[] details;
                filesTool.GetSurveyDetails(filename, out details);
                if (details[3] == "True") lvDiary.SelectedItems[0].ImageIndex = 0; else lvDiary.SelectedItems[0].ImageIndex = 1;
            }
        }

        private void ����������ToolStripMenuItem_Click(object sender, EventArgs e)
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
                if (!filesTool.RenameSurvey(filename, newname)) throw new Exception("�� ���� ����� �� �� ������. ��� �� ���");
                msg = "�� ������ ����� ������";
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
                //this.label8.Text = "Loading Survey Preview...";
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
                //label8.Text = "Good samples: " + i;
            }
            catch
            {
                //this.label8.Text = "Cannot Draw Survey";
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

        //private void hScrollBar1_ValueChanged(object sender, EventArgs e)
        //{
        //    double percent = hScrollBar1.Value;
        //    percent /= 100;
        //    this.previewDrawer.ScaleBox(percent, new double[] { 0, 0 });
        //    this.label10.Text = hScrollBar1.Value + "%";
        //}


        private void BtnRunSurveyBT_Click(object sender, EventArgs e)
        {
            if (bt.IsConnected)
            {
                try
                {
                    int i;
                    if (!int.TryParse(tbFieldId.Text, out i)) throw new Exception("��� ���");
                    if (!int.TryParse(tbSrv.Text, out i)) throw new Exception("���� ���� ���");
                    if (!int.TryParse(tbOr.Text, out i)) throw new Exception("���� ���� ����� ����� (OR)");

                    bt.Send("hv:FLD," + tbFieldId.Text + ",");
                    bt.Send("hv:ORP," + tbOr.Text + ",");
                    bt.Send("hv:SRV," + tbSrv.Text + ",");

                    switch ((int)tbQuality.Value)
                    {
                        case 1:
                            bt.Send("hv:DEG,3,");
                            bt.Send("hv:DLY,80,");
                            surveyTotalSmaples = 120;
                            break;

                        case 2:
                            bt.Send("hv:DEG,2,");
                            bt.Send("hv:DLY,100,");
                            surveyTotalSmaples = 180;
                            break;

                        case 3:
                            bt.Send("hv:DEG,2,");
                            bt.Send("hv:DLY,150,");
                            surveyTotalSmaples = 180;
                            break;

                        case 4:
                            bt.Send("hv:DEG,1,");
                            bt.Send("hv:DLY,200,");
                            surveyTotalSmaples = 360;
                            break;

                        case 5:
                            bt.Send("hv:DEG,1,");
                            bt.Send("hv:DLY,300,");
                            surveyTotalSmaples = 360;
                            break;
                    }

                    if (cmbDirection.SelectedIndex == 0) bt.Send("hv:UPW,1");
                    else bt.Send("hv:UPW,0");

                    bt.Send("hv:RUN,1,");

                }
                catch (Exception ee)
                {
                    MessageBox.Show("Invalid Parameters: " + ee.Message);
                }
            }
            else
            {
                MessageBox.Show("Bluetooth scanner is not connected");
            }
        }

        private void BtnConnectNewBt_Click(object sender, EventArgs e)
        {
            //if (bt.IsConnected) //user asks to disconnect
            //{
            //    bt.Stop();
            //    return;
            //}

            ////else - user asks to connect
            //if (int.TryParse(tbNewBtCom.Text, out int i))
            //{
            //    bt.Stop();
            //    Thread.Sleep(50);
            //    bt.Run(tbNewBtCom.Text);
            //}
            //else
            //{
            //    MessageBox.Show("Com port not set");
            //}
        }

        private void LbImportBt_Click(object sender, EventArgs e)
        {
            if (!bt.IsConnected)
            {
                MessageBox.Show("����� ����� �����");
                return;
            }


            btTransferData.Enabled = false;
            btTransferLines = new List<string>();
            lbTransferOutput.Items.Clear();
            lbTransferOutput.Visible = true;
            transferTOcount = 0;
            timerResetTransfer.Start();
            tt("started.");
            transferCount = 0;
            bt.Send("hv:TRN,5528");
            tt("command sent to mbed.");
            bt.isTransfering = true;
        }

        private void tt(string line)
        {
            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                lbTransferOutput.Items.Add(line);
            });
        }

        private void BtTransferDataRecieved(string s)
        {
            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                tt("data recieved: " + transferCount++);
                //lstBT.Items.Insert(0, ">> TRANSFER: "+);
                btTransferLines.Add(s);
                //Console.WriteLine("transfer: " + s);

                if (s.Contains("<<trn:completed>>"))
                {
                    bt.isTransfering = false;
                    btTransferData.Enabled = true;
                    tt("<<completed>> recieved");

                    if (filesTool.SaveBtRawData(btTransferLines))
                    {
                        tt("saving raw data succeeded.");
                        bt.Send("hv:EMP,5528");
                        tt("command EMP sent to mbed.");
                    }
                    else
                    {
                        tt("saving raw data failed!");
                    }
                }

                else if (s.Contains("<<trn:cancelled>>"))
                {
                    bt.isTransfering = false;
                    btTransferData.Enabled = true;
                    tt("transfer cancelled!");
                }
            });
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void BtnPipe_Click(object sender, EventArgs e)
        {
            if (bt.IsConnected)
            {
                if (MessageBox.Show("��� ��� ������?", "����� ���� ������", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button1) == DialogResult.Yes)
                {
                    bt.Send("hv:DIP," + tbPipe.Value.ToString("0"));
                }
            }
        }

        int transferTOcount;
        private void TimerResetTransfer_Tick(object sender, EventArgs e)
        {
            if (transferTOcount++ > 15)
            {
                timerResetTransfer.Stop();
                BtTransferDataRecieved("<<trn:cancelled>><<interface timeout>>");
                Console.WriteLine("Cancelling transfer - timeout.");
            }
        }

        private void TimerHideLb_Tick(object sender, EventArgs e)
        {
            timerHideLb.Stop();
            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                lbTransferOutput.Visible = false;
                lbTransferOutput.Items.Clear();
            });
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            Text = "Laser Survey Bt   [" + Width + "/" + Height + "]";
        }

        private void BtnStep_Click(object sender, EventArgs e)
        {
            if (!bt.IsConnected)
            {
                MessageBox.Show("����� ����� �����");
                return;
            }

            bt.Send("hv:JOG,1,");
        }

        private void BtnTenSteps_Click(object sender, EventArgs e)
        {
            if (!bt.IsConnected)
            {
                MessageBox.Show("����� ����� �����");
                return;
            }

            bt.Send("hv:JOG,10,");
        }

        private void Btn45Steps_Click(object sender, EventArgs e)
        {
            if (!bt.IsConnected)
            {
                MessageBox.Show("����� ����� �����");
                return;
            }

            bt.Send("hv:JOG,45,");
        }


        private void TbFilter_ValueChanged(object sender, EventArgs e)
        {
            filesTool.filter_max_mm = tbFilter.Value * 500;
            lbFilerMm.Text = filesTool.filter_max_mm + " mm";
        }

        private void btnAutoCon_Click(object sender, EventArgs e)
        {
            
        }

        void StartAutoConnect()
        {
            int com;
            if (int.TryParse(tbNewBtCom.Text, out com))
            {
                if (bt != null)
                {
                    bt.Die();
                    bt.actDataRead = null;
                    bt.actUpdateState = null;
                    bt = null;
                    Thread.Sleep(500);
                }

                bt = new serial_adapter(com); //NewScannerBt();
                //bt.actBtConnectionChanged += actBtConnectionChanged;
                bt.actDataRead += actBtDataRead;
                bt.actUpdateState += serial_rec;
                bt.Run();
            }
        }

        private void serial_rec(string status)
        {
            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                tbBtStatus.Text = status;
                //panel1.BackColor = data=="lost"? Color.Gray : Color.Green;
                switch (status)
                {
                    case "lost":
                        pnlBtStatus1.BackColor = Color.Gray;
                        pnlBtStatus2.BackColor = Color.Gray;
                        ToggleScannerUi(false);
                        break;
                    case "trying":
                        pnlBtStatus2.BackColor = Color.Orange;
                        break;
                    case "failed":
                        pnlBtStatus2.BackColor = Color.Red;
                        break;
                    case "connected":
                        pnlBtStatus2.BackColor = Color.Yellow;
                        ToggleScannerUi(true);
                        break;
                    case "keep":
                        pnlBtStatus2.BackColor = Color.Blue;
                        break;
                    case "received":
                        pnlBtStatus1.BackColor = Color.LawnGreen;
                        break;

                }
            });
        }

        
        private void ToggleScannerUi(bool show)
        {
            pnl_settings.Visible = show;
            pnl_survey2.Visible = show;
            if (show) tbNewBtCom.BackColor = Color.White;
        }

        private void chkShowOldGUI_CheckedChanged(object sender, EventArgs e)
        {
            if (!chkShowOldGUI.Checked)
            {
                tabControl1.TabPages.Remove(tpDrawing);
                tabControl1.TabPages.Remove(tpOldScannerConn);
                tabControl1.TabPages.Remove(tpOldControl);
            }
            else
            {
                tabControl1.TabPages.Add(tpDrawing);
                tabControl1.TabPages.Add(tpOldScannerConn);
                tabControl1.TabPages.Add(tpOldControl);
            }
        }

        private void btnDisconnectBt_Click(object sender, EventArgs e)
        {
            
        }

        void DisconnectAutoConnect()
        {
            if (bt != null)
            {
                bt.Die();
                bt.actDataRead = null;
                bt.actUpdateState = null;
                bt = null;
                Thread.Sleep(500);
            }
        }

        private void button6_Click_1(object sender, EventArgs e)
        {
            
            if (bt != null)
            {
                Console.WriteLine("Stopping current bt...");
                bt.Die();
                bt.actDataRead = null;
                bt.actUpdateState = null;
                bt = null;
                Thread.Sleep(500);
            }

            tbNewBtCom.BackColor = Color.White;
            lbPortFound.Text = "����...";
            lbPortFound.Visible = true;

            port_finder pf = new port_finder();
            pf.actEnd += PortFound;
            pf.Run();
            
        }

        private void PortFound(int status, string msg)
        {
            if (IsHandleCreated) Invoke((MethodInvoker)delegate
            {
                switch (status)
                {
                    case 0:
                        lbPortFound.Text = msg + "  > X"; break;
                    case 1:
                        lbPortFound.Text = msg + "  > X"; 
                        tbNewBtCom.BackColor = Color.IndianRed;
                        tbNewBtCom.Text = "";
                        lbPortFound.Visible = false;
                        break;
                    case 2:
                        lbPortFound.Text = msg + "  > V";
                        tbNewBtCom.BackColor = Color.GreenYellow;
                        tbNewBtCom.Text = msg.Substring(3);
                        lbPortFound.Visible = false;
                        break;
                }
            });
        }

        private void chkAutoConnect_CheckedChanged(object sender, EventArgs e)
        {
            if (chkAutoConnect.Checked) StartAutoConnect();
            else DisconnectAutoConnect();
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