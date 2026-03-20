using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project_auto_push_to_gitlab.model
{
    internal class cls_v_session
    {
        public string sid { get; set; }
        public string serial	 { get; set; }
        public string audsid { get; set; }
        public string username { get; set; }
        public string osuser { get; set; }
        public string machine { get; set; }
        public string client_info { get; set; }
        public string program { get; set; }
        public string status { get; set; }
        public string logon_time { get; set; }
        public string process { get; set; }
        public string seconds_in_wait { get; set; }
        public string last_call_et { get; set; }
        public string oracle_evt { get; set; }
        public string sql_id { get; set; }
        

    }

    class cls_PAGData
    {
        public string sample_time { get; set; }
        public string pga_target_mb { get; set; }
        public string pga_alloc_mb { get; set; }
        public string pga_inuse_mb { get; set; }
        public string pga_max_mb { get; set; }
        public string over_alloc_cnt { get; set; }
    }
    class cls_SessionUsingData
    {
        public string resource_name { get; set; }
        public string current_utilization { get; set; }
        public string max_utilization { get; set; }
        public string use_pct { get; set; }
        public string limit_value { get; set; }
        public string max_pct { get; set; }
    }
    class cls_sql_idData
    {
        public string hsp { get; set; }
        public string sql_id { get; set; }
        public string sql_fulltext { get; set; }
        public string cpu_sec { set; get; }
        public string elapsed_sec { get; set; }
        public string executions { get; set; }
        /// <summary>
        /// 執行程式
        /// </summary>
        public string module { set; get; }
        public string buffer_gets { get; set; }
        /// <summary>
        /// 最後使用時間
        /// </summary>
        public string LAST_LOAD_TIME { get; set; }
        public string runningDate { get; set; }
        public string runningTime { get; set; }
        /// <summary>
        /// 連線使用IP
        /// </summary>
        public string client_info { set; get; }
    }
    class Insertino013_subDtlfgExplain_PLAN: cls_sql_idData
    {

        public string dtflg { get; set; } = "explain_plan";
        public string dtflgname { get; set; } = "執行計畫查詢log";

        public string explain_Plan { get; set; }
        public string updte { get; set; }
        public string uptim { get; set; }
        /// <summary>
        /// 查詢登入院區
        /// </summary>
        public string hsp { get; set; }
    }
    class cls_LoggingForDB
    {
        public string Logvsdte { get; set; }
        public string Logtimer { get; set; }
        public string sql_id { get; set; }
        public string RunningExeName { get; set; }
        /// <summary>
        /// thread id
        /// </summary>
        public string RunningExePid { get; set; }
        public string hspArea { get; set; }
        /// <summary>
        /// 使用IP
        /// </summary>
        public string client_info { get; set; } = "N/A";
    }

    class ino013_sub
    {
        public string dtflgname { get; set; }
        public string dtcode { get; set; }
        public string sql_id { get; set; }
        public string sql_fulltext { get; set; }
    }

    class cls_DBMS_XPLAN_DISPLAY_CURSOR
    {
        public string plan_table_output { get; set; }
    }
}
