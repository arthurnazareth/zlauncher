#if STANDALONE_UPDATER
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ZLauncherUpdates {
static class UpdateTests {
    static List<string> log;
    static void Check(bool ok,string text) { if(!ok) throw new Exception("FALHOU: "+text); log.Add("OK "+text); }
    static void Reject(Action action,string label) { bool fail=false; try { action(); } catch { fail=true; } Check(fail,label); }
    static string Put(string root,string file,string value) { string path=Path.Combine(root,file.Replace('/',Path.DirectorySeparatorChar)); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,value); return path; }
    static void Fixture(string root) { Directory.CreateDirectory(root); File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(root,"ZLauncher.exe")); Put(root,"ZLauncher.exe.config","old config"); }
    static UpdateManifest Package(string work,out string zip) {
        string files=Path.Combine(work,"payload"); Directory.CreateDirectory(files);
        foreach(string name in new[]{"ZLauncher.exe","ZLauncher.Updater.exe"}) File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(files,name));
        Put(files,"ZLauncher.exe.config","new config"); Put(files,"ZLauncher.Updater.exe.config","new config"); Put(files,"ModsLauncher/src/UpdateCore.cs","fixture source");
        zip=Path.Combine(work,"update.zip"); ZipFile.CreateFromDirectory(files,zip);
        return new UpdateManifest { Format=1,Version="3.4.0",PackageBytes=new FileInfo(zip).Length,PackageSha256=UpdateCore.Hash(zip),Files=Directory.GetFiles(files,"*",SearchOption.AllDirectories).Select(f=>new UpdateFile { Path=f.Substring(files.Length+1).Replace('\\','/'),Bytes=new FileInfo(f).Length,Sha256=UpdateCore.Hash(f) }).ToList() };
    }
    public static int Run(string output) {
        Directory.CreateDirectory(output); string area=Path.Combine(output,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(area); log=new List<string>();
        try {
            string zip; var manifest=Package(area,out zip); UpdateCore.Validate(manifest);
            Check(UpdateCore.VersionOf("3.4.0")==UpdateCore.VersionOf("3.4.0.0"),"compara versões com e sem quarto componente");
            foreach(string path in new[]{"../outside","/absolute","C:/absolute","ModsLauncher/settings.json","ModsLauncher/versions.json","ModsLauncher/userdata/save.bin","bxVersions/game.exe","ModsLauncher/assets/other.exe","ModsLauncher/src/../settings.json"}) Check(!UpdateCore.Allowed(path),"recusa substituir "+path);
            string stage=Path.Combine(area,"stage"); UpdateCore.Extract(zip,stage,manifest,CancellationToken.None);
            Check(File.Exists(Path.Combine(stage,"ZLauncher.exe")),"extrai pacote completo e confere hashes");
            var poisoned=UpdateCore.Json.Deserialize<UpdateManifest>(UpdateCore.Json.Serialize(manifest)); poisoned.Files[0].Path="ModsLauncher/settings.json";
            Reject(()=>UpdateCore.Validate(poisoned),"manifesto com configurações pessoais é recusado");
            poisoned=UpdateCore.Json.Deserialize<UpdateManifest>(UpdateCore.Json.Serialize(manifest)); poisoned.Files.Add(poisoned.Files[0]);
            Reject(()=>UpdateCore.Validate(poisoned),"manifesto com caminhos duplicados é recusado");
            var canceled=new CancellationTokenSource(); canceled.Cancel(); Reject(()=>UpdateCore.Extract(zip,Path.Combine(area,"canceled"),manifest,canceled.Token),"download cancelado não produz atualização aplicável");
            string badZip=Path.Combine(area,"bad.zip"); File.Copy(zip,badZip); using(var f=new FileStream(badZip,FileMode.Append)) f.WriteByte(0);
            Reject(()=>UpdateCore.Extract(badZip,Path.Combine(area,"bad"),manifest,CancellationToken.None),"arquivo compactado adulterado é recusado");
            string root=Path.Combine(area,"installation"); Fixture(root);
            var protectedFiles=new Dictionary<string,string>(); foreach(string path in new[]{"ModsLauncher/settings.json","ModsLauncher/versions.json","ModsLauncher/userdata/build-42/mods/custom/42/mod.info","ModsLauncher/userdata/build-42/Saves/world.bin","mods/local/mod.info","bxVersions/42.21/projectzomboid.jar"}) { string p=Put(root,path,"preserve "+path); protectedFiles[p]=UpdateCore.Hash(p); }
            string backup=UpdateCore.Apply(root,stage,manifest);
            Check(File.ReadAllText(Path.Combine(root,"ZLauncher.exe.config"))=="new config","aplica arquivos novos");
            Check(protectedFiles.All(f=>UpdateCore.Hash(f.Key)==f.Value),"mods, perfis, versões e saves permanecem idênticos byte a byte");
            Check(File.ReadAllText(Path.Combine(backup,"ZLauncher.exe.config"))=="old config","backup preserva arquivos da versão anterior");
            string fail=Path.Combine(area,"rollback"); Fixture(fail);
            Reject(()=>UpdateCore.Apply(fail,stage,manifest,n=> { if(n==3) throw new IOException("falha simulada"); }),"falha durante a troca é reportada");
            Check(File.ReadAllText(Path.Combine(fail,"ZLauncher.exe.config"))=="old config" && !File.Exists(Path.Combine(fail,"ZLauncher.Updater.exe")),"rollback restaura antigos e remove apenas arquivos novos da transação");
            string interrupted=Path.Combine(area,"interrupted"); Fixture(interrupted);
            string interruptedBackup=Path.Combine(interrupted,"ModsLauncher","update-backups","interrupted"); Directory.CreateDirectory(interruptedBackup);
            File.Copy(Path.Combine(interrupted,"ZLauncher.exe.config"),Path.Combine(interruptedBackup,"ZLauncher.exe.config"));
            var journal=new UpdateJournal{State="Applying",Files=new List<BackupFile>{new BackupFile{Path="ZLauncher.exe.config",Existed=true,Sha256=UpdateCore.Hash(Path.Combine(interruptedBackup,"ZLauncher.exe.config"))}}};
            File.WriteAllText(Path.Combine(interruptedBackup,"transaction.json"),UpdateCore.Json.Serialize(journal)); File.WriteAllText(Path.Combine(interrupted,"ZLauncher.exe.config"),"interrupted config"); UpdateCore.Recover(interrupted);
            Check(File.ReadAllText(Path.Combine(interrupted,"ZLauncher.exe.config"))=="old config","recupera atualização interrompida antes de tentar novamente");
            string legacy=Path.Combine(area,"legacy"); Fixture(legacy); File.Move(Path.Combine(legacy,"ZLauncher.exe"),Path.Combine(legacy,"bxLauncher.exe")); UpdateCore.Apply(legacy,stage,manifest);
            Check(UpdateCore.Hash(Path.Combine(legacy,"bxLauncher.exe"))==UpdateCore.Hash(Path.Combine(legacy,"ZLauncher.exe")),"atualiza também o executável legado preservando atalhos bxLauncher");
            string longRoot=Path.Combine(area,new string('a',80),new string('b',80),new string('c',80));
            ZomboidLauncher.LongDirectory.CreateDirectory(longRoot); ZomboidLauncher.LongFile.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(longRoot,"ZLauncher.exe"));
            UpdateCore.Apply(longRoot,stage,manifest);
            Check(UpdateCore.InstalledVersion(longRoot)==UpdateCore.VersionOf(manifest.Version),"atualiza instalação em caminho com mais de 260 caracteres");
            string tampered=Put(stage,"ZLauncher.exe.config","changed"); Reject(()=>UpdateCore.Apply(root,stage,manifest),"revalida arquivos preparados antes de modificar a instalação");
            Check(protectedFiles.All(f=>UpdateCore.Hash(f.Key)==f.Value),"falhas também preservam dados do jogador");
            log.Add("TOTAL: "+log.Count+" verificações aprovadas."); return 0;
        } catch(Exception e) { log.Add(e.ToString()); return 1; }
        finally { File.WriteAllLines(Path.Combine(output,"update-tests.txt"),log,Encoding.UTF8); }
    }
    public static int Network(string output) {
        if(String.IsNullOrEmpty(output)) return 1; Directory.CreateDirectory(output);
        try {
            using(var cts=new CancellationTokenSource(TimeSpan.FromMinutes(5))) {
                var update=UpdateCore.Check(cts.Token).GetAwaiter().GetResult(); string stage=UpdateCore.Download(update,output,cts.Token,null).GetAwaiter().GetResult();
                string root=Path.Combine(output,"installation"); Fixture(root); var protectedFile=Put(root,"ModsLauncher/settings.json","{\"Profiles\":[\"preserve\"]}"); string hash=UpdateCore.Hash(protectedFile);
                UpdateCore.Apply(root,stage,update.Manifest);
                if(UpdateCore.Hash(protectedFile)!=hash || UpdateCore.InstalledVersion(root)!=UpdateCore.VersionOf(update.Manifest.Version)) throw new Exception("Verificação final falhou.");
                File.WriteAllText(Path.Combine(output,"network-test.txt"),"OK: release pública consultada, pacote baixado, hashes conferidos e atualização aplicada em instalação isolada. Perfis preservados. Versão: "+update.Manifest.Version); return 0;
            }
        } catch(Exception e) { File.WriteAllText(Path.Combine(output,"network-test.txt"),e.ToString()); return 1; }
    }
}
}
#endif
