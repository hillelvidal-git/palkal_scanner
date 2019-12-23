using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace LaserSurvey
{
    public class FilesAdapter
    {
        List<string> surveyLines;
        string fileHeader = "Bedek Survey Data File";
        string fileFooter = "End Of Data";

        public string bedekFolder, logFolder, dataFolder, newsFolder, oldsFolder, delFolder;
        string LastFieldFile, SettingFile, ServoParmsFile;
        string surveyAttribution;
        string surveyTime;

        public Action<bool, string> parse_done;

        internal SurveySetting setting;

        public FilesAdapter()
        {
            DefinePaths();
            setting = new SurveySetting();
        }

        private void DefinePaths()
        {
            bedekFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\Bedek";
            dataFolder = bedekFolder + "\\Survey\\SurveyData";
            logFolder = bedekFolder + "\\Logs";
            LastFieldFile = logFolder + "\\Survey_Field.txt";
            SettingFile = logFolder + "\\Survey_Setting.txt";
            ServoParmsFile = bedekFolder + "\\Survey\\Servo_Params.txt";
            newsFolder = dataFolder + "\\News";
            oldsFolder = dataFolder + "\\Olds";
            delFolder = dataFolder + "\\Deleted";

            EnsureDirectory(bedekFolder);
            EnsureDirectory(dataFolder);
            EnsureDirectory(logFolder);
            EnsureDirectory(newsFolder);
            EnsureDirectory(oldsFolder);
            EnsureDirectory(delFolder);
        }

        private void EnsureDirectory(string d)
        {
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
        }

        internal void SetSurveyDetails
            (string attribution,
            int fieldId,
            string boreName,
            string orPoint,
            bool isUpward,
            int elevation)
        {
            this.surveyAttribution = attribution;
            this.surveyTime = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            surveyLines = new List<string>();
            surveyLines.Add(this.fileHeader);
            surveyLines.Add("Attribution: " + attribution);
            surveyLines.Add("Machine: " + Environment.MachineName + " [" + Environment.UserName + "]");
            surveyLines.Add("Time: " + this.surveyTime);
            surveyLines.Add("Field ID: " + fieldId.ToString());
            surveyLines.Add("Bore Name: " + boreName);
            surveyLines.Add("OR Point: " + orPoint);
            surveyLines.Add("IsUpward: " + isUpward.ToString());
            surveyLines.Add("Elevation: " + elevation.ToString());
            surveyLines.Add("");
            surveyLines.Add("Angle (Degrees), Distance (MM)");
            surveyLines.Add("----------------------------------");
            surveyLines.Add("DATA:");
        }

        internal void ParseBtData()
        {
            if (!File.Exists(dataFolder + "\\bt_raw.dat"))
            {
                parse_done?.Invoke(false, "אין סריקות לשמור");
                return;
            }

            string line;
            List<string> srv_lines;
            int written = 0;
            bool error_occured = false;
            bool dataexists;

            using (StreamReader sr = new StreamReader(dataFolder + "\\bt_raw.dat"))
            {
                do
                {
                    try
                    {
                        srv_lines = new List<string>();
                        dataexists = false;
                        do
                        {
                            line = sr.ReadLine();
                            srv_lines.Add(line);
                            if (line.Contains("<<hv:newsurvey>>")) dataexists = true;

                        } while (!sr.EndOfStream && line != "End Of Data");

                        if (dataexists) //skip empty files
                        {
                            WriteBtSurveyFile(srv_lines, written);
                            written++;
                        }
                    }
                    catch (Exception e)
                    {
                        error_occured = true;
                        MessageBox.Show(e.Message);
                    }

                } while (!sr.EndOfStream);
            }

            if (!error_occured) File.Delete(dataFolder + "\\bt_raw.dat");
            parse_done?.Invoke(true, "קבצים נשמרו: "+ written);

            return;
        }

        private void WriteBtSurveyFile(List<string> srv, int i)
        {
            string filename = "[" + i + "][" + DateTime.Now.ToString().Replace("/", ".").Replace(":", ".") + "].DAT";

            bool begin = false;
            using (StreamWriter sw = new StreamWriter(this.newsFolder + "\\" + filename))
            {
                foreach (string l in srv)
                {
                    if (begin)
                    {
                        if (l.Contains("Time:")) sw.WriteLine(EnsureValidTime(l));
                        else if (!l.Contains("<<hv:failed>>")) sw.WriteLine(l);
                        if (l.Contains("End Of Data")) break;
                    }
                    else if (l.Contains("<<hv:newsurvey>>")) begin = true;
                }
            }
        }

        private string EnsureValidTime(string time_line)
        {
            string t = time_line.Substring(5);
            try
            {
                DateTime dt = Convert.ToDateTime(t);
                if (dt.Year < 2000) throw new Exception();
                return time_line;
            }
            catch
            {
                return "Time: " +DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            }
        }

        internal void ClearDate()
        {
            this.surveyLines.Clear();
        }

        internal bool WriteSurveyToFile(string err)
        {
            string filename = "";
            if (!Directory.Exists(this.newsFolder))
                Directory.CreateDirectory(this.newsFolder);

            try
            {
                //Determine the file name
                filename = "[" + this.surveyAttribution.Replace("\\", ".") + "][" + Environment.MachineName + "][" + this.surveyTime.Replace("/", ".").Replace(":", ".") + "].DAT";

                //Write all data to file
                File.WriteAllLines(this.newsFolder + "\\" + filename, this.surveyLines);

                //play a sound
                try
                {
                    System.Media.SoundPlayer sp = new System.Media.SoundPlayer(@"c:\Windows\Media\tada.wav");
                    sp.Play();
                }
                catch { }

                //Inform the user
                MessageBox.Show("הסקר הושלם בהצלחה, הנתונים נשמרו לקובץ:\n" + filename + "\n\nאחוז המדידות השגויות:  " + err, "Survey Complete");

                return true;
            }
            catch (Exception ef)
            {
                MessageBox.Show("שמירת הקובץ נכשלה.\n" + ef.Message + "\n" + this.newsFolder + "\n" + filename);
                return false;
            }
        }

        private bool UploadNew(string surveyFileName, BedekSurveyWebService.SurveyServiceSoapClient client, out string msg)
        {
            int id;
            bool bUpload;
            BedekSurveyWebService.ArrayOfString fileLines;

            using (StreamReader sr = new StreamReader(surveyFileName))
            {
                try
                {
                    //Read file into lines array
                    fileLines = new BedekSurveyWebService.ArrayOfString();
                    while (!sr.EndOfStream) fileLines.Add(sr.ReadLine());

                    //Send data to server
                    msg = client.InsertNewSurvey(fileLines, out id);
                    bUpload = (msg == "OK");

                    if (bUpload)
                    {
                        //if (id < 1) bUpload = false;
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    msg = e.Message;
                    return false;
                }
            }
        }


        internal bool GetNewSurveys(out string[] files, out string msg)
        {
            try
            {
                files = Directory.GetFiles(this.newsFolder, "*.DAT", SearchOption.AllDirectories);
                msg = "OK";
                return true;
            }
            catch (Exception e)
            {
                msg = e.Message;
                files = new string[0];
                return false;
            }
        }


        internal void ArchiveSurvey(string source)
        {
            if (!Directory.Exists(this.oldsFolder))
                Directory.CreateDirectory(this.oldsFolder);

            string destination = source.Replace("News", "Olds");
            File.Move(source, destination);
        }


        internal void AddSample(string angleDeg, string distanceMM)
        {
            this.surveyLines.Add(angleDeg + ", " + distanceMM);
        }

        internal void AddFooter()
        {
            this.surveyLines.Add(this.fileFooter);
        }

        //GetFileDetails
        //SetFileDetails

        internal string CheckConnection()
        {
            try
            {
                BedekSurveyWebService.SurveyServiceSoapClient client = new BedekSurveyWebService.SurveyServiceSoapClient();
                client.TryIt();
                return "השירות זמין :-)\nניתן לשלוח סריקות חדשות";
            }
            catch (Exception e)
            {
                return "השירות איננו זמין\n\n" + e.Message;
            }
        }

        internal int GetNewsNumber()
        {
            try
            {
                string[] newFiles; string msg;
                GetNewSurveys(out newFiles, out msg);
                return newFiles.Length;
            }
            catch
            {
                return 0;
            }
        }

        internal bool GetSurveyDetails(string filename, out string[] details)
        {
            details = new string[5];
            using (StreamReader sr = new StreamReader(filename))
            {
                try
                {
                    string line, word1, word2;
                    int colon;
                    int det = 0;

                    line = sr.ReadLine();
                    //Check Header for authentication:
                    if (!line.Contains(this.fileHeader)) return false;

                    //Collect details:
                    do
                    {
                        //read next line and proceed the cursor
                        line = sr.ReadLine();

                        //use line data:
                        try
                        {
                            if (line == "DATA:") break;

                            //parse line
                            colon = line.IndexOf(":");
                            word1 = line.Substring(0, colon).Trim(); //attribute
                            word2 = line.Substring(colon + 1).Trim(); //value

                            //store detail
                            switch (word1)
                            {
                                case "Attribution":
                                    details[0] = word2; det++; break;
                                case "Bore Name":
                                    details[1] = word2; det++; break;
                                case "OR Point":
                                    details[2] = word2; det++; break;
                                case "IsUpward":
                                    details[3] = word2; det++; break;
                                case "Time":
                                    details[4] = word2; det++; break;
                            }
                        }
                        catch { }

                    } while (!sr.EndOfStream);

                    if (det < 5) return false; //missing data
                    else return true; //data obtained, continue to samples

                }
                catch
                {
                    return false;
                }
            } //using sr
        } //method

        internal void DeleteSurvey(string source)
        {
            string destination = source.Replace("News", "Deleted");
            if (!Directory.Exists(this.delFolder))
                Directory.CreateDirectory(delFolder);
            File.Move(source, destination);
        }

        internal string[] LoadLastField()
        {
            string file = this.LastFieldFile;
            if (File.Exists(file))
            {
                using (StreamReader sr = new StreamReader(file))
                    return new string[]{
                        sr.ReadLine(),
                        sr.ReadLine(),
                        sr.ReadLine(),
                        sr.ReadLine()};
            }
            return new string[1];
        }

        internal void SaveLastField(TreeNode n)
        {
            string file = this.LastFieldFile;
            string path = file.Substring(0, file.LastIndexOf("\\"));
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            using (StreamWriter sw = new StreamWriter(file))
            {
                sw.WriteLine(n.Parent.Parent.Parent.Name);
                sw.WriteLine(n.Parent.Parent.Name);
                sw.WriteLine(n.Parent.Name);
                sw.WriteLine(n.Name);
            }
        }


        internal bool LoadSetting()
        {
            LoadDefaultSetting();

            try
            {
                using (StreamReader sr = new StreamReader(this.SettingFile))
                {
                    do
                    {
                        string line = sr.ReadLine();

                        if (line.StartsWith("DistoOffset"))
                        {
                            this.setting.DistoOffsetMm = Convert.ToInt32(line.Substring(line.IndexOf(":") + 1));
                        }
                        else if (line.StartsWith("ServoRpm"))
                        {
                            this.setting.ServoRpm = Convert.ToInt32(line.Substring(line.IndexOf(":") + 1));
                        }
                        else if (line.StartsWith("MovesNum"))
                        {
                            this.setting.MovesNum = Convert.ToInt32(line.Substring(line.IndexOf(":") + 1));
                        }
                        else if (line.StartsWith("servoCOM"))
                        {
                            this.setting.servoCOM = line.Substring(line.IndexOf(":") + 1).Trim();
                        }
                        else if (line.StartsWith("distoCOM"))
                        {
                            this.setting.distoCOM = line.Substring(line.IndexOf(":") + 1).Trim();
                        }
                        else if (line.StartsWith("UpwardSurvey"))
                        {
                            this.setting.UpwardSurvey = Convert.ToBoolean(line.Substring(line.IndexOf(":") + 1));
                        }

                    } while (!sr.EndOfStream);

                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LoadDefaultSetting()
        {
            this.setting.distoCOM = "COM1";
            this.setting.servoCOM = "COM2";
            this.setting.ServoRpm = 5;
            this.setting.UpwardSurvey = false;
            this.setting.MovesNum = 75;
            this.setting.DistoOffsetMm = 835;
        }

        public bool SaveSetting(string[] values)
        {
            try
            {
                if (!Directory.Exists(this.logFolder))
                    Directory.CreateDirectory(this.logFolder);

                using (StreamWriter sw = new StreamWriter(this.SettingFile))
                    foreach (string line in values)
                        sw.WriteLine(line);

                return true;
            }
            catch
            {
                return false;
            }
        }

        internal string[] GetServoParam(int Rpm)
        {
            List<string> col = new List<string>();
            string line;

            using (StreamReader sr = new StreamReader(this.ServoParmsFile))
            {
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    if (string.IsNullOrEmpty(line)) continue; //skip empty line
                    if (line.StartsWith("\\\\")) continue; //skip comment line
                    if (line.Contains("\\\\")) line = line.Substring(0, line.IndexOf("\\\\")); //trim inline comments
                    col.Add(line);
                }
            }

            //1st positioning command speed:(Rpm)
            col.Add("0224/" + Rpm.ToString());
            //Disable writing to EPROM:
            col.Add("021E/5");

            return col.ToArray();
        }

        internal bool CheckServerConnection(out string msg)
        {
            try
            {
                BedekSurveyWebService_1.SurveyServiceSoapClient client = new BedekSurveyWebService_1.SurveyServiceSoapClient();
                msg = client.TryIt();
                return true;
            }
            catch (Exception e)
            {
                msg = e.Message;
                return false;
            }
        }

        internal void UploadSurveys(string[] news, ProgressBar pb, out string[] msgLong, out int uploaded, out int errors)
        {
            List<string> msg = new List<string>();
            msg.Add("Uploading Surveys:");
            uploaded = 0; errors = 0;
            string name, ptMsg;
            BedekSurveyWebService.SurveyServiceSoapClient client = new BedekSurveyWebService.SurveyServiceSoapClient();

            try
            {
                pb.Maximum = news.Length;
                pb.Value = 0;

                foreach (string newOne in news)
                {
                    pb.Value++;
                    name = newOne.Substring(newOne.LastIndexOf("\\") + 1);
                    msg.Add("Survey: " + name);
                    msg.Add("Uploading...");

                    if (UploadNew(newOne, client, out ptMsg))
                    {
                        msg.Add("   [OK]");
                        ArchiveSurvey(newOne);
                        msg.Add("   [Archived]");
                        uploaded++;
                    }
                    else
                    {
                        errors++;
                        msg.Add("   [ERROR] " + ptMsg);
                    }
                }

            }
            catch (Exception e)
            {
                msg.Add("Error! >> " + e.Message);
            }
            finally
            {
                msgLong = msg.ToArray();
            }

        }


        internal void InvertSurvey(string filename)
        {
            List<string> lines = new List<string>();

            //קרא את הקובץ הישן
            using (StreamReader sr = new StreamReader(filename))
            {
                while (!sr.EndOfStream)
                    lines.Add(sr.ReadLine());
            }

            using (StreamWriter sw = new StreamWriter(filename))
            {
                foreach (string line in lines)
                {
                    if (line.Contains("IsUpward"))
                        sw.WriteLine(InvertUpward(line));
                    else
                        sw.WriteLine(line);
                }
            }
        }

        private string InvertUpward(string line)
        {
            if (line == "IsUpward: False") return "IsUpward: True";
            return "IsUpward: False";
        }


        internal bool RenameSurvey(string filename, string newBoreName)
        {
            try
            {
                string path = filename.Substring(0, filename.LastIndexOf("\\") + 1);
                string file = filename.Substring(filename.LastIndexOf("\\") + 1);
                string shortName = file.Substring(file.IndexOf("]") + 1);
                file = path + "[" + newBoreName.Trim() + "]" + shortName;

                if (WriteBoreName(filename, newBoreName.Trim()))
                {
                    //שנה את שם הקובץ בהתאם
                    File.Move(filename, file);
                }
                return true;
            }
            catch
            {
                return false;
            }

        }

        private bool WriteBoreName(string filename, string borename)
        {
            try
            {
                List<string> lines = new List<string>();

                //קרא את הקובץ הישן
                using (StreamReader sr = new StreamReader(filename))
                {
                    while (!sr.EndOfStream)
                        lines.Add(sr.ReadLine());
                }

                using (StreamWriter sw = new StreamWriter(filename))
                {
                    foreach (string line in lines)
                    {
                        if (line.Contains("Bore Name:"))
                            sw.WriteLine("Bore Name: " + borename);
                        else
                            sw.WriteLine(line);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal bool SaveBtRawData(List<string> btTransferLines)
        {
            try
            {
                using (StreamWriter sw = File.AppendText(dataFolder + "\\bt_raw.dat"))
                {
                    foreach (string line in btTransferLines) sw.Write(line);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }


    } //class

    struct SurveySetting
    {
        public int DistoOffsetMm;
        public int ServoRpm;
        public int MovesNum;
        public string servoCOM;
        public string distoCOM;
        public bool UpwardSurvey;
    }

} //namespace
