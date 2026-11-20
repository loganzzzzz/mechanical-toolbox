# 扩展约定

## 模块边界

`MechToolbox.Core` 不引用 WPF。模块负责标准查询语义、校验、版本和结果，界面负责交互、剪贴板与保存对话框。不同标准不共享螺栓专用数据结构。

新增标准模块需要：

1. 在 Core 中实现 `IStandardModule`，提供稳定唯一的 `Descriptor.Id`，当前 `ApiVersion` 为 1。
2. 定义该模块独立的数据结构，以及 `IStandardDataProvider<T>` 实现。数据须带结构版本、数据版本、日期、单位、标准编号和可追溯来源。
3. 在 `App.xaml.cs` 中加载并调用 `ModuleRegistry.Register(new YourModule(data))`。
4. 在 Desktop 中添加自己的 `UserControl`，在 `MainWindow` 构造函数中注册工厂：

```csharp
desktop.Register("keyways", (module, status) =>
    new KeywayQueryView((KeywayModule)module, status));
```

侧边栏从 `registry.Modules` 自动生成；界面缓存保持各模块的当前选择。重复 ID 或不支持的接口版本会被拒绝。缺失的界面工厂会明确报错。

第一版为源码级扩展，添加新模块后重新编译发布。尚未提供从外部 DLL 加载代码、插件市场、远程执行或自动更新接口。数据来源可按模块需要实现为 JSON、SQLite 或企业服务；首版仅使用本地 JSON。

## 粗牙数据结构 v1

文件：`data/metric-coarse.v1.json`。

| 字段 | 含义 |
| --- | --- |
| schemaVersion | 结构版本，当前为 1；不兼容修改应升级 |
| dataVersion | 内容版本，每次数据修订必须变化 |
| updatedOn | 核对日期，格式 yyyy-MM-dd |
| threadSeries | 当前必须为 metric-coarse |
| sources | 来源 ID、名称、HTTPS 链接、适用范围 |
| clearanceSourceId | 通孔尺寸默认来源 |
| tapSourceId | 切削攻丝底孔默认来源 |
| sizes[].diameter | 公称直径，mm |
| sizes[].pitch | 粗牙螺距，mm |
| sizes[].tapDrill | 工艺选用的切削底孔，mm |
| sizes[].fine / medium / coarse | 通孔三个系列的公称孔径，mm |
| sizes[].tapSourceId | 可选，覆盖该规格的底孔来源 |

全部尺寸使用 `decimal`。启动校验版本、来源、重复规格、正数、底孔小于螺栓、三档通孔严格递增等条件；未知字段会拒绝读取，避免拼错字段后悄悄使用默认值。结构合法不能证明符合标准，维护者仍需逐项审查原始资料。未收录规格必须明确拒绝查询，不能线性插值或按增量生成通孔。

## 数据更新

1. 核对适用标准、版本与勘误，记录原始来源及核对日期。
2. 编辑数据并同步数据版本。维护好各来源的适用范围。
3. 独立复核后更新 `tests/MechToolbox.Checks/approved-sizes.csv`。此文件是已审查基准，不能在测试中自动从 JSON 生成。
4. 跑核心检查、Windows 界面检查，审查截图，再重新打包。
5. 如添加细牙，不应在当前 `metric-coarse` 表中混用同直径多螺距。应提供复合键 `(diameter, pitch)` 和新的模式选择器，再显式迁移数据结构版本。

## 导出与界面

CSV 必须使用固定小数点、明确的 mm 单位及来源、版本字段；UI 用带 BOM 的 UTF-8 写入。导出只包含当前筛选结果，零结果时不生成误导性文件。复制结果须保留所选装配系列、三档通孔、底孔性质和数据版本。

不要把通孔装配系列直接当作定位精度；不要把切削底孔当作挤压丝锥底孔。盲孔深度、强度或扭矩如果未来加入，应单独声明输入条件和计算依据。

## v0.2.0 接入示例

`KeywayModule`、`JsonKeywayDataProvider`、`KeywayQueryView` 是完整的第二模块示例，使用独立 JSON。实现 `IStandardDataModule` 可让「标准数据与依据」页面自动显示本模块的来源和版本摘要。

`KeywayViewModel` 和 `BoltReverseViewModel` 位于 Core，能在任何操作系统上检查输入与结果更新。输入非法时必须立即清空旧结果。键槽按手选宽度精确定位国标截面，轴径只用于检验尺寸计算，不按经验区间自动选键；反查孔径可能有多个匹配，禁止只取第一项。槽宽偏差保留四位小数。

螺栓页已移除全规格对照表；Core 的 `ExportCsv` 保留为代码接口，当前没有对应界面入口。新的孔径反查使用已有的螺栓数据，不另存一份反查表，以免后续标准数据更新时正反查询不一致。


## 粗牙与反查接口（v0.2.2）

只提供粗牙，所有用户输出省略螺距。源数据中的 pitch 继续用于标准数据校验，禁止因隐藏字段而改动已核对的底孔。`BoltReverse.Find(module, diameter)` 固定查询输入孔径 ±3 mm 内（含边界）的全部通孔档位与切削攻丝底孔，半径由 `SearchRadius` 定义。结果通过 Kind / HoleName 标明用途，Difference = 推荐孔径 − 输入孔径；按绝对差值、螺栓直径、孔类型依次排序。不提供孔类型或匹配偏差选项。CSV 代码接口也省略螺距列。

## 内六角圆柱头数据（v0.2.3）

`SocketHeadDataset` / `JsonSocketHeadDataProvider` 从 `data/socket-head-gbt70-1.v1.json` 读取 GB/T 70.1-2008 表 1，作为 `BoltModule` 的附加数据集。`HeadFor(size)` 按精确公称直径匹配，未列规格返回 null，界面隐藏数值并明确提示，不影响原底孔和通孔查询。光滑头和滚花头的最大直径分别存储，头高取最大值，内六角对边为公称值，深度取最小值。不得将这些字段当作沉孔尺寸。

数据表收录标准的 20 个规格，M14 注明非优选。`approved-socket-heads.csv` 按原表人工核对，测试中不得由 JSON 自动生成。数据来源及版本并入统一来源页面；复制及 CSV 代码接口携带尺寸和头部数据版本。桌面启动要求该数据文件完整且有效。

## 卡簧模块（v0.3.0）

`CirclipModule` / `JsonCirclipDataProvider` / `CirclipViewModel` 和桌面 `CirclipQueryView` 使用独立 `circlips-gb.v1.json`。当前只收录标准型 A 型，复合键为 `(kind, diameter)`；类型为 shaft 或 bore，按标准公称规格精确匹配，未列规格不插值。源值用 decimal，槽径偏差使用 SlotTolerance；m 的公差带固定 H13，数值通过 `LinearTolerances.Hole(grooveWidth,"H13")` 展开。厚度和自由状态直径明确为基本尺寸，不给出未录入的制造偏差。

槽深 t 单独保留原表值，并与 |d1-d2|/2 校验。轴用 40 mm 为已识别原表矛盾，reviewNote 必须保留；界面与复制须完整显示国标原表槽径 37.0 mm、表列槽深 1.25 mm 及偏差和极限，同时以单独提示明确两者尺寸关系不一致。不得静默采用厂家数值改写国标值，示意图仍暂停绘制。受载边槽底圆角按第 6.3 条 r≤0.1s 计算；n 为表列最小边距，仍须按载荷及材料复核。

增加 B 型等系列时须扩展复合键并清楚区分，禁止混入 A 型同直径记录。核对基准为 approved-circlips.csv，测试中不得从 JSON 自动生成；保持来源正文页码。


`LinearTolerances` 保存已核对的 GB/T 1800.2-2020 表 1、表 6 的 H12／H13／H14 数值，当前基本尺寸范围为 (0,50] mm；不可用公称螺栓直径替代实际通孔直径分档。`MetricThreadLimits` 保存当前 23 档粗牙的成品 6H 中径／小径极限，以 `(Diameter,Pitch)` 精确匹配；未收录组合返回 null，不插值。扩展规格时须同步核对范围、来源和独立复核表。

## v0.4.0 国标来源与键槽结构 v2

当前 `KeywayDataset.SchemaVersion=2`，删除 `shaftSelectionSourceId` 和所有行的 `shaftMin/shaftMax`，只保存 GB/T 1095-2003 表 1 的原值。`KeywayModule.Query(diameter,width,fit)` 按宽度精确查询截面，轴径不改变截面。`SelectedSize` 和 `DiameterText` 独立；双击表格仅修改所选截面。旧 v1 结构及未收录宽度均拒绝，不能静默按轴径选键。

标准来源必须以国家标准全文公开系统或全国标准信息公共服务平台确认为现行，并核对实际原表；不能把厂家资料改名为 GB/T。每次标准迁移须记录题录状态、表号、正文页、差异及核对日期，见 NATIONAL-STANDARDS.md。原表扫描从公开镜像取得时须与官方题录区分，不能声称是官方直接下载。原文不进仓库；测试复核表不得由应用 JSON 自动生成。

`MetricThreadLimits` 的 23 档 6H 均采用 GB/T 15756-2008 表 3；通孔与卡簧槽宽共用 GB/T 1800.2-2020 数值表。不要仅因国际标准更新而假设国标也已更新；标准计划、征求意见稿不能冒充已实施标准。
