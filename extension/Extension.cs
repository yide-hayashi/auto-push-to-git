using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace INOP1101.cls
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
            return (aString.Year).ToString() + aString.Month.ToString("00") + aString.Day.ToString("00");
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
        /// 1130925 date的擴充

        /// </summary>
        /// <param name="d"></param>
        /// <returns>回傳民國年月日</returns>
        public static string 民國日期hasSlash(this DateTime d)
        {
            return (d.Year - 1911).ToString() +@"/"+ d.Month.ToString("00") +@"/"+ d.Day.ToString("00");
        }

        /// <summary>
        /// 1130509 修改成非固定轉換
        /// </summary>
        /// <param name="d"></param>
        /// <returns>回傳民國年月</returns>
        public static string 民國年月(this DateTime d)
        {
            return (d.Year - 1911).ToString() + d.Month.ToString("00") ;
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
        /// 1130925 date的擴充

        /// </summary>
        /// <param name="d"></param>
        /// <returns>回傳HH:MM:NN</returns>
        public static string timerHasColon(this DateTime d)
        {
            return d.Hour.ToString("00") +":"+ d.Minute.ToString("00") +":"+ d.Second.ToString("00");
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

            return DateTime.Parse(aString);
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
        /// 1140320
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string 西元str轉民國(this string str)
        {
            str = str.Replace("/", "");
            if (str.Length==8)
            {
                string 民國Y = (Convert.ToInt16(str.Substring(0, 4)) - 1911).ToString();
                民國Y += str.Substring(4, 4);
                return 民國Y;
            }
            return "";
        }

        #region Reg 正規式


        /// <summary>
        /// 1140521 stringRegex
        /// </summary>
        /// <param name="aString"></param>
        /// <param name="regString">reg parameter string</param>
        /// <returns></returns>
        public static bool StrRegForCSharp(this string aString, string regString)
        {
            if (aString == null) return false;
            if (string.IsNullOrEmpty(regString)) return false;

            return Regex.Matches(aString, regString).Count > 0;
            // 速度重視なら:
            // return Regex.IsMatch(aString, regString);
        }

        public static string GetExtensionForCSharp(this string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return string.Empty;

            string pattern = @"\.([^.\\/:]+)$";
            Match m = Regex.Match(fileName, pattern);

            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// 1140710 for debug 撈出array Value
        /// </summary>
        public static string GetAllValueForCSharp(this string[] aString)
        {
            if (aString == null) return "{}";

            string sb = "";
            sb ="{";

            for (int i = 0; i < aString.Length; i++)
            {
                if (i > 0) sb+=",";
                sb +="\""+aString[i]+"\"";
            }

            sb+=" }";
            return sb.ToString();
        }

        /// <summary>
        /// 1140923 針對特地東西replace
        /// </summary>
        public static string RegReplaceForCSharp(this string aString, string pattern, string replacement)
        {
            if (aString == null) return null;
            return Regex.Replace(aString, pattern, replacement);
        }

        /// <summary>
        /// 1140923
        /// 固定回傳 Groups(1).Value 的值。

        /// 若 GroupsIndex 超出，改用最後一個 Group。

        /// 目前用於 'so4jb/6' => 抽出 so4jb/6  reg:'([^']*)'
        /// </summary>
        public static string RegGetValueForCSharp(this string aString, string pattern, short groupsIndex = 1)
        {
            if (aString == null) return null;

            MatchCollection matches = Regex.Matches(aString, pattern);

            foreach (Match m in matches)
            {
                int idx = groupsIndex;
                if (idx < 0) idx = 0;
                if (idx >= m.Groups.Count) idx = m.Groups.Count - 1;

                return m.Groups[idx].Value;
            }

            return "";
        }

        #endregion
        public static List<string> insertOver4000TextForCSharp(this string text)
        {
            List<string> result = new List<string>();

            if (string.IsNullOrEmpty(text))
            {
                result.Add(string.Empty);
                return result;
            }

            for (int i = 0; i < text.Length; i += 3800)
            {
                int len = Math.Min(3800, text.Length - i);
                result.Add(text.Substring(i, len));
            }

            return result;
        }
    }
}
