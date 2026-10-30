using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using File = ZomboidLauncher.LongFile;
using Directory = ZomboidLauncher.LongDirectory;

namespace ZLauncherUpdates {
public sealed class UpdateFile { public string Path { get; set; } public long Bytes { get; set; } public string Sha256 { get; set; } }
public sealed class UpdateManifest {
    public int Format { get; set; } public string Version { get; set; }
    public long PackageBytes { get; set; } public string PackageSha256 { get; set; }
    public List<UpdateFile> Files { get; set; }
}
public sealed class ReleaseAsset { public string name { get; set; } public string browser_download_url { get; set; } public long size { get; set; } }
public sealed class ReleaseInfo { public string tag_name { get; set; } public bool draft { get; set; } public bool prerelease { get; set; } public List<ReleaseAsset> assets { get; set; } }
public sealed class AvailableUpdate { public UpdateManifest Manifest; public string PackageUrl; }
public sealed class BackupFile { public string Path { get; set; } public bool Existed { get; set; } public string Sha256 { get; set; } }
public sealed class UpdateJournal { public string State { get; set; } public List<BackupFile> Files { get; set; } }
public static class UpdateCore {
    public const string Repository="https://github.com/arthurnazareth/zlauncher";
    public const string LatestApi="https://api.github.com/repos/arthurnazareth/zlauncher/releases/latest";
    public static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=1024*1024 };
    static readonly UTF8Encoding Utf8=new UTF8Encoding(false);
    public static Version VersionOf(string text) {
        if(!Regex.IsMatch(text??"",@"^\d+\.\d+\.\d+(?:\.\d+)?$")) throw new IOException("Versão de atualização inválida.");
        var v=new Version(text); return new Version(v.Major,v.Minor,v.Build,Math.Max(0,v.Revision));
    }
    public static string ExistingLauncher(string root) { return new[]{"ZLauncher.exe","bxLauncher.exe"}.Select(n=>Path.Combine(root,n)).FirstOrDefault(File.Exists); }
    static Version BinaryVersion(string path) {
        // GetAssemblyName reads metadata without loading or executing the launcher.
        string normal=ZomboidLauncher.LongPaths.Normal(path);
        if(normal.Length<240) return AssemblyName.GetAssemblyName(normal).Version;
        string temp=Path.Combine(Path.GetTempPath(),"zlauncher-version-"+Guid.NewGuid().ToString("N")+".exe");
        try { File.Copy(path,temp,false); return AssemblyName.GetAssemblyName(temp).Version; }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    public static Version InstalledVersion(string root) {
        string exe=ExistingLauncher(root); if(exe==null) throw new IOException("Escolha a pasta onde está ZLauncher.exe ou bxLauncher.exe.");
        return BinaryVersion(exe);
    }
    public static bool Allowed(string path) {
        if(String.IsNullOrEmpty(path) || path.Contains("\\") || path.Contains(":") || path.StartsWith("/") || path.Split('/').Any(p=>p=="" || p=="." || p=="..")) return false;
        if(new[]{"ZLauncher.exe","ZLauncher.exe.config","ZLauncher.Updater.exe","ZLauncher.Updater.exe.config","ModsLauncher/build.ps1","ModsLauncher/LEIA-ME.txt","ModsLauncher/assets/zlauncher.ico","ModsLauncher/assets/zlauncher.png","ModsLauncher/assets/zombiebuddy-2.3.4.zip"}.Contains(path,StringComparer.Ordinal)) return true;
        return Regex.IsMatch(path,@"^ModsLauncher/src/[A-Za-z0-9_-]+\.(cs|manifest)$") || path=="ModsLauncher/src/ZLauncher.exe.config";
    }
    static bool HashValid(string s) { return Regex.IsMatch(s??"",@"^[0-9A-Fa-f]{64}$"); }
    public static void Validate(UpdateManifest m) {
        if(m==null || m.Format!=1 || m.PackageBytes<=0 || m.PackageBytes>64L*1024*1024 || !HashValid(m.PackageSha256) || m.Files==null || m.Files.Count<4 || m.Files.Count>150) throw new IOException("Manifesto de atualização inválido.");
        VersionOf(m.Version);
        if(m.Files.Any(f=>f==null || !Allowed(f.Path) || f.Bytes<0 || f.Bytes>32L*1024*1024 || !HashValid(f.Sha256)) || m.Files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=m.Files.Count || m.Files.Sum(f=>f.Bytes)>64L*1024*1024) throw new IOException("O pacote tentou incluir um arquivo fora dos componentes do launcher.");
        foreach(string required in new[]{"ZLauncher.exe","ZLauncher.exe.config","ZLauncher.Updater.exe","ZLauncher.Updater.exe.config"}) if(!m.Files.Any(f=>f.Path==required)) throw new IOException("Atualização incompleta: "+required);
    }
    static HttpClient Client() {
        var client=new HttpClient { Timeout=TimeSpan.FromMinutes(8) }; client.DefaultRequestHeaders.UserAgent.ParseAdd("ZLauncher-Updater/3.4.0"); return client;
    }
    static async Task<byte[]> ReadUrl(string url,long limit,CancellationToken token,Action<int,string> progress) {
        using(var client=Client()) using(var response=await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token)) {
            if(response.StatusCode==HttpStatusCode.Forbidden || (int)response.StatusCode==429) throw new IOException("O GitHub limitou as consultas. Tente novamente mais tarde.");
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>limit) throw new IOException("Resposta maior que o permitido.");
            using(var input=await response.Content.ReadAsStreamAsync()) using(var output=new MemoryStream()) {
                var buffer=new byte[65536]; int n; long total=response.Content.Headers.ContentLength??limit;
                while((n=await input.ReadAsync(buffer,0,buffer.Length,token))>0) {
                    if(output.Length+n>limit) throw new IOException("Download maior que o esperado."); output.Write(buffer,0,n);
                    if(progress!=null) progress((int)(output.Length*100/Math.Max(1,total)),"Baixando atualização • "+(output.Length/1048576)+" MB");
                }
                return output.ToArray();
            }
        }
    }
    public static async Task<AvailableUpdate> Check(CancellationToken token) {
        var release=Json.Deserialize<ReleaseInfo>(Encoding.UTF8.GetString(await ReadUrl(LatestApi,1024*1024,token,null)));
        if(release==null || release.draft || release.prerelease || release.assets==null || !Regex.IsMatch(release.tag_name??"",@"^v\d+\.\d+\.\d+$")) throw new IOException("A release estável do GitHub é inválida.");
        string prefix=Repository+"/releases/download/"+release.tag_name+"/";
        var manifest=release.assets.SingleOrDefault(a=>a.name=="launcher-update.json"); var package=release.assets.SingleOrDefault(a=>a.name=="ZLauncher-update.zip");
        if(manifest==null || package==null) throw new IOException("Esta release ainda não oferece atualização direta. Tente novamente mais tarde.");
        if(manifest.browser_download_url!=prefix+"launcher-update.json" || package.browser_download_url!=prefix+"ZLauncher-update.zip") throw new IOException("Origem da atualização diferente do repositório configurado.");
        var m=Json.Deserialize<UpdateManifest>(Encoding.UTF8.GetString(await ReadUrl(manifest.browser_download_url,1024*1024,token,null)).TrimStart('\uFEFF'));
        Validate(m); if("v"+m.Version!=release.tag_name || package.size!=m.PackageBytes) throw new IOException("Manifesto não corresponde à release.");
        return new AvailableUpdate { Manifest=m,PackageUrl=package.browser_download_url };
    }
    public static string Hash(string path) { using(var input=File.OpenRead(path)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-",""); }
    static string Digest(byte[] bytes) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-",""); }
    public static async Task<string> Download(AvailableUpdate update,string work,CancellationToken token,Action<int,string> progress) {
        Validate(update.Manifest); string expected=Repository+"/releases/download/v"+update.Manifest.Version+"/ZLauncher-update.zip";
        if(update.PackageUrl!=expected) throw new IOException("URL de atualização inválida.");
        byte[] bytes=await ReadUrl(update.PackageUrl,update.Manifest.PackageBytes,token,progress);
        if(bytes.LongLength!=update.Manifest.PackageBytes || !Digest(bytes).Equals(update.Manifest.PackageSha256,StringComparison.OrdinalIgnoreCase)) throw new IOException("O download falhou na conferência SHA-256. Nenhum arquivo instalado foi alterado.");
        Directory.CreateDirectory(work); string zip=Path.Combine(work,"package.zip"); File.WriteAllBytes(zip,bytes); token.ThrowIfCancellationRequested();
        string stage=Path.Combine(work,"files"); Extract(zip,stage,update.Manifest,token); return stage;
    }
    public static void Extract(string zip,string stage,UpdateManifest m,CancellationToken token) {
        Validate(m);
        if(new FileInfo(ZomboidLauncher.LongPaths.IO(zip)).Length!=m.PackageBytes || !Hash(zip).Equals(m.PackageSha256,StringComparison.OrdinalIgnoreCase)) throw new IOException("Pacote inválido.");
        Directory.CreateDirectory(stage); var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var archive=ZipFile.OpenRead(ZomboidLauncher.LongPaths.IO(zip))) {
            foreach(var entry in archive.Entries) {
                token.ThrowIfCancellationRequested(); var f=m.Files.FirstOrDefault(x=>x.Path==entry.FullName);
                if(f==null || !Allowed(entry.FullName) || !seen.Add(entry.FullName) || entry.Length!=f.Bytes) throw new IOException("Arquivo inesperado ou duplicado dentro da atualização.");
                string target=SafePath(stage,f.Path); Directory.CreateDirectory(Path.GetDirectoryName(target));
                using(var input=entry.Open()) using(var output=File.Create(target)) {
                    var buffer=new byte[65536]; int n; long count=0;
                    while((n=input.Read(buffer,0,buffer.Length))>0) { token.ThrowIfCancellationRequested(); count+=n; if(count>f.Bytes) throw new IOException("Arquivo maior que o manifesto."); output.Write(buffer,0,n); }
                    if(count!=f.Bytes) throw new IOException("Arquivo incompleto.");
                }
                if(!Hash(target).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase)) throw new IOException("Arquivo com integridade inválida: "+f.Path);
            }
        }
        if(seen.Count!=m.Files.Count) throw new IOException("Faltam arquivos na atualização.");
        if(VersionOf(BinaryVersion(Path.Combine(stage,"ZLauncher.exe")).ToString())!=VersionOf(m.Version)) throw new IOException("Executável não corresponde à versão anunciada.");
    }
    public static string SafePath(string root,string relative) {
        string fullRoot=Path.GetFullPath(root).TrimEnd('\\','/');
        string path=Path.GetFullPath(Path.Combine(fullRoot,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(fullRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new IOException("Caminho fora da instalação.");
        string current=path;
        while(current!=null) {
            if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("A atualização não pode seguir links de arquivos ou pastas: "+current);
            current=Path.GetDirectoryName(current);
        }
        return path;
    }
    public static bool LauncherRunning(string root) {
        foreach(string name in new[]{"ZLauncher","bxLauncher"}) foreach(var p in Process.GetProcessesByName(name)) using(p) {
            try { if(String.Equals(Path.GetDirectoryName(p.MainModule.FileName),Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) return true; }
            catch(System.ComponentModel.Win32Exception) { return true; } catch(InvalidOperationException) {}
        }
        return false;
    }
    static void ReplaceFrom(string source,string target) {
        Directory.CreateDirectory(Path.GetDirectoryName(target)); string temp=target+".update-"+Guid.NewGuid().ToString("N");
        try { File.Copy(source,temp,false); if(File.Exists(target)) File.Replace(temp,target,null); else File.Move(temp,target); }
        finally { if(File.Exists(temp)) File.Delete(temp); }
    }
    static void WriteJournal(string path,UpdateJournal journal) {
        string temp=path+".tmp"; File.WriteAllText(temp,Json.Serialize(journal),Utf8);
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
    static bool BackupAllowed(string path) { return Allowed(path) || path=="bxLauncher.exe" || path=="bxLauncher.exe.config"; }
    static void Restore(string root,string backup,UpdateJournal journal) {
        if(journal.Files==null || journal.Files.Count>152 || journal.Files.Any(f=>!BackupAllowed(f.Path)) || journal.Files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=journal.Files.Count) throw new IOException("Registro de recuperação inválido.");
        // Validate every backup before restoring any installed file.
        foreach(var f in journal.Files.Where(f=>f.Existed)) if(!HashValid(f.Sha256) || !Hash(SafePath(backup,f.Path)).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase)) throw new IOException("Backup danificado: "+f.Path);
        foreach(var f in journal.Files.AsEnumerable().Reverse()) {
            string dest=SafePath(root,f.Path);
            if(f.Existed) ReplaceFrom(SafePath(backup,f.Path),dest); else if(File.Exists(dest)) File.Delete(dest);
        }
        journal.State="Restored"; WriteJournal(Path.Combine(backup,"transaction.json"),journal);
    }
    public static void Recover(string root) {
        string backups=SafePath(root,"ModsLauncher/update-backups"); if(!Directory.Exists(backups)) return;
        foreach(string folder in Directory.GetDirectories(backups)) {
            string journalPath=SafePath(folder,"transaction.json"); if(!File.Exists(journalPath)) continue;
            var j=Json.Deserialize<UpdateJournal>(File.ReadAllText(journalPath));
            if(j==null) throw new IOException("Registro de atualização inválido: "+journalPath);
            if(j.State=="Applying") Restore(root,folder,j);
        }
    }
    public static string Apply(string root,string stage,UpdateManifest m,Action<int> afterFile=null) {
        Validate(m); if(ExistingLauncher(root)==null) throw new IOException("Launcher instalado não encontrado.");
        if(LauncherRunning(root)) throw new IOException("Feche o ZLauncher antes de aplicar a atualização.");
        Recover(root); InstalledVersion(root);
        var plan=m.Files.Select(f=>new KeyValuePair<string,string>(f.Path,f.Path)).ToList();
        if(File.Exists(Path.Combine(root,"bxLauncher.exe"))) { plan.Add(new KeyValuePair<string,string>("bxLauncher.exe","ZLauncher.exe")); plan.Add(new KeyValuePair<string,string>("bxLauncher.exe.config","ZLauncher.exe.config")); }
        foreach(var f in m.Files) {
            string source=SafePath(stage,f.Path); SafePath(root,f.Path);
            if(new FileInfo(ZomboidLauncher.LongPaths.IO(source)).Length!=f.Bytes || !Hash(source).Equals(f.Sha256,StringComparison.OrdinalIgnoreCase)) throw new IOException("A atualização preparada foi alterada: "+f.Path);
        }
        string backup=SafePath(root,"ModsLauncher/update-backups/"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(backup);
        var journal=new UpdateJournal { State="Preparing",Files=new List<BackupFile>() };
        foreach(var item in plan) {
            string target=SafePath(root,item.Key); bool exists=File.Exists(target);
            if(Directory.Exists(target)) throw new IOException("Existe uma pasta no lugar de um arquivo do launcher: "+item.Key);
            var f=new BackupFile { Path=item.Key,Existed=exists };
            if(exists) { string dest=SafePath(backup,item.Key); Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(target,dest,false); f.Sha256=Hash(dest); }
            journal.Files.Add(f);
        }
        string journalFile=Path.Combine(backup,"transaction.json"); journal.State="Applying"; WriteJournal(journalFile,journal);
        try {
            int count=0; foreach(var item in plan) { ReplaceFrom(SafePath(stage,item.Value),SafePath(root,item.Key)); if(afterFile!=null) afterFile(++count); }
            journal.State="Completed"; WriteJournal(journalFile,journal); return backup;
        } catch(Exception error) {
            try { Restore(root,backup,journal); } catch(Exception recovery) { throw new IOException("Atualização interrompida. A recuperação automática ficou pendente; execute o Updater novamente. Backup: "+backup+". "+recovery.Message,error); }
            throw new IOException("Atualização não aplicada. A versão anterior foi restaurada. "+error.Message,error);
        }
    }
}
}
