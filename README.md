# Conver Tuanjie To Unity Tool

用于辅助把团结工程迁移回 Unity 工程的 Unity Editor Package。工具应先安装到团结工程，并在团结编辑器内执行；转换完成后再使用 Unity 打开工程。

## 主要处理

- 修复团结引擎内创建资源的 `.meta` GUID。
- 修复 `AssetDatabase` children 关系，处理磁盘文件夹存在但 Project 窗口不显示的问题。
- 将 `Assets` 下的 `.scene` 场景改名为 `.unity`，并同步处理 `.meta` 文件名。

## 安装

在团结编辑器中打开 `Window > Package Manager`，选择 `Add package from git URL...`，输入：

```text
https://github.com/mister91jiao/ConverTuanjieToUnity.git
```

## 使用流程

1. 先提交或备份团结工程。
2. 使用团结打开待转换工程副本。
3. 安装本 Package。
4. 执行 `Tools/团结转Unity/转换GUID并修复资源`。
5. 转换完成后再用 Unity 打开工程。
6. 处理 Package 报错、重新烘焙必要场景、重新打 AssetBundle，并检查 Android 配置。

## 文档

详细教程见包内 `Documentation~` 目录。

## 沟通交流

如果遇到问题可以加入 qq群: 787652036 一起交流
也可以看下我另一个开源的关于Unity资源加载的方案
```text
https://github.com/mister91jiao/BundleMaster.git
``` 
