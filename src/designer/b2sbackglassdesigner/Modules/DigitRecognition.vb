Imports System.IO
Imports System.Diagnostics
Imports System.Web.Script.Serialization

Public Module DigitRecognition
    Public Class Glyph
        Public digit As Integer
        Public confidence As Double
        Public raw As String
        Public review As Boolean
        Public x0 As Integer
        Public y0 As Integer
        Public x1 As Integer
        Public y1 As Integer
        Public ReadOnly Property Bounds As Rectangle
            Get
                Return Rectangle.FromLTRB(x0, y0, x1, y1)
            End Get
        End Property
    End Class
    Public Class Result
        Public symbols As Glyph()
    End Class

    Private Function Runtime(name As String) As String
        Dim executable = Path.Combine(Application.StartupPath, "OcrTools", name)
        If Not File.Exists(executable) Then Throw New FileNotFoundException("The bundled digit recognition tools are missing. Please reinstall B2S Pro.", executable)
        Return executable
    End Function

    Private Sub Run(executable As String, arguments As String)
        Dim info As New ProcessStartInfo(executable, arguments) With {.UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardOutput = True, .RedirectStandardError = True, .WorkingDirectory = Path.Combine(Application.StartupPath, "OcrTools")}
        Using child = Process.Start(info)
            Dim output = child.StandardOutput.ReadToEndAsync()
            Dim errors = child.StandardError.ReadToEndAsync()
            If Not child.WaitForExit(90000) Then
                child.Kill()
                Throw New TimeoutException("Recognition took too long. Try a smaller sheet.")
            End If
            Dim message = errors.GetAwaiter().GetResult()
            output.GetAwaiter().GetResult()
            If child.ExitCode <> 0 Then Throw New InvalidDataException("Digit recognition failed: " & message.Trim())
        End Using
    End Sub

    Public Function Recognize(source As Bitmap, Optional pdfPath As String = Nothing, Optional page As Integer = 1) As Glyph()
        Dim folder = Path.Combine(DigitImportWorkPath, "Temp\OCR")
        Directory.CreateDirectory(folder)
        Dim prefix = Path.Combine(folder, Guid.NewGuid().ToString("N"))
        Dim jsonPath = prefix & ".json"
        Dim pngPath = prefix & ".png"
        Dim ratio As Double = 1
        Dim serializer As New JavaScriptSerializer With {.MaxJsonLength = 10000000}
        Try
            Dim found As Glyph() = Nothing
            If Not String.IsNullOrEmpty(pdfPath) Then
                Run(Runtime("python\python.exe"), String.Format("-B ""{0}"" ""{1}"" {2} {3} {4} ""{5}""", Path.Combine(Application.StartupPath, "OcrTools\pdf_digits.py"), pdfPath, page, source.Width, source.Height, jsonPath))
                found = serializer.Deserialize(Of Result)(File.ReadAllText(jsonPath)).symbols
                If found IsNot Nothing AndAlso found.Select(Function(g) g.digit).Distinct().Count() = 10 Then Return SelectGlyphs(found, source.Size)
            End If
            ratio = Math.Min(1, 2400.0 / Math.Max(source.Width, source.Height))
            Using input As New Bitmap(Math.Max(1, CInt(source.Width * ratio)), Math.Max(1, CInt(source.Height * ratio)))
                Using canvas = Graphics.FromImage(input)
                    canvas.Clear(Color.White)
                    canvas.DrawImage(source, New Rectangle(Point.Empty, input.Size))
                End Using
                input.Save(pngPath, Imaging.ImageFormat.Png)
            End Using
            Run(Runtime("node\node.exe"), String.Format("""{0}"" ""{1}"" ""{2}""", Path.Combine(Application.StartupPath, "OcrTools\recognize.cjs"), pngPath, jsonPath))
            found = serializer.Deserialize(Of Result)(File.ReadAllText(jsonPath)).symbols
            If found Is Nothing Then Throw New InvalidDataException("No digits were recognized.")
            For Each glyph In found
                glyph.x0 = CInt(Math.Floor(glyph.x0 / ratio))
                glyph.y0 = CInt(Math.Floor(glyph.y0 / ratio))
                glyph.x1 = Math.Min(source.Width, CInt(Math.Ceiling(glyph.x1 / ratio)))
                glyph.y1 = Math.Min(source.Height, CInt(Math.Ceiling(glyph.y1 / ratio)))
            Next
            Return SelectGlyphs(found, source.Size)
        Finally
            If File.Exists(jsonPath) Then File.Delete(jsonPath)
            If File.Exists(pngPath) Then File.Delete(pngPath)
        End Try
    End Function

    Public Function SelectGlyphs(candidates As Glyph(), size As Size) As Glyph()
        Dim usable = candidates.Where(Function(g) g.digit >= 0 AndAlso g.digit <= 9 AndAlso g.Bounds.Width > 0 AndAlso g.Bounds.Height > 0 AndAlso New Rectangle(Point.Empty, size).Contains(g.Bounds)).ToArray()
        If usable.Length = 0 Then Throw New InvalidDataException("No usable digit artwork was found.")
        ' Prefer the large reel artwork to small numbers in explanatory text.
        Dim height = usable.Max(Function(g) g.Bounds.Height)
        Dim artwork = usable.Where(Function(g) g.Bounds.Height >= height * 0.65).ToArray()
        Dim missing = Enumerable.Range(0, 10).Where(Function(d) Not artwork.Any(Function(g) g.digit = d)).ToArray()
        If missing.Length > 0 Then Throw New InvalidDataException("Could not identify all ten reel digits. Missing: " & String.Join(", ", missing) & ". Nothing can be saved from this detection.")
        Dim result(9) As Glyph
        For digit As Integer = 0 To 9
            Dim value = digit
            ' Repeated copies on one sheet: choose the first column, not a mixture of columns.
            result(digit) = artwork.Where(Function(g) g.digit = value).OrderBy(Function(g) g.x0).ThenByDescending(Function(g) g.confidence).ThenBy(Function(g) g.y0).First()
        Next
        Return result
    End Function

    Public Function Extract(source As Bitmap, glyphs As Glyph(), padding As Integer) As Image()
        If glyphs Is Nothing OrElse glyphs.Length <> 10 Then Throw New ArgumentException("Ten recognized digits are required.")
        Dim width = glyphs.Max(Function(g) g.Bounds.Width) + 2 * padding
        Dim height = glyphs.Max(Function(g) g.Bounds.Height) + 2 * padding
        Dim result(9) As Image
        Try
            For digit As Integer = 0 To 9
                If glyphs(digit).digit <> digit Then Throw New ArgumentException("Recognized digit mapping is incomplete.")
                Dim bounds = glyphs(digit).Bounds
                Dim bitmap As New Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb)
                result(digit) = bitmap
                Using canvas = Graphics.FromImage(bitmap), crop = source.Clone(bounds, Imaging.PixelFormat.Format32bppArgb)
                    canvas.Clear(Color.White)
                    canvas.CompositingMode = Drawing2D.CompositingMode.SourceCopy
                    canvas.InterpolationMode = Drawing2D.InterpolationMode.NearestNeighbor
                    canvas.PixelOffsetMode = Drawing2D.PixelOffsetMode.Half
                    canvas.DrawImage(crop, New Rectangle((width - bounds.Width) \ 2, (height - bounds.Height) \ 2, bounds.Width, bounds.Height), 0, 0, bounds.Width, bounds.Height, GraphicsUnit.Pixel)
                End Using
            Next
            Return result
        Catch
            For Each value In result
                If value IsNot Nothing Then value.Dispose()
            Next
            Throw
        End Try
    End Function
End Module
