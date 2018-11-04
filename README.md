# QQ机器人（人工智障）港姬子HKHimeko

## 基本信息

港姬子HKHimeko是我为某个正经群写的一个拥有许多有趣功能的QQ机器人。

使用了[Newbe.Mahua.Framework](https://github.com/Newbe36524/Newbe.Mahua.Framework)框架，运行在[CleverQQ](https://www.cleverqq.cn)平台上。虽然使用了大一统框架，但是由于不少地方使用了CleverQQ平台的原生接口，所以移植到其他平台的时候需要多加注意。

其实一开始是在酷Q Air上的，后来有了发图的需求，所以移植到QQLight上，结果没过多久QQLight从免费变成了收费，就只能再次移植到CleverQQ上……

## 如何使用

设置app.config里面的相关参数，比如admin/platform等等。

按照[开始第一个QQ机器人【适用于v1.9-1.12】](http://www.newbe.pro/docs/mahua/2018/06/10/Begin-First-Plugin-With-Mahua-In-v1.9.html)教程生成并打包，复制文件到相应的机器人平台，启用插件，Enjoy！

## 当前功能

* 我要色图

  主动技能，随机发送[yande.re](https://yande.re)热图。

## 曾经功能（尚未移植）

* **正义审判（核心技能）**

  主动技能，审判又双叒叕放鸽子的群友。注意，这是港姬子最核心的技能，也是开发港姬子的初衷！然并卵，后来群友沉迷其他技能无法自拔……

* 色情识别

  被动技能，调用百度图像审核API对群友发送的图片进行色情识别。

* 音乐电台

  主动技能，实现QQ音乐和网易云音乐点歌。

* 智能聊天

  主动技能，调用图灵机器人API智能聊天。

## 计划功能

* 语义分析

  中文语义分析太难了，而且又是在QQ群这么一个混乱善良/中立/邪恶充斥着流行词汇各种黑话地方……
  
* 个性推荐

  我要色图的改进方向，通过群友的反馈进行个性化推荐。
  
* 本周奶子

  主动技能，发送[比村奇石](https://twitter.com/strangestone)的月曜日のたわわ。

**欢迎提出您的宝贵意见和建议或者贡献代码！**