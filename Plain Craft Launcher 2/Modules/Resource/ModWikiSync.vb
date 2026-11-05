'本地修改：内置的 MC 百科数据库（PCLCS/Resource/WikiEntries.txt）随版本固定，无法覆盖新模组。
'因此启动时在后台线程从本项目的数据分支同步一份最新的模组中文名列表，
'缓存到 PCL 目录下的 WikiModules.tsv，由 PCLCS.WikiEntry 在解析内置数据库时并入（不覆盖已有条目）。
'数据优先走 jsDelivr 通道，GitHub 原始链接作为镜像失效时的备用手段。

''' <summary>
''' 从 GitHub 项目仓库同步最新的模组中文名列表。
''' </summary>
Public Module ModWikiSync

    ''' <summary> 数据地址：jsDelivr 通道（由 GitHub Actions 定时抓取并发布到 wiki-data 分支）。 </summary>
    Private Const CdnUrl As String =
        "https://cdn.jsdelivr.net/gh/merlin-kitsune/pcl-reborn@wiki-data/wiki/modules.tsv"
    ''' <summary> 数据地址：GitHub 原始链接，作为备用镜像。 </summary>
    Private Const RawUrl As String =
        "https://raw.githubusercontent.com/merlin-kitsune/pcl-reborn/wiki-data/wiki/modules.tsv"

    ''' <summary> 本地缓存的模组中文名列表，与 Setup.ini 位于同一目录。 </summary>
    Public ReadOnly Property LocalPath As String = Paths.Base & "PCL\WikiModules.tsv"
    ''' <summary> 记录上次同步的 ETag 的文件，用于避免重复下载。 </summary>
    Private ReadOnly Property MetaPath As String = Paths.Base & "PCL\WikiModules.meta"

    ''' <summary>
    ''' 设置补充数据的路径，并在后台线程中同步最新数据。
    ''' <para/>必须在任何 WikiEntry.All 的访问之前调用。
    ''' </summary>
    Friend Sub Init()
        Try
            PCLCS.WikiEntry.SupplementPath = LocalPath
        Catch ex As Exception
            Logger.Warn(ex, "设置模组中文名补充数据路径失败")
        End Try
        Dim SyncThread As New Thread(AddressOf Sync) With {
            .IsBackground = True,
            .Name = "WikiSync"
        }
        SyncThread.Start()
    End Sub

    ''' <summary> 同步一次列表。失败时只记录日志，不打扰用户。 </summary>
    Private Sub Sync()
        Try
            Thread.Sleep(RandomInteger(2000, 4000)) '避开启动时的其它联网请求
            Dim ETag As String = If(File.Exists(MetaPath), File.ReadAllText(MetaPath).Trim(), "")
            'GitHub 原始链接是最新的（分支更新后立刻可见）；jsDelivr 有十几小时缓存，仅作镜像回退
            If TrySyncFrom(RawUrl, ETag) Then Return
            TrySyncFrom(CdnUrl, "") 'GitHub 原始链接不可用时回退到 jsDelivr 镜像
        Catch ex As Exception
            Logger.Warn(ex, "同步模组中文名列表失败")
        End Try
    End Sub

    ''' <summary>
    ''' 从指定地址同步一次列表。成功（含 304 未修改）返回 True，失败返回 False。
    ''' </summary>
    Private Function TrySyncFrom(Url As String, ETag As String) As Boolean
        Try
            Dim Handler As New System.Net.Http.HttpClientHandler With {.AllowAutoRedirect = True}
            Using Client As New System.Net.Http.HttpClient(Handler)
                Client.Timeout = TimeSpan.FromSeconds(30)
                Using Req As New System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, Url)
                    Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}")
                    If ETag <> "" Then Req.Headers.TryAddWithoutValidation("If-None-Match", ETag)
                    Using Resp As System.Net.Http.HttpResponseMessage =
                            Client.SendAsync(Req, System.Net.Http.HttpCompletionOption.ResponseContentRead).Result
                        If Resp.StatusCode = System.Net.HttpStatusCode.NotModified Then
                            Logger.Info("模组中文名列表已是最新，跳过同步")
                            Return True
                        End If
                        If Not Resp.IsSuccessStatusCode Then Throw New Exception($"HTTP {CInt(Resp.StatusCode)}")

                        Dim Text As String = Resp.Content.ReadAsStringAsync().Result
                        Dim LineCount As Integer = 0
                        For Each RawLine As String In Text.Split({vbLf}, StringSplitOptions.None)
                            Dim Line As String = RawLine.Trim()
                            If Line <> "" AndAlso Not Line.StartsWithF("#") AndAlso Line.Contains(vbTab) Then
                                LineCount += 1
                            End If
                        Next
                        If Text.Length < 1024 OrElse LineCount < 20 Then
                            Throw New Exception($"同步到的数据不完整（{LineCount} 条）")
                        End If

                        Dim TempPath As String = LocalPath & ".tmp"
                        File.WriteAllText(TempPath, Text, New Text.UTF8Encoding(False))
                        File.Copy(TempPath, LocalPath, True)
                        File.Delete(TempPath)
                        Dim NewETag As String = If(Resp.Headers.ETag Is Nothing, "", Resp.Headers.ETag.Tag)
                        File.WriteAllText(MetaPath, NewETag, New Text.UTF8Encoding(False))
                        Logger.Info($"已同步模组中文名列表：{LineCount} 条（下次启动生效）")
                        Return True
                    End Using
                End Using
            End Using
        Catch ex As Exception
            Logger.Warn(ex, $"同步模组中文名列表失败（{Url}）")
            Return False
        End Try
    End Function

End Module
