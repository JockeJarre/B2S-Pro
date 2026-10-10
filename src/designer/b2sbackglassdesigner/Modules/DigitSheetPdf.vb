Imports System.Diagnostics
Imports System.IO
Imports System.Text.RegularExpressions
Imports System.Windows.Forms

Public Module DigitSheetPdf
    Private Function RunTool(arguments As String) As String
        Dim executable = Path.Combine(Application.StartupPath, "OcrTools\python\python.exe")
        If Not File.Exists(executable) Then Throw New FileNotFoundException("The bundled PDF reader is missing. Please reinstall B2S Pro.", executable)
        Dim info As New ProcessStartInfo(executable, "-B """ & Path.Combine(Application.StartupPath, "OcrTools\pdf_sheet.py") & """ " & arguments) With {.UseShellExecute = False, .CreateNoWindow = True, .RedirectStandardOutput = True, .RedirectStandardError = True, .WorkingDirectory = Path.GetDirectoryName(executable)}
        Using process = Diagnostics.Process.Start(info)
            Dim output = process.StandardOutput.ReadToEndAsync()
            Dim errors = process.StandardError.ReadToEndAsync()
            If Not process.WaitForExit(30000) Then
                process.Kill()
                Throw New TimeoutException("PDF rendering took too long. Try a smaller PDF page.")
            End If
            Dim message = errors.GetAwaiter().GetResult()
            If process.ExitCode <> 0 Then Throw New InvalidDataException("Unable to open this PDF. " & message.Trim())
            Return output.GetAwaiter().GetResult()
        End Using
    End Function

    Public Function PdfPageCount(path As String) As Integer
        Return Integer.Parse(RunTool("count """ & path & """").Trim())
    End Function

    Public Function RenderPdf(path As String, page As Integer) As Bitmap
        Dim folder = IO.Path.Combine(DigitImportWorkPath, "Temp\PDF")
        Directory.CreateDirectory(folder)
        Dim prefix = IO.Path.Combine(folder, Guid.NewGuid().ToString("N"))
        Try
            RunTool(String.Format("render ""{1}"" {0} ""{2}.bmp""", page, path, prefix))
            Using renderedPage = Drawing.Image.FromFile(prefix & ".bmp")
                Return New Bitmap(renderedPage)
            End Using
        Finally
            If File.Exists(prefix & ".bmp") Then File.Delete(prefix & ".bmp")
        End Try
    End Function

    Public Function ChoosePdfPage(owner As IWin32Window, path As String, Optional ByRef selectedPage As Integer = 1) As Bitmap
        Dim pages = PdfPageCount(path)
        Using dialog As New Form With {.Text = "Choose PDF digit-sheet page", .ClientSize = New Size(380, 155), .FormBorderStyle = FormBorderStyle.FixedDialog, .StartPosition = FormStartPosition.CenterParent, .MinimizeBox = False, .MaximizeBox = False}
            Dim label As New Label With {.Text = "Page containing 0–9 (" & pages.ToString() & " pages):", .AutoSize = True, .Location = New Point(15, 18)}
            Dim number As New NumericUpDown With {.Minimum = 1, .Maximum = pages, .Value = 1, .Location = New Point(275, 15), .Width = 75}
            Dim hint As New Label With {.Text = "Read the digit identities and positions, then extract their original artwork. Scanned pages use local OCR.", .Location = New Point(15, 52), .Size = New Size(350, 42)}
            Dim ok As New Button With {.Text = "Use page", .DialogResult = DialogResult.OK, .Location = New Point(170, 110)}
            Dim cancel As New Button With {.Text = "Cancel", .DialogResult = DialogResult.Cancel, .Location = New Point(270, 110)}
            dialog.Controls.AddRange(New Control() {label, number, hint, ok, cancel})
            dialog.AcceptButton = ok
            dialog.CancelButton = cancel
            If dialog.ShowDialog(owner) <> DialogResult.OK Then Return Nothing
            selectedPage = CInt(number.Value)
            Return RenderPdf(path, selectedPage)
        End Using
    End Function
End Module
