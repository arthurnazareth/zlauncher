using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class Asset { public string Name,Sha256; public long Bytes; public bool Part; }
public class Manifest { public string BaseUrl,GameBaseUrl,GameName,GameSha256; public long GameBytes; public List<Asset> Files; }
public class DownloadInstaller : Form {
    readonly TextBox destination=new TextBox(); readonly Label status=new Label();
    readonly ProgressBar progress=new ProgressBar(); readonly Button start=new Button();
    readonly Button browse=new Button(); readonly Manifest manifest;
    CancellationTokenSource cancellation; long completed,total;
    [STAThread] public static int Main(string[] args) {
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("release-manifest.json"))
            using(var reader=new StreamReader(stream)) {
                var m=new JavaScriptSerializer().Deserialize<Manifest>(reader.ReadToEnd()); ValidateManifest(m);
                if(args.Length==1 && args[0]=="--validate") { Console.WriteLine("Manifesto válido; nenhuma transferência iniciada."); return 0; }
                Application.Run(new DownloadInstaller(m)); return 0;
            }
        } catch(Exception ex) { if(args.Length==0) MessageBox.Show(ex.Message,"ZLauncher"); return 1; }
    }
    public static void ValidateManifest(Manifest m) {
        if(m==null || m.BaseUrl!="https://github.com/arthurnazareth/zlauncher/releases/download/v3.3.0/" || m.GameBaseUrl!="https://github.com/arthurnazareth/zlauncher/releases/download/v3.2.2/" || m.GameName!="ZLauncher-42.21.bxdata" || m.Files==null || m.Files.Count!=6) throw new IOException("Manifesto inválido.");
        foreach(var a in m.Files) if(Path.GetFileName(a.Name)!=a.Name || a.Name.Contains(":") || a.Bytes<=0 || a.Bytes>=2L*1024*1024*1024 || !System.Text.RegularExpressions.Regex.IsMatch(a.Sha256??"","^[A-Fa-f0-9]{64}$")) throw new IOException("Arquivo inválido no manifesto.");
        if(m.Files.Select(a=>a.Name).Distinct().Count()!=m.Files.Count || m.Files.Where(a=>a.Part).Sum(a=>a.Bytes)!=m.GameBytes || !System.Text.RegularExpressions.Regex.IsMatch(m.GameSha256??"","^[A-Fa-f0-9]{64}$")) throw new IOException("Partes inconsistentes.");
    }
    DownloadInstaller(Manifest m) {
        manifest=m; Text="ZLauncher • Preparar instalação"; ClientSize=new Size(640,350); MinimumSize=MaximumSize=Size;
        StartPosition=FormStartPosition.CenterScreen; BackColor=Color.FromArgb(23,25,27); ForeColor=Color.WhiteSmoke; Font=new Font("Segoe UI",10);
        var title=new Label { Text="Instalar ZLauncher",Font=new Font("Segoe UI Semibold",24),Bounds=new Rectangle(28,22,580,48) };
        var info=new Label { Text="Launcher 3.3.0 + Build 42.21\nBaixa cerca de 5,3 GB do GitHub. Reserve 11 GB para preparar o pacote.",ForeColor=Color.Silver,Bounds=new Rectangle(30,80,580,55) };
        destination.SetBounds(30,150,470,29); destination.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Instalador ZLauncher");
        browse.Text="Escolher…"; browse.SetBounds(510,148,100,33); browse.Click+=(s,e)=> { using(var d=new FolderBrowserDialog()) { d.SelectedPath=destination.Text; if(d.ShowDialog(this)==DialogResult.OK) destination.Text=d.SelectedPath; } };
        status.Text="Escolha onde guardar o instalador completo."; status.SetBounds(30,200,580,42);
        progress.SetBounds(30,248,580,12); start.Text="Baixar e instalar"; start.SetBounds(410,282,200,42);
        start.BackColor=Color.FromArgb(174,211,113); start.ForeColor=Color.FromArgb(20,25,15); start.FlatStyle=FlatStyle.Flat;
        start.Click+=async(s,e)=>await Run(); Controls.AddRange(new Control[]{title,info,destination,browse,status,progress,start});
        FormClosing+=(s,e)=> { if(cancellation!=null) cancellation.Cancel(); };
    }
    void Update(string text,int percent) { if(IsDisposed || !IsHandleCreated) return; BeginInvoke((Action)(()=> { status.Text=text; progress.Value=Math.Max(0,Math.Min(100,percent)); })); }
    static string Digest(string path,CancellationToken token) {
        using(var sha=SHA256.Create()) using(var input=File.OpenRead(path)) { byte[] b=new byte[4*1024*1024]; int n; while((n=input.Read(b,0,b.Length))>0) { token.ThrowIfCancellationRequested(); sha.TransformBlock(b,0,n,null,0); } sha.TransformFinalBlock(new byte[0],0,0); return BitConverter.ToString(sha.Hash).Replace("-",""); }
    }
    static bool Valid(string path,long bytes,string hash,CancellationToken token) { return File.Exists(path) && new FileInfo(path).Length==bytes && Digest(path,token).Equals(hash,StringComparison.OrdinalIgnoreCase); }
    static void Commit(string pending,string target) {
        if(File.Exists(target)) File.Replace(pending,target,target+".previous-"+DateTime.Now.ToString("yyyyMMddHHmmss")); else File.Move(pending,target);
    }
    void Fetch(Asset asset,string folder,CancellationToken token) {
        string path=Path.Combine(folder,asset.Name);
        Update("Conferindo "+asset.Name,(int)(completed*85/total));
        if(Valid(path,asset.Bytes,asset.Sha256,token)) { completed+=asset.Bytes; return; }
        string temp=path+".partial";
        for(int attempt=1;attempt<=3;attempt++) {
            try {
                var request=(HttpWebRequest)WebRequest.Create((asset.Part?manifest.GameBaseUrl:manifest.BaseUrl)+Uri.EscapeDataString(asset.Name)); request.UserAgent="ZLauncher-Installer/3.3.0"; request.Timeout=60000; request.ReadWriteTimeout=60000;
                using(token.Register(()=>request.Abort()))
                using(var response=request.GetResponse()) using(var from=response.GetResponseStream()) using(var to=File.Create(temp)) {
                    byte[] buffer=new byte[1024*1024]; long read=0; int n; var timer=Stopwatch.StartNew();
                    while((n=from.Read(buffer,0,buffer.Length))>0) {
                        token.ThrowIfCancellationRequested(); read+=n; if(read>asset.Bytes) throw new IOException("Download maior que o esperado."); to.Write(buffer,0,n);
                        if(timer.ElapsedMilliseconds>250) { Update("Baixando "+asset.Name+" • "+(read/1048576)+" / "+(asset.Bytes/1048576)+" MB",(int)((completed+read)*85/total)); timer.Restart(); }
                    }
                }
                if(!Valid(temp,asset.Bytes,asset.Sha256,token)) throw new IOException("Download incompleto ou com integridade inválida: "+asset.Name);
                Commit(temp,path); completed+=asset.Bytes; return;
            } catch(Exception ex) {
                token.ThrowIfCancellationRequested();
                if(!(ex is IOException) && !(ex is WebException)) throw;
                if(attempt==3) throw;
                Update("Conexão interrompida. Tentando novamente…",(int)(completed*85/total));
            }
        }
    }
    void Prepare(string folder,CancellationToken token) {
        Directory.CreateDirectory(folder); string cache=Path.Combine(folder,".zlauncher-downloads"); Directory.CreateDirectory(cache);
        string game=Path.Combine(folder,manifest.GameName); Update("Conferindo pacote existente…",0);
        bool haveGame=Valid(game,manifest.GameBytes,manifest.GameSha256,token);
        var files=manifest.Files.Where(a=>!haveGame || !a.Part).ToList(); total=files.Sum(a=>a.Bytes); completed=0;
        if(new DriveInfo(Path.GetPathRoot(folder)).AvailableFreeSpace<(haveGame?100L*1024*1024:manifest.GameBytes*2+100L*1024*1024)) throw new IOException("Espaço insuficiente para preparar o instalador.");
        foreach(var asset in files) Fetch(asset,asset.Part?cache:folder,token);
        if(!haveGame) {
            string pending=game+".partial"; long written=0;
            using(var output=File.Create(pending)) foreach(var asset in files.Where(a=>a.Part).OrderBy(a=>a.Name)) using(var input=File.OpenRead(Path.Combine(cache,asset.Name))) {
                byte[] buffer=new byte[4*1024*1024]; int n;
                while((n=input.Read(buffer,0,buffer.Length))>0) { token.ThrowIfCancellationRequested(); output.Write(buffer,0,n); written+=n; Update("Reunindo o pacote do jogo…",85+(int)(written*10/manifest.GameBytes)); }
            }
            Update("Verificando a integridade do pacote completo…",95);
            if(!Valid(pending,manifest.GameBytes,manifest.GameSha256,token)) throw new IOException("O pacote final não passou na verificação. As partes foram preservadas.");
            Commit(pending,game);
            foreach(var asset in files.Where(a=>a.Part)) File.Delete(Path.Combine(cache,asset.Name));
        }
        Update("Pacote pronto. Abrindo o instalador…",100);
    }
    async Task Run() {
        string folder; try { folder=Path.GetFullPath(destination.Text.Trim()); } catch { MessageBox.Show("Escolha uma pasta válida."); return; }
        start.Enabled=browse.Enabled=destination.Enabled=false; cancellation=new CancellationTokenSource();
        try {
            await Task.Run(()=>Prepare(folder,cancellation.Token));
            Process.Start(new ProcessStartInfo(Path.Combine(folder,"ZLauncher-Setup.exe")) { WorkingDirectory=folder,UseShellExecute=true }); Close();
        } catch(OperationCanceledException) { }
        catch(Exception ex) { status.Text="Não foi possível concluir. Os downloads completos foram preservados."; MessageBox.Show(ex.Message,"ZLauncher",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        finally { cancellation.Dispose(); cancellation=null; if(!IsDisposed) start.Enabled=browse.Enabled=destination.Enabled=true; }
    }
}
