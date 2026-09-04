using System.Globalization;
using Avalonia;

namespace LightDraw.Desktop.Services;

public sealed class LocalizationService
{
    private static readonly IReadOnlyDictionary<string, LocalizedText> Strings = CreateStrings();

    public static LocalizationService Instance { get; } = new();

    public event EventHandler? LanguageChanged;

    public string CultureName { get; private set; } = "zh-CN";

    public bool IsEnglish => CultureName.Equals("en-US", StringComparison.OrdinalIgnoreCase);

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (!Strings.TryGetValue(key, out var text))
        {
            return key;
        }

        return IsEnglish ? text.English : text.Chinese;
    }

    public void SetLanguage(string cultureName)
    {
        var normalizedCulture = cultureName.Equals("en-US", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "zh-CN";
        if (CultureName == normalizedCulture)
        {
            return;
        }

        CultureName = normalizedCulture;
        var culture = CultureInfo.GetCultureInfo(normalizedCulture);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        ApplyResources();
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyResources()
    {
        if (Application.Current?.Resources is not { } resources)
        {
            return;
        }

        foreach (var (key, value) in Strings)
        {
            resources[key] = IsEnglish ? value.English : value.Chinese;
        }
    }

    private static IReadOnlyDictionary<string, LocalizedText> CreateStrings()
    {
        var result = new Dictionary<string, LocalizedText>(StringComparer.Ordinal);
        void Add(string key, string chinese, string english) => result.Add(key, new(chinese, english));

        Add("App.Title", "光绘课堂 (LightDraw)", "LightDraw");
        Add("Main.Subtitle", "几何光学光路绘图", "Geometric optics drawing");
        Add("Main.OpenElectrostatic", "↯ 打开静电场仿真", "↯ Open electrostatic simulation");
        Add("Main.OpenMagnetostatic", "⌁ 打开静磁场仿真", "⌁ Open magnetostatic simulation");
        Add("Common.Pan", "✥ 平移界面", "✥ Pan view");
        Add("Common.Move", "✣ 移动/编辑", "✣ Move/Edit");
        Add("Common.Delete", "× 删除", "× Delete");
        Add("Common.SimulationSettings", "统一模拟配置", "Simulation settings");
        Add("Common.ResetScene", "⌂ 重置场景", "⌂ Reset scene");
        Add("Common.FitWindow", "◎ 适合窗口", "◎ Fit to window");
        Add("Common.ElementProperties", "元件属性", "Element properties");
        Add("Common.Name", "名称", "Name");
        Add("Common.ElementNamePlaceholder", "元件名称", "Element name");
        Add("Common.Geometry", "几何关系", "Geometry");
        Add("Common.FirstOrigin", "第一原点", "First origin");
        Add("Common.Center", "中心", "Center");
        Add("Common.SecondOrigin", "第二原点", "Second origin");
        Add("Common.RotationStep", "旋转步进", "Rotation step");
        Add("Common.Counterclockwise", "↶ 逆时针", "↶ Counterclockwise");
        Add("Common.Clockwise", "↷ 顺时针", "↷ Clockwise");
        Add("Common.CurrentAngle", "当前角度", "Current angle");
        Add("Common.Custom", "自定义", "Custom");
        Add("Common.ElementParameters", "元件参数", "Element parameters");
        Add("Common.Length", "长度", "Length");
        Add("Common.Radius", "半径", "Radius");
        Add("Common.CreateModels", "创建模型", "Create models");
        Add("Common.Noncommercial", "PolyForm Noncommercial 1.0.0 · 仅限非商业使用", "PolyForm Noncommercial 1.0.0 · Noncommercial use only");

        Add("Main.RaysPerSource", "每个光源的光线数量", "Rays per light source");
        Add("Main.OpenScene", "打开场景…", "Open scene…");
        Add("Main.SaveScene", "保存场景…", "Save scene…");
        Add("Main.About", "关于程序", "About");
        Add("Main.Hidden", "暂时隐藏", "Temporarily hide");
        Add("Main.Group", "组合", "Group");
        Add("Main.Ungroup", "取消组合", "Ungroup");
        Add("Main.SetPrimary", "设为主元件", "Set as primary");
        Add("Main.Wavelength", "波长", "Wavelength");
        Add("Main.ApertureOpening", "通孔", "Opening");
        Add("Main.FocalLength", "焦距", "Focal length");
        Add("Main.Dispersion", "色散", "Dispersion");
        Add("Main.NoDispersion", "理想（无色散）", "Ideal (no dispersion)");
        Add("Main.NormalDispersion", "正常色散", "Normal dispersion");
        Add("Main.AnomalousDispersion", "反常色散", "Anomalous dispersion");
        Add("Main.DispersionLevel", "色散等级", "Dispersion level");
        Add("Main.CentralAngle", "圆心角", "Central angle");
        Add("Main.GrooveDensity", "刻线密度", "Groove density");
        Add("Main.LinesPerMillimeter", "线/mm", "lines/mm");
        Add("Main.CreateTools", "创建工具", "Creation tools");
        Add("Main.LightSources", "光源类", "Light sources");
        Add("Main.PointLight", "● 单色点光源", "● Monochromatic point source");
        Add("Main.ParallelLight", "▥ 单色平行光源", "▥ Monochromatic parallel source");
        Add("Main.CompositePointLight", "◉ 复色点光源", "◉ Composite point source");
        Add("Main.CompositeParallelLight", "▤ 复色平行光源", "▤ Composite parallel source");
        Add("Main.Lenses", "透镜类", "Lenses");
        Add("Main.ConvexLens", "◉ 凸透镜", "◉ Convex lens");
        Add("Main.ConcaveLens", ")( 凹透镜", ")( Concave lens");
        Add("Main.Aperture", "┃ ┃ 光阑", "┃ ┃ Aperture");
        Add("Main.Reflectors", "反射镜类", "Reflectors");
        Add("Main.Mirror", "／ 平面反光镜", "／ Plane mirror");
        Add("Main.ConcaveMirror", "◖ 理想凹球面镜", "◖ Ideal concave spherical mirror");
        Add("Main.ConvexMirror", "◗ 理想凸球面镜", "◗ Ideal convex spherical mirror");
        Add("Main.BeamSplitter", "／ 平面分光镜", "／ Plane beam splitter");
        Add("Main.ReflectionGrating", "≋ 反射光栅", "≋ Reflection grating");
        Add("Main.ConcaveGrating", "◖≋ 凹面光栅", "◖≋ Concave grating");
        Add("Main.Screen", "┃ 光屏", "┃ Screen");
        Add("Main.Help", "移动/编辑：空白左拖框选，空白单击清空\n右键：随时拖动画布\n元件：先单击任意位置选中\n第一原点：选中后拖拽平移\n白色圆点：选中后固定原点旋转\n组合：单击成员选择整体\n设为主元件：先单击组合中的目标成员\n删除元件：单击后自动返回", "Move/Edit: left-drag empty space to box-select; click empty space to clear\nRight button: pan the canvas at any time\nElement: click anywhere on it to select\nFirst origin: drag after selection to translate\nWhite handle: rotate around the fixed origin\nGroup: click a member to select the group\nSet as primary: first click the target group member\nDelete: returns automatically after deleting an element");

        Add("Tip.Hidden", "保留元件绘制，但不参与光线追迹，光线将直接穿过该元件", "Keep the element visible, but exclude it from ray tracing so rays pass through it.");
        Add("Tip.SetPrimary", "先在组合中单击目标成员，再将其设为主元件", "Click the target member in the group before setting it as the primary element.");
        Add("Tip.FirstOrigin", "元件原点；组合时为主元件的第一原点", "Element origin; for a group, this is the primary element's first origin.");
        Add("Tip.SecondOrigin", "球面镜为曲率圆心；其余元件为距第一原点 100 mm 的白色旋转点", "Center of curvature for spherical mirrors; otherwise the white rotation handle 100 mm from the first origin.");
        Add("Tip.Length", "元件原点到任一端点的距离 × 2", "Twice the distance from the element origin to either endpoint.");
        Add("Tip.Wavelength", "单色光源波长；光栅按此真实波长计算衍射角", "Monochromatic source wavelength; gratings use this physical wavelength to calculate diffraction angles.");
        Add("Tip.WavelengthValue", "默认 580 nm；显示颜色按最近参考波长匹配，中值时选择更靠近黄色 580 nm 的一侧", "Default: 580 nm. Display color uses the nearest reference wavelength; ties favor the side nearer yellow at 580 nm.");
        Add("Tip.Aperture", "光阑中央允许光线通过的开口大小", "Size of the central opening that allows rays through the aperture.");
        Add("Tip.FocalLength", "透镜以 550 nm 绿光焦距为基准；球面镜焦距始终等于半径的一半", "Lens focal length is referenced to 550 nm green light; a spherical mirror's focal length is always half its radius.");
        Add("Tip.Dispersion", "正常色散：蓝光焦距短、红光焦距长；反常色散则相反", "Normal dispersion gives blue light a shorter focal length and red light a longer one; anomalous dispersion reverses this.");
        Add("Tip.DispersionLevel", "范围 0～10；每级使 450/650 nm 焦距相对 550 nm 基准变化 5%", "Range 0–10; each level changes the 450/650 nm focal length by 5% relative to the 550 nm reference.");
        Add("Tip.Radius", "曲率半径；修改后焦距自动更新为 R/2", "Radius of curvature; changing it updates the focal length to R/2.");
        Add("Tip.CentralAngle", "点光源：发射范围，默认 360°；球面镜/凹面光栅：圆弧大小，默认 180°", "Point source: emission range, default 360°. Spherical mirror/concave grating: arc size, default 180°.");
        Add("Tip.GrooveDensity", "反射光栅每毫米的刻线数量", "Number of grooves per millimeter on the reflection grating.");
        Add("Tip.Monochromatic", "默认 580 nm；选中后可修改波长", "Default: 580 nm; select the source to change its wavelength.");
        Add("Tip.Composite", "等强混合 450、550、650 nm；分光前显示为黄色", "Equal-intensity mix of 450, 550, and 650 nm; shown as yellow before spectral separation.");
        Add("Tip.Lens", "默认焦距 300 mm、理想无色散；选中后可设置色散模式和等级", "Default focal length: 300 mm, ideal with no dispersion; select it to set the dispersion mode and level.");
        Add("Tip.SphericalMirror", "第一次点击镜面中心点，第二次点击曲率圆心；默认圆心角 180°", "First click the mirror vertex, then the center of curvature; default central angle: 180°.");
        Add("Tip.BeamSplitter", "透射与反射光强各为入射光的 50%", "Transmitted and reflected intensity are each 50% of the incident light.");
        Add("Tip.ReflectionGrating", "复色光 0 级保持黄色混光；±1、±2、±3 级发生色散；光强分别为 90%、50%、25%、10%", "Composite light remains yellow at order 0 and disperses at orders ±1, ±2, and ±3; intensities are 90%, 50%, 25%, and 10%.");
        Add("Tip.ConcaveGrating", "按凹球面镜方式定义顶点、曲率圆心和圆心角；在局部切面按光栅方程产生衍射", "Define the vertex, center of curvature, and central angle like a concave spherical mirror; diffraction follows the grating equation at the local tangent.");
        Add("Tip.Screen", "命中光屏后立即截停光线", "Stops a ray immediately when it hits the screen.");
        Add("Tip.MagneticSecondOrigin", "垂直面环形电流的白色旋转控制点", "White rotation handle for a vertical circular current loop.");

        Add("Electro.Title", "静电场仿真 - 光绘课堂", "Electrostatic Simulation - LightDraw");
        Add("Electro.Heading", "静电场仿真", "Electrostatic Simulation");
        Add("Electro.Subtitle", "点电荷、等势导体平板与电场线", "Point charges, equipotential conductor plates, and electric field lines");
        Add("Electro.Law", "真空介电常数 ε₀ · 库仑定律", "Vacuum permittivity ε₀ · Coulomb's law");
        Add("Electro.LinesPerCharge", "每电荷电场线", "Field lines per charge");
        Add("Electro.Charge", "电量 q", "Charge q");
        Add("Electro.Potential", "电势 φ", "Potential φ");
        Add("Electro.Charges", "电荷", "Charges");
        Add("Electro.PointCharge", "⊕ 点电荷", "⊕ Point charge");
        Add("Electro.Conductors", "导体", "Conductors");
        Add("Electro.ChargedPlate", "▰ 带电平板", "▰ Charged plate");
        Add("Electro.Help", "红色：正电荷/正电势\n蓝色：负电荷/负电势\n\n平板两次点击创建。移动/编辑工具下可在空白处拖动画布；先单击元件任意位置选中，再拖动第一原点平移。平板长度和角度通过顶部属性框修改。\n\n所有电势均以无穷远 φ=0 为参考。", "Red: positive charge/potential\nBlue: negative charge/potential\n\nCreate a plate with two clicks. With Move/Edit, drag empty space to pan; click an element to select it, then drag its first origin to translate it. Edit plate length and angle in the property bar above.\n\nAll potentials use infinity, φ=0, as the reference.");

        Add("Magnetic.Title", "静磁场仿真 - 光绘课堂", "Magnetostatic Simulation - LightDraw");
        Add("Magnetic.Heading", "静磁场仿真", "Magnetostatic Simulation");
        Add("Magnetic.Subtitle", "理想恒定电流导体与磁场方向", "Ideal steady-current conductors and magnetic-field direction");
        Add("Magnetic.Law", "真空磁导率 μ₀ · 毕奥-萨伐尔定律", "Vacuum permeability μ₀ · Biot-Savart law");
        Add("Magnetic.Density", "场图密度", "Field-map density");
        Add("Magnetic.Current", "恒定电流 I", "Steady current I");
        Add("Magnetic.SignedHint", "（可输入正数、负数或 0）", "(positive, negative, or zero)");
        Add("Magnetic.Currents", "电流", "Currents");
        Add("Magnetic.PlanarConductor", "▰ 平面理想恒定电流导体", "▰ Planar ideal steady-current conductor");
        Add("Magnetic.VerticalConductor", "⊙ 竖直面无限长恒定电流导体", "⊙ Infinite perpendicular steady-current conductor");
        Add("Magnetic.PlanarLoop", "○ 平面环形恒定电流", "○ Planar circular steady current");
        Add("Magnetic.VerticalLoop", "◯ 垂直面环形恒定电流", "◯ Vertical circular steady current");
        Add("Magnetic.Help", "线形和环形元件均按毕奥-萨伐尔定律叠加。\n\n移动/编辑工具下可在空白处拖动画布；先单击元件任意位置选中，再拖动第一原点平移。\n\n平面圆环：圆弧箭头逆时针为正；· / × 显示法向磁场。\n\n垂直圆环：实线半环位于绘图面以上，虚线半环位于绘图面以下；绿色曲线显示平面内磁感线。选中后拖动白色第二原点可固定圆心旋转。\n\n负电流使方向反转。", "Linear and circular elements are superposed using the Biot-Savart law.\n\nWith Move/Edit, drag empty space to pan; click an element to select it, then drag its first origin to translate it.\n\nPlanar loop: a counterclockwise arc arrow is positive; · / × show the normal magnetic field.\n\nVertical loop: the solid semicircle is above the drawing plane and the dashed semicircle is below it; green curves show in-plane magnetic field lines. After selection, drag the white second origin to rotate around the fixed center.\n\nNegative current reverses the direction.");
        Add("Magnetic.Footer", "毕奥-萨伐尔定律 · 方向遵循右手定则", "Biot-Savart law · Direction follows the right-hand rule");

        Add("About.Title", "关于光绘课堂", "About LightDraw");
        Add("About.Description", "面向物理课堂、实验演示和物理学习的二维光学、静电场与静磁场绘图、仿真工具。", "A 2D drawing and simulation tool for optics, electrostatics, and magnetostatics in physics lessons, demonstrations, and learning.");
        Add("About.Repository", "项目仓库", "Project repository");
        Add("About.RepositoryDescription", "欢迎查看源代码、提交问题或参与改进。", "Explore the source code, report issues, or contribute improvements.");
        Add("About.Methods", "主要仿真方法", "Simulation methods");
        Add("About.OpticsHeading", "光学仿真 · 二维几何光线追迹", "Optics · 2D geometric ray tracing");
        Add("About.OpticsDescription", "从光源离散发射光线，计算光线与光学元件的最近交点，并依据反射定律、理想薄透镜近轴模型和光栅方程继续传播。只考虑几何光学，不考虑波动光学效应，甚至三棱镜色散这种需要设置截止常数确定色散角度的也不是很想做。", "Rays are emitted discretely from each source. The nearest intersection with an optical element is calculated, then propagation continues using the law of reflection, the paraxial ideal thin-lens model, and the grating equation. The simulation covers geometric optics only, not wave-optics effects; prism dispersion is also outside its current scope.");
        Add("About.ElectroHeading", "静电场仿真 · 库仑叠加与数值积分", "Electrostatics · Coulomb superposition and numerical integration");
        Add("About.ElectroDescription", "按库仑定律叠加点电荷的电势和电场；有限导体板采用边界元离散并求解感应电荷，再使用四阶 Runge–Kutta 方法沿电场方向追踪电场线。", "Point-charge potentials and fields are superposed using Coulomb's law. Finite conductor plates are discretized with a boundary-element method to solve induced charge, and field lines are traced with fourth-order Runge-Kutta integration.");
        Add("About.MagneticHeading", "静磁场仿真 · 毕奥-萨伐尔叠加", "Magnetostatics · Biot-Savart superposition");
        Add("About.MagneticDescription", "支持平面及竖直无限长直导体、平面及垂直面环形电流。所有电流元均按毕奥-萨伐尔定律积分并矢量叠加：平面法向磁场以圆点和叉号表示；垂直直导线以磁矢势等磁通线绘制，含垂直圆环时沿真实叠加场积分追踪。所有方向均遵循右手定则。", "Supports planar and perpendicular infinite straight conductors plus planar and vertical circular currents. All current elements are integrated and vector-superposed using the Biot-Savart law. Normal magnetic fields use dots and crosses; perpendicular straight conductors use magnetic-vector-potential flux contours, while scenes with vertical loops trace the actual superposed field. All directions follow the right-hand rule.");
        Add("About.Disclaimer", "仿真面向课堂演示和概念学习，不替代精密工程或科研计算。", "Designed for classroom demonstrations and conceptual learning; not a replacement for precision engineering or scientific computation.");
        Add("About.License", "授权许可", "License");
        Add("About.LicenseDescription", "本软件仅授权用于非商业用途。许可条款：\nhttps://polyformproject.org/licenses/noncommercial/1.0.0", "This software is licensed for noncommercial use only. License terms:\nhttps://polyformproject.org/licenses/noncommercial/1.0.0");
        Add("About.Acknowledgements", "技术支持及鸣谢", "Technical support and acknowledgements");
        Add("About.Institutions", "佛山市南海区大沥高级中学\n佛山市南海区桂城中学\n佛山市南海区罗村高级中学物理科组\n佛山大学物理与光电工程学院光源与照明系\n佛山市升阳光学科技有限公司", "Dali Senior High School, Nanhai District, Foshan\nGuicheng High School, Nanhai District, Foshan\nPhysics Department, Luocun Senior High School, Nanhai District, Foshan\nDepartment of Light Sources and Illumination, School of Physics and Optoelectronic Engineering, Foshan University\nFoshan Shengyang Optics Technology Co., Ltd.");
        Add("About.BuiltWith", "基于 .NET 10、Avalonia 和 SkiaSharp 构建", "Built with .NET 10, Avalonia, and SkiaSharp");
        Add("About.Close", "关闭", "Close");
        Add("About.Version", "版本 {0}", "Version {0}");
        Add("About.Unknown", "未知", "Unknown");
        Add("Storage.OpenTitle", "打开 LightDraw 场景", "Open LightDraw scene");
        Add("Storage.SaveTitle", "保存 LightDraw 场景", "Save LightDraw scene");
        Add("Storage.SceneType", "LightDraw 场景", "LightDraw scene");

        Add("Status.Ready", "就绪", "Ready");
        Add("Status.SceneReset", "已重置为空白场景", "Reset to an empty scene");
        Add("Status.ElectroReset", "已重置为空白静电场", "Reset to an empty electrostatic field");
        Add("Status.MagneticReset", "已重置为空白静磁场", "Reset to an empty magnetostatic field");
        Add("Status.ViewReset", "视图已复位", "View reset");
        Add("Status.Opened", "已打开：{0}", "Opened: {0}");
        Add("Status.OpenFailed", "打开失败：{0}", "Open failed: {0}");
        Add("Status.Saved", "已保存：{0}", "Saved: {0}");
        Add("Status.SaveFailed", "保存失败：{0}", "Save failed: {0}");
        Add("Status.OpticalSimulation", "{0} · 每光源 {1} 条 / 共 {2} 条 · {3} 个线段 · {4} 条衍射光线 · 计算 {5:F2} ms", "{0} · {1} rays/source / {2} total · {3} segments · {4} diffracted rays · {5:F2} ms");
        Add("Status.ElectroSimulation", "{0} · {1} 个点电荷 · {2} 块平板 · {3} 条电场线 · 计算 {4:F2} ms", "{0} · {1} point charges · {2} plates · {3} electric field lines · {4:F2} ms");
        Add("Status.MagneticSimulation", "{0} · {1} 根平面导体 · {2} 根竖直无限长导体 · {3} 个平面圆环 / {4} 个垂直圆环 · {5} 个方向标记 / 磁感线 {6} 条闭合、{7} 条延伸 · 计算 {8:F2} ms", "{0} · {1} planar conductors · {2} infinite perpendicular conductors · {3} planar loops / {4} vertical loops · {5} direction markers / magnetic field lines: {6} closed, {7} extending · {8:F2} ms");
        Add("Status.Pan", "平移工具 · 按住左键拖动画布", "Pan · Hold the left button and drag the canvas");
        Add("Status.PanZoom", "平移工具 · 按住左键拖动画布，滚轮缩放", "Pan · Hold the left button to drag; use the wheel to zoom");
        Add("Status.OpticalMove", "移动或调整元件 · 空白左拖框选，空白单击清空；右键随时平移画布", "Move or edit elements · Left-drag empty space to box-select; click empty space to clear; right-drag to pan at any time");
        Add("Status.OpticalDelete", "删除元件 · 单击光源或光学元件即可删除，随后自动返回平移工具", "Delete element · Click a source or optical element; the tool then returns to Pan");
        Add("Status.ElectroMove", "移动/编辑 · 空白处拖动画布；先单击选中，再拖动第一原点平移元件", "Move/Edit · Drag empty space to pan; select an element, then drag its first origin to translate it");
        Add("Status.ElectroDelete", "删除工具 · 单击点电荷或平板后自动返回平移工具", "Delete · Click a point charge or plate; the tool then returns to Pan");
        Add("Status.PlaceCharge", "放置点电荷 · 在画布中单击，随后可在顶部设置电量", "Place point charge · Click the canvas, then set its charge in the property bar");
        Add("Status.PlacePlate", "放置带电平板 · 第一次点击起点，第二次点击终点", "Place charged plate · Click the start point, then the end point");
        Add("Status.MagneticMove", "移动/编辑 · 空白处拖动画布；先单击选中，再拖动第一原点平移；第二原点旋转", "Move/Edit · Drag empty space to pan; drag the selected first origin to translate and the second origin to rotate");
        Add("Status.MagneticDelete", "删除工具 · 单击电流导体后自动返回平移工具", "Delete · Click a current conductor; the tool then returns to Pan");
        Add("Status.PlacePlanarConductor", "平面理想恒定电流导体 · 第一次点击起点，第二次点击终点", "Planar ideal steady-current conductor · Click the start point, then the end point");
        Add("Status.PlaceVerticalConductor", "竖直面无限长恒定电流导体 · 在画布中单击放置", "Infinite perpendicular steady-current conductor · Click the canvas to place it");
        Add("Status.PlacePlanarLoop", "平面环形恒定电流 · 第一次点击圆心，第二次点击确定半径", "Planar circular steady current · Click the center, then click to set the radius");
        Add("Status.PlaceVerticalLoop", "垂直面环形恒定电流 · 第一次点击圆心，第二次点击确定半径", "Vertical circular steady current · Click the center, then click to set the radius");

        Add("Selection.None", "未选择元件", "No element selected");
        Add("Selection.AngleNone", "当前角度 --", "Current angle --");
        Add("Selection.Angle", "当前角度 {0:F1}°", "Current angle {0:F1}°");
        Add("Selection.Group", "组合（{0} 个元件）", "Group ({0} elements)");
        Add("Selection.Multiple", "已选择 {0} 个元件", "{0} elements selected");
        Add("Selection.PointLight", "单色点光源", "Monochromatic point source");
        Add("Selection.CompositePointLight", "复色点光源", "Composite point source");
        Add("Selection.ParallelLight", "单色平行光源", "Monochromatic parallel source");
        Add("Selection.CompositeParallelLight", "复色平行光源", "Composite parallel source");
        Add("Selection.Mirror", "平面反光镜", "Plane mirror");
        Add("Selection.ConcaveMirror", "理想凹球面镜", "Ideal concave spherical mirror");
        Add("Selection.ConvexMirror", "理想凸球面镜", "Ideal convex spherical mirror");
        Add("Selection.BeamSplitter", "平面分光镜", "Plane beam splitter");
        Add("Selection.Screen", "光屏", "Screen");
        Add("Selection.Aperture", "光阑", "Aperture");
        Add("Selection.ReflectionGrating", "反射光栅", "Reflection grating");
        Add("Selection.ConcaveGrating", "凹面光栅", "Concave grating");
        Add("Selection.ConvexLens", "凸透镜", "Convex lens");
        Add("Selection.ConcaveLens", "凹透镜", "Concave lens");
        Add("Selection.PointCharge", "点电荷 #{0}", "Point charge #{0}");
        Add("Selection.ChargedPlate", "带电平板 #{0}", "Charged plate #{0}");
        Add("Selection.PlanarConductor", "平面理想恒定电流导体 #{0}", "Planar ideal steady-current conductor #{0}");
        Add("Selection.VerticalConductor", "竖直面无限长恒定电流导体 #{0}", "Infinite perpendicular steady-current conductor #{0}");
        Add("Selection.PlanarLoop", "平面环形恒定电流 #{0}", "Planar circular steady current #{0}");
        Add("Selection.VerticalLoop", "垂直面环形恒定电流 #{0}", "Vertical circular steady current #{0}");

        Add("Placement.PointLight", "单色点光源（580 nm，360° 发光，单击放置）", "Monochromatic point source (580 nm, 360° emission; click to place)");
        Add("Placement.ParallelLight", "单色平行光源（580 nm，垂直于绘制线发射）", "Monochromatic parallel source (580 nm; emits perpendicular to the drawn line)");
        Add("Placement.CompositePointLight", "复色点光源（450/550/650 nm，单击放置）", "Composite point source (450/550/650 nm; click to place)");
        Add("Placement.CompositeParallelLight", "复色平行光源（450/550/650 nm，垂直于绘制线发射）", "Composite parallel source (450/550/650 nm; emits perpendicular to the drawn line)");
        Add("Placement.ConcaveMirror", "理想凹球面镜（先定镜面中心，再定曲率圆心）", "Ideal concave spherical mirror (set the vertex, then the center of curvature)");
        Add("Placement.ConvexMirror", "理想凸球面镜（先定镜面中心，再定曲率圆心）", "Ideal convex spherical mirror (set the vertex, then the center of curvature)");
        Add("Placement.BeamSplitter", "平面分光镜（透射/反射各 50%）", "Plane beam splitter (50% transmission / 50% reflection)");
        Add("Placement.ConcaveGrating", "凹面光栅（先定光栅顶点，再定曲率圆心）", "Concave grating (set the vertex, then the center of curvature)");
        Add("Placement.Object", "物件", "Object");
        Add("Placement.DrawingSpherical", "正在绘制{0} · 移动鼠标预览，单击确定曲率圆心和半径", "Drawing {0} · Move the pointer to preview; click to set the center of curvature and radius");
        Add("Placement.StartSpherical", "{0} · 单击确定镜面中心点（第一原点）", "{0} · Click to set the mirror vertex (first origin)");
        Add("Placement.Drawing", "正在绘制{0} · 单击确定终点", "Drawing {0} · Click to set the end point");
        Add("Placement.Start", "{0} · 单击确定起点", "{0} · Click to set the start point");

        return result;
    }

    private sealed record LocalizedText(string Chinese, string English);
}
