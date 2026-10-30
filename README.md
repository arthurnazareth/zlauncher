# ZLauncher

Launcher para Project Zomboid, com perfis de mods, integração com a Workshop e seleção de versões.

## Instalar

1. Abra a [release 3.3.0](https://github.com/arthurnazareth/zlauncher/releases/tag/v3.3.0).
2. Baixe **Instalar-ZLauncher.exe** e execute-o no Windows.
3. Escolha uma pasta e clique em **Baixar e instalar**.
4. Aguarde o download e a verificação do pacote; o instalador completo abrirá em seguida.

O preparador baixa aproximadamente 5,3 GB. Reserve 11 GB para preparar os arquivos e cerca de 10 GB adicionais para instalar a build. Depois de instalar, o pacote baixado pode ser guardado em outro disco.

O pacote inclui ZLauncher 3.3.0, instalador 1.2.2, Build 42.21, ZombieBuddy 2.3.4 e os assistentes oficiais do Visual C++ x64 e da Steam. A Steam precisa de internet para atualizar e de login feito pelo usuário. O Windows poderá pedir autorização para instalar pré-requisitos.

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

O preparador reutiliza as três partes do pacote da build publicadas em v3.2.2 e baixa o instalador atualizado de v3.3.0; todos os arquivos são conferidos por SHA-256.
