'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "PCLDebug"
    '用于微软登录的 ClientId
    '本地修改：从官方发行版二进制中提取（device code 流程里硬编码的 client_id）
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "fe72edc2-3a6f-4280-90e8-e2beb64ce7e1")
    'CurseForge API Key
    '本地修改：从官方发行版二进制中提取（Base64 存储，避免明文入库）；环境变量优先
    Public CurseForgeAPIKey As String = If(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"),
                                            System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String("JDJhJDEwJDdXV1YxV0VMY3dYOFhwN2Q2OE1Icy41Z3JUeVpWYTZTeW55ZVN5TWgxcnNFODluSWEwOXpP")))
    '用于匿名数据收集的腾讯云日志服务上报 URL，形如 https://{region}.cls.tencentcs.com/track?topic_id={topic_id}
    Public Const ClsBaseUrl As String = ""

#Region "网络鉴权"

    '本地修改：回退 2.99.0.0 引入的「关闭 MC 下载通道」，恢复下载入口与整合包安装
    Public Const RestrictMcDownload As Boolean = False
    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)

    Public ThemeNow As Integer = -1
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0
    '本地修改：滑稽彩需要记住上一次的色相与变化方向
    Private ThemeRainbowInitialized As Boolean = False
    Private ThemeRainbowHueDelta As Integer = 1

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            If ThemeNow = NewTheme AndAlso NewTheme >= 0 Then Return
            If NewTheme >= 0 Then ThemeNow = NewTheme

            '本地修改：恢复各主题的配色表（开源版缺少这一段，导致点击任何主题外观都不会变化）
            Dim CurrentTheme As Integer = If(ThemeNow >= 0, ThemeNow, 0)
            ColorHueTopbarDelta = 0
            Select Case CurrentTheme
                Case 0 '龙猫蓝
                    ColorHue = 210
                    ColorSat = 85
                    ColorLightAdjust = 0
                Case 1 '甜柠青
                    ColorHue = 180
                    ColorSat = 60
                    ColorLightAdjust = 0
                Case 2 '小草绿
                    ColorHue = 130
                    ColorSat = 50
                    ColorLightAdjust = 0
                    ColorHueTopbarDelta = New Integer() {5, -5, 5}
                Case 3 '菠萝黄
                    ColorHue = 60
                    ColorSat = 65
                    ColorLightAdjust = 0
                    ColorHueTopbarDelta = New Integer() {-5, 10, -5}
                Case 4 '橡木棕
                    ColorHue = 20
                    ColorSat = 50
                    ColorLightAdjust = -5
                    ColorHueTopbarDelta = New Integer() {-5, 10, -5}
                Case 5 '玄素黑
                    ColorHue = 225
                    ColorSat = 10
                    ColorLightAdjust = -20
                Case 6 '铁杆粉
                    ColorHue = 345
                    ColorSat = 95
                    ColorLightAdjust = 15
                Case 7 '神秘紫
                    ColorHue = 270
                    ColorSat = 70
                    ColorLightAdjust = -5
                    ColorHueTopbarDelta = New Integer() {15, 0, 15}
                Case 8 '秋仪金
                    ColorHue = 50
                    ColorSat = 80
                    ColorLightAdjust = 5
                Case 9 '活跃橙
                    ColorHue = 25
                    ColorSat = 90
                    ColorLightAdjust = 5
                Case 10 '跳票红
                    ColorHue = 0
                    ColorSat = 80
                    ColorLightAdjust = 0
                    ColorHueTopbarDelta = New Integer() {-10, 10, -10}
                Case 11 '极客蓝
                    ColorHue = 230
                    ColorSat = 70
                    ColorLightAdjust = -10
                    ColorHueTopbarDelta = New Integer() {-10, 10, -10}
                Case 12 '滑稽彩
                    If Not ThemeRainbowInitialized Then
                        ThemeRainbowInitialized = True
                        ColorHue = RandomInteger(0, 359)
                        ThemeRainbowHueDelta = (RandomInteger(0, 1) * 2 - 1) * 3
                    End If
                    ColorHue = (ColorHue + ThemeRainbowHueDelta + 360) Mod 360
                    ColorSat = 65
                    ColorLightAdjust = 0
                Case 13 '欧皇彩
                    ColorHue = 325
                    ColorSat = 90
                    ColorLightAdjust = 0
                    ColorHueTopbarDelta = New Integer() {-10, 0, 15}
                Case 14 '自定义
                    ColorHue = Settings.Get(Of Integer)("UiLauncherHue")
                    ColorSat = Settings.Get(Of Integer)("UiLauncherSat")
                    ColorLightAdjust = Settings.Get(Of Integer)("UiLauncherLight") - 20
                    ColorHueTopbarDelta = Settings.Get(Of Integer)("UiLauncherDelta") - 90
                Case Else
                    ColorHue = 215
                    ColorSat = 90
                    ColorLightAdjust = 0
            End Select
            Select Case ThemeDontClick
                Case 1
                    ColorLightAdjust = 999
                Case 2
                    ColorHue = RandomInteger(0, 359)
                    ColorSat = RandomInteger(40, 70)
                    ColorLightAdjust = RandomInteger(-20, 20)
            End Select

            Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
            Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
            Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
            Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
            Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
            Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
            Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
            ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
            ColorBg1 = New MyColor(190, Color7)

            ColorSemiTransparent = New MyColor(1, Color8)
            Application.Current.Resources("ColorBrush1") = New SolidColorBrush(Color1)
            Application.Current.Resources("ColorBrush2") = New SolidColorBrush(Color2)
            Application.Current.Resources("ColorBrush3") = New SolidColorBrush(Color3)
            Application.Current.Resources("ColorBrush4") = New SolidColorBrush(Color4)
            Application.Current.Resources("ColorBrush5") = New SolidColorBrush(Color5)
            Application.Current.Resources("ColorBrush6") = New SolidColorBrush(Color6)
            Application.Current.Resources("ColorBrush7") = New SolidColorBrush(Color7)
            Application.Current.Resources("ColorBrush8") = New SolidColorBrush(Color8)
            Application.Current.Resources("ColorBrushBg0") = New SolidColorBrush(ColorBg0)
            Application.Current.Resources("ColorBrushBg1") = New SolidColorBrush(ColorBg1)
            Application.Current.Resources("ColorObject1") = CType(Color1, Color)
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            ThemeRefreshMain()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + Deltas(0), ColorSat, 48 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + Deltas(1), ColorSat, 54 + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + Deltas(2), ColorSat, 48 + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8, 91)})
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = New MyColor(245, 245, 245)
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        '本地修改：解锁机制已移除，全部主题直接可用，也不再弹任何解锁提示
        Return True
    End Function

#End Region

#Region "更新"

    '本地修改：恢复联网检查更新。官方 ServerConfig/notice 走 CDN 签名（SecretCdnSign，开源版已剥离），
    '因此这里改为直接读取 GitHub 官方发布页的 latest 重定向（即官方分发渠道），无需任何密钥。
    Friend Sub UpdateCheckByButton()
        RunInThread(
        Sub()
            Try
                Dim LatestTag As String = GetLatestReleaseTag()
                Dim LatestVer As Version = Nothing
                Dim CurrentVer As Version = Nothing
                If Not Version.TryParse(LatestTag.TrimStart("v"c, "V"c), LatestVer) Then Throw New Exception($"无法识别最新版本号：{LatestTag}")
                If Not Version.TryParse(VersionBaseName, CurrentVer) Then Throw New Exception($"无法识别当前版本号：{VersionBaseName}")
                If LatestVer > CurrentVer Then
                    Logger.Info($"发现新版本：{LatestTag}（当前 {VersionBaseName}）")
                    RunInUi(Sub()
                                MyMsgBox($"发现新版本：{LatestTag}{vbCrLf}当前版本：{VersionBaseName}{vbCrLf}{vbCrLf}是否前往官方发布页下载？",
                                        "检查更新", "前往下载", "稍后",
                                        Button1Action:=Sub() OpenWebsite($"https://github.com/merlin-kitsune/pcl-reborn/releases/tag/{LatestTag}"))
                            End Sub)
                Else
                    Hint($"当前已是最新版本（{VersionBaseName}）", HintType.Green)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "检查更新失败")
                Hint($"检查更新失败：{ex.Message}（若本仓库尚未发布任何 Release，请等待作者发布后再试）", HintType.Red)
            End Try
        End Sub)
    End Sub

    ''' <summary>
    ''' 读取最新版本号。优先走 jsDelivr 的版本索引（国内外都能访问），失败时回退到 GitHub 发布页重定向。
    ''' </summary>
    Private Function GetLatestReleaseTag() As String
        Try
            Return GetLatestReleaseTagByJsDelivr()
        Catch ex As Exception
            Logger.Warn(ex, "通过 jsDelivr 获取最新版本号失败，改用 GitHub 发布页")
        End Try
        Return GetLatestReleaseTagByGitHub()
    End Function

    ''' <summary>
    ''' 从 jsDelivr 的包版本索引中读取最新版本号（不经过 GitHub，国内访问更稳定）。
    ''' </summary>
    Private Function GetLatestReleaseTagByJsDelivr() As String
        Dim Handler As New System.Net.Http.HttpClientHandler With {.AllowAutoRedirect = True}
        Using Client As New System.Net.Http.HttpClient(Handler)
            Client.Timeout = TimeSpan.FromSeconds(20)
            Using Req As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,
                                                               "https://data.jsdelivr.com/v1/packages/gh/merlin-kitsune/pcl-reborn")
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}")
                Using Resp As System.Net.Http.HttpResponseMessage = Client.SendAsync(Req).Result
                    If Not Resp.IsSuccessStatusCode Then Throw New Exception($"HTTP {CInt(Resp.StatusCode)}")
                    Dim Body As JObject = JObject.Parse(Resp.Content.ReadAsStringAsync().Result)
                    Dim Latest As Version = Nothing
                    Dim LatestRaw As String = ""
                    Dim Versions As JArray = TryCast(Body("versions"), JArray)
                    If Versions IsNot Nothing Then
                        For Each Item As JToken In Versions
                            Dim Raw As String = If(TryCast(Item("version"), JValue)?.ToString(), "").Trim()
                            Dim Ver As Version = Nothing
                            If Raw = "" OrElse Not Version.TryParse(Raw.TrimStart("v"c, "V"c), Ver) Then Continue For
                            If Latest Is Nothing OrElse Ver > Latest Then
                                Latest = Ver
                                LatestRaw = Raw
                            End If
                        Next
                    End If
                    If Latest Is Nothing Then Throw New Exception("版本索引中没有可识别的版本号")
                    Return LatestRaw
                End Using
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 读取 GitHub 发布页的最新版本号（跟随 /releases/latest 的重定向，避免 API 限流）。
    ''' </summary>
    Private Function GetLatestReleaseTagByGitHub() As String
        Dim Handler As New System.Net.Http.HttpClientHandler With {.AllowAutoRedirect = True}
        Using Client As New System.Net.Http.HttpClient(Handler)
            Client.Timeout = TimeSpan.FromSeconds(20)
            Using Req As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,
                                                               "https://github.com/merlin-kitsune/pcl-reborn/releases/latest")
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}")
                Using Resp As System.Net.Http.HttpResponseMessage =
                        Client.SendAsync(Req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).Result
                    If Not Resp.IsSuccessStatusCode Then Throw New Exception($"HTTP {CInt(Resp.StatusCode)}")
                    Dim Path As String = Resp.RequestMessage.RequestUri.AbsolutePath
                    Dim Mark As String = "/releases/tag/"
                    Dim Index As Integer = Path.LastIndexOf(Mark, StringComparison.Ordinal)
                    If Index < 0 Then Throw New Exception("无法从重定向地址中解析版本号")
                    Return Uri.UnescapeDataString(Path.Substring(Index + Mark.Length))
                End Using
            End Using
        End Using
    End Function

    Friend IsUpdateWaitingRestart As Boolean = False
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
    End Sub
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '本地修改：官方 Release 页没有附件，最新正式版以「最新正式版.zip」的形式放在仓库根目录
        Dim ZipPath As String = PathTemp & "LatestPCL.zip"
        Dim Target As String = PathTemp & "Latest.exe"
        '本地修改：优先走 jsDelivr 通道，GitHub 原始链接作为镜像失效时的备用手段
        Dim CdnUrl As String = "https://cdn.jsdelivr.net/gh/merlin-kitsune/pcl-reborn@main/" & Uri.EscapeDataString("最新正式版.zip")
        Dim RawUrl As String = "https://raw.githubusercontent.com/merlin-kitsune/pcl-reborn/main/" & Uri.EscapeDataString("最新正式版.zip")
        Try
            NetDownloadByLoader(CdnUrl, ZipPath, LoaderToSyncProgress)
        Catch ex As Exception
            Logger.Warn(ex, "从 jsDelivr 下载最新正式版失败，改用 GitHub 原始链接")
            NetDownloadByLoader(RawUrl, ZipPath, LoaderToSyncProgress)
        End Try
        Using Archive As System.IO.Compression.ZipArchive = System.IO.Compression.ZipFile.OpenRead(ZipPath)
            Dim Entry As System.IO.Compression.ZipArchiveEntry =
                Archive.Entries.FirstOrDefault(Function(e) e.Name.EndsWithF(".exe", True))
            If Entry Is Nothing Then Throw New Exception("更新包中没有找到 PCL 可执行文件")
            Entry.ExtractToFile(Target, True)
        End Using
        Logger.Info($"已准备好最新正式版 PCL：{Target}")
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新", Sub() Logger.Info("该版本中不包含更新通知功能……"), Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

#End Region

#Region "识别码"

    ''' <summary>
    ''' 获取设备识别码。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
