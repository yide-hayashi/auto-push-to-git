using System;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
//using static EFormApi.Client.APIClient.SheetData;

namespace Project_auto_push_to_gitlab.extension
{
    public static class cls_ExtensionMethod
    {
        /// <summary>
        /// 1130524 date的擴充
        /// </summary>
        /// <param name="aString">回傳民國年月日</param>
        /// <returns></returns>
        public static string 西元日期YYYYMMDD(this DateTime aString)
        {
            return aString.Year.ToString() + aString.Month.ToString("00") + aString.Day.ToString("00");
        }


        /// <summary>
        /// 1130119 date的擴充
        /// </summary>
        /// <param name="d"></param>
        /// <returns>回傳民國年月日</returns>
        public static string 民國日期(this DateTime d)
        {
            return (d.Year - 1911).ToString() + d.Month.ToString("00") + d.Day.ToString("00");
        }

        /// <summary>
        /// 1130509 修改成非固定轉換
        /// </summary>
        /// <param name="d"></param>
        /// <returns>回傳民國年月</returns>
        public static string 民國年月(this DateTime d)
        {
            return (d.Year - 1911).ToString() + d.Month.ToString("00");
        }

        /// <summary>
        /// 1130119 date的擴充
        /// </summary>
        /// <param name="d"></param>
        /// <returns>回傳HH:MM:NN</returns>
        public static string timer(this DateTime d)
        {
            return d.Hour.ToString("00") + d.Minute.ToString("00") + d.Second.ToString("00");
        }

        /// <summary>
        ///  1130119 date的擴充
        /// </summary>
        /// <param name="d"></param>
        /// <returns>回傳HH:MM/returns>
        public static string timerHM(this DateTime d)
        {
            return d.Hour.ToString("00") + d.Minute.ToString("00");
        }



        /// <summary>
        /// 1130509
        /// string's extension for 民國日期
        /// </summary>
        /// <param name="aString">6/7個長度不帶符號的數字 </param>
        /// <returns></returns>
        public static DateTime 民國轉西元(this string aString)
        {
            DateTime result;
            MatchCollection mc;
            int year;
            //1130419 只做基本的日期驗證 不驗證是否閏年 0229 還有 1 3 5 7 8 10 12 的31日
            //覽的話 直接 \d{6,7}就好
            mc = Regex.Matches(aString, @"(\d{2}|[0-1]\d{2})(1[0-2]|0\d)([0-2]\d|3[0-1])");
            if (mc.Count != 1)
            {
                return default;
            }
            //轉成西元年看日期是否正常(驗證閏年 30 31日)
            if (aString.Length == 6)
            {
                year = Convert.ToInt32(aString.Substring(0, 2)) + 1911;
                aString = year.ToString() + aString.Substring(2, 4);
                aString = aString.Insert(4, "/").Insert(7, "/");
            }
            else if (aString.Length == 7)
            {
                year = Convert.ToInt32(aString.Substring(0, 3)) + 1911;
                aString = year.ToString() + aString.Substring(3, 4);
                aString = aString.Insert(4, "/").Insert(7, "/");
            }
            //1140403 IISにうｐした後でこのSystem.FormatException: String '2025/4/3 12:00' was not recognized as a valid DateTime.のエラーが 出てくる　わけわからん...　何だがシステムの時間転換問題みたいな...
            if (aString.Split("/").Length > 2)
            {
                if (aString.Split("/")[0].Length != 4) aString = aString.Replace(aString.Split("/")[0], aString.Split("/")[0].ToString().PadLeft(4, '0'));
                if (aString.Split("/")[1].Length != 2) aString = aString.Replace(aString.Split("/")[1], aString.Split("/")[1].ToString().PadLeft(2, '0'));
                if (aString.Split("/")[2].Length != 2) aString = aString.Replace(aString.Split("/")[2], aString.Split("/")[2].ToString().PadLeft(2, '0'));
            }
            if (!DateTime.TryParseExact(aString, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
                DateTime.TryParseExact(aString, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
            return result;
        }

        /// <summary>
        /// 1130509
        /// string's extension for 民國日期
        /// </summary>
        /// <param name="aString">5個長度不帶符號的數字 </param>
        /// <returns></returns>
        public static DateTime 民國YM轉西元(this string aString)
        {
            MatchCollection mc;
            int year;
            //1130509 先全部清掉/ 之後統一加 就不用判斷有沒有斜線

            mc = Regex.Matches(aString, @"(\d{2}|[0-1]\d{2})(1[0-2]|0\d)");
            if (mc.Count != 1)
            {
                return default;
            }
            //轉成西元年看日期是否正常(驗證閏年 30 31日)
            if (aString.Length == 5)
            {
                year = Convert.ToInt32(aString.Substring(0, 3)) + 1911;
                aString += "01";
                aString = year.ToString() + aString.Substring(3, 4);
                aString = aString.Insert(4, "/").Insert(7, "/");
            }

            return DateTime.Parse(aString);
        }


        /// <summary>
        /// 1141229 對於CLOB BLOB 塞字串用
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string DealwithCLOBForInsertDB(this string str)
        {
            string strClob = "";
            int beSubLength = 0;

            if (str == null) str = "";

            // VB: str = str.Replace("'", "''")
            str = str.Replace("'", "''");

            if (str.Length > 2999)
            {
                var sb = new StringBuilder();

                int index = 0;
                while (index <= str.Length - 1)
                {
                    if (index > 0)
                    {
                        sb.Append(" || ");
                    }

                    if (str.Length <= index + 2999)
                    {
                        beSubLength = str.Length - index;
                    }
                    else
                    {
                        beSubLength = 2999;
                    }

                    sb.Append("to_clob('");
                    sb.Append(str.Substring(index, beSubLength));
                    sb.Append("') ");

                    // VB: index = index + 2998  (Nextで+1される前提)
                    // ここは while なので同等に +2999 進める
                    index += 2999;
                }

                strClob = sb.ToString();
            }
            else
            {
                strClob = "'" + str + "'";
            }

            if (strClob == "")
            {
                return "''";
            }
            else
            {
                return strClob;
            }
        }

    }
}
