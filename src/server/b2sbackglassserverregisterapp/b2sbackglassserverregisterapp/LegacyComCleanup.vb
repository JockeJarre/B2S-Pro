Imports Microsoft.Win32
Imports System.Collections.Generic

' The registry adapter is used only by the registration application.
' Discovery and execution can also operate on an in-memory store for verification.
Public Interface ILegacyComStore
    Function SubKeyNames(path As String) As String()
    Function ReadValue(path As String, name As String) As String
    Sub DeleteTree(path As String)
End Interface

Public Class LegacyRegistryStore
    Implements ILegacyComStore, IDisposable
    Private ReadOnly root As RegistryKey
    Public Sub New(view As RegistryView)
        root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view)
    End Sub
    Public Function SubKeyNames(path As String) As String() Implements ILegacyComStore.SubKeyNames
        If path.Length = 0 Then Return root.GetSubKeyNames()
        Using key = root.OpenSubKey(path, False)
            Return If(key Is Nothing, New String() {}, key.GetSubKeyNames())
        End Using
    End Function
    Public Function ReadValue(path As String, name As String) As String Implements ILegacyComStore.ReadValue
        Using key = root.OpenSubKey(path, False)
            Return If(key Is Nothing, "", CStr(If(key.GetValue(name), "")))
        End Using
    End Function
    Public Sub DeleteTree(path As String) Implements ILegacyComStore.DeleteTree
        root.DeleteSubKeyTree(path, False)
    End Sub
    Public Sub Dispose() Implements IDisposable.Dispose
        root.Dispose()
    End Sub
End Class

Public Module LegacyComCleanup
    Public Class Plan
        Public ReadOnly ProgIds As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Public ReadOnly Clsids As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
    End Class
    Private Function ProtectedIds(store As ILegacyComStore) As HashSet(Of String)
        Dim ids As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each value In New String() {"09e233a3-cc79-457a-b49e-f637588891e5", "5693c68c-5834-466d-aaac-a86922076efd", "a48a5c0a-656c-4253-9f33-2426cc9c87b7", store.ReadValue("B2S.Server\CLSID", "")}
            Dim parsed As Guid
            If Guid.TryParse(value, parsed) Then ids.Add(parsed.ToString("B"))
        Next
        Return ids
    End Function
    Private Function IsProtected(value As String, ids As HashSet(Of String)) As Boolean
        Dim parsed As Guid
        Return Guid.TryParse(value, parsed) AndAlso ids.Contains(parsed.ToString("B"))
    End Function
    Public Function Discover(store As ILegacyComStore) As Plan
        Dim result As New Plan
        Dim protectedSet = ProtectedIds(store)
        For Each name In store.SubKeyNames("")
            If Not name.StartsWith("B2S.", StringComparison.OrdinalIgnoreCase) OrElse name.Equals("B2S.Server", StringComparison.OrdinalIgnoreCase) OrElse name.StartsWith("B2S.Server.", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim clsid = store.ReadValue(name & "\CLSID", "")
            If IsProtected(clsid, protectedSet) Then Continue For
            result.ProgIds.Add(name)
            Dim parsed As Guid
            If Guid.TryParse(clsid, parsed) Then result.Clsids.Add(parsed.ToString("B"))
        Next
        For Each clsid In store.SubKeyNames("CLSID")
            If IsProtected(clsid, protectedSet) Then Continue For
            Dim path = "CLSID\" & clsid & "\InprocServer32"
            Dim className = store.ReadValue(path, "Class")
            If className.Equals("B2S.Server", StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim dllPath = store.ReadValue(path, "")
            Dim codeBase = store.ReadValue(path, "CodeBase")
            Dim assembly = store.ReadValue(path, "Assembly")
            If dllPath.IndexOf("B2SBackglassServer.dll", StringComparison.OrdinalIgnoreCase) >= 0 OrElse codeBase.IndexOf("B2SBackglassServer.dll", StringComparison.OrdinalIgnoreCase) >= 0 OrElse className.StartsWith("B2S.", StringComparison.OrdinalIgnoreCase) OrElse assembly.StartsWith("B2SBackglassServer", StringComparison.OrdinalIgnoreCase) Then
                Dim parsed As Guid
                If Guid.TryParse(clsid, parsed) Then result.Clsids.Add(parsed.ToString("B"))
            End If
        Next
        Return result
    End Function
    Public Function Execute(store As ILegacyComStore, plan As Plan) As Integer()
        Dim protectedSet = ProtectedIds(store)
        Dim progIds As Integer = 0, clsids As Integer = 0
        For Each name In plan.ProgIds
            If Not name.StartsWith("B2S.", StringComparison.OrdinalIgnoreCase) OrElse name.Equals("B2S.Server", StringComparison.OrdinalIgnoreCase) OrElse name.StartsWith("B2S.Server.", StringComparison.OrdinalIgnoreCase) OrElse IsProtected(store.ReadValue(name & "\CLSID", ""), protectedSet) Then Continue For
            Try
                store.DeleteTree(name)
                progIds += 1
            Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is System.Security.SecurityException OrElse TypeOf ex Is System.IO.IOException
            End Try
        Next
        For Each id In plan.Clsids
            If IsProtected(id, protectedSet) OrElse store.ReadValue("CLSID\" & id & "\InprocServer32", "Class").Equals("B2S.Server", StringComparison.OrdinalIgnoreCase) Then Continue For
            Try
                store.DeleteTree("CLSID\" & id)
                clsids += 1
            Catch ex As Exception When TypeOf ex Is UnauthorizedAccessException OrElse TypeOf ex Is System.Security.SecurityException OrElse TypeOf ex Is System.IO.IOException
            End Try
        Next
        Return {progIds, clsids}
    End Function
End Module
