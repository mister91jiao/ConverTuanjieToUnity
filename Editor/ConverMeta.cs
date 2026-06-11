using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConverTuanjieToUnityTool.Editor
{
    /// <summary>
    /// 处理从团结工程转换回 Unity 工程后,团结引擎内创建资源的 .meta GUID 与 AssetDatabase 文件夹索引可能不一致的问题。
    /// </summary>
    /// <remarks>
    /// 这个工具主要做三件事：
    /// 1. 对比 AssetDatabase 当前识别到的 GUID 与 .meta 文件内记录的 GUID,发现不一致时把团结引擎内创建资源的 .meta 写回 Unity 当前 GUID。
    /// 2. 检查磁盘文件夹与 AssetDatabase.GetSubFolders 的 children 关系,修复“文件夹存在但 Project 窗口父级下不显示”的情况。
    /// 3. 将团结场景使用的 .scene 后缀统一改为 Unity 场景使用的 .unity 后缀。
    /// </remarks>
    public static class ConverMeta
    {
        private const string MenuPath = "Tools/团结转Unity/转换GUID并修复资源";

        /// <summary>
        /// 执行完整的团结转 Unity 资源修复流程。
        /// </summary>
        [MenuItem(MenuPath)]
        private static void CheckGuid()
        {
            RepairMetaGuid();

            // GUID 写入后强制刷新,让 Unity 重新读取 .meta 与资源数据库状态。
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            RepairAssetDatabaseChildren();
            ConvertTuanjieSceneExtension();
        }

        /// <summary>
        /// 检查所有已纳入 AssetDatabase 的资源,并修复团结引擎内创建资源的 .meta GUID。
        /// </summary>
        private static void RepairMetaGuid()
        {
            // AssetDatabase.GetAllAssetPaths 会返回 Unity 当前已纳入资源数据库的所有路径。
            // 这里只处理存在 .meta 文件的资源,避免 Packages 或内置资源路径被误处理。
            string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < allAssetPaths.Length; i++)
            {
                string assetPath = allAssetPaths[i];

                // Unity 当前从 AssetDatabase 里识别到的 GUID。
                string unityGuid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(unityGuid))
                {
                    continue;
                }

                string metaFilePath = assetPath + ".meta";
                if (!File.Exists(metaFilePath))
                {
                    continue;
                }

                string metaContent = File.ReadAllText(metaFilePath);
                string currentGuid = GetGuidFromMetaContent(metaContent, assetPath);
                if (string.IsNullOrEmpty(currentGuid) || string.Equals(currentGuid, unityGuid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                ChangeAssetsGuid(metaFilePath, currentGuid, unityGuid);
                Debug.Log("文件: " + assetPath + "\nTuanJieGuid: " + currentGuid + "\nUnityGuid: " + unityGuid);
            }
        }

        /// <summary>
        /// 从 .meta 文件内容中读取 guid 字段。
        /// </summary>
        private static string GetGuidFromMetaContent(string metaContent, string originFilePath)
        {
            string[] lines = metaContent.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("guid:", StringComparison.Ordinal))
                {
                    return line.Replace("guid:", "").Trim();
                }
            }

            Debug.LogError("获取 GUID 失败: " + originFilePath);
            return string.Empty;
        }

        /// <summary>
        /// 将团结引擎内创建资源的 .meta GUID 替换为真实的 Unity GUID。
        /// </summary>
        private static void ChangeAssetsGuid(string metaFilePath, string currentGuid, string newGuid)
        {
            string metaContent = File.ReadAllText(metaFilePath);
            metaContent = metaContent.Replace(currentGuid, newGuid);
            File.WriteAllText(metaFilePath, metaContent);

            AssetDatabase.SaveAssetIfDirty(new GUID(newGuid));
        }

        /// <summary>
        /// 检查并修复 AssetDatabase 的父子文件夹索引关系。
        /// </summary>
        private static void RepairAssetDatabaseChildren()
        {
            HashSet<string> parentPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 只扫描 Application.dataPath,也就是工程 Assets 目录。
            string[] allDirectories = Directory.GetDirectories(Application.dataPath, "*", SearchOption.AllDirectories);
            for (int i = 0; i < allDirectories.Length; i++)
            {
                string assetPath = ToAssetPath(allDirectories[i]);
                string parentPath = GetParentAssetPath(assetPath);
                if (!string.IsNullOrEmpty(parentPath) && AssetDatabase.IsValidFolder(parentPath))
                {
                    parentPaths.Add(parentPath);
                }
            }

            int repairCount = 0;
            foreach (string parentPath in parentPaths)
            {
                // AssetDatabase 路径需要转回磁盘路径,才能枚举真实存在的子目录。
                string fullParentPath = ToFullPath(parentPath);
                if (!Directory.Exists(fullParentPath))
                {
                    continue;
                }

                // indexedChildren 表示 Unity 当前认为这个父文件夹下已有的子文件夹。
                HashSet<string> indexedChildren = new HashSet<string>(AssetDatabase.GetSubFolders(parentPath), StringComparer.OrdinalIgnoreCase);

                // physicalChildren 表示磁盘上真实存在的子文件夹。
                string[] physicalChildren = Directory.GetDirectories(fullParentPath);
                for (int i = 0; i < physicalChildren.Length; i++)
                {
                    string childPath = ToAssetPath(physicalChildren[i]);
                    if (indexedChildren.Contains(childPath) || !AssetDatabase.IsValidFolder(childPath))
                    {
                        // 已在父级 children 里,或者 AssetDatabase 不认为它是有效文件夹,都不处理。
                        continue;
                    }

                    Debug.LogWarning("检测到 AssetDatabase children 关系缺失: parent=" + parentPath + ", child=" + childPath);
                    if (RepairFolderChildrenIndex(parentPath, childPath))
                    {
                        repairCount++;
                    }
                }
            }

            Debug.Log("AssetDatabase children 检查完成,修复数量: " + repairCount);
        }

        /// <summary>
        /// 将 Assets 目录下团结场景使用的 .scene 文件改名为 Unity 场景使用的 .unity 文件。
        /// </summary>
        /// <remarks>
        /// 这里使用 AssetDatabase.MoveAsset 改名,而不是 File.Move 直接改磁盘文件。
        /// 这样 Unity 会同步移动 xxx.scene.meta 到 xxx.unity.meta,可以保留原 GUID 和 AssetBundle 配置。
        /// </remarks>
        private static void ConvertTuanjieSceneExtension()
        {
            string[] sceneFullPaths = Directory.GetFiles(Application.dataPath, "*.scene", SearchOption.AllDirectories);
            if (sceneFullPaths.Length <= 0)
            {
                Debug.Log("未检测到需要转换后缀的 .scene 场景文件。");
                return;
            }

            int convertedCount = 0;
            for (int i = 0; i < sceneFullPaths.Length; i++)
            {
                string sceneAssetPath = ToAssetPath(sceneFullPaths[i]);
                string unityAssetPath = sceneAssetPath.Substring(0, sceneAssetPath.Length - ".scene".Length) + ".unity";
                string unityFullPath = ToFullPath(unityAssetPath);

                if (File.Exists(unityFullPath))
                {
                    Debug.LogError("转换场景后缀失败,目标 .unity 文件已存在: " + unityAssetPath);
                    continue;
                }

                if (File.Exists(unityFullPath + ".meta"))
                {
                    Debug.LogError("转换场景后缀失败,目标 .unity.meta 文件已存在: " + unityAssetPath + ".meta");
                    continue;
                }

                AssetDatabase.ImportAsset(sceneAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                string oldGuid = AssetDatabase.AssetPathToGUID(sceneAssetPath);
                if (string.IsNullOrEmpty(oldGuid))
                {
                    Debug.LogError("转换场景后缀失败,AssetDatabase 中找不到场景 GUID: " + sceneAssetPath);
                    continue;
                }

                // AssetDatabase.MoveAsset 会一起移动 .meta 文件,等价于把 xxx.scene 和 xxx.scene.meta 改成 xxx.unity 和 xxx.unity.meta。
                string moveError = AssetDatabase.MoveAsset(sceneAssetPath, unityAssetPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    Debug.LogError("转换场景后缀失败: " + sceneAssetPath + " -> " + unityAssetPath + "\n" + moveError);
                    continue;
                }

                AssetDatabase.ImportAsset(unityAssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                if (!File.Exists(unityFullPath + ".meta"))
                {
                    Debug.LogError("转换场景后缀后未找到 .meta 文件: " + unityAssetPath + ".meta");
                    continue;
                }

                string newGuid = AssetDatabase.AssetPathToGUID(unityAssetPath);
                if (!string.Equals(oldGuid, newGuid, StringComparison.OrdinalIgnoreCase))
                {
                    Debug.LogError("转换场景后缀后 GUID 发生变化: " + unityAssetPath + "\nold=" + oldGuid + "\nnew=" + newGuid);
                    continue;
                }

                convertedCount++;
                Debug.Log("已转换场景后缀: " + sceneAssetPath + " -> " + unityAssetPath);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("场景后缀检查完成,转换数量: " + convertedCount);
        }

        /// <summary>
        /// 通过 AssetDatabase.MoveAsset 临时移动再还原文件夹,强制 Unity 重建父级 children 关系。
        /// </summary>
        /// <param name="parentPath">子文件夹所属父路径,格式为 Assets 开头的 Unity 资源路径。</param>
        /// <param name="folderPath">需要修复的子文件夹路径,格式为 Assets 开头的 Unity 资源路径。</param>
        /// <returns>修复成功返回 true;任意步骤失败返回 false。</returns>
        /// <remarks>
        /// 使用 AssetDatabase.MoveAsset 而不是直接移动磁盘文件,是为了让 Unity 同步移动 .meta 文件并保持 GUID。
        /// 修复后会再次读取 GUID 做校验,防止临时移动过程意外生成新 .meta。
        /// </remarks>
        private static bool RepairFolderChildrenIndex(string parentPath, string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                Debug.LogError("AssetDatabase children 修复失败,父文件夹无效: " + parentPath);
                return false;
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogError("AssetDatabase children 修复失败,子文件夹无效: " + folderPath);
                return false;
            }

            // 临时目录放在同一个父级下,可以减少跨目录移动带来的导入范围。
            // 名称包含原文件夹名,便于在异常中定位,但会先检查是否已存在,避免覆盖真实资源。
            string tempFolderPath = parentPath + "/__AssetDatabaseChildrenRepairTemp_" + Path.GetFileName(folderPath) + "__";
            if (AssetDatabase.IsValidFolder(tempFolderPath) || Directory.Exists(ToFullPath(tempFolderPath)))
            {
                Debug.LogError("AssetDatabase children 修复失败,临时文件夹已存在: " + tempFolderPath);
                return false;
            }

            // 记录移动前 GUID,移动还原后必须保持一致。
            string oldGuid = AssetDatabase.AssetPathToGUID(folderPath);

            // 第一次移动：把有问题的 child 暂时移出原路径,让父级 children 发生一次真实变化。
            string moveError = AssetDatabase.MoveAsset(folderPath, tempFolderPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError("临时移动文件夹失败: " + folderPath + "\n" + moveError);
                return false;
            }

            ImportAssetOptions refreshOptions = ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport;
            AssetDatabase.Refresh(refreshOptions);

            // 第二次移动：还原到原路径,触发 Unity 重新把 child 写回父级 children 关系。
            moveError = AssetDatabase.MoveAsset(tempFolderPath, folderPath);
            if (!string.IsNullOrEmpty(moveError))
            {
                Debug.LogError("还原文件夹失败: " + folderPath + "\n" + moveError);
                return false;
            }

            // 同步重导入父级和子级,确保 Project 窗口 AssetDatabase 查询和磁盘状态一致。
            ImportAssetOptions importOptions = refreshOptions | ImportAssetOptions.ImportRecursive;
            AssetDatabase.ImportAsset(parentPath, importOptions);
            AssetDatabase.ImportAsset(folderPath, importOptions);
            AssetDatabase.Refresh(refreshOptions);

            // GUID 一旦变化,说明修复过程中出现了 .meta 丢失或重建,这种情况必须明确报错。
            string newGuid = AssetDatabase.AssetPathToGUID(folderPath);
            if (!string.Equals(oldGuid, newGuid, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("AssetDatabase children 修复后 GUID 发生变化: " + folderPath + "\nold=" + oldGuid + "\nnew=" + newGuid);
                return false;
            }

            Debug.Log("已修复 AssetDatabase children 关系: " + folderPath);
            return true;
        }

        /// <summary>
        /// 将磁盘绝对路径转换为 Unity 使用的 Assets 相对路径。
        /// </summary>
        private static string ToAssetPath(string fullPath)
        {
            // Unity AssetDatabase 使用正斜杠路径;Windows 磁盘路径可能包含反斜杠。
            string normalizedFullPath = fullPath.Replace('\\', '/');
            string normalizedDataPath = Application.dataPath.Replace('\\', '/');
            if (string.Equals(normalizedFullPath, normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                return "Assets";
            }

            if (normalizedFullPath.StartsWith(normalizedDataPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return "Assets/" + normalizedFullPath.Substring(normalizedDataPath.Length + 1);
            }

            return normalizedFullPath;
        }

        /// <summary>
        /// 将 Unity 使用的 Assets 相对路径转换为磁盘绝对路径。
        /// </summary>
        /// <param name="assetPath">Assets 开头的资源路径。</param>
        /// <returns>磁盘绝对路径。</returns>
        private static string ToFullPath(string assetPath)
        {
            if (string.Equals(assetPath, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                return Application.dataPath;
            }

            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// 获取 Unity 资源路径的父级路径。
        /// </summary>
        /// <param name="assetPath">Assets 开头的资源路径。</param>
        /// <returns>父级资源路径;没有父级时返回空字符串。</returns>
        private static string GetParentAssetPath(string assetPath)
        {
            int slashIndex = assetPath.LastIndexOf('/');
            if (slashIndex <= 0)
            {
                return string.Empty;
            }

            return assetPath.Substring(0, slashIndex);
        }
    }
}
