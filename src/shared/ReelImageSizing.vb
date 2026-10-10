Imports System.Drawing

Public NotInheritable Class ReelImageSizing
    Public Shared Function Fit(source As Image, target As Size) As Bitmap
        If source Is Nothing OrElse target.Width <= 0 OrElse target.Height <= 0 Then Return Nothing
        Dim result As New Bitmap(target.Width, target.Height, Imaging.PixelFormat.Format32bppArgb)
        Dim scale As Double = Math.Min(target.Width / CDbl(source.Width), target.Height / CDbl(source.Height))
        Dim width As Single = CSng(source.Width * scale)
        Dim height As Single = CSng(source.Height * scale)
        Using g As Graphics = Graphics.FromImage(result)
            Dim background As Color = Color.Transparent
            If TypeOf source Is Bitmap Then background = DirectCast(source, Bitmap).GetPixel(0, 0)
            g.Clear(background)
            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
            g.DrawImage(source, New RectangleF((target.Width - width) / 2.0F, (target.Height - height) / 2.0F, width, height), New RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel)
        End Using
        Return result
    End Function
End Class
