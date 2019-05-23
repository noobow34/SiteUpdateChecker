using Force.Crc32;
using Noobow.Commons.Utils;
using SiteUpdateChecker.Constants;
using Noobow.Commons.EF;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SiteUpdateChecker
{
    class Program
    {
        static async System.Threading.Tasks.Task Main(string[] args)
        {
            string pushWhenNoChange = "0";
            if (args != null && args.Length > 0)
            {
                pushWhenNoChange = args[0];
            }

            var optionsBuilder = new DbContextOptionsBuilder<ToolsContext>();
            using (var context = new ToolsContext(optionsBuilder.Options))
            {
                using (var httpClient = new HttpClient())
                {
                    var csList = context.CheckSites;
                    foreach(var cs in csList)
                    {
                        bool updated = false;
                        var response = await httpClient.GetAsync(cs.Url,HttpCompletionOption.ResponseHeadersRead);
                        string identifier = null;
                        cs.LastCheck = DateTime.Now;

                        Console.WriteLine(cs.SiteName);

                        if(response.StatusCode != System.Net.HttpStatusCode.NotModified)
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
                            catch(Exception ex)
                            {
                                LineUtil.PushMe($"【確認エラー】\n{cs.SiteName}\n{ex.Message}",httpClient);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Status304");
                        }


                        //通知
                        if (updated)
                        {
                            cs.CheckIdentifier = identifier;
                            Console.WriteLine("通知実施");
                            string notifyString = $"【更新通知】\n{cs.SiteName}\n{cs.LastUpdate?.ToString("yyyy/MM/dd HH:mm:ss")}\n{cs.Url}";
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
