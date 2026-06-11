# Android 设置以及变化

团结工程迁移到 Unity 后，Android 设置需要重点检查。相关变化主要集中在 AndroidManifest、Gradle 模板

## 1. AndroidManifest 变化

Android activity theme 从团结名称改为 Unity 名称：

```diff
-android:theme="@style/TuanjieThemeSelector"
+android:theme="@style/UnityThemeSelector"
```

检查文件：

```text
Assets/Plugins/Android/AndroidManifest.xml
```

## 2. mainTemplate.gradle 变化

`mainTemplate.gradle` 中有两处需要从团结名称切回 Unity 名称。

Proguard 文件：

```diff
-consumerProguardFiles 'proguard-tuanjie.txt'**USER_PROGUARD**
+consumerProguardFiles 'proguard-unity.txt'**USER_PROGUARD**
```

StreamingAssets 变量：

```diff
-noCompress = **BUILTIN_NOCOMPRESS** + tuanjieStreamingAssets.tokenize(', ')
+noCompress = **BUILTIN_NOCOMPRESS** + unityStreamingAssets.tokenize(', ')
```

检查文件：

```text
Assets/Plugins/Android/mainTemplate.gradle
```

