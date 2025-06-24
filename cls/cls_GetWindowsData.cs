using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Threading.Tasks;

namespace Project_auto_push_to_gitlab.cls
{
    internal class cls_GetWindowsData
    {
       public static List<portData> listport =new List<portData>();
       public static void Mainfunc()
        {
            //1140624
            //避免 mutilthread 一直run 中間介入 導致重複塞 
            //先從記憶體內鎖起來 避免資料互撞
            lock (listport)
            {
                listport.Clear();

                var ps = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                var process = Process.Start(ps);
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // 正規表現で IP:ポート を抽出（TCP/UDP 対応）
                var regex = new Regex(@"(TCP|UDP)\s+([\[\]A-Fa-f0-9\.:]+):(\d+)\s+\S+\s+(\S+)?\s+(\d+)", RegexOptions.Multiline);

                //Console.WriteLine("使用中ポート (3000-3100) とプロセス一覧：");

                foreach (Match match in regex.Matches(output))
                {
                    string proto = match.Groups[1].Value;
                    string ip = match.Groups[2].Value;
                    int port = int.Parse(match.Groups[3].Value);
                    string stauts = match.Groups[4].Value;
                    int pid = int.Parse(match.Groups[5].Value);

                    if (port >= 3000 && port <= 3100)
                    {
                        try
                        {
                            Process proc = Process.GetProcessById(pid);
                            lock (listport)
                                listport.Add(new portData() { proto = proto, pid = pid, ip = ip, stauts = stauts, port = port, processname = proc.ProcessName });
                            //Console.WriteLine($"{proto}\t{ip}:{port}\tPID: {pid}\tProcess: {proc.ProcessName}");
                        }
                        catch
                        {
                            Debug.WriteLine($"{proto}\t{ip}:{port}\tPID: {pid}\tProcess: <不明または終了済>");
                        }
                    }
                }
            }
        }
        public class portData
        {
            public string proto { get; set; }
            public int port { set; get; }
            public int pid { set; get; }
            public string ip { set; get; }  
            public string stauts { set; get; }
            public string processname { set; get; }
        }


        public static async Task SendRestfulGet(string port,string pgmName)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    // APIのURL
                    string url = "http://127.0.0.1:"+port+"/OpenGit?pgm="+ pgmName;

                    // GET リクエスト送信
                    HttpResponseMessage response = await client.GetAsync(url);

                    // 成功したか確認
                    response.EnsureSuccessStatusCode();

                    // 結果を文字列として読み取り
                    string responseBody = await response.Content.ReadAsStringAsync();

                    // コンソール出力
                    Console.WriteLine(responseBody);
                }
                catch (HttpRequestException e)
                {
                    Debug.WriteLine($"エラー: {e.Message}");
                }
            }
        }
    }
}
