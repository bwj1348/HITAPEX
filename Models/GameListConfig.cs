namespace HITAPEX.Models;

/// <summary>
/// 硬编码游戏列表配置。
/// 游戏 ID 与 TelemetryAPI.GameId 枚举一一对应，便于为每个游戏配置遥测。
/// </summary>
public static class GameListConfig
{
    /// <summary>
    /// 获取所有 31 款支持遥测的游戏列表。
    /// </summary>
    public static List<GameItem> GetGames()
    {
        return new List<GameItem>
        {
            // ============================
            // Assetto Corsa 系列 (4 款) — 启动后自动遥测，无需配置
            // ============================
            new()
            {
                Id = 244210,
                Name = "Assetto Corsa",
                Abbreviation = "AC",
                SteamId = "244210",
                Description = "Kunos Simulazioni 出品的经典赛车模拟器，以出色的物理引擎和极高的 MOD 自由度闻名。拥有从民用车到方程式赛车的广泛车型，Nordschleife 激光扫描赛道为标杆之作。",
                DescriptionEn = "A classic racing simulator by Kunos Simulazioni, renowned for its outstanding physics engine and extensive mod support. Features a vast lineup from road cars to Formula racing, with the laser-scanned Nordschleife as its benchmark track.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 805550,
                Name = "AC Competizione",
                Abbreviation = "ACC",
                SteamId = "805550",
                Description = "AC 系列的 GT 赛事正统续作，获 Blancpain GT Series 官方授权。专注 GT3/GT4 级别竞赛，拥有昼夜循环、动态天气和极为精细的轮胎与空气动力学模型。",
                DescriptionEn = "The official GT racing successor to AC, licensed by the Blancpain GT Series. Focuses on GT3/GT4 competition with day-night cycles, dynamic weather, and highly detailed tire and aerodynamics models.",
                CoverImageUrl = "/Assets/77_cover.jpg",
                BgImageUrl = "/Assets/77_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 3917090,
                Name = "AC Rally",
                Abbreviation = "ACR",
                SteamId = "3917090",
                Description = "AC 系列拉力赛分支，将系列标志性的物理引擎带入拉力赛场景。支持砂石、雪地、柏油等多种路面类型，兼容大量 ACC 社区内容。",
                DescriptionEn = "The rally branch of the AC series, bringing its signature physics engine to rally racing. Features gravel, snow, and tarmac surfaces, with compatibility for a wide range of ACC community content.",
                CoverImageUrl = "/Assets/77_cover.jpg",
                BgImageUrl = "/Assets/77_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 3058630,
                Name = "AC EVO",
                Abbreviation = "AC EVO",
                SteamId = "3058630",
                Description = "Assetto Corsa 最新世代作品，采用 Kunos 自研引擎全面重构。支持 VR、三屏和大型网格比赛，画面保真度与物理精度均有质的飞跃。",
                DescriptionEn = "The latest generation of Assetto Corsa, fully rebuilt with Kunos' in-house engine. Supports VR, triple screens, and large-grid racing with a quantum leap in visual fidelity and physics accuracy.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },

            // ============================
            // F1 系列 (4 款) — 需在游戏设置中开启 UDP 遥测
            // ============================
            new()
            {
                Id = 1692250,
                Name = "F1 22",
                Abbreviation = "F1 22",
                SteamId = "1692250",
                Description = "Codemasters 出品的 FIA F1 2022 赛季官方游戏，包含全部车队、车手与赛历。支持 VR、双人生涯模式和 Formula 1 Sprint 赛制。",
                DescriptionEn = "The official FIA F1 2022 season game by Codemasters, featuring all teams, drivers, and race calendars. Supports VR, two-player career mode, and the Formula 1 Sprint format.",
                CoverImageUrl = "/Assets/77_cover.jpg",
                BgImageUrl = "/Assets/77_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 2108330,
                Name = "F1 23",
                Abbreviation = "F1 23",
                SteamId = "2108330",
                Description = "F1 2023 赛季官方游戏，新增拉斯维加斯大道赛道和 Braking Point 故事模式第二章。引入 35% 赛程选项，操控手感更贴近真实。",
                DescriptionEn = "The official F1 2023 season game with the new Las Vegas Strip Circuit and Braking Point story mode chapter 2. Introduces 35% race distance option and more realistic handling.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 2488620,
                Name = "F1 24",
                Abbreviation = "F1 24",
                SteamId = "2488620",
                Description = "F1 2024 赛季官方游戏，更新车手市场动态与赛道布局。改进悬挂物理和轮胎热力学模型，新增自定义赛事与生涯模式增强。",
                DescriptionEn = "The official F1 2024 season game with updated driver market dynamics and track layouts. Features improved suspension physics, tire thermodynamics, and enhanced career mode.",
                CoverImageUrl = "/Assets/77_cover.jpg",
                BgImageUrl = "/Assets/77_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 3059520,
                Name = "F1 25",
                Abbreviation = "F1 25",
                SteamId = "3059520",
                Description = "F1 2025 赛季官方游戏，搭载最新赛历、车手阵容及规则变更。画面与 AI 全面升级，提供迄今最完整的 F1 模拟体验。",
                DescriptionEn = "The official F1 2025 season game, featuring the latest race calendar, driver lineup, and regulation changes. With fully upgraded graphics and AI for the most complete F1 simulation yet.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },

            // ============================
            // Forza 系列 (4 款) — 需在游戏内开启并配置 UDP 遥测
            // ============================
            new()
            {
                Id = 2440510,
                Name = "Forza Motorsport (2023)",
                Abbreviation = "FM2023",
                SteamId = "2440510",
                Description = "Turn 10 工作室回归初心之作，专注赛道竞速模拟。收录超过 500 辆高精度赛车与 20 条世界级赛道，支持实时光线追踪与 4K 60fps。",
                DescriptionEn = "Turn 10 Studios' back-to-basics track racing simulator. Features over 500 high-precision cars and 20 world-class tracks, with real-time ray tracing and 4K 60fps support.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 1293830,
                Name = "Forza Horizon 4",
                Abbreviation = "FH4",
                SteamId = "1293830",
                Description = "Playground Games 以英伦三岛为舞台打造的开放世界竞速巨作。动态四季变换一周一循环，拥有 700 余辆授权车辆和共享世界多人模式。",
                DescriptionEn = "An open-world racing epic set across a stunning recreation of Great Britain by Playground Games. Dynamic seasons change weekly, with over 700 licensed cars and a shared-world multiplayer experience.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 1551360,
                Name = "Forza Horizon 5",
                Abbreviation = "FH5",
                SteamId = "1551360",
                Description = "以墨西哥为舞台的系列巅峰之作，地图规模为前作 1.5 倍。涵盖沙漠、丛林、火山、海岸等多样地貌，拥有迄今最丰富的车辆收藏与赛事内容。",
                DescriptionEn = "The series' crowning achievement set in Mexico, with a map 1.5 times the size of its predecessor. Spanning deserts, jungles, volcanoes, and coastlines, with the richest car collection and events to date.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 2483190,
                Name = "Forza Horizon 6",
                Abbreviation = "FH6",
                SteamId = "2483190",
                Description = "Forza Horizon 系列最新力作，延续开放世界竞速标杆品质。更多车型、更大地图、更丰富的赛事与社交玩法。",
                DescriptionEn = "The latest installment of the Forza Horizon series, continuing the benchmark open-world racing experience. More cars, a larger map, and deeper events and social features.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },

            // ============================
            // DiRT 系列 (2 款) — 需手动修改配置文件开启 UDP 遥测
            // ============================
            new()
            {
                Id = 421020,
                Name = "DiRT 4",
                Abbreviation = "D4",
                SteamId = "421020",
                Description = "Codemasters 拉力赛力作，融合严谨的拉力模拟与轻松的 Landrush 越野模式。创新的 Your Stage 系统可自动生成近乎无限的赛道变体。",
                DescriptionEn = "A rally powerhouse by Codemasters, blending serious simulation with casual Landrush off-road mode. The innovative Your Stage system generates near-infinite track variations automatically.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 690790,
                Name = "DiRT Rally 2.0",
                Abbreviation = "DR2.0",
                SteamId = "690790",
                Description = "硬核拉力赛模拟标杆，获 FIA World Rallycross 官方授权。极其精细的路面物理使每寸颠簸与抓地力变化都可感知，被誉为拉力模拟之王。",
                DescriptionEn = "The gold standard of hardcore rally simulation, officially licensed by FIA World Rallycross. Ultra-detailed surface physics make every bump and grip change tangible — widely regarded as the king of rally sims.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },

            // ============================
            // rFactor / LMU 系列 (2 款) — 需添加遥测插件
            // ============================
            new()
            {
                Id = 365960,
                Name = "rFactor 2",
                Abbreviation = "RF2",
                SteamId = "365960",
                Description = "ISI 出品的专业级赛车模拟平台，被 F1 车队和专业培训机构广泛采用。拥有先进的轮胎模型、实时赛道演变和高度开放的 MOD 架构。",
                DescriptionEn = "A professional-grade racing simulation platform by ISI, widely used by F1 teams and training institutions. Features an advanced tire model, real-time track evolution, and a highly open modding architecture.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 2399420,
                Name = "Le Mans Ultimate",
                Abbreviation = "LMU",
                SteamId = "2399420",
                Description = "FIA 世界耐力锦标赛与勒芒 24 小时耐力赛官方游戏，基于 rFactor 2 引擎打造。涵盖 Hypercar、LMP2、LMGT3 等全部组别，支持大型多人耐力赛。",
                DescriptionEn = "The official game of the FIA World Endurance Championship and 24 Hours of Le Mans, built on the rFactor 2 engine. Covers all classes including Hypercar, LMP2, and LMGT3, with full endurance multiplayer.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },

            // ============================
            // Project CARS / AMS2 系列 (3 款) — 需在游戏内将遥测切换为 Project Car 2 共享内存
            // ============================
            new()
            {
                Id = 378860,
                Name = "Project CARS 2",
                Abbreviation = "PC2",
                SteamId = "378860",
                Description = "Slightly Mad Studios 综合性赛车模拟，LiveTrack 3.0 实现实时赛道演变与动态天气。涵盖 GT、方程式、拉力、卡丁车等 180 余种车型与 60 条赛道。",
                DescriptionEn = "A comprehensive racing sim by Slightly Mad Studios, with LiveTrack 3.0 enabling real-time track evolution and dynamic weather. Covers 180+ vehicles across GT, formula, rally, karting, and 60 tracks.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 958400,
                Name = "Project CARS 3",
                Abbreviation = "PC3",
                SteamId = "958400",
                Description = "Project CARS 第三作，定位更偏向大众玩家。简化生涯模式，增加车辆改装与自定义涂装系统，兼顾模拟驾驶深度与上手友好度。",
                DescriptionEn = "The third Project CARS title, positioned for a broader audience. Streamlined career mode with added car customization and livery systems, balancing simulation depth with accessibility.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 1066890,
                Name = "Automobilista 2",
                Abbreviation = "AMS2",
                SteamId = "1066890",
                Description = "Reiza Studios 出品的巴西赛车模拟，基于 Project CARS 引擎深度改造。拥有丰富的南美赛车文化内容，从 Stock Car Brasil 到经典 F1 均有收录。",
                DescriptionEn = "A Brazilian racing sim by Reiza Studios, heavily modified from the Project CARS engine. Rich in South American motorsport culture, from Stock Car Brasil to classic F1.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },

            // ============================
            // WRC 系列 (5 款) — 均需手动配置（补丁 DLL 或修改配置）
            // ============================
            new()
            {
                Id = 1004750,
                Name = "WRC 8",
                Abbreviation = "WRC8",
                SteamId = "1004750",
                Description = "KT Racing 出品的 WRC 2019 赛季官方游戏。动态天气系统可在一场拉力赛中经历晴雨交替，路面退化机制影响后续车辆抓地力。",
                DescriptionEn = "The official WRC 2019 season game by KT Racing. A dynamic weather system lets you experience sun and rain within a single rally, with surface degradation affecting grip for later cars.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 1267540,
                Name = "WRC 9",
                Abbreviation = "WRC9",
                SteamId = "1267540",
                Description = "WRC 2020 赛季官方游戏，新增肯尼亚 Safari Rally、新西兰和日本拉力赛。改进悬挂物理，碎石路面反馈更加细腻真实。",
                DescriptionEn = "The official WRC 2020 season game, adding Kenya Safari Rally, Rally New Zealand, and Rally Japan. Improved suspension physics deliver more detailed gravel surface feedback.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 1462810,
                Name = "WRC 10",
                Abbreviation = "WRC10",
                SteamId = "1462810",
                Description = "纪念 WRC 50 周年的力作，收录传奇拉力赛车与经典历史赛段。新增周年纪念模式和俱乐部创建系统，致敬半个世纪的拉力赛历史。",
                DescriptionEn = "Celebrating WRC's 50th anniversary with legendary rally cars and classic historic stages. Adds anniversary mode and a club creation system, honoring half a century of rally history.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 1953520,
                Name = "WRC Generations",
                Abbreviation = "WRCG",
                SteamId = "1953520",
                Description = "KT Racing WRC 系列收官之作，首次引入 Rally1 混合动力赛车。拥有迄今最全的拉力赛段和车辆阵容，支持完整的车队管理模式。",
                DescriptionEn = "The final KT Racing WRC title, introducing Rally1 hybrid cars for the first time. Features the most comprehensive rally stages and car roster to date, with full team management mode.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 1849250,
                Name = "EA Sports WRC",
                Abbreviation = "EA WRC",
                SteamId = "1849250",
                Description = "Codemasters 加盟 EA 后首款 WRC 官方游戏，采用 Unreal Engine 打造。赛道长度与精度大幅提升，支持 Builder 自定义拉力赛车系统。",
                DescriptionEn = "The first official WRC game by Codemasters under EA, built on Unreal Engine. Significantly longer and more detailed stages, with a Builder system for custom rally cars.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },

            // ============================
            // 其他竞速 (3 款)
            // ============================
            new()
            {
                Id = 266410,
                Name = "iRacing",
                Abbreviation = "iR",
                SteamId = "266410",
                Description = "全球顶级在线赛车模拟订阅服务，由职业车手参与开发。拥有激光扫描赛道、官方锦标赛体系和严格的评级匹配系统，是全球赛车电竞的核心平台。",
                DescriptionEn = "The world's premier online racing simulation subscription service, co-developed with professional drivers. Laser-scanned tracks, official championships, and a strict rating system make it the core platform of global sim racing esports.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 211500,
                Name = "RaceRoom Racing Experience",
                Abbreviation = "R3E",
                SteamId = "211500",
                Description = "Sector3 Studios 出品的免费入门竞速模拟。拥有 DTM、WTCR、GT3 等丰富的官方授权内容，下载即玩，按需购买额外内容。",
                DescriptionEn = "A free-to-start racing simulation by Sector3 Studios. Packed with officially licensed DTM, WTCR, GT3, and more content — download and play, purchase additional content as needed.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 284160,
                Name = "BeamNG.drive",
                Abbreviation = "BNG",
                SteamId = "284160",
                Description = "基于软体物理引擎的独特沙盒驾驶模拟。每辆车数千个结构节点实时形变，碰撞与损坏效果无可匹敌，是探索驾驶极限与物理破坏的终极乐园。",
                DescriptionEn = "A unique sandbox driving sim powered by a soft-body physics engine. Thousands of structural nodes deform in real-time per vehicle, delivering unmatched crash and damage effects — the ultimate playground for driving extremes.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },

            // ============================
            // 模拟驾驶 (2 款) — 需向游戏目录添加相应 SDK
            // ============================
            new()
            {
                Id = 227300,
                Name = "Euro Truck Simulator 2",
                Abbreviation = "ETS2",
                SteamId = "227300",
                Description = "SCS Software 经典卡车模拟标杆，驾驶授权卡车穿越欧洲大陆数十个国家。经营运输公司、升级车库、雇佣司机，体验欧洲公路文化与沿途优美风景。",
                DescriptionEn = "The definitive truck simulator by SCS Software. Drive licensed trucks across dozens of European countries, build a transport company, upgrade garages, hire drivers, and enjoy Europe's scenic highways.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
            new()
            {
                Id = 270880,
                Name = "American Truck Simulator",
                Abbreviation = "ATS",
                SteamId = "270880",
                Description = "穿越美国各州的卡车模拟大作，地图以 DLC 形式持续扩展。从加州海岸到得克萨斯平原，体验美式长头卡车与广袤的洲际公路风光。",
                DescriptionEn = "A truck simulation epic crossing US states, with the map continuously expanding via DLC. From the California coast to the Texas plains, experience American long-nose trucks and vast interstate landscapes.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },

            // ============================
            // 非 Steam 游戏 (2 款)
            // ============================
            new()
            {
                Id = 22,
                Name = "Richard Burns Rally",
                Abbreviation = "RBR",
                SteamId = "22",
                Description = "Richard Burns Rally，2004 年发布至今的拉力赛传奇。以严苛的物理模拟和极高的驾驶难度著称，至今仍是硬核拉力社区的首选平台，拥有海量 MOD 支持。",
                DescriptionEn = "A rally legend since 2004, revered for its unforgiving physics and punishing difficulty. Still the go-to platform for the hardcore rally community, with a massive library of mods.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = false,
            },
            new()
            {
                Id = 25,
                Name = "Live for Speed",
                Abbreviation = "LFS",
                SteamId = "25",
                Description = "Scawen Roberts 等三人开发，2003 年发布至今仍在更新的赛车模拟先驱。以精准的物理模型和极低的硬件门槛闻名，拥有忠实的在线社区和丰富的第三方内容。",
                DescriptionEn = "A pioneering racing sim developed by Scawen Roberts and two others, still updated since its 2003 release. Renowned for its precise physics and minimal hardware requirements, with a loyal online community.",
                CoverImageUrl = "/Assets/80_cover.jpg",
                BgImageUrl = "/Assets/80_bg.jpg",
                NeedsTelemetryConfig = true,
            },
        };
    }
}
