using Newbe.Mahua.MahuaEvents;
using Newbe.Mahua.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
//using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;

namespace Newbe.Mahua.Plugins.HKHimeko.MahuaEvents
{
    /// <summary>
    /// 群消息接收事件
    /// 我要色图
    /// 随机发送yande.re(https://yande.re)今日热图，热图发送完毕以后发送怀旧图，每日更新。
    /// </summary>
    public class GroupMessageReceivedMahuaEventNeedPorn
        : IGroupMessageReceivedMahuaEvent
    {
        private readonly IMahuaApi _mahuaApi;

        private class Status /*记录每位群友的色图统计状态*/
        {
            public Queue<DateTime> dateTimeQueue; /*最近两次我要色图的时间*/
            public int used; /*已经使用的限额*/
            public int totalScore; /*当前抽到的色图的总分，即使怀旧色图平均分必然比今日色图高不少，我还是没有给今日色图加权，因为我觉得现在这么设置的话不太可能进入二阶段了，如果能够进入那就当做奖励吧，也不考虑惩罚分了。*/
            public double averageScore; /*当前抽到的色图的平均分*/
            public bool buff; /*是否获得了港姬子的Super AI Buff*/
        };

        private static JArray jArr = new JArray(); /*query返回的JSON*/
        private static DateTime date = new DateTime(); /*检查是否需要更新热图的日期*/
        private static bool popular = false; /*决定热图还是怀旧图*/
        private static int[] pornArray = null; /*决定发送顺序的随机数组*/
        private static int pornIndex = 0; /*记录当前将要发送的数组索引*/
        private static bool updateLock = false; /*更新的锁*/
        private static HashSet<int> record = new HashSet<int>(); /*记录发送过的图片id的哈希集*/
        private static Dictionary<string, Status> status = new Dictionary<string, Status>();  /*不加限额根本扛不住*/
        // 每人每日限额12张，CD每30分钟2张。Super AI Buff：+100%限额，-90%冷却缩减。
        private readonly int quota = 12;
        private readonly int cooldown = 30;
        private readonly int quotaInCooldown = 2;
        private static string firstQq = "";
        //private static double[] rankingAverageScore = new double[3] { 0.0, 0.0, 0.0 };
        private static double firstAverageScore = 0.0;

        public GroupMessageReceivedMahuaEventNeedPorn(
            IMahuaApi mahuaApi)
        {
            _mahuaApi = mahuaApi;
        }

        /// <summary>
        /// 更新图片，先热图，再怀旧。
        /// </summary>
        /// <param name="popular">true热图，false怀旧</param>
        private void Update(GroupMessageReceivedContext context, bool popular)
        {
            try
            {
                using (WebClient webClient = new WebClient())
                {
                    webClient.Encoding = Encoding.UTF8;
                    updateLock = true;

                    // yande.re服务器采用GMT+0时间，GMT+8零点更新的时候GMT+0的昨天还没有过完，Score还不够高，所以需要取前一天（严格来说不算是今日呢（<ゝω・）☆）。
                    // 热图recent（https://yande.re/post?tags=date%3A2018-10-18+score%3A%3E%3D20+order%3Ascore+limit%3A42+-rating%3Aexplicit）/date:2018-10-18+score:>=10+order:score+limit:42+-rating:explicit，评分高于20分从高到低排列，确保高分图可以看到，跳过Rating: Explicit这些过于硬核的色图，42：Answer to the Ultimate Question of Life, the Universe, and Everything。
                    // 放弃了之前用的两个链接（https://yande.re/post/popular_by_day?day=18&month=10&year=2018）（https://yande.re/post/popular_recent），这两个的数量似乎默认就是40个，不可以用limit来query。
                    // 怀旧oldDays（https://yande.re/post?tags=date%3A%3C%3D2011-10-18+score%3A%3E%3D25+order%3Arandom+limit%3A42+-rating%3Aexplicit），可以用score和limit来query，这里我选择七年前的今天，评分高于25分乱序，确保是打乱的，要不然就只是日期倒序。
                    DateTime recent = DateTime.Now.AddDays(-2);
                    DateTime oldDays = DateTime.Now.AddYears(-7);
                    string queryUrl = popular
                        ? $"https://yande.re/post.json?tags=date%3A{recent.ToString("yyyy-MM-dd")}+score%3A%3E%3D20+order%3Ascore+limit%3A42+-rating%3Aexplicit"
                        : $"https://yande.re/post.json?tags=date%3A%3C%3D{oldDays.ToString("yyyy-MM-dd")}+score%3A%3E%3D25+order%3Arandom+limit%3A42+-rating%3Aexplicit";
                    string json = webClient.DownloadString(queryUrl);
                    jArr = (JArray)JsonConvert.DeserializeObject(json);

                    // 更换成CleverQQ以后似乎直接通过图片网址发送成功率也很高，就不用下载到本地再发送这样子曲线救国了。以前QQLight直接通过图片网址发送经常失败。
                    //// 检查指定目录是否存在，如果不存在则创建。
                    //if (!Directory.Exists("./AppData/Images/"))
                    //{
                    //    Directory.CreateDirectory("./AppData/Images/");
                    //}

                    //// 先清空文件夹，然后更新。
                    //DirectoryInfo dir = new DirectoryInfo("./AppData/Images/");
                    //foreach (FileInfo file in dir.GetFiles())
                    //{
                    //    file.Delete();
                    //}
                    //for (int i = 0; i < jArr.Count; i++)
                    //{
                    //    webClient.DownloadFile(jArr[i]["sample_url"].ToString(), $"./AppData/Images/{i}.jpg");
                    //}
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                        .Text($"{(popular ? "今日" : "怀旧")}色图更新完毕，更新时间：{DateTime.Now}。")
                        .Done();
                    if (popular)
                    {
                        // 只有热图更新成功才会更新这个date。
                        date = DateTime.Now.Date;
                    }
                    updateLock = false;
                }
            }
            catch (WebException e)
            {
                ILog logger = LogProvider.For<GroupMessageReceivedMahuaEventNeedPorn>();
                logger.Info($"WebException: {e}");
                _mahuaApi.SendPrivateMessage(MahuaEventCommon.admin)
                    .Text("嘤嘤嘤，有异常！")
                    .Newline()
                    .Text($"FromGroup: {context.FromGroup}")
                    .Newline()
                    .Text($"FromQq: {context.FromQq}")
                    .Newline()
                    .Text($"FromAnonymous: {context.FromAnonymous}")
                    .Newline()
                    .Text($"Message: {context.Message}")
                    .Newline()
                    .Text($"WebException: {e}")
                    .Done();
                _mahuaApi.SendGroupMessage(context.FromGroup)
                    .At(MahuaEventCommon.admin)
                    .Text("Houston, we have a problem.")
                    .Done();
                updateLock = false;
            }
        }

        /// <summary>
        /// 将数组洗牌打乱，返回一个原数组的随机排序。
        /// </summary>
        private int[] Shuffle(int[] array)
        {
            Random random = new Random();
            for (int i = 0; i < array.Length; i++)
            {
                int index = random.Next(0, array.Length - i);
                int temp = array[index];
                array[index] = array[array.Length - i - 1];
                array[array.Length - i - 1] = temp;
            }
            return array;
        }

        /// <summary>
        /// 虽然协议支持通过直接发送图片URL的方式发送图片，但是实际使用下来经常出现明明本地已经发送了但是实际却并没有发送出去的情况。
        /// 目前只能曲线救国通过把图片下载到本地然后通过发送本地图片的方式发送图片。
        /// </summary>
        private void NeedPorn(GroupMessageReceivedContext context)
        {
            // 如果是新的一天，则先更新色图。
            if (DateTime.Now.Date != date)
            {
                popular = true;
                status = new Dictionary<string, Status>();
                if (firstQq != "")
                {
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                    .At(firstQq)
                    .Text($" 恭喜！由于你昨天得分第一，你今天一整天都会获得港姬子的Super AI Buff（+100%限额，-90%冷却缩减）哦！")
                    .Done();
                    status[firstQq] = new Status
                    {
                        dateTimeQueue = new Queue<DateTime>(),
                        used = 0,
                        totalScore = 0,
                        averageScore = 0.0,
                        buff = true
                    };
                }
                firstQq = "";
                firstAverageScore = 0.0;
                _mahuaApi.SendGroupMessage(context.FromGroup)
                    .Text($"今日色图开始更新，更新完毕之前管住鸡儿请勿反复请求。")
                    .Done();
                Update(context, popular);

                // 生成随机数组pornArray，初始化pornIndex。
                pornArray = new int[jArr.Count];
                for (int i = 0; i < jArr.Count; i++)
                {
                    pornArray[i] = i;
                }
                pornArray = Shuffle(pornArray);
                pornIndex = 0;
            }

            // 发送色图，检测重复，确定要发送的数组索引。
            while (pornIndex < jArr.Count && record.Contains(jArr[pornArray[pornIndex]]["id"].ToObject<int>()))
            {
                pornIndex++;
            }
            // 如果当前爬的图发送完毕，则更新怀旧图。
            if (pornIndex == jArr.Count)
            {
                if (popular)
                {
                    popular = false;
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                        .At(context.FromQq)
                        .Text(" あんたバカ？今日色图已经全部发过一遍了！你们这群射精机器！")
                        .Done();
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                        .Text("二阶段准备！")
                        .Done();
                }
                else
                {
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                        .At(context.FromQq)
                        .Text(" 噫！怀旧色图也已经发了这么多了！你们这群变态还不停下吗！")
                        .Done();
                }
                _mahuaApi.SendGroupMessage(context.FromGroup)
                    .Text($"怀旧色图开始更新，更新完毕之前管住鸡儿请勿反复请求。")
                    .Done();
                Update(context, popular);
                _mahuaApi.SendGroupMessage(context.FromGroup)
                    .At(context.FromQq)
                    .Text(" 哼！人家才，才不是为了你才特意去找的啦！")
                    .Done();

                // 每次更新以后都需要重新生成随机数组pornArray，因为长度jArr.Count可能不一样。
                pornArray = new int[jArr.Count];
                for (int i = 0; i < jArr.Count; i++)
                {
                    pornArray[i] = i;
                }
                pornArray = Shuffle(pornArray);
                pornIndex = 0;
            }

            if (status.ContainsKey(context.FromQq))
            {
                if (status[context.FromQq].used < (status[context.FromQq].buff ? 2 * quota : quota))
                {
                    if (DateTime.Now.Subtract(status[context.FromQq].dateTimeQueue.Peek()).TotalMinutes < (status[context.FromQq].buff ? 0.1 * cooldown : cooldown) &&
                        status[context.FromQq].dateTimeQueue.Count >= quotaInCooldown)
                    {
                        GetStatus(context);
                        return;
                    }
                    else
                    {
                        status[context.FromQq].dateTimeQueue.Enqueue(DateTime.Now);
                        status[context.FromQq].used++;
                        status[context.FromQq].totalScore += jArr[pornArray[pornIndex]]["score"].ToObject<int>();
                        status[context.FromQq].averageScore = Math.Round((double)status[context.FromQq].totalScore / status[context.FromQq].used, 2);
                        if (status[context.FromQq].averageScore > firstAverageScore)
                        {
                            firstAverageScore = status[context.FromQq].averageScore;
                            firstQq = context.FromQq;
                        }
                        if (status[context.FromQq].dateTimeQueue.Count > quotaInCooldown)
                        {
                            status[context.FromQq].dateTimeQueue.Dequeue();
                        }
                    }
                }
                else /*Triskaidekaphobia*/
                {
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                        .At(context.FromQq)
                        .Text($" 讨厌，你今天已经超过限额啦，明天再来试试吧！")
                        .Done();
                    GetStatus(context);
                    return;
                }
            }
            else
            {
                status[context.FromQq] = new Status
                {
                    dateTimeQueue = new Queue<DateTime>(new DateTime[] { DateTime.Now }),
                    used = 1,
                    totalScore = jArr[pornArray[pornIndex]]["score"].ToObject<int>(),
                    averageScore = Math.Round(jArr[pornArray[pornIndex]]["score"].ToObject<double>(), 2),
                    buff = false
                };
                if (status[context.FromQq].averageScore > firstAverageScore)
                {
                    firstAverageScore = status[context.FromQq].averageScore;
                    firstQq = context.FromQq;
                }
            }

            string rarity;
            if (jArr[pornArray[pornIndex]]["score"].ToObject<int>() > 100)
            {
                rarity = "UR";
            }
            else if (jArr[pornArray[pornIndex]]["score"].ToObject<int>() > 75)
            {
                rarity = "SSR";
            }
            else if (jArr[pornArray[pornIndex]]["score"].ToObject<int>() > 50)
            {
                rarity = "SR";
            }
            else if (jArr[pornArray[pornIndex]]["score"].ToObject<int>() > 25)
            {
                rarity = "R";
            }
            else
            {
                rarity = "N";
            }

            // 暂时框架对于发图功能支持有问题，会直接发送图片地址而不是图片。
            //string msg = $"[IR:pic={new DirectoryInfo("./AppData/Images/").FullName}{pornArray[pornIndex]}.jpg]\r\nhttps://yande.re/post/show/{jArr[pornArray[pornIndex]]["id"]}";
            //_mahuaApi.SendGroupMessage(context.FromGroup)
            //    .Image($"{new DirectoryInfo("./AppData/Images/").FullName}{pornArray[pornIndex]}.jpg")
            //    .Newline()
            //    .Text($"https://yande.re/post/show/{jArr[pornArray[pornIndex]]["id"]}")
            //    .Done();
            // 只能自己手动拼接字符串构造，注意因为Api_UpLoadPic上传的图片尺寸应小于4MB，否则会上传失败返回空，所以这里采用sample_url而不是jpeg_url。
            // 我怀疑因为之前只发一张图片和对应地址导致被腾讯认为是广告机所以总是被屏蔽群消息发送不出去，尝试添加Score和Tags信息来解决这个问题。
            // 我现在进一步怀疑是因为yande.re这个网站被腾讯认为不安全所以总是被屏蔽群消息发送不出去，yande.re现在经常被墙大家也很少会去点开，因此尝试去掉网址直接使用id来解决这个问题。
            // 最后尝试添加表情和Emoji来解决这个问题。
            // 通过多次尝试得出结论：带有不安全链接/消息长度过长/消息内容雷同/图片太色都会被腾讯屏蔽，前两个我已经尽力避免了，第三个手动发都发不出去了更别提QQ机器人发了，无解。总之现在应该把被屏蔽的概率降到最低了。
            string msg = $"[IR:pic={jArr[pornArray[pornIndex]]["sample_url"]}]\r\n" +
                $"[IR:at={context.FromQq}]\r\n" +
                $"Id: [emoji=F09F9189]{jArr[pornArray[pornIndex]]["id"]}[emoji=F09F9188]\r\n" +
                $"Tags: {jArr[pornArray[pornIndex]]["tags"]}\r\n" +
                $"Score: {jArr[pornArray[pornIndex]]["score"]}\r\n" +
                $"Rarity: [Face175.gif]{rarity}[Face175.gif]";
            _mahuaApi.SendGroupMessage(context.FromGroup, msg);

            if (rarity == "UR")
            {
                status[context.FromQq].buff = true;
                _mahuaApi.SendGroupMessage(context.FromGroup)
                    .Text("ハラショー！")
                    .Newline()
                    .At(context.FromQq)
                    .Text($" 恭喜！由于抽出了UR，你一整天都会获得港姬子的Super AI Buff（+100%限额，-90%冷却缩减）哦！")
                    .Done();
            }

            // 一个小彩蛋：Uploader是群主，触发概率大概是4%。
            if (jArr[pornArray[pornIndex]]["author"].ToString() == "fireattack")
            {
                status[context.FromQq].buff = true;
                _mahuaApi.SendGroupMessage(context.FromGroup)
                    .Text("娘子呀，跟牛魔王出来看上帝。")
                    .Newline()
                    .At(context.FromQq)
                    .Text($" 恭喜！由于触发了彩蛋，你一整天都会获得港姬子的Super AI Buff（+100%限额，-90%冷却缩减）哦！")
                    .Done();
            }

            record.Add(jArr[pornArray[pornIndex]]["id"].ToObject<int>());
            pornIndex++;
        }

        /// <summary>
        /// 获取色图统计状态
        /// </summary>
        /// <param name="context"></param>
        private void GetStatus(GroupMessageReceivedContext context)
        {
            if (status.ContainsKey(context.FromQq))
            {
                if (DateTime.Now.Subtract(status[context.FromQq].dateTimeQueue.Peek()).TotalMinutes < (status[context.FromQq].buff ? 0.1 * cooldown : cooldown) &&
                        status[context.FromQq].dateTimeQueue.Count >= quotaInCooldown)
                {
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                    .At(context.FromQq)
                    .Text($" 你刚刚才要过色图哦，等{(status[context.FromQq].buff ? 0.1 * cooldown : cooldown) - DateTime.Now.Subtract(status[context.FromQq].dateTimeQueue.Peek()).Minutes}分钟再试试看吧！")
                    .Newline()
                    .Text($"你今天已经要过{status[context.FromQq].used}张色图了，当前平均分是{status[context.FromQq].averageScore}。")
                    .Done();
                }
                else
                {
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                    .At(context.FromQq)
                    .Text($" 你现在就可以要色图哦！")
                    .Newline()
                    .Text($"你今天已经要过{status[context.FromQq].used}张色图了，当前平均分是{status[context.FromQq].averageScore}。")
                    .Done();
                }
            }
            else
            {
                _mahuaApi.SendGroupMessage(context.FromGroup)
                    .At(context.FromQq)
                    .Text(" 你今天还没有要过色图呢，快去要一份试试看吧！")
                    .Done();
            }
        }

        /// <summary>
        /// 获取排名状态
        /// </summary>
        /// <param name="context"></param>
        private void GetRanking(GroupMessageReceivedContext context)
        {
            _mahuaApi.SendGroupMessage(context.FromGroup)
                .At(context.FromQq)
                .Text($" 贵群当前最高分为{firstAverageScore}，请继续努力哦！")
                .Done();
        }

        public void ProcessGroupMessage(GroupMessageReceivedContext context)
        {
            // todo 填充处理逻辑
            //throw new NotImplementedException();
            if (context.Message.Equals("我要色图"))
            {
                // 如果其他线程正在更新，先睡上10秒。
                if (updateLock)
                {
                    _mahuaApi.SendGroupMessage(context.FromGroup)
                        .At(context.FromQq)
                        .Text(" 老娘都说了还没更新完！花Q！花Q花Q！花～Q！")
                        .Done();
                    Thread.Sleep(10000);
                }
                NeedPorn(context);
            }
            if (context.Message.Equals("我要色图 状态"))
            {
                GetStatus(context);
            }
            if (context.Message.Equals("我要色图 排名"))
            {
                GetRanking(context);
            }

            // 不要忘记在MahuaModule中注册
        }
    }
}
