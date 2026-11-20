"""Package existing Windows publishes and source with integrity checks (stdlib only)."""
from pathlib import Path
import hashlib
import json
import shutil
import struct
import zipfile
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
DIST = ROOT / "dist"
VERSION = ET.parse(ROOT / "src/MechToolbox.Desktop/MechToolbox.Desktop.csproj").findtext(".//Version")
outputs = []
for kind in ("portable", "lite"):
    stage = DIST / f"win-x64-{kind}"
    with (stage / "MechToolbox.exe").open("rb") as stream:
        assert stream.read(2) == b"MZ"
        stream.seek(0x3C)
        offset = struct.unpack("<I", stream.read(4))[0]
        stream.seek(offset)
        assert stream.read(4) == b"PE\0\0"
        assert struct.unpack("<H", stream.read(2))[0] == 0x8664
    for name in ("metric-coarse.v1.json", "parallel-keyways-gb.v1.json", "socket-head-gbt70-1.v1.json", "circlips-gb.v1.json"):
        assert json.loads((stage / "data" / name).read_text())["schemaVersion"] == 1
    (stage / "Assets").mkdir(exist_ok=True)
    shutil.copy2(ROOT / "src/MechToolbox.Desktop/Assets/toolbox.ico", stage / "Assets/toolbox.ico")
    assert (stage / "Assets/toolbox.ico").read_bytes()[:4] == b"\x00\x00\x01\x00"
    (stage / "Install-CurrentUser.ps1").write_text((ROOT / "scripts/install-current-user.ps1").read_text(), encoding="utf-8-sig")
    shutil.copy2(ROOT / "README.md", stage / "README.md")
    for name in ("LICENSE", "THIRD_PARTY_NOTICES.md"):
        shutil.copy2(ROOT / name, stage / name)
    if kind == "portable":
        shutil.copytree(ROOT / "licenses/dotnet", stage / "licenses/dotnet", dirs_exist_ok=True)
    requirement = "自带运行环境，无需安装 .NET。" if kind == "portable" else "需安装 .NET 10 Desktop Runtime x64。"
    (stage / "开始使用.txt").write_text(
        f"机设工具箱 v{VERSION} · Windows x64\n{requirement}\n\n"
        "完整解压后双击 MechToolbox.exe，保留同级 data 文件夹。\n"
        "螺栓页：仅公制粗牙，省略螺距；增加 GB/T 70.1-2008 内六角圆柱头尺寸。孔径反查只输入一个孔径，自动同时列出 ±3 mm 内的通孔与攻丝底孔，含边界，按差值从小到大排列。\n"
        "v0.3.1：卡簧槽宽与三档通孔公差带展开为数值偏差和允许范围；成品内螺纹 6H 显示中径与小径范围；增加程序图标。\n"
        "卡簧与卡簧槽页：标准型 A 型，轴用与孔用；按公称轴径或孔径查询。轴用 40 mm 按现行国标原表显示槽径 37.0 mm、表列槽深 1.25 mm，尺寸关系存在差异，已明确提示加工前核实。\n"
        "键与键槽页：输入轴径，查看普通平键截面及轴、毂槽宽和槽深。\n"
        "键槽截面尺寸按 GB/T 1095-2003 查询，按轴径选键属于常用设计推荐。\n"
        "源码附带 Windows 自动界面检查入口，验证记录见 README.md。\n",
        encoding="utf-8-sig",
    )
    target = DIST / f"MechToolbox-{VERSION}-win-x64-{kind}.zip"
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in sorted(stage.rglob("*")):
            if path.is_file():
                archive.write(path, Path("MechToolbox") / path.relative_to(stage))
    outputs.append(target)

source = DIST / f"MechToolbox-{VERSION}-source.zip"
excluded = {"bin", "obj", "dist", "tmp", "artifacts", ".git", ".DS_Store", "__pycache__"}
with zipfile.ZipFile(source, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
    for path in sorted(ROOT.rglob("*")):
        relative = path.relative_to(ROOT)
        if path.is_file() and not any(part in excluded for part in relative.parts):
            archive.write(path, Path("mechanical-toolbox") / relative)
outputs.append(source)
sums = []
for path in outputs:
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    sums.append(f"{digest}  {path.name}")
    print(f"{path.name}: {path.stat().st_size / 1024 / 1024:.2f} MiB; CRC verified", flush=True)
(DIST / f"SHA256SUMS-{VERSION}.txt").write_text("\n".join(sums) + "\n")
