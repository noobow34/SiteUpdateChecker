using Force.Crc32;
using Noobow.Commons.Utils;
using SiteUpdateChecker.Constants;
using Noobow.Commons.EF;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Noobow.Commons.EF.Tools;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shuttle.Core.Cron;
using System.Threading;
using Mono.Unix;
using Mono.Unix.Native;

namespace SiteUpdateChecker
{
    class Program
    {
        private static DbContextOptionsBuilder<ToolsContext> optionsBuilder = new DbContextOptionsBuilder<ToolsContext>();
        private static HttpClient httpClient = new HttpClient();
        private static List<Task> tasks = null;
        private static List<CancellationTokenSource> cancelTokens = null;
        private static string pushWhenNoChange = "0";
        static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                pushWhenNoChange = args[0];
            }

            var sig1 = new UnixSignal(Signum.SIGUSR1);
            var sig2 = new UnixSignal(Signum.SIGUSR2);

            CreateTaskList();

            int signalIndex = 0;
            while( (signalIndex = UnixSignal.WaitAny(new UnixSignal[] {sig1,sig2})) != -1)
            {
                switch (signalIndex)
                {
                    case 0:
                        //リセット
                        Console.WriteLine("-----------USR1受信:タスクを再読込-----------");
                        CreateTaskList();
                        break;
                    case 1:
                        Console.WriteLine("-----------USR2受信:全タスクを実行-----------");
                        CheckAllSite();
                        break;
                }
            }

        }

        /// <summary>
        /// タスクリストを作成して実行する
        /// </summary>
        private static void CreateTaskList()
        {
            //実行中のすべてのタスクをキャンセル
            if(cancelTokens != null)
            {
                CancelAllTask();
            }

            //タスクリストとキャンセルトークンリストをクリア
            tasks = new List<Task>();
            cancelTokens = new List<CancellationTokenSource>();

            //対象を全件取得
            List<CheckSite> csList;
            using (var context = new ToolsContext(optionsBuilder.Options))
            {
                csList = context.CheckSites.AsNoTracking().ToList();
            }

            //タスク登録
            foreach (var cs in csList)
            {
                var tokenSource = new CancellationTokenSource();
                var cancelToken = tokenSource.Token;
                tasks.Add(Task.Run(() => SleepAndCheckSite(cs, cancelToken)));
            }
        }

        /// <summary>
        /// すべてのタスクをキャンセルする
        /// </summary>
        private static void CancelAllTask()
        {
            foreach(var cancellationToken in cancelTokens)
            {
                cancellationToken.Cancel();
            }
        }

        /// <summary>
        /// スケジュール時間まで待機してチェックを実行
        /// </summary>
        /// <param name="cs"></param>
        /// <param name="cancelToken"></param>
        private static async void SleepAndCheckSite(CheckSite cs,CancellationToken cancelToken)
        {
            var c = new CronExpression(cs.Schedule);
            while (true)
            {
                var next = c.NextOccurrence();
                Console.WriteLine($"{cs.SiteName}:{next.ToString()}");
                while (true)
                {
                    //30秒ごとにキャンセルされていないか・実行時刻が来ていないかチェック
                    if (cancelToken.IsCancellationRequested || DateTime.Now >= next)
                    {
                        break;
                    }
                    Thread.Sleep(30_000);
                }
                await CheckTaskAsync(cs.SiteId, pushWhenNoChange);
            }
        }

        /// <summary>
        /// 全サイトのチェックを実行
        /// </summary>
        private static async void CheckAllSite()
        {
            List<CheckSite> csList;
            using (var context = new ToolsContext(optionsBuilder.Options))
            {
                csList = context.CheckSites.AsNoTracking().ToList();
            }

            //タスク登録
            foreach (var cs in csList)
            {
                await CheckTaskAsync(cs.SiteId,pushWhenNoChange);
            }
        }

        /// <summary>
        /// チェックを実行
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pushWhenNoChange"></param>
        /// <returns></returns>
        private static async System.Threading.Tasks.Task CheckTaskAsync(int id, string pushWhenNoChange)
        {
            CheckSite cs;
            using(var context = new ToolsContext(optionsBuilder.Options))
            {
                cs = context.CheckSites.Where(c => c.SiteId == id).SingleOrDefault();

                bool updated = false;
                var response = await httpClient.GetAsync(cs.Url, HttpCompletionOption.ResponseHeadersRead);
                string identifier = null;
                cs.LastCheck = DateTime.Now;

                Console.WriteLine("-------------");
                Console.WriteLine(cs.SiteName);

                if (response.StatusCode != System.Net.HttpStatusCode.NotModified)
                {
                    try
                    {
                        //タイプごとにチェック
                        switch (cs.CheckType)
                        {
                            case CheckTypeEnum.ETag:
                                Console.Write("ETag:");
                                identifier = response.Headers.GetValues("ETag").ToArray()?[0];
                                Console.WriteLine(identifier);
                                Console.WriteLine($"前回値:{cs.CheckIdentifier}");
                                if (identifier != cs.CheckIdentifier || cs.CheckIdentifier == null)
                                {
                                    updated = true;
                                    cs.LastUpdate = DateTime.Now;
                                }
                                break;

                            case CheckTypeEnum.LastModified:
                                Console.Write("Last-Modified:");
                                Console.WriteLine(response.Content.Headers.LastModified?.LocalDateTime.ToString("yyyy/MM/dd HH:mm:ss"));
                                Console.WriteLine($"前回値:{cs.LastUpdate?.ToString("yyyy/MM/dd HH:mm:ss")}");
                                if (cs.LastUpdate != response.Content.Headers.LastModified || cs.LastUpdate == null)
                                {
                                    updated = true;
                                    cs.LastUpdate = response.Content.Headers.LastModified?.LocalDateTime;
                                }
                                break;

                            case CheckTypeEnum.HtmlHash:
                                string html = await response.Content.ReadAsStringAsync();
                                byte[] bytes = new UTF8Encoding().GetBytes(html);
                                uint crc32 = Crc32CAlgorithm.Compute(bytes);
                                identifier = Convert.ToString(crc32, 16);
                                Console.Write("HtmlHash:");
                                Console.WriteLine(identifier);
                                Console.WriteLine($"前回値:{cs.CheckIdentifier}");
                                if (identifier != cs.CheckIdentifier || cs.CheckIdentifier == null)
                                {
                                    updated = true;
                                    cs.LastUpdate = DateTime.Now;
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        LineUtil.PushMe($"【確認エラー】\n{cs.SiteName}\n{ex.Message}", httpClient);
                    }
                }

                //通知
                if (updated)
                {
                    cs.CheckIdentifier = identifier;
                    Console.WriteLine("通知実施");
                    string notifyString = $"【更新通知】\n{cs.SiteName}\n{cs.LastUpdate?.ToString("yyyy/MM/dd HH:mm:ss")}\n{cs.Url}";
                    LineUtil.PushMe(notifyString, httpClient);
                }
                else if (pushWhenNoChange == "1")
                {
                    LineUtil.PushMe($"【更新なし】\n{cs.SiteName}\n{cs.LastUpdate?.ToString("yyyy/MM/dd HH:mm:ss")}", httpClient);
                }
                await context.SaveChangesAsync();
            }
        }
    }
}
