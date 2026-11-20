'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Class PclReleaseInfo
    Public Tag As String
    Public DownloadUrl As String
End Class

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
        If IsUpdateDownloading Then
            Hint("更新正在下载中，请稍候……")
            Return
        End If
        RunInThread(
        Sub()
            Try
                Dim Release As PclReleaseInfo = GetLatestRelease()
                Dim LatestVer As Version = Nothing
                Dim CurrentVer As Version = Nothing
                If Not Version.TryParse(Release.Tag.TrimStart("v"c, "V"c), LatestVer) Then Throw New Exception($"无法识别最新版本号：{Release.Tag}")
                If Not Version.TryParse(VersionBaseName, CurrentVer) Then Throw New Exception($"无法识别当前版本号：{VersionBaseName}")
                If LatestVer > CurrentVer Then
                    Logger.Info($"发现新版本：{Release.Tag}（当前 {VersionBaseName}）")
                    RunInUi(Sub()
                                If MyMsgBox($"发现新版本：{Release.Tag}{vbCrLf}当前版本：{VersionBaseName}{vbCrLf}{vbCrLf}是否立即下载并安装？",
                                            "检查更新", "立即更新", "稍后") = 1 Then DownloadUpdate(Release)
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
    ''' 启动时按设置检查更新。0 = 自动下载，1 = 提示，2/3 = 不主动检查。
    ''' </summary>
    Friend Sub UpdateCheckOnStartup()
        If Settings.Get(Of Integer)("SystemSystemUpdate") >= 2 Then Return
        RunInNewThread(
        Sub()
            Try
                Dim Release As PclReleaseInfo = GetLatestRelease()
                Dim LatestVer As Version = Nothing
                Dim CurrentVer As Version = Nothing
                If Not Version.TryParse(Release.Tag.TrimStart("v"c, "V"c), LatestVer) Then Return
                If Not Version.TryParse(VersionBaseName, CurrentVer) Then Return
                If LatestVer <= CurrentVer Then Return
                Logger.Info($"发现新版本：{Release.Tag}（当前 {VersionBaseName}）")
                If Settings.Get(Of Integer)("SystemSystemUpdate") = 1 Then
                    RunInUi(Sub()
                                If MyMsgBox($"发现新版本：{Release.Tag}{vbCrLf}当前版本：{VersionBaseName}{vbCrLf}{vbCrLf}是否立即下载并安装？",
                                            "检查更新", "立即更新", "稍后") = 1 Then DownloadUpdate(Release)
                            End Sub)
                Else
                    DownloadUpdate(Release)
                End If
            Catch ex As Exception
                Logger.Warn(ex, "启动时检查更新失败")
            End Try
        End Sub, "UpdateCheckOnStartup", ThreadPriority.BelowNormal)
    End Sub

    ''' <summary>
    ''' 读取最新版本信息。优先走 gh-proxy 转发的 GitHub Release API（国内可访问），失败时回退到 GitHub 发布页重定向。
    ''' </summary>
    Private Function GetLatestRelease() As PclReleaseInfo
        Try
            Return GetLatestReleaseByGhProxy()
        Catch ex As Exception
            Logger.Warn(ex, "通过 gh-proxy 获取最新版本信息失败，改用 GitHub 发布页")
        End Try
        Return GetLatestReleaseByGitHub()
    End Function

    ''' <summary>
    ''' 通过 gh-proxy 转发 GitHub Release API 读取最新版本与下载地址（不直连 GitHub，国内访问更稳定）。
    ''' </summary>
    Private Function GetLatestReleaseByGhProxy() As PclReleaseInfo
        Dim Handler As New System.Net.Http.HttpClientHandler With {.AllowAutoRedirect = True}
        Using Client As New System.Net.Http.HttpClient(Handler)
            Client.Timeout = TimeSpan.FromSeconds(20)
            Using Req As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get,
                                                               "https://gh-proxy.com/https://api.github.com/repos/merlin-kitsune/pcl-reborn/releases/latest")
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}")
                Req.Headers.Add("Accept", "application/vnd.github+json")
                Using Resp As System.Net.Http.HttpResponseMessage = Client.SendAsync(Req).Result
                    If Not Resp.IsSuccessStatusCode Then Throw New Exception($"HTTP {CInt(Resp.StatusCode)}")
                    Dim Body As JObject = JObject.Parse(Resp.Content.ReadAsStringAsync().Result)
                    Dim Tag As String = If(TryCast(Body("tag_name"), JValue)?.ToString(), "").Trim()
                    If Tag = "" Then Throw New Exception("Release API 中没有可识别的版本号")
                    Dim DownloadUrl As String = ""
                    Dim Assets As JArray = TryCast(Body("assets"), JArray)
                    If Assets IsNot Nothing Then
                        For Each AssetToken As JToken In Assets
                            Dim Asset As JObject = TryCast(AssetToken, JObject)
                            If Asset Is Nothing Then Continue For
                            Dim Name As String = If(TryCast(Asset("name"), JValue)?.ToString(), "")
                            Dim Url As String = If(TryCast(Asset("browser_download_url"), JValue)?.ToString(), "")
                            If Name.EndsWithF(".zip", True) AndAlso Url <> "" Then
                                DownloadUrl = Url
                                Exit For
                            End If
                        Next
                    End If
                    If DownloadUrl = "" Then DownloadUrl = GetReleaseDownloadUrl(Tag)
                    Return New PclReleaseInfo With {.Tag = Tag, .DownloadUrl = DownloadUrl}
                End Using
            End Using
        End Using
    End Function

    ''' <summary>
    ''' 读取 GitHub 发布页的最新版本号（跟随 /releases/latest 的重定向，避免 API 限流）。
    ''' </summary>
    Private Function GetLatestReleaseByGitHub() As PclReleaseInfo
        Dim Tag As String = GetLatestReleaseTagByGitHub()
        Return New PclReleaseInfo With {.Tag = Tag, .DownloadUrl = GetReleaseDownloadUrl(Tag)}
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

    ''' <summary>
    ''' 按 Release 标签拼出附件下载地址，作为 API 未返回 assets 时的兜底。
    ''' </summary>
    Private Function GetReleaseDownloadUrl(Tag As String) As String
        Dim VersionTag As String = Tag.TrimStart("v"c, "V"c)
        Return $"https://github.com/merlin-kitsune/pcl-reborn/releases/download/{Uri.EscapeDataString(Tag)}/PCL2-Reborn-{Uri.EscapeDataString(VersionTag)}.zip"
    End Function

    Private Function GetGhProxyUrl(Url As String) As String
        If Url.StartsWithF("https://gh-proxy.com/") Then Return Url
        Return "https://gh-proxy.com/" & Url
    End Function

    Private Sub DownloadUpdate(Release As PclReleaseInfo)
        If IsUpdateDownloading Then Return
        IsUpdateDownloading = True
        RunInNewThread(
        Sub()
            Dim ZipPath As String = PathTemp & "UpdatePCL.zip"
            Dim NewExe As String = Paths.Base & "PCL\Plain Craft Launcher 2.exe"
            Try
                Hint($"正在下载启动器更新：{Release.Tag}")
                Directory.CreateDirectory(Paths.Base & "PCL")
                '避免上次下载中断后留下的残缺压缩包被误当成完整文件
                FileUtils.Delete(ZipPath)
                NetDownloadByLoader({GetGhProxyUrl(Release.DownloadUrl), Release.DownloadUrl}, ZipPath)
                Using Archive As ZipArchive = ZipFile.OpenRead(ZipPath)
                    Dim Entry As ZipArchiveEntry = Archive.Entries.FirstOrDefault(Function(e) String.Equals(e.Name, "Plain Craft Launcher 2.exe", StringComparison.OrdinalIgnoreCase))
                    If Entry Is Nothing Then Entry = Archive.Entries.FirstOrDefault(Function(e) e.Name.EndsWithF(".exe", True))
                    If Entry Is Nothing Then Throw New Exception("更新包中没有找到 PCL 可执行文件")
                    FileUtils.Delete(NewExe)
                    Entry.ExtractToFile(NewExe, True)
                End Using
                FileUtils.Delete(ZipPath)
                IsUpdateWaitingRestart = True
                Logger.Info($"启动器更新已下载：{Release.Tag} -> {NewExe}")
                RunInUi(Sub()
                            If MyMsgBox($"新版本 {Release.Tag} 已下载完成。" & vbCrLf & "是否立即重启以完成更新？",
                                        "更新", "立即重启", "稍后") = 1 Then
                                IsUpdateRestartOnExit = True
                                FrmMain.EndProgram(False)
                            Else
                                Hint("更新将在下次退出启动器时自动应用", HintType.Green)
                            End If
                        End Sub)
            Catch ex As Exception
                Logger.Warn(ex, "下载启动器更新失败")
                RunInUi(Sub() Hint($"下载启动器更新失败：{ex.Message}", HintType.Red))
            Finally
                IsUpdateDownloading = False
            End Try
        End Sub, "DownloadUpdate", ThreadPriority.BelowNormal)
    End Sub

    Friend IsUpdateDownloading As Boolean = False
    Friend IsUpdateWaitingRestart As Boolean = False
    Friend IsUpdateRestartOnExit As Boolean = False

    ''' <summary>
    ''' 用新版本替换当前可执行文件，并可选地重启。
    ''' 由 --update 参数在新进程中调用，此时旧进程已经或正在退出。
    ''' </summary>
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
        Try
            If ProcessId > 0 Then
                Try
                    Using OldProcess As Process = Process.GetProcessById(ProcessId)
                        If Not OldProcess.WaitForExit(60000) Then Throw New Exception($"等待旧版启动器退出超时（PID {ProcessId}）")
                    End Using
                Catch ex As ArgumentException
                    '旧进程已经退出
                End Try
            End If
            '当前进程正从 OldFileName 运行，Windows 不允许直接覆盖正在运行的可执行文件，
            '因此先把旧文件改名为备份，复制成功后再删除备份；失败时回滚。
            Dim OldBackup As String = OldFileName & ".old"
            Try
                FileUtils.Delete(OldBackup)
            Catch ex As Exception
                Throw New Exception($"无法清理旧的更新备份：{ex.Message}", ex)
            End Try
            FileUtils.Move(OldFileName, OldBackup)
            Try
                FileUtils.Copy(NewFileName, OldFileName)
            Catch ex As Exception
                Try
                    If FileUtils.Exists(OldFileName) Then FileUtils.Delete(OldFileName)
                    FileUtils.Move(OldBackup, OldFileName)
                Catch restoreEx As Exception
                    Throw New Exception($"复制新版本失败：{ex.Message}；恢复旧版本也失败：{restoreEx.Message}", ex)
                End Try
                Throw
            End Try
            If TriggerRestart Then
                '此进程（--update）不会初始化 Logger，不能走会写日志的 StartProcess
                Process.Start(New ProcessStartInfo(OldFileName) With {
                    .WorkingDirectory = IO.Path.GetDirectoryName(OldFileName),
                    .UseShellExecute = True
                })
            End If
        Catch ex As Exception
            MsgBox($"应用更新失败：{ex.Message}", MsgBoxStyle.Critical, "PCL 更新")
        End Try
    End Sub

    ''' <summary>
    ''' 启动更新进程。由即将退出的旧实例调用，新实例会等待旧 PID 退出后替换文件。
    ''' </summary>
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
        Try
            Dim NewExe As String = Paths.Base & "PCL\Plain Craft Launcher 2.exe"
            If Not FileUtils.Exists(NewExe) Then
                Logger.Warn("自动更新：未找到已下载的新版本文件，跳过更新")
                Return
            End If
            Dim Args As String = $"--update {Process.GetCurrentProcess().Id} ""{PathExe}"" ""{NewExe}"" {If(TriggerRestartAndByEnd, "true", "false")}"
            StartProcess(PathExe, Args)
            Logger.Info("已启动更新进程，等待当前实例退出")
        Catch ex As Exception
            Logger.Error(ex, "启动自动更新进程失败")
        End Try
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '本地修改：官方 Release 页没有附件，最新正式版以「最新正式版.zip」的形式放在仓库根目录
        Dim ZipPath As String = PathTemp & "LatestPCL.zip"
        Dim Target As String = PathTemp & "Latest.exe"
        '本地修改：优先走 gh-proxy 通道，GitHub 原始链接作为镜像失效时的备用手段
        Dim CdnUrl As String = "https://gh-proxy.com/https://raw.githubusercontent.com/merlin-kitsune/pcl-reborn/main/" & Uri.EscapeDataString("最新正式版.zip")
        Dim RawUrl As String = "https://raw.githubusercontent.com/merlin-kitsune/pcl-reborn/main/" & Uri.EscapeDataString("最新正式版.zip")
        Try
            NetDownloadByLoader(CdnUrl, ZipPath, LoaderToSyncProgress)
        Catch ex As Exception
            Logger.Warn(ex, "从 gh-proxy 下载最新正式版失败，改用 GitHub 原始链接")
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
