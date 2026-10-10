Imports System
Imports System.Runtime.InteropServices

Public Class formReelType

    <DllImport("gdi32.dll")>
    Private Shared Function CreateDC(ByVal lpszDriver As String, ByVal lpszDevice As String, ByVal lpszOutput As String, ByVal lpInitData As IntPtr) As IntPtr
    End Function
    <DllImport("gdi32.dll")> _
    Private Shared Function DeleteDC(ByVal hdc As IntPtr) As Boolean
    End Function
    <DllImport("gdi32.dll")> _
    Private Shared Function GetPixel(ByVal hdc As IntPtr, ByVal nXPos As Integer, ByVal nYPos As Integer) As Integer
    End Function
    <DllImport("user32.dll")> _
    Private Shared Function GetAsyncKeyState(ByVal vKey As Int32) As Short
    End Function

    Private IsDirty As Boolean = False
    Private currentKeycode As Keys = Keys.D0
    Private syncingDream7Text As Boolean = False

    Public Shadows Function ShowDialog(ByVal owner As IWin32Window,
                                       ByRef reeltypes As String,
                                       ByRef reelcolor As Color,
                                       ByRef reelrollingdirection As eReelRollingDirection,
                                       ByRef reelrollinginterval As Integer,
                                       ByRef reelintermediatecount As Integer,
                                       ByRef usedream7leds As Boolean,
                                       ByRef d7glow As Single,
                                       ByRef d7thickness As Single,
                                       ByRef d7shear As Single) As DialogResult
        ' set a value to the LED controls
        formReelType_KeyDown(Me, New KeyEventArgs(Keys.D0))
        ' set current color for rendered LEDs
        If reelcolor = Nothing Then
            B2SColorBarLEDs.CurrentColor = Color.OrangeRed
        Else
            B2SColorBarLEDs.CurrentColor = reelcolor
        End If
        cmbRollingDirection.SelectedIndex = reelrollingdirection
        If reelrollinginterval < numRollingInterval.Minimum Then reelrollinginterval = numRollingInterval.Minimum
        numRollingInterval.Value = reelrollinginterval
        numIntermediateCount.Value = reelintermediatecount
        ' set some value for Dream7 LEDs
        chkUseDream7LEDs.Checked = usedream7leds
        If d7glow = 0 Then d7glow = 9
        If d7thickness = 0 Then d7thickness = 14
        If d7shear = 0 Then d7shear = 0.1
        TrackBarGlow.Value = CInt(d7glow)
        TrackBarThickness.Value = Math.Max(CInt(d7thickness / 2), 1)
        TrackBarShear.Value = CInt(d7shear * 50)
        ' show dialog
        ConfigureImageLibraryMenu()
        If lvEMReels.Items.Count = 0 Then FillReelListView(eImageSetType.ReelImages, ilReels, lvEMReels, DefaultEMReels)
        If lvEMCreditReels.Items.Count = 0 Then FillReelListView(eImageSetType.CreditReelImages, ilEMCeditReels, lvEMCreditReels, DefaultEMCreditReels)
        If lvLEDs.Items.Count = 0 Then FillReelListView(eImageSetType.LEDImages, ilLEDs, lvLEDs, DefaultLEDs)
        If Not String.IsNullOrEmpty(reeltypes) Then
            Dim i As Integer = 0
            For Each item As Object In lvEMReels.Items
                lvEMReels.Items(i).Selected = (reeltypes.Contains("," & item.Name & ","))
                i += 1
            Next
            i = 0
            For Each item As Object In lvEMCreditReels.Items
                lvEMCreditReels.Items(i).Selected = (reeltypes.Contains("," & item.Name & ","))
                i += 1
            Next
            i = 0
            For Each item As Object In lvLEDs.Items
                lvLEDs.Items(i).Selected = (reeltypes.Contains("," & item.Name & ","))
                i += 1
            Next
            For Each item As Object In GetAllRenderedLEDs()
                item.Selected = (reeltypes.Contains("," & GetRenderedLEDName(item.Name) & ","))
            Next
        End If
        IsDirty = False
        ' now show the dialog
        Dim nRet As DialogResult = MyBase.ShowDialog(owner)
        If nRet = Windows.Forms.DialogResult.OK Then
            reeltypes = String.Empty
            reelrollingdirection = cmbRollingDirection.SelectedIndex
            reelrollinginterval = numRollingInterval.Value
            reelintermediatecount = numIntermediateCount.Value
            usedream7leds = chkUseDream7LEDs.Checked
            d7glow = TrackBarGlow.Value
            d7thickness = TrackBarThickness.Value * 2
            d7shear = TrackBarShear.Value / 50
            ' return some values
            reelcolor = Nothing
            reelcolor = B2SColorBarLEDs.CurrentColor
            For Each item As Object In lvEMReels.SelectedItems
                reeltypes &= "," & item.Name
            Next
            For Each item As Object In lvEMCreditReels.SelectedItems
                reeltypes &= "," & item.Name
            Next
            For Each item As Object In lvLEDs.SelectedItems
                reeltypes &= "," & item.Name
            Next
            For Each item As Control In GetSelectedRenderedLEDs()
                If Not reeltypes.Contains(GetRenderedLEDName(item.Name)) Then reeltypes &= "," & GetRenderedLEDName(item.Name)
            Next
            If Not String.IsNullOrEmpty(reeltypes) Then
                reeltypes &= ","
            End If
            For Each score As ReelAndLED.ScoreInfo In Backglass.currentData.Scores
                If Not reeltypes.Contains("," & score.ReelType & ",") Then
                    reeltypes &= "," & score.ReelType & ","
                End If
                If String.IsNullOrEmpty(score.ReelType) Then
                    score.ReelType = GetFirstSelectedReelType(reeltypes)
                End If
            Next
        End If
        Return nRet
    End Function

    Private Sub formReelType_Load(sender As Object, e As System.EventArgs) Handles Me.Load
        PanelRenderedLEDs.Location = PanelDream7LEDs.Location
    End Sub
    Private Sub formReelType_KeyDown(sender As Object, e As System.Windows.Forms.KeyEventArgs) Handles Me.KeyDown
        If e.KeyCode >= Keys.D0 AndAlso e.KeyCode <= Keys.D9 Then
            currentKeycode = e.KeyCode
            RenderedLED08.LED.Draw(e.KeyCode)
            RenderedLED10.LED.Draw(e.KeyCode)
            RenderedLED14.LED.Draw(e.KeyCode)
            Dream7LED08.LED.SetValue(0, Chr(e.KeyCode))
            Dream7LED10.LED.SetValue(0, Chr(e.KeyCode))
            Dream7LED14.LED.SetValue(0, Chr(e.KeyCode))
        End If
    End Sub

    Private Sub Ok_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnOk.Click
        MyBase.DialogResult = Windows.Forms.DialogResult.OK
        Me.Close()
    End Sub
    Private Sub Cancel_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles btnCancel.Click
        If IsDirty Then
            Dim ret As DialogResult = B2SMessageBox.Show(My.Resources.MSG_IsDirty, AppTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question)
            If ret = Windows.Forms.DialogResult.Yes Then
                btnOk.PerformClick()
            ElseIf ret = Windows.Forms.DialogResult.No Then
                Me.Close()
            End If
        Else
            Me.Close()
        End If
    End Sub

    Private Sub UseDream7LEDs_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkUseDream7LEDs.CheckedChanged
        PanelDream7LEDs.Visible = chkUseDream7LEDs.Checked
        PanelRenderedLEDs.Visible = Not chkUseDream7LEDs.Checked
    End Sub
    Private Sub Dream7LED_Click(sender As Object, e As System.EventArgs) Handles Dream7LED08.Click, Dream7LED10.Click, Dream7LED14.Click
        DirectCast(PanelRenderedLEDs.Controls("RenderedLED" & sender.Name.ToString().Substring(sender.Name.ToString().Length - 2)), B2SRenderedLEDPanel).Selected = sender.Selected
    End Sub
    Private Sub RenderedLED_Click(sender As Object, e As System.EventArgs) Handles RenderedLED08.Click, RenderedLED10.Click, RenderedLED14.Click
        DirectCast(PanelDream7LEDs.Controls("Dream7LED" & sender.Name.ToString().Substring(sender.Name.ToString().Length - 2)), B2SDream7LEDPanel).Selected = sender.Selected
    End Sub

    Private Sub B2SColorBarLEDs_ColorChanged(sender As Object, e As System.EventArgs) Handles B2SColorBarLEDs.ColorChanged
        RenderedLED08.LED.ForeColor = B2SColorBarLEDs.CurrentColor
        RenderedLED10.LED.ForeColor = B2SColorBarLEDs.CurrentColor
        RenderedLED14.LED.ForeColor = B2SColorBarLEDs.CurrentColor
        ' set dream7 colors
        Dream7LED08.LEDColor = B2SColorBarLEDs.CurrentColor
        Dream7LED10.LEDColor = B2SColorBarLEDs.CurrentColor
        Dream7LED14.LEDColor = B2SColorBarLEDs.CurrentColor
    End Sub

    Private Sub TrackBarGlow_ValueChanged(sender As Object, e As System.EventArgs) Handles TrackBarGlow.ValueChanged
        Dream7LED08.LED.Glow = TrackBarGlow.Value
        Dream7LED10.LED.Glow = TrackBarGlow.Value
        Dream7LED14.LED.Glow = TrackBarGlow.Value
        SetDream7Text(txtGlow, TrackBarGlow.Value)
    End Sub
    Private Sub TrackBarThickness_ValueChanged(sender As Object, e As System.EventArgs) Handles TrackBarThickness.ValueChanged
        Dream7LED08.LED.Thickness = TrackBarThickness.Value * 2
        Dream7LED10.LED.Thickness = TrackBarThickness.Value * 2
        Dream7LED14.LED.Thickness = TrackBarThickness.Value * 2
        SetDream7Text(txtSize, TrackBarThickness.Value)
        ' refresh current key
        formReelType_KeyDown(sender, New KeyEventArgs(currentKeycode))
    End Sub
    Private Sub TrackBarShear_ValueChanged(sender As Object, e As System.EventArgs) Handles TrackBarShear.ValueChanged
        Dream7LED08.LED.Shear = TrackBarShear.Value / 50
        Dream7LED10.LED.Shear = TrackBarShear.Value / 50
        Dream7LED14.LED.Shear = TrackBarShear.Value / 50
        SetDream7Text(txtShear, TrackBarShear.Value)
    End Sub

    Private Sub SetDream7Text(ByVal editor As TextBox, ByVal value As Integer)
        syncingDream7Text = True
        Try
            editor.Text = value.ToString()
        Finally
            syncingDream7Text = False
        End Try
    End Sub

    Private Sub Dream7TextChanged(ByVal sender As Object, ByVal e As EventArgs) Handles txtGlow.TextChanged, txtSize.TextChanged, txtShear.TextChanged
        If syncingDream7Text Then Return
        Dim editor As TextBox = DirectCast(sender, TextBox)
        Dim slider As TrackBar = If(Object.ReferenceEquals(editor, txtGlow), TrackBarGlow,
                                    If(Object.ReferenceEquals(editor, txtSize), TrackBarThickness, TrackBarShear))
        Dim value As Integer
        If Integer.TryParse(editor.Text, value) AndAlso value >= slider.Minimum AndAlso value <= slider.Maximum Then
            slider.Value = value
        End If
    End Sub

    Private Sub Dream7TextValidated(ByVal sender As Object, ByVal e As EventArgs) Handles txtGlow.Validated, txtSize.Validated, txtShear.Validated
        Dim editor As TextBox = DirectCast(sender, TextBox)
        Dim slider As TrackBar = If(Object.ReferenceEquals(editor, txtGlow), TrackBarGlow,
                                    If(Object.ReferenceEquals(editor, txtSize), TrackBarThickness, TrackBarShear))
        Dim value As Integer
        If Not Integer.TryParse(editor.Text, value) Then value = slider.Value
        value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, value))
        slider.Value = value
        SetDream7Text(editor, value)
    End Sub

    Private Sub Reels_MouseDoubleClick(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles lvEMReels.MouseDoubleClick
        If e.Button = Windows.Forms.MouseButtons.Left Then
            MyBase.DialogResult = Windows.Forms.DialogResult.OK
            Me.Close()
        End If
    End Sub

    Private Sub GetColor_Click(sender As System.Object, e As System.EventArgs) Handles btnGetColor.Click
        If TimerGetColor.Enabled Then
            TimerGetColor.Stop()
            Me.CancelButton = btnCancel
        Else
            Me.CancelButton = Nothing
            B2SMessageBox.Show(My.Resources.MSG_GetColor, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
            TimerGetColor.Start()
        End If
    End Sub
    Private Sub TimerGetColor_Tick(sender As Object, e As System.EventArgs) Handles TimerGetColor.Tick
        Dim keyState As Short = GetAsyncKeyState(Keys.Escape)
        If keyState <> 0 Then
            TimerGetColor.Stop()
            Me.CancelButton = btnCancel
        Else
            B2SColorBarLEDs.CurrentColor = GetPixelColor(Cursor.Position.X, Cursor.Position.Y)
        End If
    End Sub
    Private Function GetPixelColor(ByVal x As Integer, ByVal y As Integer) As Color
        Dim hdcScreen As IntPtr = CreateDC("Display", Nothing, Nothing, IntPtr.Zero)
        Dim colorRef As Integer = GetPixel(hdcScreen, x, y)
        DeleteDC(hdcScreen)
        Return Color.FromArgb(colorRef And &HFF, (colorRef And &HFF00) >> 8, (colorRef And &HFF0000) >> 16)
    End Function

    Private Sub Reels_Click(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles lvEMReels.MouseClick
        If lvEMReels.SelectedItems.Count > 0 Then
            If e.Button = Windows.Forms.MouseButtons.Right Then
                Dim cms = New ContextMenuStrip
                Dim itemExport = cms.Items.Add("Export")
                itemExport.Tag = 1
                AddHandler itemExport.Click, AddressOf btnExportReels_Click

                Dim itemDelete = cms.Items.Add("Delete image set…")
                AddHandler itemDelete.Click, AddressOf DeleteReel_Click
                Dim rename = cms.Items.Add("Rename image set…")
                AddHandler rename.Click, Sub() RenameSet(lvEMReels)

                cms.Show(lvEMReels, e.Location)
            End If
        End If
    End Sub

    Private Sub CreditReels_Click(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles lvEMCreditReels.MouseClick
        If lvEMCreditReels.SelectedItems.Count > 0 Then
            If e.Button = Windows.Forms.MouseButtons.Right Then
                Dim cms = New ContextMenuStrip
                Dim itemExport = cms.Items.Add("Export")
                itemExport.Tag = 1
                AddHandler itemExport.Click, AddressOf btnExportCreditReels_Click

                Dim itemDelete = cms.Items.Add("Delete image set…")
                AddHandler itemDelete.Click, AddressOf DeleteCreditReel_Click
                Dim rename = cms.Items.Add("Rename image set…")
                AddHandler rename.Click, Sub() RenameSet(lvEMCreditReels)

                cms.Show(lvEMCreditReels, e.Location)
            End If
        End If
    End Sub

    Private Sub LEDs_Click(sender As Object, e As System.Windows.Forms.MouseEventArgs) Handles lvLEDs.MouseClick
        If lvLEDs.SelectedItems.Count > 0 Then
            If e.Button = Windows.Forms.MouseButtons.Right Then
                Dim cms = New ContextMenuStrip
                Dim itemExport = cms.Items.Add("Export")
                itemExport.Tag = 1
                AddHandler itemExport.Click, AddressOf btnExportLEDs_Click

                Dim itemDelete = cms.Items.Add("Delete image set…")
                AddHandler itemDelete.Click, AddressOf DeleteLED_Click
                Dim rename = cms.Items.Add("Rename image set…")
                AddHandler rename.Click, Sub() RenameSet(lvLEDs)

                cms.Show(lvLEDs, e.Location)
            End If
        End If
    End Sub

    Private Sub DeleteReel_Click(sender As Object, e As EventArgs)
        DeleteSet(lvEMReels, eImageSetType.ReelImages, DefaultEMReels)
    End Sub
    Private Sub DeleteCreditReel_Click(sender As Object, e As EventArgs)
        DeleteSet(lvEMCreditReels, eImageSetType.CreditReelImages, DefaultEMCreditReels)
    End Sub
    Private Sub DeleteLED_Click(sender As Object, e As EventArgs)
        DeleteSet(lvLEDs, eImageSetType.LEDImages, DefaultLEDs)
    End Sub

    Private Function CollectionFor(type As eImageSetType) As GeneralData.Data.ImageSetCollection
        Select Case type
            Case eImageSetType.ReelImages : Return GeneralData.currentData.ImportedReelImageSets
            Case eImageSetType.CreditReelImages : Return GeneralData.currentData.ImportedCreditReelImageSets
            Case Else : Return GeneralData.currentData.ImportedLEDImageSets
        End Select
    End Function

    Private Sub RefreshSetList(type As eImageSetType)
        Select Case type
            Case eImageSetType.ReelImages : FillReelListView(type, ilReels, lvEMReels, DefaultEMReels, True)
            Case eImageSetType.CreditReelImages : FillReelListView(type, ilEMCeditReels, lvEMCreditReels, DefaultEMCreditReels, True)
            Case Else : FillReelListView(type, ilLEDs, lvLEDs, DefaultLEDs, True)
        End Select
    End Sub

    Private Sub ConfigureImageLibraryMenu()
        btnImportReels.Text = "Import EM Reels…"
        btnImportLEDs.Text = "Import LEDs…"
        btnImportCreditReels.Text = "Import Credit Reels…"
        lblInfo.Text = "Choose a reel or LED set, or recognize digits 0–9 from one PDF/image sheet. Name and preview the extracted digits before saving. Right-click a set to rename or delete it; built-in sets can be restored from Import / Restore."
    End Sub

    Private Function ImportedKey(name As String) As Integer
        Dim match = System.Text.RegularExpressions.Regex.Match(name, "^Imported(?:EMR_T|EMR_CT|LED_T)(\d+)_0+$")
        If Not match.Success Then Throw New ArgumentException("Invalid imported image-set identifier.")
        Return Integer.Parse(match.Groups(1).Value)
    End Function

    Private Sub DeleteSet(view As ListView, type As eImageSetType, defaults As String())
        If view.SelectedItems.Count <> 1 Then Return
        Dim item = view.SelectedItems(0)
        If Backglass.currentData IsNot Nothing AndAlso
            (Backglass.currentData.Scores.Any(Function(score) score.ReelType = item.Name) OrElse
             Backglass.currentData.DMDScores.Any(Function(score) score.ReelType = item.Name) OrElse
             ("," & Backglass.currentData.ReelType.Trim(","c) & ",").Contains("," & item.Name & ",")) Then
            B2SMessageBox.Show("This set is used by the current backglass. Select another set for it before deleting.", AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim builtin = Not item.Name.StartsWith(ImportedStartString, StringComparison.Ordinal)
        Dim explanation = If(builtin, "Remove this built-in set from the chooser? Its original artwork remains available to existing backglasses. Use Import / Restore to restore it.", "Delete this imported set from the library? A backup is saved first. Other saved backglasses are not rewritten.")
        If B2SMessageBox.Show(item.Text & vbCrLf & vbCrLf & explanation, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) <> DialogResult.Yes Then Return
        Try
            If builtin Then
                DeleteBuiltInSet(item.Name)
            Else
                Dim collection = CollectionFor(type)
                Dim index = collection.IndexOfKey(ImportedKey(item.Name))
                If index < 0 Then Throw New InvalidOperationException("The image set no longer exists.")
                BackupLibraryFile(ImportFileName)
                GeneralData.currentData.RemoveImageSet(index, type)
                ForgetImageSetName(item.Name)
            End If
            RefreshSetList(type)
            IsDirty = True
        Catch ex As Exception
            B2SMessageBox.Show(ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub RenameSet(view As ListView)
        If view.SelectedItems.Count <> 1 Then Return
        Dim item = view.SelectedItems(0)
        Using dialog As New Form With {.Text = "Name image set", .ClientSize = New Size(360, 100), .StartPosition = FormStartPosition.CenterParent, .FormBorderStyle = FormBorderStyle.FixedDialog, .MaximizeBox = False, .MinimizeBox = False}
            Dim editor As New TextBox With {.Text = item.Text, .MaxLength = 80, .Location = New Point(12, 12), .Width = 336}
            Dim ok As New Button With {.Text = "Save name", .Location = New Point(180, 56), .DialogResult = DialogResult.OK}
            Dim cancel As New Button With {.Text = "Cancel", .Location = New Point(272, 56), .DialogResult = DialogResult.Cancel}
            dialog.Controls.AddRange(New Control() {editor, ok, cancel})
            dialog.AcceptButton = ok
            dialog.CancelButton = cancel
            If dialog.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                NameImageSet(item.Name, editor.Text)
                item.Text = editor.Text.Trim()
            Catch ex As Exception
                B2SMessageBox.Show(ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub ShowImportMenu(type As eImageSetType, button As Button)
        Dim menu As New ContextMenuStrip
        Dim category = If(type = eImageSetType.ReelImages, "EM reel", If(type = eImageSetType.CreditReelImages, "EM credit reel", "LED"))
        AddHandler menu.Items.Add("Import " & category & " digit sheet (PDF or image)…").Click, Sub() ImportSheet(type)
        AddHandler menu.Items.Add("Import individual images…").Click, Sub()
                                                                        BackupLibraryFile(ImportFileName)
                                                                        If type = eImageSetType.CreditReelImages Then
                                                                            AddCreditReel()
                                                                        Else
                                                                            AddReelOrLEDSet(type)
                                                                        End If
                                                                    End Sub
        AddHandler menu.Items.Add("Restore built-in image sets").Click, Sub()
                                                                          Dim names = If(type = eImageSetType.ReelImages, DefaultEMReels, If(type = eImageSetType.CreditReelImages, DefaultEMCreditReels, DefaultLEDs))
                                                                          RestoreBuiltInSets(names)
                                                                          RefreshSetList(type)
                                                                      End Sub
        menu.Show(button, New Point(0, button.Height))
    End Sub

    Private Sub ImportSheet(type As eImageSetType)
        Using preview As New formDigitSheetImport
            preview.Text = If(type = eImageSetType.ReelImages, "Import EM Reel Digit Sheet", If(type = eImageSetType.CreditReelImages, "Import EM Credit Reel Digit Sheet", "Import LED Digit Sheet"))
            If type = eImageSetType.CreditReelImages Then preview.ConfigureCreditRange()
            If preview.ShowDialog(Me) <> DialogResult.OK Then Return
            Dim images = preview.TakeImages()
            Try
                BackupLibraryFile(ImportFileName)
                GeneralData.currentData.AddImageSet(images, type)
                Dim key = CollectionFor(type).Keys.Last()
                Dim name = ImportedStartString & If(type = eImageSetType.ReelImages, "EMR_T" & key & "_0", If(type = eImageSetType.CreditReelImages, "EMR_CT" & key & "_00", "LED_T" & key & "_0"))
                NameImageSet(name, preview.SetName)
                MarkProportionalImageSet(name)
                RefreshSetList(type)
                IsDirty = True
            Catch ex As Exception
                B2SMessageBox.Show(ex.Message, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Function ExportIndex(view As ListView, type As eImageSetType, defaults As String()) As Integer
        Dim name = view.SelectedItems(0).Name
        If name.StartsWith(ImportedStartString, StringComparison.Ordinal) Then Return defaults.Length + CollectionFor(type).IndexOfKey(ImportedKey(name))
        Return Array.IndexOf(defaults, name)
    End Function

    Private Sub ImportReels_Click(sender As System.Object, e As System.EventArgs) Handles btnImportReels.Click
        ShowImportMenu(eImageSetType.ReelImages, btnImportReels)
    End Sub
    Private Sub ImportCreditReels_Click(sender As System.Object, e As System.EventArgs) Handles btnImportCreditReels.Click
        ShowImportMenu(eImageSetType.CreditReelImages, btnImportCreditReels)
    End Sub
    Private Sub ImportLEDs_Click(sender As System.Object, e As System.EventArgs) Handles btnImportLEDs.Click
        ShowImportMenu(eImageSetType.LEDImages, btnImportLEDs)
    End Sub

    Private Function GetAllRenderedLEDs() As Generic.List(Of Control)
        Dim ret As Generic.List(Of Control) = New Generic.List(Of Control)
        For Each panel As Panel In GroupBoxRendered.Controls.OfType(Of Panel)()
            For Each cntrl As B2SRenderedLEDPanel In panel.Controls.OfType(Of B2SRenderedLEDPanel)()
                ret.Add(cntrl)
            Next
            For Each cntrl As B2SDream7LEDPanel In panel.Controls.OfType(Of B2SDream7LEDPanel)()
                ret.Add(cntrl)
            Next
        Next
        Return ret
    End Function
    Private Function GetSelectedRenderedLEDs() As Generic.List(Of Control)
        Dim ret As Generic.List(Of Control) = New Generic.List(Of Control)
        For Each panel As Panel In GroupBoxRendered.Controls.OfType(Of Panel)()
            For Each cntrl As B2SRenderedLEDPanel In panel.Controls.OfType(Of B2SRenderedLEDPanel)()
                If cntrl.Selected Then
                    ret.Add(cntrl)
                End If
            Next
            For Each cntrl As B2SDream7LEDPanel In panel.Controls.OfType(Of B2SDream7LEDPanel)()
                If cntrl.Selected Then
                    ret.Add(cntrl)
                End If
            Next
        Next
        Return ret
    End Function
    Private Function GetFirstSelectedReelType(ByVal reeltypes As String) As String
        Dim ret As String = String.Empty
        For Each reeltype As String In reeltypes.Split(",")
            If Not String.IsNullOrEmpty(reeltype) Then
                ret = reeltype
                Exit For
            End If
        Next
        Return ret
    End Function

    Private Sub AddReelOrLEDSet(ByVal type As eImageSetType)
        If B2SMessageBox.Show(My.Resources.MSG_ReelImportStart, AppTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) = Windows.Forms.DialogResult.OK Then
            Dim images(10) As Image
            Using filedialog As OpenFileDialog = New OpenFileDialog
                With filedialog
                    .Filter = ImageFileExtensionFilter
                    Dim i As Integer = 0
                    Do While i <= 10
                        Dim ret As DialogResult = .ShowDialog(Me)
                        If ret = Windows.Forms.DialogResult.OK Then
                            Try
                                images(i) = New Bitmap(.FileName)
                            Catch
                                B2SMessageBox.Show(My.Resources.MSG_CannotLoadPic, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Finally
                                If images(i) IsNot Nothing Then i += 1
                            End Try
                            'If i = 10 Then
                            '    B2SMessageBox.Show(My.Resources.MSG_ReelImportAt10, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                            'End If
                        Else
                            If B2SMessageBox.Show(String.Format(My.Resources.MSG_ReelStopImagesImport, i), AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) = Windows.Forms.DialogResult.Yes Then
                                Exit Do
                            End If
                        End If
                    Loop
                End With
            End Using
            ' is the import okay
            If images(9) IsNot Nothing Then
                B2SMessageBox.Show(My.Resources.MSG_ReelImagesImportIsOkay, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                GeneralData.currentData.AddImageSet(images, type)
                If type = eImageSetType.LEDImages Then
                    FillReelListView(type, ilLEDs, lvLEDs, DefaultLEDs, True)
                Else
                    FillReelListView(type, ilReels, lvEMReels, DefaultEMReels, True)
                End If
            End If
        End If
    End Sub
    Private Sub AddCreditReel()
        If B2SMessageBox.Show(My.Resources.MSG_CreditReelImportStart, AppTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) = Windows.Forms.DialogResult.OK Then
            Dim images() As Image = Nothing
            Dim save As Boolean = True
            Using filedialog As OpenFileDialog = New OpenFileDialog
                With filedialog
                    .Filter = ImageFileExtensionFilter
                    Dim i As Integer = 0
                    Do While True
                        Dim ret As DialogResult = .ShowDialog(Me)
                        If ret = Windows.Forms.DialogResult.OK Then
                            Try
                                'If images Is Nothing Then
                                '    ReDim images(0)
                                'Else
                                ReDim Preserve images(i)
                                'End If
                                images(i) = New Bitmap(.FileName)
                            Catch
                                B2SMessageBox.Show(My.Resources.MSG_CannotLoadPic, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error)
                            Finally
                                If images(i) IsNot Nothing Then i += 1
                            End Try
                        Else
                            If B2SMessageBox.Show(String.Format(My.Resources.MSG_CreditReelStopImagesImport, i), AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) = Windows.Forms.DialogResult.Yes Then
                                save = (B2SMessageBox.Show(My.Resources.MSG_CreditReelSaveImagesImport, AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) = Windows.Forms.DialogResult.Yes)
                                Exit Do
                            End If
                        End If
                    Loop
                End With
            End Using
            ' is the import okay
            If save Then
                B2SMessageBox.Show(My.Resources.MSG_CreditReelImagesImportIsOkay, AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information)
                GeneralData.currentData.AddImageSet(images, eImageSetType.CreditReelImages)
                FillReelListView(eImageSetType.CreditReelImages, ilEMCeditReels, lvEMCreditReels, DefaultEMCreditReels, True)
            End If
        End If
    End Sub

    Private Sub btnExportReels_Click(sender As Object, e As EventArgs) Handles btnExportReels.Click
        Try
            ' Ensure a reel is selected
            If lvEMReels.SelectedItems.Count = 0 Then
                B2SMessageBox.Show("Please select a reel to export.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Get the selected reel name and index
            Dim selectedReelName As String = lvEMReels.SelectedItems(0).Text
            Dim selectedReelIndex As Integer = ExportIndex(lvEMReels, eImageSetType.ReelImages, DefaultEMReels)

            ' Path to the XML file
            Dim xmlPath As String = ImportFileName

            ' Call the export function
            If Not ExportReelImages(selectedReelName, selectedReelIndex, xmlPath) Then
                B2SMessageBox.Show("Failed to export the selected reel.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Catch ex As Exception
            B2SMessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnExportCreditReels_Click(sender As Object, e As EventArgs) Handles btnExportCreditReels.Click
        Try
            ' Ensure a credit reel is selected
            If lvEMCreditReels.SelectedItems.Count = 0 Then
                B2SMessageBox.Show("Please select a credit reel to export.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Get the selected credit reel name and index
            Dim selectedCreditReelName As String = lvEMCreditReels.SelectedItems(0).Text
            Dim selectedCreditReelIndex As Integer = ExportIndex(lvEMCreditReels, eImageSetType.CreditReelImages, DefaultEMCreditReels)

            ' Path to the XML file
            Dim xmlPath As String = ImportFileName

            ' Call the export function
            If Not ExportCreditReelImages(selectedCreditReelName, selectedCreditReelIndex, xmlPath) Then
                B2SMessageBox.Show("Failed to export the selected credit reel.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Catch ex As Exception
            B2SMessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub btnExportLEDs_Click(sender As Object, e As EventArgs) Handles btnExportLEDs.Click
        Try
            ' Ensure an LED set is selected
            If lvLEDs.SelectedItems.Count = 0 Then
                B2SMessageBox.Show("Please select a LED to export.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End If

            ' Get the selected LED name and index
            Dim selectedLEDName As String = lvLEDs.SelectedItems(0).Text
            Dim selectedLEDIndex As Integer = ExportIndex(lvLEDs, eImageSetType.LEDImages, DefaultLEDs)

            ' Path to the XML file
            Dim xmlPath As String = ImportFileName

            ' Call the export function
            If Not ExportLEDImages(selectedLEDName, selectedLEDIndex, xmlPath) Then
                B2SMessageBox.Show("Failed to export the selected LED.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Catch ex As Exception
            B2SMessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

End Class
