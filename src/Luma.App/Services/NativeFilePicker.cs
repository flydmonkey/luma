using System.Runtime.InteropServices;

namespace Luma.App.Services;

internal static class NativeFilePicker
{
    public static string? Pick(IntPtr owner, string title, string filterName, string filterSpec)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        dialog.SetOptions(0x40 | 0x800 | 0x1000 | 0x8);
        dialog.SetFileTypes(1, [new FilterSpec { Name = filterName, Spec = filterSpec }]);
        dialog.SetTitle(title);
        var result = dialog.Show(owner);
        if (result == unchecked((int)0x800704C7))
        {
            return null;
        }

        if (result != 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        dialog.GetResult(out var item);
        item.GetDisplayName(0x80058000, out var path);
        return path;
    }

    public static string? Save(IntPtr owner, string title, string filterName, string filterSpec, string fileName, string? folder)
    {
        var dialog = (IFileSaveDialog)new FileSaveDialog();
        dialog.SetOptions(0x2 | 0x4 | 0x8 | 0x40 | 0x800);
        dialog.SetFileTypes(1, [new FilterSpec { Name = filterName, Spec = filterSpec }]);
        dialog.SetFileTypeIndex(1);
        dialog.SetTitle(title);
        dialog.SetFileName(fileName);
        dialog.SetDefaultExtension("png");
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder)
            && SHCreateItemFromParsingName(folder, IntPtr.Zero, ref ShellItemId, out var start) == 0
            && start is not null)
        {
            dialog.SetFolder(start);
        }

        var result = dialog.Show(owner);
        if (result == unchecked((int)0x800704C7))
        {
            return null;
        }

        if (result != 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        dialog.GetResult(out var item);
        item.GetDisplayName(0x80058000, out var path);
        return path;
    }

    private static Guid ShellItemId = new("43826D1E-E718-42EE-BC55-A1E261C37BFE");

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        ref Guid itemId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItem item);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FilterSpec
    {
        public string Name;
        public string Spec;
    }

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog
    {
    }

    [ComImport]
    [Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private class FileSaveDialog
    {
    }

    [ComImport]
    [Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileSaveDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] filters);

        void SetFileTypeIndex(uint index);

        void GetFileTypeIndex(out uint index);

        void Advise(IntPtr events, out uint cookie);

        void Unadvise(uint cookie);

        void SetOptions(uint options);

        void GetOptions(out uint options);

        void SetDefaultFolder(IShellItem item);

        void SetFolder(IShellItem item);

        void GetFolder(out IShellItem item);

        void GetCurrentSelection(out IShellItem item);

        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

        void GetResult(out IShellItem item);

        void AddPlace(IShellItem item, uint placement);

        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string extension);
    }

    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        [PreserveSig]
        int Show(IntPtr parent);

        void SetFileTypes(uint count, [MarshalAs(UnmanagedType.LPArray)] FilterSpec[] filters);

        void SetFileTypeIndex(uint index);

        void GetFileTypeIndex(out uint index);

        void Advise(IntPtr events, out uint cookie);

        void Unadvise(uint cookie);

        void SetOptions(uint options);

        void GetOptions(out uint options);

        void SetDefaultFolder(IntPtr item);

        void SetFolder(IntPtr item);

        void GetFolder(out IntPtr item);

        void GetCurrentSelection(out IntPtr item);

        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string name);

        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string title);

        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string text);

        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string label);

        void GetResult(out IShellItem item);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr bindCtx, ref Guid handler, ref Guid riid, out IntPtr result);

        void GetParent(out IShellItem parent);

        void GetDisplayName(uint nameType, [MarshalAs(UnmanagedType.LPWStr)] out string name);

        void GetAttributes(uint mask, out uint attributes);

        void Compare(IShellItem other, uint hint, out int order);
    }
}
