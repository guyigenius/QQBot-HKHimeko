using System.Configuration;

namespace Newbe.Mahua.Plugins.HKHimeko.MahuaEvents
{
    /// <summary>
    /// 存储港姬子的所有全局静态变量
    /// </summary>
    public static class MahuaEventCommon
    {
        public static Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None); /*获取Configuration对象*/
        public static readonly string admin = config.AppSettings.Settings["admin"].Value; /*发送Exception的QQ号*/
    }
}
