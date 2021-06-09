using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

// https://stackoverflow.com/questions/13130052/directoryinfo-enumeratefiles-causes-unauthorizedaccessexception-and-other
public class FileSystemEnumerable : IEnumerable<FileSystemInfo>
{
    private readonly DirectoryInfo _root;
    private readonly SearchOption _option;

    public FileSystemEnumerable(DirectoryInfo root, SearchOption option)
    {
        _root = root;
        _option = option;
    }

    public IEnumerator<FileSystemInfo> GetEnumerator()
    {
        if (_root == null || !_root.Exists)
        {
            yield break;
        }

        IEnumerable<FileSystemInfo> matches;

        try
        {
            matches = _root.EnumerateFileSystemInfos(@"*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            yield break;
        }

        foreach (var file in matches)
        {
            yield return file;
            if ((_option == SearchOption.AllDirectories) && (file is DirectoryInfo dir))
            {
                foreach (var match in new FileSystemEnumerable(dir, _option))
                {
                    yield return match;
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}