using Noobow.Commons.Utils;
using SiteUpdateChecker.Constants;
using SiteUpdateChecker.EF;
using System;
using System.Linq;
using System.Net.Http;

namespace SiteUpdateChecker
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            string pushWhenNoChange = args?[0];

            using(var context = new ToolsContext())
            {
                using (var httpClient = new HttpClient())
                {
                    var csList = context.CheckSites;
                    foreach(var cs in csList)
                    {
                        HttpResponseMessage response;
                        bool updated = false;
                        response = await httpClient.GetAsync(cs.Url);

                        Console.WriteLine(cs.SiteName);

                        //タイプごとにチェック
                        switch (cs.CheckType)
                        {
                            case CheckType.E_TAG:
                                Console.Write("ETag:");
                                var etag = response.Headers.GetValues("ETag").ToArray()?[0];
                                Console.WriteLine(etag);
                                Console.WriteLine($"前回値:{cs.CheckIdentifier}");
                                if(etag != cs.CheckIdentifier || cs.CheckIdentifier == null)
                                {
                                    updated = true;
                                    cs.LastUpdate = DateTime.Now;
                                    cs.CheckIdentifier = etag;
                                }
                                cs.LastCheck = DateTime.Now;
                                break;
                        }

                        //通知
                        if (updated)
                        {
                            Console.WriteLine("通知実施");
                            string notifyString = $"【更新通知】\n{cs.SiteName}\n{cs.LastUpdate?.ToString("yyyy/MM/dd HH:mm:ss")}";
                            LineUtil.PushMe(notifyString, httpClient);
                        }else if (pushWhenNoChange == "1")
                        {
                            LineUtil.PushMe($"【更新なし】\n{cs.SiteName}\n{cs.LastUpdate?.ToString("yyyy/MM/dd HH:mm:ss")}", httpClient);
                        }
                    }
                }
                context.SaveChanges();
            }
        }
    }
}
