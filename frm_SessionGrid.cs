using Project_auto_push_to_gitlab.cls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static System.Net.WebRequestMethods;
using System.Threading;
using KTCC.Core;
using System.Net.NetworkInformation;
using Project_auto_push_to_gitlab.model;
using System.Windows.Forms;
using Project_auto_push_to_gitlab.extension;
using static System.Windows.Forms.MonthCalendar;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Reflection;
using INOP1101.cls;

namespace Project_auto_push_to_gitlab
{
    /// <summary>
    /// 1140617 
    /// </summary>
    public partial class frm_SessionGrid : Form
    {
        bool closeflg = false;
        public frm_SessionGrid()
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

            LoggingData.InstanceData();

            toolStripSplitButton1.ButtonClick += (s, e) =>
            {
                toolStripSplitButton1.ShowDropDown();
            };

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

            Task.Run(() =>
            {
                GetDBSessionStatus("KTGH00");
            });
            Task.Run(() =>
            {
                GetDBSessionStatus("HPK210");
            });
            Task.Run(() =>
            {
                GetPGAStauts("KTGH00");
            });
            Task.Run(() =>
            {
                GetPGAStauts("HPK210");
            });

            Task.Run(() =>
            {
                GetSessionUsingStauts("KTGH00");
            });
            Task.Run(() =>
            {
                GetSessionUsingStauts("HPK210");
            });
        }
        void GetPGAStauts(string hsparea)
        {
            IEnumerable<cls_PAGData> result;
            using (KTConnectionController conn = new KTConnectionController())
            {
                conn.SettingConnectWithHspArea(hsparea);
                string strsql = "";
                strsql = "	SELECT 	";
                strsql += "	       sample_time,	";
                strsql += "	       ROUND (pga_target_bytes / 1024 / 1024, 1) AS pga_target_mb,	";
                strsql += "	       ROUND (pga_alloc_bytes / 1024 / 1024, 1) AS pga_alloc_mb,	";
                strsql += "	       ROUND (pga_inuse_bytes / 1024 / 1024, 1) AS pga_inuse_mb,	";
                strsql += "	       ROUND (pga_max_bytes / 1024 / 1024, 1) AS pga_max_mb,	";
                strsql += "	       over_alloc_cnt	";
                strsql += "	  FROM (SELECT SYSDATE AS sample_time,	";
                strsql += "	               MAX (	";
                strsql += "	                  CASE	";
                strsql += "	                     WHEN name = 'aggregate PGA target parameter' THEN VALUE	";
                strsql += "	                  END)	";
                strsql += "	                  pga_target_bytes,	";
                strsql += "	               MAX (CASE WHEN name = 'total PGA allocated' THEN VALUE END)	";
                strsql += "	                  pga_alloc_bytes,	";
                strsql += "	               MAX (CASE WHEN name = 'total PGA inuse' THEN VALUE END)	";
                strsql += "	                  pga_inuse_bytes,	";
                strsql += "	               MAX (CASE WHEN name = 'maximum PGA allocated' THEN VALUE END)	";
                strsql += "	                  pga_max_bytes,	";
                strsql += "	               MAX (CASE WHEN name = 'over allocation count' THEN VALUE END)	";
                strsql += "	                  over_alloc_cnt	";
                strsql += "	          FROM v$pgastat	";
                strsql += "	         WHERE name IN ('aggregate PGA target parameter',	";
                strsql += "	                        'total PGA allocated',	";
                strsql += "	                        'total PGA inuse',	";
                strsql += "	                        'maximum PGA allocated',	";
                strsql += "	                        'over allocation count'	";
                strsql += "	                        )	";
                strsql += "	        )	";

                IEnumerable<cls_PAGData> listSession = new List<cls_PAGData>();
                result = conn.DoSql<cls_PAGData>(strsql);
            }
            this.Invoke(new Action(() =>
            {
                switch (hsparea)
                {
                    case "KTGH00":
                        GridKTGH_PGA.DataSource = result;
                        GridKTGH_PGA.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                        break;
                    case "HPK210":
                        GridHPK_PGA.DataSource = result;
                        GridHPK_PGA.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                        break;
                    case "TSHIS":
                        break;
                }
            })
                );
        }
        /// <summary>
        /// session 使用量
        /// </summary>
        /// <param name="hsparea"></param>
        void GetSessionUsingStauts(string hsparea)
        {
            IEnumerable<cls_SessionUsingData> result;
            using (KTConnectionController conn = new KTConnectionController())
            {
                conn.SettingConnectWithHspArea(hsparea);
                string strsql = "";
                strsql = "	SELECT resource_name,	";
                strsql += "	       current_utilization,	";
                strsql += "	       max_utilization,	";
                strsql += "	       TO_CHAR(ROUND(current_utilization / TO_NUMBER(max_utilization) * 100, 2)) || '%' use_pct,	";
                strsql += "	       limit_value,	";
                strsql += "	       CASE	";
                strsql += "	         WHEN REGEXP_LIKE(limit_value, '\\d+')	";
                strsql += "	           THEN TO_CHAR(ROUND(current_utilization / TO_NUMBER(limit_value) * 100, 2)) || '%'	";
                strsql += "	         ELSE 'N/A'	";
                strsql += "	       END AS max_pct	";
                strsql += "	  FROM v$resource_limit	";
                strsql += "	 WHERE resource_name IN ('processes','sessions','transactions')	";
                strsql += "	 ORDER BY resource_name	";


                IEnumerable<cls_SessionUsingData> listSession = new List<cls_SessionUsingData>();
                result = conn.DoSql<cls_SessionUsingData>(strsql);
            }
            this.Invoke(new Action(() =>
            {
                switch (hsparea)
                {
                    case "KTGH00":
                        GridKTGH_sessionUsing.DataSource = result;
                        GridKTGH_sessionUsing.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                        break;
                    case "HPK210":
                        GridHPK_sessionUsing.DataSource = result;
                        GridHPK_sessionUsing.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                        break;
                    case "TSHIS":
                        break;
                }
            })
                );
        }

        void GetDBSessionStatus(string hsparea)
        {
            IEnumerable<cls_v_session> result;
            List<string> PGName = new List<string>();
            //PGName.Add("INOP1100.exe".ToLower());
            ////PGName.Add("toad.EXE".ToLower());
            //PGName.Add("inock111.exe");
            //PGName.Add("inock100.exe");
            //PGName.Add("ino1200.exe");
            //PGName.Add("ino2100.exe");
            //PGName.Add("ino2200.exe");
            //PGName.Add("infp1000.exe");
            //PGName.Add("infp2000.exe");
            //PGName.Add("samp160.exe");
            //PGName.Add("samp170.exe");
            PGName=frm_AddWatchProgressList.Getinf100_watch_Progress().Select( (x)=> x.itemnme1).ToList();
            string sqlPGName = "";
            PGName.ForEach((x) => sqlPGName += "'" + x + "',");
            sqlPGName = sqlPGName.Substring(0, sqlPGName.Length - 1);


            using (KTConnectionController conn = new KTConnectionController())
            {
                conn.SettingConnectWithHspArea(hsparea);
                string strsql = "";
                strsql = "	 SELECT	";
                strsql += "	    sid,	";
                strsql += "	    serial# serial,	";
                strsql += "	    audsid,	";
                strsql += "	    username,	";
                strsql += "	    osuser,	";
                strsql += "	    machine,	";
                strsql += "	    client_info,	";
                strsql += "	    program,	";
                strsql += "	    status,	";
                strsql += "	    logon_time,	";
                strsql += "	    process,	";
                strsql += "	    seconds_in_wait,	";
                strsql += "	    last_call_et,	";
                strsql += "	    event oracle_evt,	";
                strsql += "	    sql_id	";
                strsql += "	FROM v$session	";
                strsql += "	where LOWER(program) in ( " + sqlPGName + " )	";
                IEnumerable<cls_v_session> listSession = new List<cls_v_session>();
                result = conn.DoSql<cls_v_session>(strsql);
            }
            this.Invoke(new Action(() =>
            {
                switch (hsparea)
                {
                    case "KTGH00":
                        GridforKTGH.DataSource = result;
                        GridforKTGH.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                        break;
                    case "HPK210":
                        GridForHPK210.DataSource = result;
                        GridForHPK210.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
                        break;
                    case "TSHIS":
                        break;
                }

            }));

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
                List<string> lqpath = dataStore.projectpath.AsParallel().Where(x => x.ToLower().IndexOf("\\" + strreg.ToLower() + "\\.git") > -1).ToList();
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
                        if (td.projectpath != "" && td.projectpath != null)
                            publicData.threadingDatas.Add(td);

                    }
                }
            }

            foreach (ThreadingData td in publicData.threadingDatas)
            {
                if (!publicData.Getdevforpre10sec) break;

                if (!td.Runthreading)
                {
                    //3.丟出去thread去監控是否關閉 如果關閉自動彈dialog =Y 自動打開vs code/自動push
                    td.Runthreading = true;
                    lock (publicData.threadingDatas)
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
            Process MyDos = new Process();
            MyDos.StartInfo.FileName = "cmd.exe"; //'DOS執行命令
            MyDos.StartInfo.UseShellExecute = false; // '設定可以作輸入及輸出導向
            MyDos.StartInfo.RedirectStandardInput = true;
            MyDos.StartInfo.RedirectStandardOutput = true;
            MyDos.StartInfo.CreateNoWindow = true; // '不產生DOS視窗
            MyDos.Start(); //'開始執行
            MyDos.StandardInput.WriteLine("cd D:");
            MyDos.StandardInput.WriteLine("cd " + td.projectpath);
            MyDos.StandardInput.WriteLine("code .");
            MyDos.CloseMainWindow();
            td.Runthreading = false;
            //1140617 明天來做自動切畫面

            Task.Run(async () =>
            {
                Thread.Sleep(3000);
                //等3秒後 在撈一次code的程式 抓到非原本的pid 再丟get出去
                Process[] prs = Process.GetProcessesByName("code");
                List<Process> lq = prs.AsParallel().Where((x) => x.MainWindowTitle.ToLower().IndexOf(td.devprocessName.ToLower()) > -1).ToList();
                int cnt = 0;
                while (lq.Count() == 0 && cnt <= 10)
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

        private void WatchModel_CheckedChanged(object sender, EventArgs e)
        {
            List<Task> tasks = new List<Task>();

            Task tmptask = Task.Run(() =>
            {
                while (true)
                {
                    if (!WatchModel.Checked) break;
                    this.Invoke(new Action(() =>
                    {
                        lblmsg.Text = "更新中...";
                    }));
                    GetDBSessionStatus("KTGH00");
                    if (WatchModel.Checked)
                        Thread.Sleep(int.Parse(txtWatchTime.Text));
                }
            });

            tasks.Add(tmptask);

            tmptask = Task.Run(() =>
            {
                while (true)
                {
                    if (!WatchModel.Checked) break;
                    GetDBSessionStatus("HPK210");
                    if (WatchModel.Checked)
                        Thread.Sleep(int.Parse(txtWatchTime.Text));
                }
            });

            tasks.Add(tmptask);

            Task.Run(() =>
            {

                foreach (Task t in tasks)
                { t.Wait(); }

                this.Invoke(new Action(() =>
                {
                    lblmsg.Text = "完成";
                }));
            });
        }

        private void Grid_DoubleClick(object sender, EventArgs e)
        {
            if (GridforKTGH.CurrentCell != null && GridforKTGH.CurrentRow.Cells["sql_id"].Value != null)
            {
                int col = GridforKTGH.CurrentCell.ColumnIndex;
                string title = GridforKTGH.Columns[col].HeaderText;
                if (LoggingData.cls_Loggings.Where((x) => x.sql_id == GridforKTGH.CurrentRow.Cells["sql_id"].Value && x.hspArea.ToUpper() == "KTGH00").Count() == 0)
                {
                    LoggingData.cls_Loggings.Add(new cls_LoggingForDB()
                    {
                        Logvsdte = DateTime.Now.民國日期(),
                        Logtimer = DateTime.Now.timer(),
                        sql_id = GridforKTGH.CurrentRow.Cells["sql_id"].Value.ToString(),
                        RunningExeName = GridforKTGH.CurrentRow.Cells["program"].Value.ToString(),
                        RunningExePid = GridforKTGH.CurrentRow.Cells["process"].Value.ToString().Split(':')[0],
                        hspArea = "KTGH00",
                        client_info = GridforKTGH.CurrentRow.Cells["client_info"].Value.ToString()
                    });
                    LoggingData.InsertLogToDB(LoggingData.cls_Loggings[LoggingData.cls_Loggings.Count - 1]);
                }
                GetDBsqlStautsData getDBsqlStautsData = new GetDBsqlStautsData();
                getDBsqlStautsData.hsparea = "KTGH00";
                getDBsqlStautsData.sql_id = GridforKTGH.CurrentRow.Cells["sql_id"].Value.ToString();
                getDBsqlStautsData.client_info = GridforKTGH.CurrentRow.Cells["client_info"].Value.ToString();
                getDBsqlStautsData.RunningExeName = GridforKTGH.CurrentRow.Cells["program"].Value.ToString() + " " + GridforKTGH.CurrentRow.Cells["process"].Value.ToString().Split(':')[0];
                GetDBsqlStauts(getDBsqlStautsData);
                // MessageBox.Show("列タイトル = " + title);
            }
        }
        public class GetDBsqlStautsData
        {
            public string sql_id { get; set; }
            public string hsparea { get; set; }
            public string client_info { get; set; }
            public string RunningExeName { get; set; }
        }

        public void GetDBsqlStauts(GetDBsqlStautsData obj)
        {
            IEnumerable<cls_sql_idData> result;
            using (KTConnectionController conn = new KTConnectionController())
            {
                conn.SettingConnectWithHspArea(obj.hsparea);
                string strsql = "";
                //新增 LAST_LOAD_TIME 
                strsql = @"	SELECT sql_id,	";
                strsql += "	         sql_fulltext,	";
                strsql += "	       executions,	";
                strsql += "	       cpu_time/1e6      AS cpu_sec,	";
                strsql += "	       elapsed_time/1e6  AS elapsed_sec,	";
                strsql += "	       buffer_gets,	";
                strsql += "	       substr(last_load_time,1,10) runningDate,	";
                strsql += "	       substr(last_load_time,12,8) runningtime,	";
                strsql += "	       LAST_LOAD_TIME,	";
                strsql += "	      '" + obj.hsparea + "' hsp,	";
                strsql += "	      '" + obj.client_info + "' client_info,	";
                strsql += "	      '" + obj.RunningExeName + "' module	";
                strsql += "	       FROM v$sql v 	";
                strsql += "	WHERE sql_id =  :Sql_id	";
                strsql += "	order by last_load_time desc	";


                IEnumerable<cls_sql_idData> listSession = new List<cls_sql_idData>();
                result = conn.DoSql<cls_sql_idData>(strsql, new { Sql_id = obj.sql_id });
            }
            this.Invoke(new Action(() =>
            {
                Gridsql_stauts.DataSource = result;
                Gridsql_stauts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            }));

        }

        private void sqlidLogginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frm_Sql_idLoggingGrid grid = new frm_Sql_idLoggingGrid();
            grid.frm_Session = this;
            grid.Show();
        }

        private void Getsql_id_Click(object sender, EventArgs e)
        {
            if (txtsql_id.Text == "") return;

            if (LoggingData.cls_Loggings.Where((x) => x.sql_id == txtsql_id.Text && x.hspArea == cbHspArea.Text).Count() == 0)
            {
                LoggingData.cls_Loggings.Add(new cls_LoggingForDB()
                {
                    Logvsdte = DateTime.Now.民國日期(),
                    Logtimer = DateTime.Now.timer(),
                    sql_id = txtsql_id.Text,
                    RunningExeName = "手動查詢",
                    RunningExePid = "N/A",
                    hspArea = cbHspArea.Text
                });
                LoggingData.InsertLogToDB(LoggingData.cls_Loggings[LoggingData.cls_Loggings.Count - 1]);
            }
            GetDBsqlStautsData getDBsqlStautsData = new GetDBsqlStautsData();
            getDBsqlStautsData.hsparea = cbHspArea.Text;
            getDBsqlStautsData.sql_id = txtsql_id.Text;
            getDBsqlStautsData.client_info = "N/A";
            getDBsqlStautsData.RunningExeName = "手動查詢";
            GetDBsqlStauts(getDBsqlStautsData);

        }

        private void GridForHPK210_DoubleClick(object sender, EventArgs e)
        {
            if (GridForHPK210.CurrentCell != null && GridForHPK210.CurrentRow.Cells["sql_id"].Value != null)
            {
                int col = GridForHPK210.CurrentCell.ColumnIndex;
                string title = GridForHPK210.Columns[col].HeaderText;
                if (LoggingData.cls_Loggings.Where((x) => x.sql_id == GridForHPK210.CurrentRow.Cells["sql_id"].Value && x.hspArea.ToUpper() == "HPK210").Count() == 0)
                {
                    LoggingData.cls_Loggings.Add(new cls_LoggingForDB()
                    {
                        Logvsdte = DateTime.Now.民國日期(),
                        Logtimer = DateTime.Now.timer(),
                        sql_id = GridForHPK210.CurrentRow.Cells["sql_id"].Value.ToString(),
                        RunningExeName = GridForHPK210.CurrentRow.Cells["program"].Value.ToString(),
                        RunningExePid = GridForHPK210.CurrentRow.Cells["process"].Value.ToString().Split(':')[0],
                        hspArea = "HPK210"
                    });
                    LoggingData.InsertLogToDB(LoggingData.cls_Loggings[LoggingData.cls_Loggings.Count - 1]);
                }
                GetDBsqlStautsData getDBsqlStautsData = new GetDBsqlStautsData();
                getDBsqlStautsData.hsparea = "HPK210";
                getDBsqlStautsData.sql_id = GridForHPK210.CurrentRow.Cells["sql_id"].Value.ToString();
                getDBsqlStautsData.client_info = GridForHPK210.CurrentRow.Cells["client_info"].Value.ToString();
                getDBsqlStautsData.RunningExeName = GridForHPK210.CurrentRow.Cells["program"].Value.ToString() + " " + GridForHPK210.CurrentRow.Cells["process"].Value.ToString().Split(':')[0];
                GetDBsqlStauts(getDBsqlStautsData);
                // MessageBox.Show("列タイトル = " + title);
            }
        }

        private void Gridsql_stauts_DoubleClick(object sender, EventArgs e)
        {
            string strsql = "";
            IEnumerable<cls_DBMS_XPLAN_DISPLAY_CURSOR> result = null;
            if (Gridsql_stauts.CurrentCell != null && Gridsql_stauts.CurrentRow.Cells["sql_id"].Value != null)
            {
                using (KTConnectionController conn = new KTConnectionController())
                {
                    conn.SettingConnectWithHspArea(cbHspArea.Text);
                    strsql = "	SELECT *	";
                    strsql += "	      FROM TABLE(	";
                    strsql += "	      DBMS_XPLAN.DISPLAY_CURSOR ( :sql_id , 0,	";
                    strsql += "	                                    'ALLSTATS LAST +PEEKED_BINDS'	";
                    strsql += "	                                    )	";
                    strsql += "	                  )	";

                    result = conn.DoSql<cls_DBMS_XPLAN_DISPLAY_CURSOR>(strsql, new { sql_id = Gridsql_stauts.CurrentRow.Cells["sql_id"].Value });
                }

            }
            if (result != null && result.Count() != 0)
            {
                frmText frm = new frmText();
                string strplan = "";
                result.ToList().ForEach((x) => strplan += x.plan_table_output.ToString() + "\r\n");
                frm.txtList.Text = strplan;
                frm.txtList.SelectionStart = 0;
                frm.txtList.SelectionLength = 0;
                frm.txtList.ScrollToCaret();
                frm.Show();
                //塞入ino013_sub
                Insertino013_subDtlfgExplain_PLAN insertData = new Insertino013_subDtlfgExplain_PLAN();
                insertData.dtflg = "explain_Plan";
                insertData.dtflgname = "執行計畫查詢log";
                insertData.hsp = Gridsql_stauts.CurrentRow.Cells["hsp"].Value.ToString();
                insertData.sql_fulltext = Gridsql_stauts.CurrentRow.Cells["SQL_FULLTEXT"].Value.ToString();
                insertData.cpu_sec = Gridsql_stauts.CurrentRow.Cells["CPU_SEC"].Value.ToString();
                insertData.executions = Gridsql_stauts.CurrentRow.Cells["executions"].Value.ToString();
                insertData.explain_Plan = strplan;
                insertData.buffer_gets = Gridsql_stauts.CurrentRow.Cells["BUFFER_GETS"].Value.ToString();
                insertData.module = Gridsql_stauts.CurrentRow.Cells["MODULE"].Value.ToString();
                insertData.updte = DateTime.Now.民國日期();
                insertData.uptim = DateTime.Now.timer();
                insertData.runningDate = Gridsql_stauts.CurrentRow.Cells["runningDate"].Value.ToString().Replace("-", "");
                insertData.runningTime = Gridsql_stauts.CurrentRow.Cells["runningtime"].Value.ToString().Replace(":", "");
                insertData.client_info = Gridsql_stauts.CurrentRow.Cells["client_info"].Value.ToString();

                if (Selectino013_sub(insertData)) return;

                insertIno013_sub(insertData);
            }

        }
        bool Selectino013_sub(Insertino013_subDtlfgExplain_PLAN listSession)
        {
            List<ino013_sub> dtino013_sub;
            string strsql;
            strsql = "	select * from ino013_sub  		";
            strsql += "	where dtflg= 	:dtflg	";
            strsql += "	and dtcode= 	:hsp	";
            strsql += "	and sql_id= 	:sql_id	";
            strsql += "	and runningDate= 	:runningDate	";
            strsql += "	and runningTime= 	:runningTime	";
            using (KTConnectionController conn = new KTConnectionController())
            {
                conn.SettingConnectWithHspArea("KTGH00");
                dtino013_sub = conn.DoSql<ino013_sub>(strsql, new
                {
                    dtflg = listSession.dtflg,
                    hsp = listSession.hsp,
                    sql_id = Gridsql_stauts.CurrentRow.Cells["sql_id"].Value.ToString(),
                    runningDate = listSession.runningDate,
                    runningTime = listSession.runningTime
                }).ToList();

            }

            if (dtino013_sub.Count > 0)
                return true;


            return false;
        }

        void insertIno013_sub(Insertino013_subDtlfgExplain_PLAN listSession)
        {
            string strsql = "";
            //1150402 clob blob 都要用字串切割方式塞進去 無法用參數化
            List<string> strings = listSession.sql_fulltext.insertOver4000TextForCSharp();
            List<string> explain_Plan = listSession.explain_Plan.insertOver4000TextForCSharp();
            strsql = "	insert into ino013_sub	";
            strsql += " (";
            strsql += "	dtflg,dtflgname,DTCODE,	";
            strsql += "	SQL_ID,SQL_FULLTEXT,CPU_SEC,	";
            strsql += "	EXECUTIONS,BUFFER_GETS,MODULE,	";
            strsql += "	explain_Plan,UPDTE,UPTIM,UPOP,	";
            strsql += "	runningDate,runningTime,client_info	";
            strsql += "   )";
            strsql += "	values	";
            strsql += "	(:dtflg, :dtflgname,:hsp,	";
            strsql += "	:sql_id, ";
            strings.ForEach((x) => strsql += "TO_CLOB( '" + x.Replace("'", "''") + "' )" + (strings.IndexOf(x) != strings.Count - 1 ? " || " : ""));
            strsql += " ,:CPU_SEC,	";
            strsql += "	:EXECUTIONS,:BUFFER_GETS,:MODULE,	";
            explain_Plan.ForEach((x) => strsql += "TO_CLOB( '" + x.Replace("'", "''") + "')" + (explain_Plan.IndexOf(x) != explain_Plan.Count - 1 ? " || " : ""));
            strsql += "	,:updte,:uptim,'SUPER',	";
            strsql += "	:runningDate,:runningTime,:client_info	";
            strsql += "	)	";
            using (KTConnectionController conn = new KTConnectionController())
            {
                conn.SettingConnectWithHspArea("KTGH00");


                conn.DoSql(strsql, new
                {
                    dtflg = listSession.dtflg,
                    dtflgname = listSession.dtflgname,
                    hsp = listSession.hsp,
                    sql_id = Gridsql_stauts.CurrentRow.Cells["sql_id"].Value.ToString(),
                    CPU_SEC = listSession.cpu_sec,
                    EXECUTIONS = listSession.executions,
                    BUFFER_GETS = listSession.buffer_gets,
                    MODULE = listSession.module,
                    updte = listSession.updte,
                    uptim = listSession.uptim,
                    runningDate = listSession.runningDate,
                    runningTime = listSession.runningTime,
                    client_info = listSession.client_info
                });
            }
        }

        private void watchingUseing_CheckedChanged(object sender, EventArgs e)
        {
            List<Task> tasks = new List<Task>();
            Task tmptask = Task.Run(() =>
            {
                while (true)
                {
                    if (!watchingUseing.Checked) break;
                    GetPGAStauts("KTGH00");
                    if (watchingUseing.Checked)
                        Thread.Sleep(int.Parse(txtWatchTime.Text));
                }

            });
            tasks.Add(tmptask);

            tmptask = Task.Run(() =>
            {
                while (true)
                {
                    if (!watchingUseing.Checked) break;
                    GetPGAStauts("HPK210");
                    if (watchingUseing.Checked)
                        Thread.Sleep(int.Parse(txtWatchTime.Text));
                }
            });

            tmptask = Task.Run(() =>
           {
               while (true)
               {
                   if (!watchingUseing.Checked) break;
                   GetSessionUsingStauts("KTGH00");
                   if (watchingUseing.Checked)
                       Thread.Sleep(int.Parse(txtWatchTime.Text));
               }

           });
            tasks.Add(tmptask);

            tmptask = Task.Run(() =>
            {
                while (true)
                {
                    if (!watchingUseing.Checked) break;
                    GetSessionUsingStauts("HPK210");
                    if (watchingUseing.Checked)
                        Thread.Sleep(int.Parse(txtWatchTime.Text));
                }
            });

            tasks.Add(tmptask);
            Task.Run(() =>
            {

                foreach (Task t in tasks)
                { t.Wait(); }

                this.Invoke(new Action(() =>
                {
                    lblmsg.Text = "PGA/session 監視停止";
                }));
            });


        }

        private void addWatchingProgressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            frm_AddWatchProgressList frm =new frm_AddWatchProgressList();
            frm.Show();
        }
    }

    public static class publicData
    {
        public static List<ThreadingData> threadingDatas = new List<ThreadingData>();
        /// <summary>
        /// 每10抓一次devList threading flag   false=stop true=running
        /// </summary>
        public static bool Getdevforpre10sec { get; set; } = false;
        public static List<Task> RunningThreading = new List<Task>();
    }

    public class ThreadingData
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
            string[] mainpath = Directory.GetDirectories(path);
            //濾頭
            string strchkreg = reg.reg(path, @"\\old\\|\\?\s*old\s*|\s*upgrade\s*|up\s*grade|\s*測試用\s*|\s*封存\s*");
            if (strchkreg != "")
                return "";
            foreach (string sub in mainpath)
            {
                var lqGitdir = Directory.GetDirectories(sub).Cast<string>().AsParallel().Where(x => x.IndexOf(".git") > -1).ToList();
                //濾child path
                strchkreg = reg.reg(sub, @"\\old\\|\\?\s*old\s*|\s*upgrade\s*|up\s*grade|\s*測試用\s*|\s*封存\s*");
                if (lqGitdir.Count() > 0 && strchkreg == "")
                {
                    foreach (string item in lqGitdir)
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



