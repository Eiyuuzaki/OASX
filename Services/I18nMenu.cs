using System.Collections.Generic;

namespace OASX.Services;

/// <summary>菜单/任务名 英文 → 中文（自 Flutter cn_menu.dart 生成）。</summary>
public static class I18nMenu
{
    public static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["About"] = "关于",
        ["Activity Task"] = "限时活动",
        ["ActivityShikigami"] = "当期爬塔",
        ["AreaBoss"] = "地域鬼王",
        ["BondlingFairyland"] = "契灵之境",
        ["CollectiveMissions"] = "集体任务",
        ["Daily Task"] = "日常任务",
        ["DailyTrifles"] = "每日琐事",
        ["Delegation"] = "式神委派",
        ["DemonEncounter"] = "逢魔之时",
        ["DemonRetreat"] = "首领退治",
        ["Duel"] = "斗技",
        ["DyeTrials"] = "灵染试炼",
        ["EternitySea"] = "永生之海",
        ["EvoZone"] = "觉醒副本",
        ["ExperienceYoukai"] = "经验妖怪",
        ["Exploration"] = "探索",
        ["FallenSun"] = "日轮之陨",
        ["GlobalGame"] = "全局配置",
        ["GoldYoukai"] = "金币妖怪",
        ["GoryouRealm"] = "御灵之境",
        ["Guild"] = "阴阳寮",
        ["Home"] = "主页",
        ["Hunt"] = "狩猎战",
        ["KekkaiActivation"] = "结界挂卡",
        ["KekkaiUtilize"] = "结界蹭卡",
        ["Liver Emperor Exclusive"] = "肝帝专属",
        ["MemoryScrolls"] = "绘卷",
        ["MetaDemon"] = "超鬼王",
        ["MysteryShop"] = "神秘商店",
        ["Nian"] = "年兽",
        ["Orochi"] = "八岐大蛇",
        ["OtherWorldTwilight"] = "彼世逢魔",
        ["Overview"] = "总览",
        ["Pets"] = "小猫咪",
        ["RealmRaid"] = "个人突破",
        ["Restart"] = "重启",
        ["RichMan"] = "大富翁",
        ["RyouToppa"] = "寮突破",
        ["Script"] = "脚本",
        ["Secret"] = "秘闻副本",
        ["Sougenbi"] = "业原火",
        ["Soul Zones"] = "御魂副本",
        ["SoulsTidy"] = "御魂整理",
        ["Tako"] = "石距",
        ["TalismanPass"] = "花合战",
        ["TaskList"] = "任务列表",
        ["Tool"] = "工具",
        ["Tools"] = "工具",
        ["TrueOrochi"] = "真八岐大蛇",
        ["Updater"] = "更新器",
        ["WantedQuests"] = "悬赏封印",
        ["Weekly Task"] = "每周任务",
        ["WeeklyTrifles"] = "每周琐事",
    };

    public static string Tr(string? text) =>
        text is not null && Map.TryGetValue(text, out var zh) ? zh : text;
}