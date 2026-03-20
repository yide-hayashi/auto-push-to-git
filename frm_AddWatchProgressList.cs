using KTCC.Core;
using Project_auto_push_to_gitlab.extension;
using Project_auto_push_to_gitlab.model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.MonthCalendar;

namespace Project_auto_push_to_gitlab
{
    public partial class frm_AddWatchProgressList : Form
    {
        List<string> processNames=new List<string>();
        

        static class cls_inf100Data
        {
            public static List<inf100Data> Listinf100Data { get; set; }
            public static DateTime GetDateTime   { get; set; }
        }
        public frm_AddWatchProgressList()
        {
            InitializeComponent();
        }

        private void frm_AddWatchProgressList_Load(object sender, EventArgs e)
        {
           
            GetWatchProgressList();
            GetNowProgressList();
            //cbList.TextUpdate += cbo_TextUpdate;
        }

        void GetWatchProgressList()
        {

           var result= Getinf100_watch_Progress();
            Grid.DataSource = result;
        }
        public static List<inf100Data> Getinf100_watch_Progress()
        {
            IEnumerable<inf100Data> result;
            if ((DateTime.Now - cls_inf100Data.GetDateTime).TotalMinutes > 10)
                using (KTConnectionController conn = new KTConnectionController())
                {
                    conn.SettingConnectWithHspArea("ktgh00");
                    string strsql = "";
                    strsql = "	 select * from inf100	";
                    strsql += "	    where dtflg='watch_Progress'  ";
                    IEnumerable<inf100Data> listSession = new List<inf100Data>();
                    result = conn.DoSql<inf100Data>(strsql);
                    cls_inf100Data.Listinf100Data = result.ToList();
                    cls_inf100Data.GetDateTime = DateTime.Now;
                }
            

            return cls_inf100Data.Listinf100Data;
        }

        void GetNowProgressList()
        {
            Process[] process = Process.GetProcesses();
            cbList.Items.Clear();
            processNames = process
                .Select(p => p.ProcessName)
                .Distinct()
                .OrderBy(name => name)
                .ToList();

            foreach (var item in processNames)
            {
                cbList.Items.Add(item);
            }

        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            if (!cbList.Text.EndsWith(".exe"))
                cbList.Text += ".exe";
            insertinf100("ktgh00", cbList.Text);
            insertinf100("hpk210", cbList.Text);
            insertinf100("tshis", cbList.Text);

            cls_inf100Data.GetDateTime = DateTime.Now.AddMinutes(-20);
            GetNowProgressList();
        }

        void insertinf100(string hsp, string progressName)
        {
            using (KTConnectionController conn = new KTConnectionController())
            {
                conn.SettingConnectWithHspArea(hsp);
                string strsql = "";
                strsql = "insert into inf100 ( dtflg ,item1 , itemnme1 ) values (:dtflg,:item1, :itemnme1) ";
                var param = new { dtflg = "watch_Progress", item1 = "His", itemnme1 = progressName.ToLower() };
                var result = conn.DoSql(strsql, param);
            }
        }

        private void refrash_Click(object sender, EventArgs e)
        {
            GetNowProgressList();
        }
        private void cbo_TextUpdate(object sender, EventArgs e)
        {
            ComboBox cbo = sender as ComboBox;
            string keyword = cbo.Text;

            if (keyword != "")
            {
                var filtered = processNames
            .Where(x => x.Contains(keyword))
            .ToArray();

                cbo.Items.Clear();
                cbo.Items.AddRange(filtered);

                cbo.SelectionStart = keyword.Length;
                cbo.DroppedDown = true;
                Cursor.Current = Cursors.Default;
            }
            else
            {
                cbo.Items.Clear();
                foreach (string str in processNames)
                    cbo.Items.Add(str);
            }
        }
    }
}
