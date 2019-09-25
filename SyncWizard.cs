using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LaserSurvey
{
    public partial class SyncWizard : Form
    {
        PictureBox[] pbs;
        Label[] lbls;
        List<string> log;
        FilesAdapter myFA;

        TreeNodeCollection nodes;
        Label treeLbl;

        public SyncWizard(FilesAdapter fa, TreeNodeCollection TreeNodes, Label lblTreeTime)
        {
            this.myFA = fa;
            InitializeComponent();
            this.nodes = TreeNodes;
            this.treeLbl = lblTreeTime;

            pbs = new PictureBox[] { pb1, pb2, pb3, pb4};
            lbls = new Label[] { lbl1, lbl2, lbl3, lbl4};
            log = new List<string>();
        }

        public void DoSync()
        {
            string[] localNews;

            log.Add(DateTime.Now + "  Starting Syncronization...");
            string msg;
            string[] msgLong;
            int uploaded, errors;

            log.Add("Checking connection to server");
            pbs[0].Image = imageList1.Images[1]; //web
            pbs[0].Refresh();

            lbls[0].Enabled = true;
            if (!myFA.CheckServerConnection(out msg))
            {
                log.Add("Connection failed: " + msg);
                pbs[0].Image = imageList1.Images[3]; //error
                DownloadTree(out msg, false); //Download old tree
                return;
            }
            pbs[0].Image = imageList1.Images[0]; //done
            pbs[0].Refresh(); 
            log.Add("[OK]");


            log.Add("");
            log.Add("Downloading projects tree...");
            pbs[1].Image = imageList1.Images[4]; pbs[1].Refresh(); //working
            lbls[1].Enabled = true;
            if (!DownloadTree(out msg, true))
            {
                log.Add("Downloading Tree failed: " + msg);
                pbs[1].Image = imageList1.Images[3];
                return;
            }
            pbs[1].Image = imageList1.Images[0]; //done
            pbs[1].Refresh();
            log.Add("[OK]");


            log.Add("");
            log.Add("Retrieving local new surveys...");
            pbs[2].Image = imageList1.Images[4]; pbs[2].Refresh();  //working
            lbls[2].Enabled = true;
            if (!myFA.GetNewSurveys(out localNews, out msg))
            {
                log.Add("Retrieving local news failed: " + msg);
                pbs[2].Image = imageList1.Images[3]; pbs[2].Refresh();
                goto End;
            }
            pbs[2].Image = imageList1.Images[0]; //done
            pbs[2].Refresh();
            log.Add("[OK]" + "   Surveys found: " + localNews.Length);
            res3.Text = localNews.Length.ToString();

            if (localNews.Length > 0)
            {
                log.Add("");
                log.Add("Uploading local " + localNews.Length + " new surveys...");
                pbs[3].Image = imageList1.Images[4]; pbs[3].Refresh();   //working
                lbls[3].Enabled = true;
                myFA.UploadSurveys(localNews, this.progressBar1, out msgLong, out uploaded, out errors);
                log.AddRange(msgLong);
                log.Add("Total: " + uploaded + " uploaded, " + errors + " errors");
                res4.Text = uploaded.ToString();

                if (errors == 0)
                {
                    pbs[3].Image = imageList1.Images[0]; //done
                }
                else
                {
                    pbs[3].Image = imageList1.Images[2]; //errors
                }

            }
            else
            {
                log.Add("No points to upload.");
                pbs[3].Image = imageList1.Images[0]; //done
            }
        
            End:
            pbs[3].Refresh();
            log.Add("");
            log.Add("--End--");
        }

        private bool DownloadTree(out string msg, bool tryServer)
        {
            try
            {
                bool downloaded;
                BedekTreeAdapter.TreeAdapter ta = new BedekTreeAdapter.TreeAdapter();
                string time;
                ta.GetProjectsTree(tryServer, this.nodes, out time, out downloaded);
                this.treeLbl.Text = time;
                if (downloaded) this.treeLbl.ForeColor = Color.FromArgb(0, 0, 192);
                else this.treeLbl.ForeColor = Color.FromKnownColor(KnownColor.ControlText);

                msg = "";
                return true;
            }
            catch (Exception e)
            {
                msg = e.Message;
                return false;
            }
        }

        private void btnLog_Click(object sender, EventArgs e)
        {
            LogForm.LogForm lf = new LogForm.LogForm(this.log);
            lf.Show();
        }

        private void SyncWizard_Shown(object sender, EventArgs e)
        {
            //Application.DoEvents();
            this.DoSync();
        }
    }
}
