using System.Text.RegularExpressions;

namespace PZServerManager;

internal static class ConfigSettingLocalization
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StartYear"] = "世界開始年份", ["StartMonth"] = "世界開始月份",
        ["StartDay"] = "世界開始日期", ["StartTime"] = "世界開始時間",
        ["DayNightCycle"] = "日夜循環模式", ["ClimateCycle"] = "氣候循環模式",
        ["FogCycle"] = "霧氣循環模式", ["WaterShut"] = "停水時機",
        ["ElecShut"] = "停電時機", ["AlarmDecay"] = "警報器衰減",
        ["AlarmDecayModifier"] = "警報器衰減倍率", ["Temperature"] = "氣溫",
        ["Rain"] = "降雨量", ["ErosionSpeed"] = "自然侵蝕速度",
        ["ErosionDays"] = "完全侵蝕所需天數", ["Farming"] = "農作物生長速度",
        ["CompostTime"] = "堆肥時間", ["NatureAbundance"] = "自然資源豐富度",
        ["Alarm"] = "房屋警報器機率", ["LockedHouses"] = "上鎖房屋比例",
        ["FoodRotSpeed"] = "食物腐敗速度", ["FridgeFactor"] = "冰箱保鮮效果",
        ["SeenHoursPreventLootRespawn"] = "玩家看見後禁止物資重生時間",
        ["MaxItemsForLootRespawn"] = "容器允許重生的物品上限",
        ["ConstructionPreventsLootRespawn"] = "玩家建築阻止物資重生",
        ["HoursForWorldItemRemoval"] = "地面物品移除時間",
        ["ItemRemovalListBlacklistToggle"] = "地面物品移除清單模式",
        ["TimeSinceApo"] = "末日爆發後經過時間", ["PlantResilience"] = "植物抗性",
        ["PlantAbundance"] = "植物產量", ["Helicopter"] = "直升機事件頻率",
        ["MetaEvent"] = "環境事件頻率", ["SleepingEvent"] = "睡眠事件",
        ["GeneratorFuelConsumption"] = "發電機耗油倍率",
        ["GeneratorSpawning"] = "發電機生成率", ["SurvivorHouseChance"] = "倖存者房屋機率",
        ["VehicleStoryChance"] = "道路車輛事件機率",
        ["EnableTaintedWaterText"] = "顯示受污染水源提示",
        ["Map.AllowMiniMap"] = "允許小地圖", ["Map.AllowWorldMap"] = "允許世界地圖",
        ["Map.MapAllKnown"] = "開局顯示完整地圖", ["Map.MapNeedsLight"] = "閱讀地圖需要光源",

        ["ConstructionBonusPoints"] = "玩家建築耐久加成",
        ["MinutesPerPage"] = "閱讀每頁所需分鐘", ["AttackBlockMovements"] = "攻擊時限制移動",
        ["EnablePoisoning"] = "啟用中毒機制", ["AllClothesUnlocked"] = "解鎖所有創角服裝",
        ["EnableVehicles"] = "啟用車輛", ["CarSpawnRate"] = "車輛生成率",
        ["VehicleEasyUse"] = "簡易車輛使用", ["LockedCar"] = "車輛上鎖比例",
        ["CarGasConsumption"] = "車輛耗油倍率", ["CarGeneralCondition"] = "車輛整體狀況",
        ["CarDamageOnImpact"] = "車輛撞擊受損", ["DamageToPlayerFromHitByACar"] = "車輛撞擊玩家傷害",
        ["CarAlarm"] = "車輛警報器機率", ["AllowExteriorGenerator"] = "允許室外發電機供電",

        ["Zombies"] = "殭屍數量", ["Distribution"] = "殭屍分布方式",
        ["ZombieVoronoiNoise"] = "殭屍區域密度差異", ["ZombieRespawn"] = "啟用殭屍重生",
        ["ZombieMigrate"] = "啟用殭屍遷徙", ["ZombieHealthImpact"] = "受傷對殭屍行為的影響",
        ["ZombieAttractionMultiplier"] = "殭屍吸引力倍率",
        ["ZombieLore.Speed"] = "殭屍速度", ["ZombieLore.Strength"] = "殭屍力量",
        ["ZombieLore.Toughness"] = "殭屍耐久", ["ZombieLore.Transmission"] = "感染傳播方式",
        ["ZombieLore.Mortality"] = "感染致死時間", ["ZombieLore.Reanimate"] = "屍體復活時間",
        ["ZombieLore.Cognition"] = "殭屍認知能力", ["ZombieLore.CrawlUnderVehicle"] = "鑽入車底能力",
        ["ZombieLore.Memory"] = "殭屍記憶力", ["ZombieLore.Sight"] = "殭屍視力",
        ["ZombieLore.Hearing"] = "殭屍聽力", ["ZombieLore.SpottedLogic"] = "發現玩家的判定方式",
        ["ZombieLore.ThumpNoChasing"] = "未追逐時破壞障礙物",
        ["ZombieLore.ThumpOnConstruction"] = "攻擊玩家建造物",
        ["ZombieLore.ActiveOnly"] = "殭屍活躍時段", ["ZombieLore.TriggerHouseAlarm"] = "殭屍觸發房屋警報",
        ["ZombieLore.ZombiesDragDown"] = "殭屍拖倒玩家", ["ZombieLore.ZombiesFenceLunge"] = "翻越圍欄撲擊",
        ["ZombieLore.ZombiesCrawlersDragDown"] = "爬行殭屍拖倒玩家",
        ["ZombieLore.DisableFakeDead"] = "停用裝死殭屍", ["ZombieLore.PlayerSpawnZombieRemoval"] = "出生點清除殭屍範圍",
        ["ZombieLore.FenceDamageMultiplier"] = "圍欄傷害倍率", ["ZombieLore.FenceThumpersRequired"] = "破壞圍欄所需殭屍數",
        ["ZombieLore.ChanceOfAttachedWeapon"] = "殭屍攜帶武器機率",
        ["ZombieLore.DoorOpeningPercentage"] = "可開門殭屍比例",
        ["ZombieLore.SprinterPercentage"] = "衝刺殭屍比例", ["ZombieLore.ZombiesArmorFactor"] = "殭屍護甲效果",
        ["ZombieLore.ZombiesMaxDefense"] = "殭屍最大防禦", ["ZombieLore.ZombiesFallDamage"] = "殭屍墜落傷害",
        ["ZombieConfig.PopulationMultiplier"] = "殭屍人口倍率",
        ["ZombieConfig.PopulationStartMultiplier"] = "開局人口倍率",
        ["ZombieConfig.PopulationPeakMultiplier"] = "巔峰人口倍率",
        ["ZombieConfig.PopulationPeakDay"] = "人口巔峰日",
        ["ZombieConfig.RespawnHours"] = "殭屍重生間隔",
        ["ZombieConfig.RespawnUnseenHours"] = "區域未被看見多久才可重生",
        ["ZombieConfig.RespawnMultiplier"] = "每次重生比例",
        ["ZombieConfig.RedistributeHours"] = "殭屍重新分布間隔",
        ["ZombieConfig.FollowSoundDistance"] = "追蹤聲音距離",
        ["ZombieConfig.RallyGroupSize"] = "群聚最大數量",
        ["ZombieConfig.RallyGroupSizeVariance"] = "群聚數量變動",
        ["ZombieConfig.RallyTravelDistance"] = "群聚移動距離",
        ["ZombieConfig.RallyGroupSeparation"] = "群聚之間距離",
        ["ZombieConfig.RallyGroupRadius"] = "群聚半徑",
        ["ZombieConfig.ZombiesCountBeforeDelete"] = "遠端殭屍刪除門檻",

        ["AntiCheatChecksum"] = "檔案校驗反作弊", ["AntiCheatHit"] = "攻擊命中反作弊",
        ["AntiCheatNoClip"] = "穿牆反作弊", ["AntiCheatPacketException"] = "異常封包反作弊",
        ["AntiCheatPermission"] = "權限反作弊", ["AntiCheatPlayer"] = "玩家狀態反作弊",
        ["AntiCheatSafeHouse"] = "安全屋反作弊", ["AntiCheatSafety"] = "PVP 安全模式反作弊",
        ["AntiCheatSpeed"] = "移動速度反作弊", ["AntiCheatXP"] = "經驗值反作弊"
    };

    private static readonly Dictionary<string, string> SkillNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Aiming"] = "瞄準", ["Axe"] = "斧頭", ["Blacksmith"] = "鍛造", ["Blunt"] = "長鈍器",
        ["Butchering"] = "屠宰", ["Carving"] = "雕刻", ["Cooking"] = "烹飪", ["Doctor"] = "醫療",
        ["Electricity"] = "電工", ["Farming"] = "耕作", ["Fishing"] = "釣魚", ["Fitness"] = "體適能",
        ["FlintKnapping"] = "燧石打製", ["Glassmaking"] = "玻璃製作", ["Husbandry"] = "畜牧",
        ["Lightfoot"] = "輕盈腳步", ["LongBlade"] = "長刃", ["Maintenance"] = "維護",
        ["Masonry"] = "石工", ["Mechanics"] = "機械", ["MetalWelding"] = "金屬加工",
        ["Nimble"] = "靈活", ["PlantScavenging"] = "採集", ["Pottery"] = "製陶",
        ["Reloading"] = "裝填", ["SmallBlade"] = "短刃", ["SmallBlunt"] = "短鈍器",
        ["Sneak"] = "潛行", ["Spear"] = "長矛", ["Sprinting"] = "衝刺", ["Strength"] = "力量",
        ["Tailoring"] = "裁縫", ["Tracking"] = "追蹤", ["Trapping"] = "陷阱", ["Woodwork"] = "木工"
    };

    private static readonly (string English, string Chinese)[] OptionTranslations =
    {
        ("Inside the building and around it", "建築內及周圍"),
        ("Random between Normal and None", "在正常與無之間隨機"),
        ("Random between Normal and Poor", "在正常與差之間隨機"),
        ("Navigate and Use Doors", "會導航並使用門"),
        ("Only bleach poisoning is disabled", "只停用漂白水中毒"),
        ("World and Combat Zombies", "世界及戰鬥產生的殭屍"),
        ("Inside the building", "建築內"), ("Inside the room", "房間內"),
        ("Zombies can spawn anywhere", "殭屍可在任何位置生成"),
        ("Everyone's Infected", "所有人已感染"),
        ("Blood and Saliva", "血液＋唾液"), ("Blood + Saliva", "血液＋唾液"),
        ("None (not recommended)", "無（不建議）"),
        ("Endless Blizzard", "無盡暴風雪"), ("Endless Storm", "無盡風暴"),
        ("Endless Night", "無盡黑夜"), ("Endless Rain", "無盡降雨"),
        ("Endless Snow", "無盡降雪"), ("Endless Fog", "無盡濃霧"),
        ("Endless Day", "無盡白晝"), ("No Weather", "無天氣"), ("No Fog", "無霧"),
        ("No decay", "不衰減"), ("Always Tries", "總是嘗試"),
        ("Basic Navigation", "基本導航"), ("Crawlers Only", "僅爬行殭屍"),
        ("Fast Shamblers", "快速蹣跚者"), ("Urban Focused", "集中於都市"),
        ("Saliva Only", "僅唾液"), ("World Zombies", "世界殭屍"),
        ("Very Abundant", "非常豐富"), ("Very Frequent", "非常頻繁"),
        ("Extremely Rare", "極度稀少"), ("Insanely Rare", "近乎不會出現"),
        ("Very Rainy", "雨量非常多"), ("Very Dry", "非常乾燥"),
        ("Very Poor", "非常差"), ("Very Often", "非常常見"),
        ("Very Cold", "非常寒冷"), ("Very Hot", "非常炎熱"),
        ("Very High", "非常高"), ("Very Low", "非常低"),
        ("Very Fast", "非常快"), ("Very Slow", "非常慢"),
        ("Superhuman", "超人"), ("Sprinters", "衝刺者"),
        ("Shamblers", "蹣跚者"), ("Fragile", "脆弱"), ("Tough", "堅韌"),
        ("Pinpoint", "精準"), ("Navigate", "會導航"), ("Disabled", "停用"),
        ("Always", "總是"), ("Both", "兩者"), ("Common", "常見"),
        ("Abundant", "豐富"), ("Frequent", "頻繁"), ("Sometimes", "偶爾"),
        ("Often", "常見"), ("Once", "一次"), ("Randomized", "隨機化"),
        ("Random", "隨機"), ("Uniform", "均勻"), ("Insane", "瘋狂"),
        ("Rainy", "多雨"), ("Dry", "乾燥"), ("Rare", "稀少"),
        ("Never", "永不"), ("Instant", "立即"), ("High", "高"),
        ("Normal", "正常"), ("Low", "低"), ("Fast", "快"), ("Slow", "慢"),
        ("Long", "長"), ("Short", "短"), ("Poor", "差"), ("Good", "良好"),
        ("Cold", "寒冷"), ("Hot", "炎熱"), ("Weak", "虛弱"), ("Eagle", "鷹眼"),
        ("None", "無"), ("Day", "白天"), ("Night", "夜晚"),
        ("January", "一月"), ("February", "二月"), ("March", "三月"),
        ("April", "四月"), ("May", "五月"), ("June", "六月"),
        ("July", "七月"), ("August", "八月"), ("September", "九月"),
        ("October", "十月"), ("November", "十一月"), ("December", "十二月"),
        ("Years", "年"), ("Year", "年"), ("Months", "個月"), ("Month", "個月"),
        ("Weeks", "週"), ("Week", "週"), ("Days", "天"),
        ("Hours", "小時"), ("Minutes", "分鐘"), ("Seconds", "秒"),
        ("AM", "上午"), ("PM", "下午"), ("On", "開啟"), ("Off", "關閉"),
        ("True", "true（開啟）"), ("False", "false（關閉）")
    };

    public static void Apply(ConfigValueRow row)
    {
        var english = LocalizationService.CurrentLanguage.Equals("en-US", StringComparison.OrdinalIgnoreCase);
        if (english)
        {
            row.DisplayName = Humanize(row.Key);
            row.LocalizedAllowedRange = row.AllowedRange;
            row.LocalizedNotes = string.IsNullOrWhiteSpace(row.Notes)
                ? $"Build 42 setting: {row.Key}." : row.Notes;
            return;
        }

        row.DisplayName = DisplayName(row);
        row.LocalizedAllowedRange = TranslateOptions(row.AllowedRange);
        row.LocalizedNotes = ChineseDescription(row);
    }

    private static string DisplayName(ConfigValueRow row)
    {
        if (Names.TryGetValue(row.Key, out var name)) return name;
        if (row.Key.StartsWith("MultiplierConfig.", StringComparison.OrdinalIgnoreCase))
        {
            var skill = row.Key[(row.Key.IndexOf('.') + 1)..];
            return $"{(SkillNames.TryGetValue(skill, out var translated) ? translated : "其他技能")}經驗倍率";
        }
        if (row.Category is not ("沙盒世界" or "Map" or "ZombieLore" or "ZombieConfig" or "MultiplierConfig" or "伺服器 INI"))
            return $"{row.Category} 自訂設定";
        return "其他 Build 42 設定";
    }

    private static string ChineseDescription(ConfigValueRow row)
    {
        if (row.Key.StartsWith("MultiplierConfig.", StringComparison.OrdinalIgnoreCase))
            return $"控制「{row.DisplayName.Replace("經驗倍率", "") }」技能取得經驗的倍率；數值越高，升級越快。";
        if (row.Key.StartsWith("AntiCheat", StringComparison.OrdinalIgnoreCase))
            return $"控制「{row.DisplayName}」的檢查等級。修改前請確認用途，避免過度放寬伺服器驗證。";
        if (row.Key.StartsWith("ZombieLore.", StringComparison.OrdinalIgnoreCase))
            return $"控制殭屍個體特性中的「{row.DisplayName}」。";
        if (row.Key.StartsWith("ZombieConfig.", StringComparison.OrdinalIgnoreCase))
            return $"控制殭屍人口、重生或群聚系統中的「{row.DisplayName}」。";
        if (row.Key is "Zombies" or "Distribution" or "ZombieVoronoiNoise" or "ZombieRespawn" or
            "ZombieMigrate" or "ZombieHealthImpact" or "ZombieAttractionMultiplier")
            return $"控制殭屍人口與世界分布中的「{row.DisplayName}」。";
        if (FeaturedWorld(row.Key)) return $"控制世界、事件、物資或環境中的「{row.DisplayName}」。";
        if (FeaturedPlayer(row.Key)) return $"控制玩家、生存、技能或車輛中的「{row.DisplayName}」。";
        if (row.Category is not ("沙盒世界" or "伺服器 INI"))
            return "此欄位由模組作者定義；管理器保留原始設定鍵，不會猜測未知值的用途。";
        return $"控制 Build 42 的「{row.DisplayName}」。";
    }

    private static bool FeaturedWorld(string key) => Names.ContainsKey(key) &&
        !key.StartsWith("Zombie", StringComparison.OrdinalIgnoreCase) &&
        !key.StartsWith("AntiCheat", StringComparison.OrdinalIgnoreCase) &&
        !FeaturedPlayer(key);

    private static bool FeaturedPlayer(string key) => key.StartsWith("MultiplierConfig.", StringComparison.OrdinalIgnoreCase) ||
        key is "ConstructionBonusPoints" or "MinutesPerPage" or "AttackBlockMovements" or
            "EnablePoisoning" or "AllClothesUnlocked" or "EnableVehicles" or "CarSpawnRate" or
            "VehicleEasyUse" or "LockedCar" or "CarGasConsumption" or "CarGeneralCondition" or
            "CarDamageOnImpact" or "DamageToPlayerFromHitByACar" or "CarAlarm" or "AllowExteriorGenerator";

    private static string TranslateOptions(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return "未註明";
        var result = source;
        foreach (var (english, chinese) in OptionTranslations)
            result = Regex.Replace(result, $@"(?<![A-Za-z]){Regex.Escape(english)}(?![A-Za-z])",
                chinese, RegexOptions.IgnoreCase);
        return result.Replace(" ; ", "；").Replace(";", "；");
    }

    private static string Humanize(string key)
    {
        var leaf = key.Contains('.') ? key[(key.LastIndexOf('.') + 1)..] : key;
        return Regex.Replace(leaf, "(?<=[a-z0-9])(?=[A-Z])", " ");
    }
}
