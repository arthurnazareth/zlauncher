using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ZomboidLauncher {
// Keep normal paths in profiles and UI; use extended absolute paths only at IO boundaries.
public static class LongPaths {
    public static string Normal(string path) {
        if(path!=null && path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase)) return @"\\"+path.Substring(8);
        if(path!=null && path.StartsWith(@"\\?\",StringComparison.OrdinalIgnoreCase)) return path.Substring(4);
        return path;
    }
    public static string IO(string path) {
        if(String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Caminho vazio.","path");
        if(path.StartsWith(@"\\.\") || path.StartsWith(@"\??\")) throw new IOException("Caminho de dispositivo não permitido.");
        string normal=Normal(path);
        if(path.StartsWith(@"\\?\") && !(normal.StartsWith(@"\\") || (normal.Length>=3 && Char.IsLetter(normal[0]) && normal[1]==':' && normal[2]=='\\'))) throw new IOException("Caminho estendido inválido.");
        string full=Path.GetFullPath(normal);
        return full.StartsWith(@"\\")?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full;
    }
}
internal static class LongFile {
    public static bool Exists(string path) { try { return System.IO.File.Exists(LongPaths.IO(path)); } catch(ArgumentException) { return false; } catch(IOException) { return false; } catch(NotSupportedException) { return false; } }
    public static FileAttributes GetAttributes(string path) { return System.IO.File.GetAttributes(LongPaths.IO(path)); }
    public static DateTime GetLastWriteTimeUtc(string path) { return System.IO.File.GetLastWriteTimeUtc(LongPaths.IO(path)); }
    public static string ReadAllText(string path) { return System.IO.File.ReadAllText(LongPaths.IO(path)); }
    public static string[] ReadAllLines(string path) { return System.IO.File.ReadAllLines(LongPaths.IO(path)); }
    public static void WriteAllText(string path,string text,Encoding encoding) { System.IO.File.WriteAllText(LongPaths.IO(path),text,encoding); }
    public static void WriteAllText(string path,string text) { System.IO.File.WriteAllText(LongPaths.IO(path),text); }
    public static void WriteAllBytes(string path,byte[] bytes) { System.IO.File.WriteAllBytes(LongPaths.IO(path),bytes); }
    public static FileStream Create(string path) { return System.IO.File.Create(LongPaths.IO(path)); }
    public static FileStream OpenRead(string path) { return System.IO.File.OpenRead(LongPaths.IO(path)); }
    public static void Copy(string source,string target,bool overwrite=false) { System.IO.File.Copy(LongPaths.IO(source),LongPaths.IO(target),overwrite); }
    public static void Move(string source,string target) { System.IO.File.Move(LongPaths.IO(source),LongPaths.IO(target)); }
    public static void Replace(string source,string target,string backup) { System.IO.File.Replace(LongPaths.IO(source),LongPaths.IO(target),backup==null?null:LongPaths.IO(backup)); }
    public static void Delete(string path) { System.IO.File.Delete(LongPaths.IO(path)); }
}
internal static class LongDirectory {
    public static bool Exists(string path) { try { return System.IO.Directory.Exists(LongPaths.IO(path)); } catch(ArgumentException) { return false; } catch(IOException) { return false; } catch(NotSupportedException) { return false; } }
    public static DirectoryInfo CreateDirectory(string path) { return System.IO.Directory.CreateDirectory(LongPaths.IO(path)); }
    public static string[] GetFiles(string path,string pattern="*") { return System.IO.Directory.GetFiles(LongPaths.IO(path),pattern).Select(LongPaths.Normal).ToArray(); }
    public static string[] GetDirectories(string path) { return System.IO.Directory.GetDirectories(LongPaths.IO(path)).Select(LongPaths.Normal).ToArray(); }
    public static void Delete(string path,bool recursive) { System.IO.Directory.Delete(LongPaths.IO(path),recursive); }
    public static void Move(string source,string target) { System.IO.Directory.Move(LongPaths.IO(source),LongPaths.IO(target)); }
}
}
