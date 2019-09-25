using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace LaserSurvey
{
    public partial class ServoControl : Form
    {
        ServoAdapter myServo;
        public ServoControl(object newServo)
        {
            this.myServo = (ServoAdapter)newServo;
            InitializeComponent();
            comboBox1.SelectedItem = 0;
            comboBox2.SelectedItem = 0;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (passwordTB.Text == "hvhv") groupBox1.Enabled = true;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string action="";
            switch (comboBox1.SelectedIndex)
            {
                case 0:
                    action = ServoAdapter.ServoIOAction.Read;
                    break;
                case 1:
                    action = ServoAdapter.ServoIOAction.Write;
                    break;
                case 2:
                    action = ServoAdapter.ServoIOAction.Write;
                    break;
            }
            
            string dataAdress;
            dataAdress = "0" + comboBox2.SelectedIndex.ToString() + textBox3.Text;

            string Response = "";
            try
            {
                Response = this.myServo.SendCommand(action, dataAdress, Convert.ToInt16(textBox1.Text));
            }
            catch (Exception er)
            {
                Response = "Error:\t" + er.Message;
            }
            textBox2.Text = Response;
        }

        private void passwordTB_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Return) 
                button2_Click(new object(),new EventArgs() );
        }
    }
}