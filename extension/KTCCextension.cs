using KTCC.Core;
using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using Dapper;
using System.Collections.Generic;
using Oracle.ManagedDataAccess.Client;

namespace Project_auto_push_to_gitlab.extension
{
    /// <summary>
    /// 1140813
    /// </summary>
    public static class KTConnectionControllerExtension
    {
        /// <summary>
        /// debug用 for Daaper
        /// </summary>
        /// <param name="ktcc"></param>
        /// <param name="strcmd"></param>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static string GetDebugsqlForDaaper(this KTConnectionControllerCommon ktcc, string strcmd, object obj)
        {
            string debugsql;
            int i = 0;

            if (string.IsNullOrEmpty(strcmd))
            {
                return "";
            }

            var dict = obj.GetType()
              .GetProperties()
              .ToDictionary(p => p.Name, p => p.GetValue(obj));

            debugsql = strcmd;

            foreach (var p in dict)
            {
                debugsql = debugsql.Replace(":" + p.Key, "'" + p.Value.ToString() + "'");
            }

            return debugsql;
        }

        /// <summary>
        /// 1141205 針對如果用string判斷院區 直接call 這段用文字 KTGH00/HPK210/TSHIS 自動建立連線
        /// </summary>
        /// <param name="ktcc"></param>
        /// <param name="hsparea"></param>
        public static void SettingConnectWithHspArea(this KTConnectionController conn, string hsparea)
        {
            if (conn == null) return;
            switch (hsparea.ToUpper())
            {
                case "KTGH00":
                case "KTGH":
                case "SX":
                case "SA":
                    conn.Connect(ConnectHost.KTGH00);
                    break;
                case "HPK210":
                case "HPK":
                case "TA":
                    conn.Connect(ConnectHost.HPK210);
                    break;
                case "TSHIS":
                case "TS":
                    conn.Connect(ConnectHost.TSHIS);
                    break;
            }
        }
    }
}
