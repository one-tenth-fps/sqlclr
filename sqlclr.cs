using System;
using System.IO;
using System.Collections;
using System.Data.SqlTypes;
using Microsoft.SqlServer.Server;
using System.Linq;
using System.Net;

// https://www.sqlservercentral.com/stairways/stairway-to-sqlclr
public class FileSystemUtils
{
    [SqlFunction(
        Name = "GetFileSystemInfo",
        FillRowMethodName = "FileSystemInfo_FillRow",
        DataAccess = DataAccessKind.None,
        SystemDataAccess = SystemDataAccessKind.None,
        IsDeterministic = false,
        TableDefinition = @"Name NVARCHAR(4000), FullName NVARCHAR(4000), IsDirectory BIT, Length BIGINT, LastWriteTime DATETIME, Attributes NVARCHAR(4000)")]
    public static IEnumerable GetFileSystemInfo(SqlString Path, SqlBoolean Recurse)
    {
        try
        {
            return new FileSystemEnumerable(
                new DirectoryInfo(Path.Value),
                Recurse.Value ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Enumerable.Empty<FileSystemInfo>();
        }
    }

    private static void FileSystemInfo_FillRow(
        object obj,
        out SqlString Name,
        out SqlString FullName,
        out SqlBoolean IsDirectory,
        out SqlInt64 Length,
        out SqlDateTime LastWriteTime,
        out SqlString Attributes)
    {
        FileSystemInfo fsi = (FileSystemInfo)obj;
        Name = fsi.Name;
        FullName = fsi.FullName;
        LastWriteTime = fsi.LastWriteTime;
        Attributes = fsi.Attributes.ToString();
        if (obj is FileInfo file)
        {
            IsDirectory = SqlBoolean.False;
            Length = file.Length;
        }
        else
        {
            IsDirectory = SqlBoolean.True;
            Length = SqlInt64.Null;
        }
    }
}

public class NetUtils
{
    [Microsoft.SqlServer.Server.SqlProcedure]
    public static void SendHTTPRequest(
        SqlString URL,
        SqlString Method,
        SqlString Headers,
        SqlChars Request,
        out SqlInt32 StatusCode,
        out SqlChars Response)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2, which the old framework is unaware about
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create((string)URL);
        request.Method = (string)Method;

        if (!Headers.IsNull)
        {
            int index;
            string headerName;
            string headerValue;

            foreach (var header in ((string)Headers).Split('|'))
            {
                index = header.IndexOf(':');
                if (index >= 0)
                {
                    headerName = header.Substring(0, index).Trim();
                    headerValue = header.Substring(index + 1).Trim();
                    if (headerName == "Accept")
                        request.Accept = headerValue;
                    else if (headerName == "Content-Type")
                        request.ContentType = headerValue;
                    else
                        request.Headers.Add(headerName, headerValue);
                }
            }
        }

        if (!Request.IsNull)
            using (Stream requestStream = request.GetRequestStream())
            using (StreamWriter streamWriter = new StreamWriter(requestStream))
            {
                streamWriter.Write(Request.Buffer, 0, (int)Request.Length);
                streamWriter.Flush();
            }

        try
        {
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader streamReader = new StreamReader(responseStream))
            {
                StatusCode = (int)response.StatusCode;
                Response = new SqlChars(streamReader.ReadToEnd());
            }
        }
        catch (WebException e)
        {
            StatusCode = (int)e.Status;
            Response = new SqlChars(e.Message);
        }
    }
}
