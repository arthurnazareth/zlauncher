# ZLauncher

Launcher para Project Zomboid, com perfis de mods, integração com a Workshop e seleção de versões.

## Atualizar sem reinstalar o jogo

Se você já tem o ZLauncher, baixe somente [ZLauncher.Updater.exe](https://github.com/arthurnazareth/zlauncher/releases/latest/download/ZLauncher.Updater.exe).

1. Feche o launcher antigo.
2. Execute o Updater, clique em **Escolher** e selecione o `ZLauncher.exe` ou `bxLauncher.exe` instalado.
3. Clique em **Buscar e atualizar**. O Updater baixa apenas os arquivos do launcher e pode reabri-lo ao terminar.

Na versão 3.4.0 e seguintes, use **Ajuda → Atualizar launcher**. A opção **Verificar atualizações ao abrir** avisa quando há uma versão nova. O botão fecha o launcher, atualiza e reabre automaticamente. Você não precisa baixar o instalador nem a build novamente.

O pacote de atualização 3.4.0 tem cerca de 24,5 MB. Perfis (`settings.json`), configuração de versões, mods, saves e arquivos do jogo ficam fora da lista de arquivos permitidos para atualização. Cada atualização guarda a versão anterior em `ModsLauncher/update-backups`; erros na troca restauram os arquivos anteriores e atualizações interrompidas são recuperadas na próxima execução do Updater. Instalações antigas com `bxLauncher.exe` continuam funcionando pelos atalhos existentes.

Os downloads vêm das releases estáveis deste repositório e são conferidos com SHA-256. Se o GitHub estiver indisponível, a atualização não será aplicada. SHA-256 verifica integridade, não assinatura do desenvolvedor.

### Código do Updater

Os arquivos `src/UpdateCore.cs`, `src/UpdaterProgram.cs` e `src/UpdateTests.cs` contêm o atualizador e seus testes. Execute `Build-Updater.ps1` no Windows com .NET Framework 4.8 para compilá-lo. Para rodar os testes isolados: `ZLauncher.Updater.exe --self-test --output C:\Temp\ZLauncherTests`.

## Instalar

1. Abra a [release 3.4.0](https://github.com/arthurnazareth/zlauncher/releases/tag/v3.4.0).
2. Baixe **Instalar-ZLauncher.exe** e execute-o no Windows.
3. Escolha uma pasta e clique em **Baixar e instalar**.
4. Aguarde o download e a verificação do pacote; o instalador completo abrirá em seguida.

O preparador baixa aproximadamente 5,3 GB. Reserve 11 GB para preparar os arquivos e cerca de 10 GB adicionais para instalar a build. Depois de instalar, o pacote baixado pode ser guardado em outro disco.

O pacote inclui ZLauncher 3.4.0, instalador 1.2.2, Build 42.21, ZombieBuddy 2.3.4 e os assistentes oficiais do Visual C++ x64 e da Steam. A Steam precisa de internet para atualizar e de login feito pelo usuário. O Windows poderá pedir autorização para instalar pré-requisitos.

O arquivo grande é distribuído em três partes porque o GitHub limita o tamanho de cada asset. O preparador reúne as partes e verifica SHA-256; não é necessário juntá-las manualmente. Os arquivos `.part001` a `.part003` não são instaladores independentes.

## Pacote já baixado

Mantenha `ZLauncher-Setup.exe`, `ZLauncher-Setup.exe.config`, `ZLauncher-42.21.bxdata` e `LEIA-ME.txt` na mesma pasta. Execute `ZLauncher-Setup.exe`. Não é necessário ter outra cópia do jogo para extrair a build incluída.

## Compatibilidade e segurança

- Projeto não oficial, sem vínculo com The Indie Stone, Valve ou Microsoft.
- O pacote contém componentes do Online-Fix. `OnlineFix64.dll` foi detectada pelo Microsoft Defender como **HackTool:Win32/Crack!MSR**. Não houve confirmação de falso positivo nem certificação de segurança dessa DLL.
- O instalador não desativa o antivírus, não cria exclusões e não restaura arquivos da quarentena.
- Se um componente for bloqueado ou removido, a instalação poderá falhar. A inclusão do arquivo não garante sua aceitação pelo antivírus ou o funcionamento de uma sessão online.
- A verificação SHA-256 confirma integridade de transferência; não é uma análise antimalware.
- Prefira versões verificadas pelo fornecedor. Suspeitas de falso positivo podem ser enviadas ao [canal de análise da Microsoft](https://www.microsoft.com/en-us/wdsi/filesubmission).

## Código neste repositório

`src/DownloadInstaller.cs` contém o preparador de download usado nesta release. Os arquivos binários de distribuição ficam nos assets da release, não no histórico Git.

## Modpacks na versão 3.3.0

Importar lista inicia automaticamente os downloads, incluindo mods desativados. A seleção e a ordem são preservadas. Listas antigas precisam ser exportadas novamente para incluir os mods desativados. Mods locais sem origem Workshop exigem importação por pasta ou ZIP.

Em Perfis, use Renomear ou Apagar modpack. Apagar um perfil preserva mods e saves. Em Meus mods, Remover mod guarda os arquivos em Backups e atualiza os perfis da mesma biblioteca. Dependências ativas precisam ser desativadas antes da remoção.

O preparador reutiliza as três partes do pacote da build publicadas em v3.2.2 e baixa o instalador atualizado de v3.4.0; todos os arquivos são conferidos por SHA-256.
