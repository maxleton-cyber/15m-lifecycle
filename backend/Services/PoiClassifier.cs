using Lifecycle.Api.Models;

namespace Lifecycle.Api.Services;

/// <summary>
/// 将原始 POI 映射到 7 类民生设施（见 docs/15min-life-circle-blind-area-spec.md）。
/// 一个 POI 只归一类；冲突时排除规则优先，再按类别优先级裁决。
/// </summary>
public static class PoiClassifier
{
    /// <summary>类别 key、中文名、默认可达阈值 R（米）。</summary>
    public static readonly (string Category, string Label, double RequiredRadiusMeters)[] Categories =
    [
        ("medical", "基础医疗", 1000),
        ("pharmacy", "药店", 1000),
        ("education", "学校", 1000),
        ("market", "购物", 1000),
        ("elderly", "养老服务", 1000),
        ("park", "公园绿地", 1000),
        ("transit", "公共交通", 1000),
    ];

    // 冲突优先级：越靠前越优先
    private static readonly string[] Priority =
        ["pharmacy", "medical", "education", "elderly", "market", "park", "transit"];

    public static IReadOnlyList<string> Classify(PoiItem poi)
    {
        var text = BuildSearchText(poi);
        var matched = new List<string>();

        if (IsPharmacy(text)) matched.Add("pharmacy");
        if (IsMedical(text)) matched.Add("medical");
        if (IsSchool(text)) matched.Add("education");
        if (IsElderly(text)) matched.Add("elderly");
        if (IsShopping(text)) matched.Add("market");
        if (IsPark(poi)) matched.Add("park");
        if (IsTransit(text)) matched.Add("transit");

        if (matched.Count == 0)
            return matched;

        // 单类别输出：按优先级取第一个命中
        foreach (var key in Priority)
        {
            if (matched.Contains(key))
                return [key];
        }

        return [matched[0]];
    }

    /// <summary>
    /// 仅用名称 + 百度 tag 做分类匹配，不用详细地址。
    /// 地址里常含邻近地标（如「XX幼儿园西南侧」），用地址会把商户误判成学校等。
    /// </summary>
    private static string BuildSearchText(PoiItem poi)
    {
        var tag = poi.Tag ?? string.Empty;
        return $"{poi.Name}|{tag}";
    }

    /// <summary>连锁药店品牌（店名常不带「药店」二字，如北京同仁堂）。</summary>
    private static readonly string[] PharmacyChains =
    [
        "同仁堂",
        "国大药房",
        "老百姓大药房",
        "老百姓",
        "益丰大药房",
        "益丰",
        "大参林",
        "一心堂",
        "健之佳",
        "海王星辰",
        "叮当快药",
        "叮当",
        "高济",
        "漱玉平民",
        "漱玉",
        "芝林大药房",
        "养天和",
        "华人健康",
        "全亿健康",
        "康之家",
        "德生堂",
        "采芝林",
        "雷允上",
        "桐君阁",
        "九芝堂",
        "云南白药",
        "片仔癀",
        "东盛大药房",
        "芝林",
        "百信药房",
        "百信药业",
        "杏林大药房",
        "乐仁堂",
        "胡庆余堂",
        "蔡同德",
        "童涵春堂",
        "达仁堂",
        "天士力",
        "和平大药房",
        "金象大药房",
        "金象",
        "嘉事堂",
        "养和堂",
        "尚德大药房",
    ];

    private static bool IsPharmacy(string text)
    {
        if (ContainsAny(text, "宠物", "兽医", "批发", "器械", "保健品", "院内药房", "医院药房"))
            return false;

        // 「同仁堂中医医院」等医疗主体不算药店
        if (ContainsAny(text, "医院", "诊所", "门诊部", "卫生院", "卫生服务中心") &&
            !ContainsAny(text, "药店", "药房", "药局", "大药房"))
            return false;

        if (ContainsAny(
                text,
                "药店",
                "药房",
                "药局",
                "Pharmacy",
                "大药房",
                "中药店",
                "中药铺",
                "药行",
                "药铺",
                "医疗;药店",
                "购物;药店",
                "生活服务;药店"))
            return true;

        return ContainsAny(text, PharmacyChains);
    }

    /// <summary>基础医疗：含口腔、中医、妇幼等专科；药店单独成类。</summary>
    private static bool IsMedical(string text)
    {
        if (IsPharmacy(text)) return false;
        if (ContainsAny(
                text,
                "宠物",
                "兽医",
                "美容整形",
                "医美",
                "整形外科",
                "齿科美白",
                "体检中心",
                "健康体检",
                "医疗器械",
                "器械店"))
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
            "口腔",
            "牙科",
            "中医",
            "妇幼",
            "妇产",
            "儿科",
            "眼科",
            "医疗;医院",
            "医疗;诊所",
            "医疗;综合",
            "医疗;专科",
            "医疗;卫生",
            "医疗;社区",
            "医疗;口腔",
            "医疗;中医");
    }

    /// <summary>学校：幼儿园、小学、初中及「实验学校」等；排除高中/大学与培训机构。</summary>
    private static bool IsSchool(string text)
    {
        // 子点 / 非目标学段 / 培训机构
        if (ContainsAny(
                text,
                "高中",
                "大学",
                "学院",
                "院校",
                "高等院校",
                "培训学校",
                "培训机构",
                "培训中心",
                "补习",
                "辅导班",
                "早教中心",
                "早教机构",
                "驾校",
                "留学",
                "家教",
                "网校",
                "进修",
                "专修学校",
                "教育咨询",
                "教育;高中",
                "教育;高等院校",
                "教育;大学",
                "教育;学院",
                "食堂",
                "宿舍",
                "操场"))
            return false;

        // 明确学段与常见校名
        if (ContainsAny(
                text,
                "幼儿园",
                "托儿所",
                "托幼",
                "幼稚园",
                "小学",
                "附小",
                "实验小学",
                "附属小学",
                "初中",
                "初级中学",
                "中学",
                "附中",
                "实验学校",
                "实验中学",
                "九年一贯",
                "一贯制",
                "教育;幼儿园",
                "教育;小学",
                "教育;中学",
                "教育;学校"))
            return true;

        // 名称含「学校」，但排除培训类
        if (text.Contains("学校", StringComparison.Ordinal) &&
            !ContainsAny(text, "培训", "辅导", "驾校", "网校", "专修", "进修", "成人"))
            return true;

        return false;
    }

    /// <summary>购物：仅菜市场/农贸、超市、便利店。</summary>
    private static bool IsShopping(string text)
    {
        if (IsPharmacy(text) || IsMedical(text))
            return false;
        if (ContainsAny(text, "专卖", "服饰", "数码", "烟酒", "自提", "批发市场", "奥特莱斯", "购物中心", "百货"))
            return false;

        return ContainsAny(
            text,
            "菜市场",
            "农贸市场",
            "菜场",
            "农贸",
            "便利店",
            "超市",
            "大卖场",
            "仓储超市",
            "会员店",
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
            "购物;农贸市场",
            "购物;菜市场");
    }

    private static bool IsElderly(string text)
    {
        if (ContainsAny(text, "殡葬", "墓园", "公墓", "保健品", "售楼", "地产"))
            return false;

        return ContainsAny(
            text,
            "养老",
            "敬老院",
            "颐养",
            "护理院",
            "老年公寓",
            "福利院",
            "长者",
            "日间照料",
            "老年活动",
            "养老驿站",
            "养老服务");
    }

    private static bool IsPark(PoiItem poi)
    {
        var name = poi.Name ?? string.Empty;
        var tag = poi.Tag ?? string.Empty;
        var text = BuildSearchText(poi);

        if (IsParkCommercialFalsePositive(name, tag, text))
            return false;
        if (ContainsAny(text, "墓园", "公墓", "游乐园", "收费", "小区绿化", "绿化带"))
            return false;

        if (HasParkTag(tag))
            return true;

        return IsParkFacilityName(name);
    }

    private static bool HasParkTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        if (ContainsAny(
                tag,
                "旅游景点;公园",
                "风景名胜;公园",
                "休闲娱乐;公园",
                "休闲娱乐;公园广场",
                "旅游景点;动物园",
                "旅游景点;植物园",
                "公园绿地",
                "城市公园",
                "湿地公园",
                "森林公园"))
            return true;

        var parkInTag = tag.Equals("公园", StringComparison.Ordinal) ||
                        tag.Contains(";公园", StringComparison.Ordinal) ||
                        tag.Contains("公园;", StringComparison.Ordinal) ||
                        tag.EndsWith("公园", StringComparison.Ordinal);
        return parkInTag &&
               !ContainsAny(tag, "购物", "美食", "餐饮", "酒店", "房地产", "生活服务", "公司企业");
    }

    private static bool IsParkFacilityName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (System.Text.RegularExpressions.Regex.IsMatch(
                name,
                @"(公园|花园|绿地|广场).{0,6}(店|餐厅|饭店|咖啡|酒店|宾馆|超市|便利店|专卖)"))
            return false;

        if (ContainsAny(name, "小学", "中学", "幼儿园", "大学", "学院", "学校", "地铁", "公交"))
            return false;

        if (ContainsAny(
                name,
                "湿地公园",
                "森林公园",
                "滨江公园",
                "中央公园",
                "中心公园",
                "城市公园",
                "植物园",
                "口袋公园",
                "街心公园",
                "公共绿地",
                "滨江绿地",
                "中心绿地",
                "街心绿地"))
            return true;

        if ((name.EndsWith("公园", StringComparison.Ordinal) ||
             name.EndsWith("公园景区", StringComparison.Ordinal)) &&
            !ContainsAny(name, "公园店", "公园路", "公园旁", "公园对面"))
            return true;

        if (ContainsAny(
                name,
                "市民广场",
                "文化广场",
                "休闲广场",
                "纪念广场",
                "市政广场",
                "站前广场",
                "城市广场") &&
            !ContainsAny(name, "购物", "商业", "建材", "美食", "娱乐", "餐饮"))
            return true;

        return false;
    }

    private static bool IsParkCommercialFalsePositive(string name, string tag, string text)
    {
        if (ContainsAny(
                tag,
                "购物;",
                "美食;",
                "餐饮;",
                "酒店;",
                "房地产;",
                "生活服务;商铺",
                "生活服务;商场",
                "公司企业",
                "购物中心",
                "超市",
                "便利店"))
            return true;

        if (ContainsAny(
                text,
                "购物广场",
                "商业广场",
                "建材广场",
                "美食广场",
                "娱乐广场",
                "购物中心",
                "百货",
                "奥特莱斯",
                "商场",
                "专卖店",
                "便利店",
                "超市"))
            return true;

        if (System.Text.RegularExpressions.Regex.IsMatch(
                name,
                @"(店|餐厅|饭店|咖啡|酒店|宾馆|超市|便利店|专卖|会所|会馆)$"))
            return true;

        return false;
    }

    private static bool IsTransit(string text)
    {
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
                "大厦",
                "停车场",
                "长途",
                "客运站"))
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
                "站台"))
            return true;

        if (HasBusRouteAddress(text))
            return true;

        return ContainsAny(text, "交通设施") &&
               ContainsAny(text, "站", "出口", "出入口", "枢纽");
    }

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
