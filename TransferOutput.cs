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
    public partial class TransferOutput : Form
    {
        public TransferOutput()
        {
            InitializeComponent();
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            Clipboard.Clear();    //Clear if any old value is there in Clipboard        
            Clipboard.SetText(textBox1.Text); //Copy text to Clipboard
        }

        internal void SetOutput(List<string> lines)
        {
            foreach (string l in lines)
                textBox1.Text += l;
        }
    }
}
