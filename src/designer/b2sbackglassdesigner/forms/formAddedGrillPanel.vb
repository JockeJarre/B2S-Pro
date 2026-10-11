Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Imaging
Imports System.IO
Imports System.Windows.Forms

Public Module AddedGrillPanel
    Public Function BaseHeight(data As Backglass.Data) As Integer
        Return If(data.AddedGrillOriginalHeight > 0, data.AddedGrillOriginalHeight, data.Image.Height)
    End Function

    Public Function Compose(source As Image, originalHeight As Integer, panel As Image, panelHeight As Integer) As Bitmap
        If source Is Nothing OrElse originalHeight < 1 OrElse originalHeight > source.Height Then Throw New InvalidOperationException("The original backglass boundary is invalid.")
        If panelHeight < 0 OrElse (panelHeight > 0 AndAlso panel Is Nothing) Then Throw New ArgumentException("Choose a panel image and height.")
        Dim output As New Bitmap(source.Width, originalHeight + panelHeight, PixelFormat.Format32bppArgb)
        Using g As Graphics = Graphics.FromImage(output)
            g.CompositingMode = CompositingMode.SourceCopy
            g.DrawImage(source, New Rectangle(0, 0, source.Width, originalHeight), New Rectangle(0, 0, source.Width, originalHeight), GraphicsUnit.Pixel)
            If panelHeight > 0 Then
                g.CompositingMode = CompositingMode.SourceOver
                g.FillRectangle(Brushes.Black, 0, originalHeight, source.Width, panelHeight)
                g.InterpolationMode = InterpolationMode.HighQualityBicubic
                g.DrawImage(panel, New Rectangle(0, originalHeight, source.Width, panelHeight))
            End If
        End Using
        Using sourceBitmap As Bitmap = DirectCast(source, Bitmap).Clone(New Rectangle(0, 0, source.Width, originalHeight), PixelFormat.Format32bppArgb)
            Dim rect As New Rectangle(0, 0, source.Width, originalHeight)
            Dim readBits As BitmapData = sourceBitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
            Dim writeBits As BitmapData = output.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb)
            Try
                Dim row(source.Width * 4 - 1) As Byte
                For y As Integer = 0 To originalHeight - 1
                    Runtime.InteropServices.Marshal.Copy(IntPtr.Add(readBits.Scan0, y * readBits.Stride), row, 0, row.Length)
                    Runtime.InteropServices.Marshal.Copy(row, 0, IntPtr.Add(writeBits.Scan0, y * writeBits.Stride), row.Length)
                Next
            Finally
                output.UnlockBits(writeBits)
                sourceBitmap.UnlockBits(readBits)
            End Try
        End Using
        Return output
    End Function

    Private Function ExtendMask(encoded As String, width As Integer, originalHeight As Integer, height As Integer, inverted As Boolean) As String
        If String.IsNullOrEmpty(encoded) Then Return encoded
        Using stream As New MemoryStream(Convert.FromBase64String(encoded)), mask As Image = Image.FromStream(stream)
            If mask.Width <> width OrElse mask.Height < originalHeight Then Throw New InvalidOperationException("The global mask does not match the original backglass. The panel was not applied.")
            Using extension As New Bitmap(width, Math.Max(1, height), PixelFormat.Format32bppArgb)
                Using g As Graphics = Graphics.FromImage(extension)
                    g.Clear(If(inverted, Color.Black, Color.White))
                End Using
                Using result As Bitmap = Compose(mask, originalHeight, extension, height), output As New MemoryStream()
                    result.Save(output, ImageFormat.Png)
                    Return Convert.ToBase64String(output.ToArray())
                End Using
            End Using
        End Using
    End Function

    Public Class Snapshot
        Public ReadOnly MainImage As Image
        Public ReadOnly OriginalHeight As Integer
        Public ReadOnly GrillHeight As Integer
        Public ReadOnly SmallHeight As Integer
        Public ReadOnly Mask As String
        Public ReadOnly MaskSource As String
        Public ReadOnly Artwork As New Generic.Dictionary(Of Images.ImageInfo, Image)
        Public Sub New(data As Backglass.Data)
            MainImage = data.Image
            OriginalHeight = data.AddedGrillOriginalHeight
            GrillHeight = data.GrillHeight
            SmallHeight = data.SmallGrillHeight
            Mask = data.GlobalIlluminationMaskData
            MaskSource = data.GlobalIlluminationMaskSourceData
            For Each item As Images.ImageInfo In data.Images
                If item.Type = Images.eImageInfoType.BackgroundImage OrElse item.Type = Images.eImageInfoType.IlluminationImage Then Artwork(item) = item.Image
            Next
        End Sub
        Public Sub Restore(target As B2STabPage)
            Dim data As Backglass.Data = target.BackglassData
            For Each item In Artwork
                item.Key.Image = item.Value
            Next
            target.Image = MainImage
            data.AddedGrillOriginalHeight = OriginalHeight
            data.GrillHeight = GrillHeight
            data.SmallGrillHeight = SmallHeight
            data.GlobalIlluminationMaskData = Mask
            data.GlobalIlluminationMaskSourceData = MaskSource
            RefreshCanvas(target)
        End Sub
    End Class

    Public Sub Apply(target As B2STabPage, panel As Image, height As Integer)
        Dim data As Backglass.Data = target.BackglassData
        If data.Image Is Nothing Then Throw New InvalidOperationException("Open a backglass image first.")
        If data.AddedGrillOriginalHeight = 0 AndAlso (data.GrillHeight > 0 OrElse data.SmallGrillHeight > 0) Then Throw New InvalidOperationException("This backglass already has a grill boundary. Its existing artwork will not be changed by the add-on editor.")
        Dim original As Integer = BaseHeight(data)
        Dim finalHeight As Integer = original + height
        If finalHeight < data.Image.Height Then
            For Each bulb As Illumination.BulbInfo In data.Bulbs
                If bulb.Location.Y + bulb.Size.Height > finalHeight Then Throw New InvalidOperationException("Move the lights above the new bottom edge before reducing or removing the panel.")
            Next
            For Each score As ReelAndLED.ScoreInfo In data.Scores
                If score.Location.Y + score.Size.Height > finalHeight Then Throw New InvalidOperationException("Move the score reels above the new bottom edge before reducing or removing the panel.")
            Next
        End If
        ' Prepare everything before touching any document values.
        Dim nextMain As Bitmap = Compose(data.Image, original, panel, height)
        Dim nextImages As New Generic.Dictionary(Of Images.ImageInfo, Image)
        Dim nextMask As String
        Dim nextMaskSource As String
        Try
            For Each item As Images.ImageInfo In data.Images
                If item.Image IsNot Nothing AndAlso (item.Type = Images.eImageInfoType.BackgroundImage OrElse item.Type = Images.eImageInfoType.IlluminationImage) Then
                    If item.Image.Width <> data.Image.Width OrElse item.Image.Height <> data.Image.Height Then Throw New InvalidOperationException("A background or illumination image has different dimensions. The panel was not applied; no original artwork was changed.")
                    nextImages(item) = Compose(item.Image, original, panel, height)
                End If
            Next
            nextMask = ExtendMask(data.GlobalIlluminationMaskData, data.Image.Width, original, height, data.GlobalIlluminationMaskInverted)
            nextMaskSource = ExtendMask(data.GlobalIlluminationMaskSourceData, data.Image.Width, original, height, data.GlobalIlluminationMaskInverted)
        Catch
            nextMain.Dispose()
            For Each image In nextImages.Values
                image.Dispose()
            Next
            Throw
        End Try
        Dim previous As New Snapshot(data)
        For Each item In nextImages
            item.Key.Image = item.Value
        Next
        target.Image = nextMain
        data.AddedGrillOriginalHeight = If(height > 0, original, 0)
        data.GrillHeight = height
        data.SmallGrillHeight = 0
        data.GlobalIlluminationMaskData = nextMask
        data.GlobalIlluminationMaskSourceData = nextMaskSource
        Undo.AddEntry(New Undo.UndoEntry(Undo.Type.GrillPanelChanged, previous) With {.Owner = target})
        RefreshCanvas(target)
    End Sub

    Private Sub RefreshCanvas(target As B2STabPage)
        target.BackglassData.IsDirty = True
        target.BackglassData.IsSavedImageDirty = True
        For Each bulb As Illumination.BulbInfo In target.BackglassData.Bulbs
            bulb.IsIlluminatedImageDirty = True
        Next
        target.ClearAddedPanelImageCache()
        target.Zoom(target.BackglassData.Zoom)
        target.Invalidate()
    End Sub

    Public ReadOnly Property SavedTemplatesFolder As String
        Get
            Return Path.Combine(Application.StartupPath, "My Templates", "Speaker Grills")
        End Get
    End Property
    Public Function SaveTemplate(art As Image, name As String) As String
        name = name.Trim()
        If name.Length = 0 OrElse name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 OrElse name.EndsWith(".") Then Throw New ArgumentException("Enter a valid template name without folder separators.")
        Directory.CreateDirectory(SavedTemplatesFolder)
        For suffix As Integer = 0 To 9999
            Dim file = Path.Combine(SavedTemplatesFolder, name & If(suffix = 0, "", " (" & suffix & ")") & ".png")
            Try
                Using stream As New FileStream(file, FileMode.CreateNew, FileAccess.Write)
                    art.Save(stream, ImageFormat.Png)
                End Using
                Return file
            Catch ex As IOException When System.IO.File.Exists(file)
                ' Existing templates are never replaced.
            End Try
        Next
        Throw New IOException("There are too many templates with that name.")
    End Function
    Public Function SavedTemplateFiles() As String()
        If Not Directory.Exists(SavedTemplatesFolder) Then Return New String() {}
        Dim files As New Generic.List(Of String)
        For Each file In Directory.GetFiles(SavedTemplatesFolder)
            If {".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"}.Contains(Path.GetExtension(file).ToLowerInvariant()) Then files.Add(file)
        Next
        files.Sort(StringComparer.OrdinalIgnoreCase)
        Return files.ToArray()
    End Function
    Public ReadOnly TemplateNames As String() = {"Williams Gold", "Bally Gold", "Bally Silver", "Gottlieb Pinball", "Matte Black Honeycomb", "Black Stereo Bezel with Dual Grilles", "Black Arcade Speaker and DMD Bezel", "Williams Silver"}
    Private ReadOnly TemplateBounds As Rectangle() = {
        New Rectangle(16, 444, 1222, 363),
        New Rectangle(17, 447, 1221, 359),
        New Rectangle(16, 445, 1222, 362),
        New Rectangle(30, 94, 1715, 684),
        New Rectangle(15, 43, 1930, 712),
        New Rectangle(24, 99, 1726, 678),
        New Rectangle(17, 447, 1220, 359),
        New Rectangle(16, 444, 1222, 363)}
    Public Function LoadTemplate(index As Integer) As Bitmap
        If index < 0 OrElse index >= TemplateNames.Length Then Throw New ArgumentOutOfRangeException(NameOf(index))
        Using stream = GetType(formAddedGrillPanel).Assembly.GetManifestResourceStream("B2SPro.SpeakerPanel." & index & ".png")
            If stream Is Nothing Then Throw New InvalidOperationException("The embedded speaker panel is missing.")
            Using original As New Bitmap(stream)
                Return original.Clone(TemplateBounds(index), PixelFormat.Format32bppArgb)
            End Using
        End Using
    End Function
End Module

Public Class formAddedGrillPanel
    Inherits B2SThemedForm
    Private ReadOnly target As B2STabPage
    Private ReadOnly choice As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 190}
    Private ReadOnly panelHeight As New NumericUpDown With {.Minimum = 24, .Maximum = 4096, .Width = 95}
    Private ReadOnly preview As New PictureBox With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.FromArgb(3, 5, 9)}
    Private ReadOnly saveTemplateButton As New Button With {.Text = "Save to My Templates", .AutoSize = True, .Enabled = False}
    Private imported As Image
    Private rendered As Bitmap
    Public Sub New(page As B2STabPage)
        target = page
        Name = "formAddedGrillPanel"
        Text = "Add / Edit Speaker & Grill Panel"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(950, 720)
        MinimumSize = New Size(760, 550)
        BackColor = Color.FromArgb(10, 13, 20)
        ForeColor = Color.White
        Font = New Font("Segoe UI", 9)
        choice.Items.AddRange(AddedGrillPanel.TemplateNames)
        choice.SelectedIndex = 0
        Dim data = page.BackglassData
        panelHeight.Value = Math.Min(4096, Math.Max(24, If(data.AddedGrillOriginalHeight > 0, data.Image.Height - data.AddedGrillOriginalHeight, CInt(data.Image.Width * 0.22))))
        If data.AddedGrillOriginalHeight > 0 Then
            imported = New Bitmap(data.Image.Width, data.Image.Height - data.AddedGrillOriginalHeight)
            Using g As Graphics = Graphics.FromImage(imported)
                g.DrawImage(data.Image, New Rectangle(0, 0, imported.Width, imported.Height), New Rectangle(0, data.AddedGrillOriginalHeight, imported.Width, imported.Height), GraphicsUnit.Pixel)
            End Using
            choice.Items.Add("Current panel")
            choice.SelectedIndex = AddedGrillPanel.TemplateNames.Length
        End If
        Dim controlsRow As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 110, .Padding = New Padding(10), .WrapContents = True}
        controlsRow.Controls.Add(New Label With {.Text = "Panel", .AutoSize = True, .Margin = New Padding(3, 8, 3, 3)})
        controlsRow.Controls.Add(choice)
        Dim importButton As New Button With {.Text = "Import image...", .AutoSize = True}
        AddHandler importButton.Click, AddressOf ImportImage
        controlsRow.Controls.Add(importButton)
        controlsRow.Controls.Add(New Label With {.Text = "Height (pixels)", .AutoSize = True, .Margin = New Padding(3, 8, 3, 3)})
        controlsRow.Controls.Add(panelHeight)
        Dim browse As New Button With {.Text = "Choose template...", .AutoSize = True}
        AddHandler browse.Click, Sub()
                                     Using gallery As New formGrillTemplatePicker(choice.SelectedIndex)
                                         If gallery.ShowDialog(Me) = DialogResult.OK Then choice.SelectedIndex = gallery.SelectedTemplate
                                     End Using
                                 End Sub
        controlsRow.Controls.Add(browse)
        Dim savedBrowse As New Button With {.Text = "My Saved Templates", .AutoSize = True}
        AddHandler savedBrowse.Click, Sub()
                                          Try
                                              Using gallery As New formGrillTemplatePicker(-1, True)
                                                  If gallery.ShowDialog(Me) = DialogResult.OK Then LoadImported(gallery.SelectedFile)
                                              End Using
                                          Catch ex As Exception
                                              MessageBox.Show(Me, ex.Message, "Cannot open saved templates", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                          End Try
                                      End Sub
        controlsRow.Controls.Add(savedBrowse)
        AddHandler saveTemplateButton.Click, Sub()
                                                Dim name = Microsoft.VisualBasic.Interaction.InputBox("Name this speaker grill template:", "Save to My Templates", Path.GetFileNameWithoutExtension(choice.Text))
                                                If String.IsNullOrWhiteSpace(name) Then Return
                                                Try
                                                    Dim saved = AddedGrillPanel.SaveTemplate(imported, name)
                                                    MessageBox.Show(Me, "Saved as " & Path.GetFileName(saved), "My Templates", MessageBoxButtons.OK, MessageBoxIcon.Information)
                                                Catch ex As Exception
                                                    MessageBox.Show(Me, ex.Message, "Cannot save template", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                                End Try
                                            End Sub
        controlsRow.Controls.Add(saveTemplateButton)
        Dim explanation As New Label With {.Dock = DockStyle.Top, .Height = 46, .Padding = New Padding(12, 6, 12, 2), .Text = "Width matches your backglass. Original artwork and objects stay in place. Add lights and score reels normally after applying. Template openings are artwork; existing DMD routing is preserved."}
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 54, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10)}
        Dim cancel As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Width = 90}
        Dim apply As New Button With {.Text = "Apply panel", .Width = 115}
        Dim remove As New Button With {.Text = "Remove panel", .Width = 115, .Enabled = data.AddedGrillOriginalHeight > 0}
        AddHandler apply.Click, Sub() Commit(CInt(panelHeight.Value))
        AddHandler remove.Click, Sub() Commit(0)
        buttons.Controls.Add(cancel)
        buttons.Controls.Add(apply)
        buttons.Controls.Add(remove)
        Controls.Add(preview)
        Controls.Add(explanation)
        Controls.Add(controlsRow)
        Controls.Add(buttons)
        CancelButton = cancel
        AddHandler choice.SelectedIndexChanged, Sub() UpdatePreview()
        AddHandler panelHeight.ValueChanged, Sub() UpdatePreview()
        UpdatePreview()
    End Sub
    Private Function PanelArt() As Image
        If choice.SelectedIndex >= AddedGrillPanel.TemplateNames.Length Then Return New Bitmap(imported)
        Return AddedGrillPanel.LoadTemplate(choice.SelectedIndex)
    End Function
    Private Sub UpdatePreview()
        saveTemplateButton.Enabled = imported IsNot Nothing AndAlso choice.SelectedIndex >= AddedGrillPanel.TemplateNames.Length
        Using art As Image = PanelArt()
            Dim nextPreview As Bitmap = AddedGrillPanel.Compose(target.BackglassData.Image, AddedGrillPanel.BaseHeight(target.BackglassData), art, CInt(panelHeight.Value))
            Using g As Graphics = Graphics.FromImage(nextPreview), pen As New Pen(Color.LimeGreen, 2)
                g.DrawLine(pen, 0, AddedGrillPanel.BaseHeight(target.BackglassData), nextPreview.Width, AddedGrillPanel.BaseHeight(target.BackglassData))
            End Using
            preview.Image = nextPreview
            If rendered IsNot Nothing Then rendered.Dispose()
            rendered = nextPreview
        End Using
    End Sub
    Private Sub ImportImage(sender As Object, e As EventArgs)
        Using picker As New OpenFileDialog With {.Filter = "Panel images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"}
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                LoadImported(picker.FileName)
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Cannot import panel", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub
    Private Sub LoadImported(fileName As String)
                Using image As Image = Image.FromFile(fileName)
                    Dim replacement As New Bitmap(image)
                    If imported IsNot Nothing Then imported.Dispose()
                    imported = replacement
                End Using
                If choice.Items.Count = AddedGrillPanel.TemplateNames.Length Then choice.Items.Add("Imported panel")
                choice.Items(AddedGrillPanel.TemplateNames.Length) = Path.GetFileName(fileName)
                choice.SelectedIndex = AddedGrillPanel.TemplateNames.Length
                UpdatePreview()
    End Sub
    Private Sub Commit(height As Integer)
        Try
            Using art As Image = PanelArt()
                AddedGrillPanel.Apply(target, art, height)
            End Using
            DialogResult = DialogResult.OK
            Close()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Panel unchanged", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Try
    End Sub
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            preview.Image = Nothing
            If rendered IsNot Nothing Then rendered.Dispose()
            If imported IsNot Nothing Then imported.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

Public Class formGrillTemplatePicker
    Inherits B2SThemedForm
    Public Property SelectedTemplate As Integer = -1
    Public Property SelectedFile As String
    Private ReadOnly pictures As New Generic.List(Of Image)
    Public Sub New(current As Integer, Optional saved As Boolean = False)
        Name = If(saved, "formSavedGrillTemplatePicker", "formGrillTemplatePicker")
        Text = If(saved, "My Saved Templates", "Choose Speaker & Grill Template")
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1040, 740)
        MinimumSize = New Size(700, 500)
        BackColor = Color.FromArgb(10, 13, 20)
        ForeColor = Color.White
        Dim grid As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoScroll = True, .Padding = New Padding(12)}
        Dim files As String() = If(saved, AddedGrillPanel.SavedTemplateFiles(), New String() {})
        Dim count As Integer = If(saved, files.Length, AddedGrillPanel.TemplateNames.Length)
        For i As Integer = 0 To count - 1
            Dim index As Integer = i
            Dim art As Bitmap
            Try
                If saved Then
                    Using source As Image = Image.FromFile(files(i))
                        art = New Bitmap(source)
                    End Using
                Else
                    art = AddedGrillPanel.LoadTemplate(i)
                End If
            Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is IOException OrElse TypeOf ex Is OutOfMemoryException
                Continue For
            End Try
            pictures.Add(art)
            Dim tile As New Panel With {.Size = New Size(310, 195), .Margin = New Padding(8), .BorderStyle = BorderStyle.FixedSingle, .BackColor = If(i = current, Color.FromArgb(20, 65, 75), Color.FromArgb(18, 22, 29)), .Cursor = Cursors.Hand}
            Dim picture As New PictureBox With {.Dock = DockStyle.Fill, .Image = art, .SizeMode = PictureBoxSizeMode.Zoom, .Padding = New Padding(8), .BackColor = Color.Black, .Cursor = Cursors.Hand}
            Dim label As New Label With {.Dock = DockStyle.Bottom, .Height = 44, .Text = If(saved, Path.GetFileNameWithoutExtension(files(i)), AddedGrillPanel.TemplateNames(i)), .TextAlign = ContentAlignment.MiddleCenter, .ForeColor = Color.White, .Cursor = Cursors.Hand}
            Dim selectTile As EventHandler = Sub(sender, e)
                                                SelectedTemplate = index
                                                If saved Then SelectedFile = files(index)
                                                DialogResult = DialogResult.OK
                                                Close()
                                            End Sub
            AddHandler picture.Click, selectTile
            AddHandler label.Click, selectTile
            AddHandler tile.Click, selectTile
            tile.Controls.Add(picture)
            tile.Controls.Add(label)
            grid.Controls.Add(tile)
        Next
        If saved Then
            Directory.CreateDirectory(AddedGrillPanel.SavedTemplatesFolder)
            Dim hint As New Label With {.Dock = DockStyle.Top, .Height = 58, .Padding = New Padding(12, 4, 12, 4), .Text = If(grid.Controls.Count = 0, "No saved templates yet. Import an image and use Save to My Templates." & vbCrLf, "") & "You can also add images directly to: " & AddedGrillPanel.SavedTemplatesFolder}
            Controls.Add(hint)
            hint.BringToFront()
        End If
        Dim cancel As New Button With {.Text = "Cancel", .Dock = DockStyle.Bottom, .Height = 38, .DialogResult = DialogResult.Cancel}
        Controls.Add(grid)
        grid.SendToBack()
        Controls.Add(cancel)
        CancelButton = cancel
    End Sub
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            For Each art In pictures
                art.Dispose()
            Next
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
