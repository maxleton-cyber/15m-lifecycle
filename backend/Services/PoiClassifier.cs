using Lifecycle.Api.Models;

namespace Lifecycle.Api.Services;

/// <summary>将原始 POI 按 tag/名称规则映射到民生设施类别。</summary>
public static class PoiClassifier
{
    public static readonly (string Category, string Label, double RequiredRadiusMeters)[] Categories =
    [
        ("medical", "基础医疗", 1200),
        ("pharmacy", "药店", 1000),
        ("education", "学校", 1500),
        ("market", "购物", 1000),
        ("elderly", "养老服务", 1500),
        ("convenience", "商超", 800),
        ("park", "公园绿地", 800),
        ("transit", "公共交通", 800),
    ];

    public static IReadOnlyList<string> Classify(PoiItem poi)
    {
        var text = BuildSearchText(poi);
        var matched = new List<string>();

        if (IsPharmacy(text)) matched.Add("pharmacy");
        if (IsMedical(text)) matched.Add("medical");
        if (IsEducation(text)) matched.Add("education");
        if (IsElderly(text)) matched.Add("elderly");
        if (IsPark(text)) matched.Add("park");
        if (IsTransit(text)) matched.Add("transit");

        // 商超优先于广义购物，避免同一 POI 重复归类
        if (IsSupermarketConvenience(text))
            matched.Add("convenience");
        else if (IsShopping(text))
            matched.Add("market");

        return matched;
    }

    private static string BuildSearchText(PoiItem poi)
    {
        var tag = poi.Tag ?? string.Empty;
        return $"{poi.Name}|{tag}|{poi.Address}";
    }

    private static bool IsPharmacy(string text) =>
        ContainsAny(text, "药店", "药房", "药局", "Pharmacy") &&
        !ContainsAny(text, "宠物", "兽医");

    /// <summary>基础医疗：社区机构、诊所、各级医院（含专科），排除宠物/医美。</summary>
    private static bool IsMedical(string text)
    {
        if (IsPharmacy(text)) return false;
        if (ContainsAny(text, "宠物", "兽医", "美容整形", "医美", "整形外科", "齿科美白"))
            return false;

        return ContainsAny(
            text,
            "医院",
            "诊所",
            "门诊",
            "医务室",
            "社区卫生",
            "社区医院",
            "卫生站",
            "卫生院",
            "社康",
            "卫生服务",
            "医疗中心",
            "急救中心",
            "综合医院",
            "专科医院",
            "医疗;医院",
            "医疗;诊所",
            "医疗;综合",
            "医疗;专科",
            "医疗;卫生",
            "医疗;社区");
    }

    private static bool IsEducation(string text)
    {
        if (ContainsAny(text, "驾校", "留学咨询", "留学中介", "教育咨询", "家教", "网校"))
            return false;

        if (ContainsAny(text, "培训", "补习", "托管") &&
            !ContainsAny(text, "学校", "学院", "大学", "幼儿园", "小学", "中学"))
            return false;

        return ContainsAny(
                   text,
                   "幼儿园",
                   "托儿所",
                   "小学",
                   "中学",
                   "初中",
                   "高中",
                   "大学",
                   "学院",
                   "院校",
                   "学府",
                   "学校",
                   "附小",
                   "附中",
                   "实验学校",
                   "教育;幼儿园",
                   "教育;小学",
                   "教育;中学",
                   "教育;高中",
                   "教育;高等院校",
                   "教育;学校",
                   "教育;学院",
                   "教育;大学") ||
               (ContainsAny(text, "教育") &&
                ContainsAny(text, "幼儿园", "小学", "中学", "高中", "大学", "学院", "学校", "院校"));
    }

    /// <summary>购物：菜市场、商场、百货及各类零售店铺（不含商超、药店）。</summary>
    private static bool IsShopping(string text)
    {
        if (IsPharmacy(text) || IsMedical(text) || IsSupermarketConvenience(text))
            return false;

        return ContainsAny(
            text,
            "菜市场",
            "农贸市场",
            "菜场",
            "农贸",
            "农产品",
            "综合市场",
            "商场",
            "购物中心",
            "购物广场",
            "百货",
            "奥特莱斯",
            "商业街",
            "步行街",
            "商铺",
            "店铺",
            "专卖",
            "零售",
            "商业",
            "市场",
            "家居",
            "建材",
            "家具",
            "五金",
            "服装",
            "电器",
            "购物;",
            "商业;",
            "生活服务;商铺",
            "生活服务;市场");
    }

    /// <summary>商超：便利店、连锁超市及大型卖场。</summary>
    private static bool IsSupermarketConvenience(string text)
    {
        if (IsPharmacy(text)) return false;

        return ContainsAny(
            text,
            "便利店",
            "超市",
            "大卖场",
            "会员店",
            "仓储超市",
            "7-11",
            "7-ELEVEN",
            "全家",
            "罗森",
            "喜士多",
            "便利蜂",
            "华润万家",
            "物美",
            "家乐福",
            "沃尔玛",
            "永辉",
            "盒马",
            "麦德龙",
            "山姆",
            "Costco",
            "购物;超市",
            "购物;便利店",
            "购物;超级市场",
            "购物;仓储");
    }

    private static bool IsElderly(string text) =>
        ContainsAny(text, "养老", "敬老院", "颐养", "护理院", "老年公寓", "福利院", "长者");

    private static bool IsPark(string text) =>
        ContainsAny(
            text,
            "公园",
            "绿地",
            "花园",
            "湿地",
            "森林公园",
            "风景;公园",
            "风景名胜",
            "广场") &&
        !ContainsAny(text, "购物广场", "商业广场", "建材广场");

    private static bool IsTransit(string text)
    {
        // 站名/地址里带「地铁站」的商户，不是交通设施本身
        if (ContainsAny(
                text,
                "酒店",
                "宾馆",
                "客栈",
                "民宿",
                "饭店",
                "餐馆",
                "餐厅",
                "咖啡",
                "奶茶",
                "银行",
                "SPA",
                "按摩",
                "洗浴",
                "维修",
                "4S店",
                "销售中心",
                "物业",
                "写字楼",
                "大厦"))
            return false;

        if (ContainsAny(
                text,
                "地铁",
                "轨道交通",
                "公交站",
                "公交车站",
                "巴士站",
                "公共汽车站",
                "巴士",
                "轻轨",
                "BRT",
                "有轨",
                "交通设施;地铁",
                "交通设施;公交",
                "交通设施;公交车站",
                "交通设施;巴士",
                "交通设施;轻轨",
                "地铁站",
                "地铁口",
                "轻轨站",
                "客运",
                "站台"))
            return true;

        if (HasBusRouteAddress(text))
            return true;

        return ContainsAny(text, "交通设施") &&
               ContainsAny(text, "站", "出口", "出入口", "枢纽");
    }

    /// <summary>地址/name 含多条公交线路号，典型公交站牌格式如「451路;593路」。</summary>
    private static bool HasBusRouteAddress(string text) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            text,
            @"\d+路(?:;|\||$|\s).*\d+路|\d+路;\d+路");

    private static bool ContainsAny(string text, params string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
