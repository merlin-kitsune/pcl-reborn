# FreeTypeHintAgent

运行时补丁，用于在 Minecraft 使用 lwjgl-freetype 时强制 `FT_LOAD_NO_HINTING`。

## 为什么需要

2026-11 公布的 Minecraft2Shell 类漏洞利用 TrueType 字体的 hinting 字节码在
`FreeType` 原生库中执行越界写。当前所有官方 lwjgl-freetype 原生库（包括
Minecraft 版本 JSON 中的 `-mojang` 构建）都不包含该漏洞的修复，因此不能只靠
替换原生库解决问题。

本 agent 在 `org.lwjgl.util.freetype.FreeType` 的
`FT_Load_Glyph` / `FT_Load_Char` 入口给 flags 强制加上
`FT_LOAD_NO_HINTING`（2），同时覆盖 `nFT_Load_Glyph` / `nFT_Load_Char`。
字体自带的 TrueType 字节码不会执行，攻击链在进入解释器前被切断。

补丁必须命中 4 个加载方法；若变换失败会直接终止游戏 JVM，不会静默放行未受保护的 FreeType。未来 lwjgl-freetype 若调整方法签名，需同步更新 flagLocal 与期望方法数，否则会按 fail-closed 处理。

代价是小字号文字会略微变柔；这是有意的安全取舍。

## 构建

需要 JDK 8+ 和 ASM 9.x。jar 内已包含 ASM 9.7.1（BSD-3-Clause）：

```
javac --release 8 -cp asm-9.7.1.jar -d build src/pcl/freetype/FreetypeHintAgent.java
# 把 asm.jar 解压到 build，删除 module-info.class 和 META-INF/MANIFEST.MF
jar cfm FreetypeHintAgent.jar manifest.txt -C build .
```

manifest 必须包含：

```
Premain-Class: pcl.freetype.FreetypeHintAgent
```

产物同时放在：

- `PCLCS/Launch/FreetypeHintAgent.jar`（PCLCS 内嵌资源，运行时由 `LaunchUtils.ExtractPatch` 释放）
- `Plain Craft Launcher 2/Resources/FreetypeHintAgent.jar`（随启动器分发的副本）

## 测试

`FreetypeHintAgent.patch(byte[])` 是公开的纯函数，可直接对
`org/lwjgl/util/freetype/FreeType.class` 做变换，再用 `javap -c` 确认
`FT_Load_Glyph` 开头出现 `iload_2 / iconst_2 / ior / istore_2`。
`-javaagent:FreetypeHintAgent.jar` 加载 `FreeType` 类时也会打印
`[PCL] FreeType hinting guard active`。
