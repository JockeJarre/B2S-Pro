Imports System.IO
Imports System.Xml.Serialization

Namespace Illumination
    ' Text appearance has its own profile; IDs, lettering and canvas masks belong
    ' to each object and are never copied into a newly created text box.
    Public Module TextSettingsDefaults
        Private ReadOnly profileFile As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "B2SBackglassDesigner", "text-settings.xml")

        Public Class Profile
            Public FontName As String
            Public FontSize As Single
            Public FontStyle As FontStyle
            Public TextAlignment As eTextAlignment
            Public DodgeColor As Integer
            Public DodgeColorEmpty As Boolean
            Public LightColor As Integer
            Public LightColorEmpty As Boolean
            Public Intensity As Integer
            Public GlowIntensity As Integer
            Public LightTemperature As Integer
            Public GlowSpread As Integer
            Public GlowSoftness As Integer
            Public GlowFalloff As Integer
            Public LightDiffusion As Integer
        End Class

        Friend Sub Save(ByVal bulb As BulbInfo, Optional ByVal fileName As String = Nothing)
            If bulb Is Nothing Then Return
            Dim settings As New Profile With {
                .FontName = bulb.FontName, .FontSize = bulb.FontSize, .FontStyle = bulb.FontStyle,
                .TextAlignment = bulb.TextAlignment,
                .DodgeColor = bulb.DodgeColor.ToArgb(), .DodgeColorEmpty = bulb.DodgeColor.IsEmpty,
                .LightColor = bulb.LightColor.ToArgb(), .LightColorEmpty = bulb.LightColor.IsEmpty,
                .Intensity = bulb.Intensity, .GlowIntensity = bulb.GlowIntensity,
                .LightTemperature = bulb.LightTemperature, .GlowSpread = bulb.GlowSpread,
                .GlowSoftness = bulb.GlowSoftness, .GlowFalloff = bulb.GlowFalloff,
                .LightDiffusion = bulb.LightDiffusion
            }
            Try
                Dim target As String = If(fileName, profileFile)
                Directory.CreateDirectory(Path.GetDirectoryName(target))
                Using stream As New FileStream(target, FileMode.Create, FileAccess.Write)
                    Dim serializer As New XmlSerializer(GetType(Profile))
                    serializer.Serialize(stream, settings)
                End Using
            Catch
                ' Remembering defaults must not interrupt saving the text object.
            End Try
        End Sub

        Friend Sub ApplyTo(ByVal bulb As BulbInfo, Optional ByVal fileName As String = Nothing)
            If bulb Is Nothing Then Return
            Try
                Dim target As String = If(fileName, profileFile)
                If Not File.Exists(target) Then Return
                Dim settings As Profile
                Using stream As New FileStream(target, FileMode.Open, FileAccess.Read)
                    settings = DirectCast(New XmlSerializer(GetType(Profile)).Deserialize(stream), Profile)
                End Using
                If settings Is Nothing OrElse String.IsNullOrWhiteSpace(settings.FontName) OrElse
                   Single.IsNaN(settings.FontSize) OrElse settings.FontSize < 1 OrElse settings.FontSize > 500 Then Return
                bulb.FontName = settings.FontName
                bulb.FontSize = settings.FontSize
                bulb.FontStyle = settings.FontStyle
                bulb.TextAlignment = settings.TextAlignment
                bulb.DodgeColor = If(settings.DodgeColorEmpty, Color.Empty, Color.FromArgb(settings.DodgeColor))
                bulb.LightColor = If(settings.LightColorEmpty, Color.Empty, Color.FromArgb(settings.LightColor))
                bulb.Intensity = settings.Intensity
                bulb.GlowIntensity = settings.GlowIntensity
                bulb.LightTemperature = settings.LightTemperature
                bulb.GlowSpread = settings.GlowSpread
                bulb.GlowSoftness = settings.GlowSoftness
                bulb.GlowFalloff = settings.GlowFalloff
                bulb.LightDiffusion = settings.LightDiffusion
                bulb.IsIlluminatedImageDirty = True
            Catch
                ' Missing or unreadable preferences leave the normal defaults intact.
            End Try
        End Sub
    End Module
End Namespace
