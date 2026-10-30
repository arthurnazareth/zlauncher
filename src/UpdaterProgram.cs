#if STANDALONE_UPDATER
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ZLauncherUpdates {
static class UpdaterProgram {
    static string Value(string[] args,string key) { int i=Array.IndexOf(args,key); return i>=0 && i+1<args.Length?args[i+1]:null; }
    [STAThread] static int Main(string[] args) {
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if(args.Contains("--self-test")) return UpdateTests.Run(Value(args,"--output")??Path.Combine(Path.GetTempPath(),"ZLauncher-update-tests"));
            if(args.Contains("--network-test")) return UpdateTests.Network(Value(args,"--output"));
            string target=Value(args,"--target");
            if(target==null && UpdateCore.ExistingLauncher(AppDomain.CurrentDomain.BaseDirectory)!=null) target=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
            // Always execute from a separate folder so the installed updater can itself be replaced.
            if(!args.Contains("--worker")) {
                string work=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZLauncher","Updater",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
                string exe=Path.Combine(work,"ZLauncher.Updater.exe"); File.Copy(Application.ExecutablePath,exe);
                using(var config=Assembly.GetExecutingAssembly().GetManifestResourceStream("updater.config")) using(var output=File.Create(exe+".config")) config.CopyTo(output);
                Process.Start(new ProcessStartInfo(exe,"--worker"+(target==null?"":" --target \""+Path.GetFullPath(target).TrimEnd('\\')+"\"")+(args.Contains("--auto")?" --auto":"")+(Value(args,"--wait-pid")==null?"":" --wait-pid "+Int32.Parse(Value(args,"--wait-pid")))) { UseShellExecute=false,WorkingDirectory=work }); return 0;
            }
            int pid=0; Int32.TryParse(Value(args,"--wait-pid"),out pid);
            bool first; using(var mutex=new Mutex(true,"Local\\ZLauncher_Updater",out first)) {
                if(!first) { MessageBox.Show("O Updater já está aberto.","ZLauncher"); return 0; }
                Application.Run(new UpdaterForm(target,pid,args.Contains("--auto"))); return 0;
            }
        } catch(Exception e) { MessageBox.Show(e.Message,"Não foi possível atualizar",MessageBoxButtons.OK,MessageBoxIcon.Warning); return 1; }
    }
}
sealed class UpdaterForm : Form {
    readonly TextBox target=new TextBox(),output=new TextBox();
    readonly Button choose=new Button(),start=new Button();
    readonly Label status=new Label(); readonly ProgressBar progress=new ProgressBar();
    readonly CheckBox reopen=new CheckBox(); CancellationTokenSource operation; bool applying;
    readonly int waitPid; readonly bool auto;
    public UpdaterForm(string folder,int pid,bool automatic) {
        waitPid=pid; auto=automatic; Text="ZLauncher • Atualização"; ClientSize=new Size(700,490); MinimumSize=MaximumSize=Size;
        BackColor=Color.FromArgb(18,20,22); ForeColor=Color.FromArgb(235,238,239); Font=new Font("Segoe UI",10); StartPosition=FormStartPosition.CenterScreen;
        try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch {}
        var heading=new Label{Text="Atualizar ZLauncher",Font=new Font("Segoe UI Semibold",23),Bounds=new Rectangle(28,24,640,45)};
        var intro=new Label{Text="Baixa a versão mais recente do GitHub. Seus mods, perfis e saves são preservados.",ForeColor=Color.Silver,Bounds=new Rectangle(30,79,635,44)};
        var label=new Label{Text="PASTA DO LAUNCHER",Font=new Font("Segoe UI",9),ForeColor=Color.Silver,Bounds=new Rectangle(30,132,600,22)};
        target.SetBounds(30,158,520,28); target.Text=folder??""; target.BackColor=Color.FromArgb(32,35,38); target.ForeColor=ForeColor; target.BorderStyle=BorderStyle.FixedSingle;
        choose.Text="Escolher…"; choose.SetBounds(560,155,110,34); choose.Click+=(s,e)=> { using(var dialog=new OpenFileDialog { Title="Selecione o launcher instalado",Filter="Launcher instalado|ZLauncher.exe;bxLauncher.exe",CheckFileExists=true }) if(dialog.ShowDialog(this)==DialogResult.OK) target.Text=Path.GetDirectoryName(dialog.FileName); };
        status.Text="Selecione a pasta que contém seu launcher atual."; status.SetBounds(30,211,640,43);
        progress.SetBounds(30,261,640,10);
        output.SetBounds(30,285,640,105); output.Multiline=true; output.ReadOnly=true; output.ScrollBars=ScrollBars.Vertical; output.BackColor=Color.FromArgb(25,28,30); output.ForeColor=Color.Silver; output.BorderStyle=BorderStyle.None; output.Font=new Font("Consolas",9);
        reopen.Text="Abrir o launcher ao terminar"; reopen.Checked=true; reopen.SetBounds(30,419,320,30);
        start.Text="Buscar e atualizar"; start.SetBounds(442,411,228,44); start.BackColor=Color.FromArgb(153,210,78); start.ForeColor=Color.FromArgb(20,25,15); start.FlatStyle=FlatStyle.Flat; start.Click+=async(s,e)=> { if(operation!=null) { if(!applying) operation.Cancel(); } else await Run(); };
        Controls.AddRange(new Control[]{heading,intro,label,target,choose,status,progress,output,reopen,start});
        FormClosing+=(s,e)=> { if(operation!=null) { e.Cancel=true; if(!applying) { operation.Cancel(); status.Text="Cancelando download…"; } else status.Text="Concluindo a troca de arquivos. Aguarde um instante."; } };
        Shown+=async(s,e)=> { if(auto) await Run(); };
    }
    void Log(string text) { output.AppendText(text+Environment.NewLine); }
    async Task Run() {
        string root;
        try { root=Path.GetFullPath(target.Text.Trim()).TrimEnd('\\'); if(UpdateCore.ExistingLauncher(root)==null) throw new IOException("Escolha a pasta onde está ZLauncher.exe ou bxLauncher.exe."); }
        catch(Exception e) { status.Text=e.Message; return; }
        operation=new CancellationTokenSource(); target.Enabled=choose.Enabled=false; start.Text="Cancelar"; progress.Value=0; output.Clear();
        try {
            status.Text="Consultando a versão estável no GitHub…"; Log(UpdateCore.Repository);
            if(waitPid>0) {
                try { using(var old=Process.GetProcessById(waitPid)) { for(int i=0;!old.HasExited && i<120;i++) { operation.Token.ThrowIfCancellationRequested(); await Task.Delay(500,operation.Token); } if(!old.HasExited) throw new IOException("O launcher continua aberto. Feche os avisos dele e tente novamente."); } }
                catch(ArgumentException) {}
            }
            if(UpdateCore.LauncherRunning(root)) throw new IOException("Feche o launcher instalado e clique em Buscar e atualizar novamente.");
            await Task.Run(()=>UpdateCore.Recover(root));
            Version installed=UpdateCore.InstalledVersion(root); var update=await UpdateCore.Check(operation.Token);
            Log("Versão instalada: "+installed+" • disponível: "+update.Manifest.Version);
            if(installed>=UpdateCore.VersionOf(update.Manifest.Version)) { status.Text="Seu launcher já está atualizado."; return; }
            var reporter=new Progress<Tuple<int,string>>(p=> { progress.Value=Math.Max(0,Math.Min(100,p.Item1)); status.Text=p.Item2; });
            string work=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZLauncher","Updates",Guid.NewGuid().ToString("N"));
            string stage=await UpdateCore.Download(update,work,operation.Token,(n,s)=>((IProgress<Tuple<int,string>>)reporter).Report(Tuple.Create(n,s)));
            operation.Token.ThrowIfCancellationRequested(); applying=true; start.Enabled=false; status.Text="Aplicando atualização e guardando a versão anterior…";
            string backup=await Task.Run(()=>UpdateCore.Apply(root,stage,update.Manifest));
            Log("Backup: "+backup); Log("Mods, perfis, versões do jogo e saves preservados."); progress.Value=100; status.Text="ZLauncher "+update.Manifest.Version+" atualizado com sucesso.";
            if(reopen.Checked) {
                try { Process.Start(new ProcessStartInfo(Path.Combine(root,"ZLauncher.exe")) { WorkingDirectory=root,UseShellExecute=true }); }
                catch(Exception e) { Log("Atualização concluída; abra o launcher manualmente. "+e.Message); }
            }
        } catch(OperationCanceledException) { status.Text="Atualização cancelada. O launcher instalado foi preservado."; }
        catch(Exception e) { status.Text="Não foi possível concluir a atualização."; Log(e.Message); }
        finally { operation.Dispose(); operation=null; applying=false; start.Enabled=true; start.Text="Buscar e atualizar"; target.Enabled=choose.Enabled=true; }
    }
}
}
#endif
