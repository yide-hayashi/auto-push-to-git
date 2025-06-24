using Project_auto_push_to_gitlab.cls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static System.Net.WebRequestMethods;
using System.Threading;


namespace Project_auto_push_to_gitlab
{
/// <summary>
/// 1140617 
/// </summary>
    public partial class Form1 : Form
    {
        bool closeflg=false;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //讓程式在工具列中隱藏
            this.ShowInTaskbar = false;
            //隱藏程式本身的視窗
            this.Hide();
            //通知欄顯示Icon
            notifyIcon.Visible = true;

            

            publicData.threadingDatas.Clear();
            string path = "D:\\VB\\2008";
           
            DateTime x = DateTime.Now;
            //0.塞入現有project path
            dataStore.getchildpathHasGitdir(path);
            Task.WaitAll(dataStore.tasksPath.ToArray());
            int sub_x = (DateTime.Now - x).Seconds;
            publicData.Getdevforpre10sec = true;
            //0.1 固定每 10s去撈現在執行中的dev 丟到running list
            Task.Run(() =>
            {
                while (true)
                {
                    if (!publicData.Getdevforpre10sec) break;
                    per10sGetDevList();
                    Thread.Sleep(10000);
                }
            });


        }
     
        void per10sGetDevList()
        {
            cls_reg reg = new cls_reg();
            var processes = Process.GetProcessesByName("devenv"); // exe名
            foreach (var process in processes)
            {
                string strreg = reg.RegReplace(process.MainWindowTitle, "", "\\s*(\\[\\w+])?(\\s*-)?\\s*Microsoft Visual Studio ?\\(\\w+\\)|\\s*(\\[\\w+])?(\\s*-)?\\s*Microsoft Visual Studio");
                //1.取代文字掉之後 只留專案編號
                var lq = publicData.threadingDatas.AsParallel().Where<ThreadingData>(x => x.devprocessName == strreg);
                if (strreg == "" || strreg == null)
                    continue;
                List<string> lqpath = dataStore.projectpath.AsParallel().Where(x => x.ToLower().IndexOf(strreg.ToLower()) > -1).ToList();
                if (lq.Count() == 0)
                {
                    lock (publicData.threadingDatas)
                    {
                        ThreadingData td = new ThreadingData();
                        td.Runthreading = false;
                        td.devprocessName = strreg;
                        td.devProcess = process;
                        //2.去找該專案path
                        if (lqpath.Count > 0) td.projectpath = lqpath[0].Replace("\\.git", "");
                        if (td.projectpath != "" && td.projectpath !=null)
                            publicData.threadingDatas.Add(td);

                    }
                }
            }

            foreach (ThreadingData td in publicData.threadingDatas)
            {
                if (!publicData.Getdevforpre10sec) break;

                if (!td.Runthreading )
                {
                    //3.丟出去thread去監控是否關閉 如果關閉自動彈dialog =Y 自動打開vs code/自動push
                       td.Runthreading = true;
                    lock(publicData.threadingDatas)
                        publicData.RunningThreading.Add
                            (
                                Task.Run(() =>
                                {
                                    watchingProcess(td);
                                })
                        );
                }
                   
            }

        }

        void watchingProcess(ThreadingData td)
        {
            while (!td.devProcess.HasExited)
            {
                if (!publicData.Getdevforpre10sec)
                    return;
                    Thread.Sleep(500);
            }

            
            
            //已關閉後 自動開vs code
            Process MyDos =new Process();
            MyDos.StartInfo.FileName = "cmd.exe"; //'DOS執行命令
            MyDos.StartInfo.UseShellExecute = false; // '設定可以作輸入及輸出導向
            MyDos.StartInfo.RedirectStandardInput = true;
            MyDos.StartInfo.RedirectStandardOutput = true;
            MyDos.StartInfo.CreateNoWindow = true; // '不產生DOS視窗
            MyDos.Start(); //'開始執行
            MyDos.StandardInput.WriteLine("cd D:");
            MyDos.StandardInput.WriteLine("cd "+td.projectpath);
            MyDos.StandardInput.WriteLine("code .");
            MyDos.CloseMainWindow();
            td.Runthreading = false;
            //1140617 明天來做自動切畫面
           
            Task.Run(async () =>
            {
                Thread.Sleep(3000);
                //等3秒後 在撈一次code的程式 抓到非原本的pid 再丟get出去
                Process[] prs = Process.GetProcessesByName("code");
                List<Process> lq = prs.AsParallel().Where((x) => x.MainWindowTitle.ToLower().IndexOf(td.devprocessName.ToLower()) > -1 ).ToList();
                int cnt = 0;
                while(lq.Count()==0 && cnt <=5)
                {
                    Thread.Sleep(1000);
                    prs = Process.GetProcessesByName("code");
                    lq = prs.AsParallel().Where((x) => x.MainWindowTitle.ToLower().IndexOf(td.devprocessName.ToLower()) > -1).ToList();
                    cnt++;
                }
                cls_GetWindowsData.Mainfunc();
                foreach (cls_GetWindowsData.portData item in cls_GetWindowsData.listport)
                {
                    await cls_GetWindowsData.SendRestfulGet(item.port.ToString(), td.devprocessName); 
                }
               
            });

            lock (publicData.threadingDatas)
                publicData.threadingDatas.Remove(td);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            //closefrm();
  
        }

        private void closefrm()
        {
            publicData.Getdevforpre10sec = false;

            foreach (var x in publicData.threadingDatas)
            {
                x.Runthreading = false;
            }
            Task.WaitAll(publicData.RunningThreading.ToArray());
            closeflg = true;
            this.Close();

        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            closefrm(); 
        }

        private void notifyIcon_DoubleClick(object sender, EventArgs e)
        {
            //讓程式在工具列中隱藏
            this.ShowInTaskbar = false;
            //隱藏程式本身的視窗
            this.Show();
            //通知欄顯示Icon
            notifyIcon.Visible = false;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!closeflg)
            {
                e.Cancel = true;

                //讓程式在工具列中隱藏
                this.ShowInTaskbar = false;
                //隱藏程式本身的視窗
                this.Hide();
                //通知欄顯示Icon
                notifyIcon.Visible = true;
            }
        }
    }

    public static class publicData
    {
        public static List<ThreadingData> threadingDatas= new List<ThreadingData>();
        /// <summary>
        /// 每10抓一次devList threading flag   false=stop true=running
        /// </summary>
        public static bool Getdevforpre10sec { get; set; } = false;
        public static List<Task> RunningThreading = new List<Task>() ;
    }

    public  class ThreadingData
    {
        /// <summary>
        /// 是否在執行
        /// </summary>
         public bool Runthreading { set; get; } = false;
        /// <summary>
        /// 專案路徑
        /// </summary>
        public string projectpath { set; get; }
        /// <summary>
        /// dev開發名稱
        /// </summary>
        public string devprocessName { set; get; }
        /// <summary>
        /// dev執行process
        /// </summary>
        public Process devProcess { set; get; }
    }


    public class dataStore
    {
        //public static List<string> data = new List<string>();
        public static List<string> projectpath = new List<string>();
        /// <summary>
        /// 
        /// </summary>
        public static List<Task> tasksPath = new List<Task>();

     
        public static string getchildpathHasGitdir(string path)
        {
          
            cls_reg reg = new cls_reg();
            string [] mainpath = Directory.GetDirectories(path);
            //濾頭
            string strchkreg=reg.reg(path, @"\\old\\|\\?\s*old\s*|\s*upgrade\s*|up\s*grade|\s*測試用\s*|\s*封存\s*");
            if (strchkreg!="")
                return "";
            foreach (string sub in mainpath)
            {
                var lqGitdir = Directory.GetDirectories(sub).Cast<string>().AsParallel().Where(x => x.IndexOf(".git") > -1).ToList();
                //濾child path
                 strchkreg = reg.reg(sub, @"\\old\\|\\?\s*old\s*|\s*upgrade\s*|up\s*grade|\s*測試用\s*|\s*封存\s*");
                if (lqGitdir.Count() > 0 && strchkreg=="")
                {
                    foreach(string item in lqGitdir)
                        lock (projectpath)
                        {
                            projectpath.Add(item);
                        }
                        
                }
                else
                {
                    //getchildpathHasGitdir(sub);
                    tasksPath.Add(
                        Task.Run(
                            () => getchildpathHasGitdir(sub)
                            )
                        );
                }
                    
            }
            
            return path;
        }
    }
}



