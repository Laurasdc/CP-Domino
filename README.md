# QUINA — Dominó Ponta de Quina em C#

## Integrantes

| Laura Souza | 556320 |
| Vinicius Henrique | 556908 |
| Gustavo Pierre | 558928 |
| Enzo Dias | 558225 |
| Gabriel Belo | 551669 |

## Duração, retomada e ajuda

Escolha a duração na tela inicial. Por rodadas, vence o maior placar ao final;
placares iguais dão empate. Entre rodadas, confira motivo, pontos e bônus antes
de clicar em Próxima rodada. Como jogar explica as convenções locais.
O resultado escurece a mesa e oculta dicas e automação sem utilidade.
O salvamento preserva mão, monte, placar, rodada e histórico; Encerrar descarta
o jogo salvo. Uma nova partida substitui o salvamento anterior.
A comparação completa com as regras do professor e o teste online em dupla
continuam pendentes. O bônus local de +1 permanece explicitamente simplificado.

## Novidades da experiência de jogo

- Escolha **Fácil** (jogadas válidas aleatórias) ou **Normal** (estratégia de pontuação)
  antes de iniciar contra a CPU. Reiniciar usa a escolha atual da tela inicial.
- Resultado integrado à mesa com vencedor, placar e histórico. Contra a CPU,
  **Jogar novamente** inicia uma nova partida; online, volte ao salão para escolher uma mesa.
- Peças novas entram com animação curta; pontos confirmados aparecem em um aviso
  temporário. Atualizações sem mudança não repetem o aviso. Animações de entrada
  respeitam a preferência de animação do Windows.
- Sala de espera com ID selecionável e botão de cópia. Iniciar continua exigindo
  dois participantes. Configuração técnica da API permanece no salão.
- Validação real online com duas pessoas permanece pendente; roteiro em
  `docs/homologacao.md`. Os novos testes das duas dificuldades foram escritos,
  mas precisam ser executados com o SDK .NET.

## Modos de jogo

- **Contra a CPU:** partida local contra o computador, sem login ou internet,
  com metas de 15, 30 ou 50 pontos ou duração de 3, 5 ou 7 rodadas.
  Inclui salvamento automático no usuário do Windows e retomada após fechar o app.
  **Encerrar partida** pede confirmação, descarta o jogo local e volta ao início.
  Cancelar mantém a partida; **Voltar ao início** continua apenas pausando o jogo.
  O encerramento é exclusivo da CPU: o contrato online incluído não oferece uma operação de desistência.
- **Jogar online:** acesso com e-mail FIAP para criar, entrar e retomar mesas pela
  API do professor.
  **Sair da sessão**, no cabeçalho, remove o token salvo e volta ao início após
  confirmação. Tenta liberar o perfil no servidor; se a rede falhar, informa que
  a liberação não foi confirmada. Preserva o ID da mesa, a chave de ativação e
  intenções pendentes para retomada. Não encerra a partida online nem revoga o token
  em outros dispositivos. **Fechar app** fecha a janela.

O modo CPU mantém a regra local de bônus de fim de rodada: 1 ponto para quem
ficar com a menor soma nas peças restantes, sem bônus no empate. Essa diferença
está explicada em **Sobre esta partida** e ainda não foi alinhada às regras do
servidor. A mudança de apresentação não altera a pontuação.


Cliente desktop do **Checkpoint 6**, desenvolvido em **C# 12 / .NET 8 / WPF**, com
projeto preparado para **Visual Studio Code no Windows**.

Inclui acesso à API do professor, perfis, sala de partidas, mesa com pedras vetoriais,
jogo manual, jogador autônomo, treino local, documentação e testes com **xUnit,
FluentAssertions e Moq**. Entrega indicada no enunciado: **27/10/2026**.

> **Status honesto desta versão:** código implementado, sintaxe C# e estrutura de
> arquivos revisadas. **Compilação, execução dos testes C#, inspeção visual no Windows
> e partida autenticada ainda não foram realizadas.** O ambiente de criação não
> dispõe do .NET SDK e bloqueou seu download. Os resultados da versão anterior
> em Python não são evidência de funcionamento desta versão C#.

## Retorno da criação de partidas

O botão mostra **Criando mesa…** durante a solicitação e impede cliques repetidos.
O andamento aparece abaixo dele. Se o servidor rejeitar a criação ou ocorrer
uma falha de rede, uma janela apresenta o erro. Copie essa mensagem ao relatar
um problema. Em resultado desconhecido, consulte as mesas antes de criar outra.
Esta atualização melhora o diagnóstico; a causa da falha relatada ainda depende
da resposta obtida no computador em uso.

## Mesa de jogo — revisão de usabilidade

Mesa em grafite e violeta, peças maiores na mão, pontas realçadas em lavanda
e indicação central de turno. Selecione uma peça: na sua vez, os encaixes
válidos aparecem diretamente nas extremidades da sequência. Clique no encaixe
para enviar a jogada; na abertura existe apenas um ponto de colocação. Comprar e
passar dependem da ação permitida pela API. A seleção permanece nas atualizações
enquanto a peça continuar na mão. Sequências longas formam linhas alternadas e se ajustam à área da mesa.

O adversário é representado por até sete versos decorativos e pela contagem
pública exata ao lado do nome; nenhuma face adversária é consultada. Botões
habilitados, abas e cartões de seleção usam cursor de mão. Botões têm estados
de hover, pressão, foco e desabilitado; controles indisponíveis usam a seta.
Informações de ID e histórico ficam recolhidas na lateral.

## Controles da janela

No canto superior direito: **Maximizar** amplia a janela, **Restaurar janela**
retorna ao tamanho anterior, **Tela cheia** oculta a moldura do Windows e
**Sair** fecha o aplicativo. **F11** alterna a tela cheia e **Esc** sai dela.
O botão Sair usa o mesmo encerramento do X da janela: para as atualizações e
mantém a ativação do perfil para retomada. Fechar não encerra a partida na API.

## Navegação por sessão

- Ao abrir: somente **Começar**, com caminhos separados para sala livre e online.
- **Jogar contra a CPU**, na área Contra a CPU, abre a aba **Contra a CPU**, sem mostrar o salão online.
- Login e ativação do jogador liberam **Salão de partidas**.
- Criar, entrar ou retomar uma partida abre **Na mesa**. Uma ação pendente
  recuperada também libera essa aba para recuperação.
- Falha de autenticação/ativação volta à entrada e oculta as áreas online.
  Liberar o perfil também oculta as áreas online até nova ativação.

## Entrada guiada

Na aba **Começar**, escolha entre experimentar um treino local sem conta ou
entrar com seu grupo. O acesso apresenta uma etapa por vez:

1. **E-mail:** solicite a chave com seu endereço FIAP. Se já tiver a chave,
   use o atalho correspondente; também é possível recuperar o acesso salvo.
2. **Acesso:** cole o token e clique em **Continuar**.
3. **Jogador:** selecione seu perfil ou crie um novo e clique em **Entrar no salão**.

Os avisos aparecem junto ao formulário. Atualizar jogadores, trocar a chave e
liberar o perfil ficam em **Gerenciar meu acesso**. A demonstração é um treino
local contra bot com fim de rodada simplificado, sem conexão com uma partida da API.

## Visual do jogo — edição Clube

Identidade em **grafite, violeta e verde-menta**: fundos abstratos discretos,
peças de porcelana maiores, tipografia sem serifa e navegação arredondada.
Violeta identifica as ações principais; verde-menta destaca o jogador da vez.
A área do jogo tem altura máxima de 720 e a janela conteúdo máximo de 1540
unidades WPF, reduzindo o vazio em telas grandes. Botões usam transições suaves de realce em 140–180 ms ao passar o mouse,
sem alterar o tamanho; a peça selecionada fica elevada.

A sala mostra partidas reais retornadas pela API. A composição decorativa de
três pedras no centro da sala é apenas ambientação; não representa uma partida.
Na aba **Na mesa**, todas as pedras exibidas vêm do estado confirmado. A mão
fica sobre um suporte com rolagem horizontal; a sequência da mesa segue a ordem oficial
em linhas alternadas. Histórico técnico e configuração ficam em painéis recolhíveis.

O desenho é feito em WPF, sem arquivos de imagem externos nem downloads em tempo
de execução. Não reproduz o avatar, a paleta ou a composição exata da referência.
Esta revisão visual ainda precisa ser conferida no Windows; não foi gerada uma
captura de execução do WPF neste ambiente.

Para atualizar: feche o aplicativo, extraia o ZIP atualizado para uma pasta nova
e abra a pasta `quina-csharp` no VS Code. Isso evita misturar arquivos antigos.
As credenciais continuam no armazenamento protegido do Windows, fora da pasta.
Se você alterou código localmente, preserve a pasta anterior antes de integrar.

## 1. Abrir no Visual Studio Code

Você não precisa do Visual Studio. O VS Code edita o C#/XAML e os comandos do SDK
compilam o aplicativo. O projeto não depende de um designer visual de janelas.

1. Instale o **.NET 8 SDK para Windows x64**: https://dotnet.microsoft.com/download/dotnet/8.0
   Escolha **SDK**, não apenas Runtime.
2. No VS Code, instale **C# Dev Kit**, da Microsoft. A extensão C# também é recomendada.
3. Extraia completamente o ZIP.
4. No VS Code, use **Arquivo → Abrir Pasta** e selecione a pasta **`quina-csharp`**,
   que contém `Quina.sln`, `README.md` e `.vscode`.
5. Abra **Terminal → Novo Terminal** e execute:

```powershell
dotnet --version
dotnet restore Quina.sln
dotnet build Quina.sln
dotnet test tests/Quina.Tests/Quina.Tests.csproj
dotnet run --project src/Quina.Desktop/Quina.Desktop.csproj
```

O primeiro restore precisa de internet para baixar os pacotes NuGet. Se algum
comando falhar, pare e leia a mensagem antes de prosseguir. Não há Python neste
projeto, nem necessidade de instalar Node.js ou um banco de dados.

### Atalhos incluídos

| Ação no VS Code | Configuração incluída |
|---|---|
| **F5** | Compila a solução e inicia o depurador da aplicação WPF |
| **Ctrl+Shift+B** | Compila `Quina.sln` |
| Terminal → Executar Tarefa → **Quina: executar** | Abre o aplicativo |
| Terminal → Executar Tarefa → **Quina: testar** | Executa xUnit e gera relatório TRX |
| Aba de testes do C# Dev Kit | Descoberta dos testes após restauração dos pacotes |

Também existem `iniciar.bat` e `testar.bat` para uso fora do editor. Se preferir,
use os comandos do terminal, pois deixam as mensagens de erro mais visíveis.

**WPF executa no Windows.** Não rode a interface dentro do WSL. Os projetos Core,
Infrastructure e Tests são .NET multiplataforma; a interface e o cofre são Windows.

## 2. Integrantes

Dados informados para o trabalho anterior de dominó. Confirme se este checkpoint
usa o mesmo grupo antes de entregar.

| Nome | RM |
|---|---|
| Laura Souza | 556320 |
| Vinicius Henrique | 556908 |
| Enzo Dias | 558225 |
| Gustavo Pierre | 558928 |

## 3. API oficial atualizada

- Base: https://domino-quina-api-b1-pf2261.azurewebsites.net
- Swagger: https://domino-quina-api-b1-pf2261.azurewebsites.net/swagger/index.html
- OpenAPI: https://domino-quina-api-b1-pf2261.azurewebsites.net/swagger/v1/swagger.json
- Contrato baixado em 22/10/2026: [`docs/openapi.json`](docs/openapi.json).

O endereço antigo do PDF não é usado. A constante de base fica em
`src/Quina.Infrastructure/DominoApiClient.cs`.

### Lacuna no contrato: códigos de próxima ação

O Swagger descreve `SituacaoPartida` como `0 = aguardando`, `1 = em andamento`,
`2 = encerrada`. Porém `ProximaAcaoPartida` apenas lista `[0,1,2,3,4,5]`, sem nomes.

O aplicativo oferece uma **hipótese configurável**, sem tratá-la como fato:

| Ação | Código sugerido, ainda a confirmar |
|---|---:|
| Aguardar | 0 |
| Jogar abertura | 1 |
| Jogar | 2 |
| Comprar | 3 |
| Passar | 4 |
| Encerrar | 5 |

Pergunte ao professor: **“Qual é o nome de cada valor 0 a 5 de
ProximaAcaoPartida?”** Depois use **Configurar ações da API**, na sala de partidas,
e informe os códigos na ordem solicitada. Essa confirmação libera as ações do
turno e o jogador autônomo. O treino local tem códigos conhecidos e já vem liberado.

Outra premissa a verificar na homologação: `tabuleiro` é interpretado como cadeia
orientada, em que o primeiro `valorA` é a ponta esquerda e o último `valorB` é a
direita. O Swagger fornece a lista de pedras, mas não explicita essa orientação.

## 4. Primeiro acesso

1. Informe seu e-mail **@fiap.com.br** e solicite o token. A API responde `202`,
   sem corpo; o token chega por e-mail. Confira também o spam.
2. Cole o token no campo protegido e clique em **Entrar**.
   `GET /api/identidade/sessao` valida a conta.
3. Crie ou selecione um perfil. O nome pode ter de 1 a 150 caracteres.
4. Clique em **Ativar / retomar perfil**. O cliente gera 32 bytes aleatórios,
   converte em 64 caracteres hexadecimais e persiste a chave **antes de enviá-la**.
5. Confirme os códigos de próxima ação conforme explicado acima.

Token e chave de ativação são diferentes. O token pode ser reutilizado até
revogação. A chave reservada ao perfil é enviada em `X-Jogador-Ativacao` junto
com o Bearer em ações e consultas privadas.

### Armazenamento seguro

O aplicativo usa **DPAPI com `CurrentUser`**. O conteúdo é cifrado em:

```text
%LOCALAPPDATA%\QuinaCP6\credentials.bin
```

São armazenados: token (opcional), chaves de ativação, última partida, mapeamento
e intenção pendente por perfil/API. O arquivo fica fora do projeto e não deve
ser copiado para outro usuário/computador ou incluído no GitHub.

A gravação usa arquivo temporário e substituição atômica, com mutex para impedir
sobrescritas concorrentes de duas instâncias. Não há fallback em texto puro.
Se o cofre falhar antes de um comando, o comando não é enviado.

## 5. Jogar uma partida real

1. Jogador A cria uma mesa e escolhe a pontuação alvo.
2. Jogador B, com outro perfil, atualiza a lista e ingressa na mesa selecionada.
3. Com dois participantes, um jogador clica explicitamente em **Iniciar partida**.
4. O servidor distribui as pedras e informa quem abre e a pedra obrigatória.
5. Na sua vez, selecione uma pedra da mão e use **Jogar à esquerda** ou **Jogar
   à direita**. Encaixes incompatíveis ficam desabilitados.
6. **Comprar** e **Passar** seguem a próxima ação retornada pela API.
7. A tela atualiza somente após confirmação. Não remove pedra nem altera placar
   por uma intenção ainda não confirmada.
8. Ao terminar, o vencedor aparece na tela. **Resultado e histórico** consulta os
   eventos oficiais do participante.

A mesa segue a ordem oficial em linhas alternadas, ligadas por curvas. A escala
se ajusta para mostrar todas as pedras. A mão mantém rolagem horizontal. O adversário aparece apenas com nome, pontos e quantidade
na mão. Não existe acesso às pedras privadas do adversário pela interface.

### Atualização e retomada

- Consulta aproximadamente a cada 1,5 segundo, sem sobrepor operações.
- `204 No Content` preserva a tela; apenas versões mais novas são aplicadas.
- **Copiar ID** facilita identificar a mesa para outro grupo.
- **Retomar por ID** sugere a última partida salva.
- Reabrir e reativar o mesmo perfil reutiliza a chave persistida.
- Fechar o aplicativo não libera o perfil. **Liberar perfil** permite transferência
  para outro cliente. Não há tomada automática de um perfil ativo.
- Se a resposta de uma ação se perder, use **Repetir intenção pendente**.
  O mesmo UUID, versão, payload e rota são preservados, inclusive após reinício.

## 6. Jogador autônomo

Ative **Jogador autônomo** na mesa, após confirmar os códigos da API. Para uma
partida entre bots reais, ambos os clientes devem estar na mesma mesa, cada um
com seu próprio perfil e sua ativação.

A heurística usa somente a mão do participante e a mesa recebida:

1. Esvaziar a mão.
2. Maximizar pontos imediatos estimados nas pontas.
3. Descartar a pedra de maior soma.
4. Preservar encaixes com as pedras restantes.
5. Desempatar de forma determinística.

Respeita abertura obrigatória e turno. Só compra/passa quando indicado pelo
estado. Se não houver decisão segura, para. Em erro, o modo autônomo pausa.
**Sugerir jogada** mostra a decisão sem enviar um comando.

A heurística é explicável, não matematicamente ótima. A estimativa não altera
placar: pontuação, vencedor e validade continuam sendo decisões do servidor.

## 7. Tratamento de falhas

| Situação | Comportamento |
|---|---|
| 401 | Interrompe acompanhamento e pede autenticação |
| 403 | Interrompe acompanhamento para revisar conta/ativação |
| 409 | Aplica estado autoritativo, quando presente; informa rejeição |
| 422 | Não simula sucesso; preserva a visão confirmada |
| 429 | Informa limite; polling respeita `Retry-After` |
| Timeout/rede/5xx em comando | Mantém intenção; não gera UUID novo automaticamente |
| Resposta incompatível ou UUID diferente | Mantém intenção para reconciliação |
| Versão antiga | Ignora a atualização |
| Duplo envio | Semáforo impede comandos simultâneos no controlador |
| Cofre indisponível | Falha explícita, sem gravar segredo aberto |

Timeout HTTP de 15 segundos. As consultas usam espera crescente em falhas,
até 30 segundos, ou prazo maior indicado por `Retry-After`. O transporte não
segue redirecionamentos, não registra cabeçalhos e limita resposta a 2 MB.

**Criação de perfil e partida não têm idempotência no contrato.** Em resultado
incerto, consulte perfis/mesas antes de repetir uma criação. Nenhuma criação é
reenviada automaticamente.

O relógio mede tempo desde que o cliente observou o turno. Não é o relógio oficial.
A penalidade por demora, apresentada como regra futura no PDF, não é inventada
nem aplicada localmente.

## 8. Demonstração local

**Jogar demonstração** inicia treino contra um bot com meta de 15 pontos, sem
credenciais ou rede. Serve para conhecer a interface e exercitar o fluxo.

O simulador possui distribuição, abertura, encaixe, compra de uma pedra,
passagem, pontos nas pontas, novas rodadas e encerramento. **O fim da rodada é
simplificado:** um ponto extra para quem tem menor soma restante; empate não
concede esse bônus. Essa é uma convenção exclusiva de treino, pois o enunciado
não detalha o cálculo oficial de travamento. A API real nunca usa esse cálculo.

## 9. Arquitetura

| Projeto | Responsabilidade |
|---|---|
| `Quina.Core` | DTOs, interfaces, controlador, versões, intenção e bot |
| `Quina.Infrastructure` | HttpClient, contrato da API e simulador separado |
| `Quina.Desktop` | XAML/WPF, desenho vetorial, coordenação de telas e DPAPI |
| `Quina.Tests` | xUnit, FluentAssertions, Moq e HTTP simulado |

A interface usa XAML e code-behind para apresentação. As decisões e a consistência
ficam em serviços testáveis; não há regras oficiais reimplementadas na tela.
A rede é assíncrona com `async/await`. As atualizações visuais ficam na thread
WPF, sem bloquear com `.Result` ou `.Wait()`.

Diagrama de classes: [`docs/arquitetura.md`](docs/arquitetura.md).

## 10. Testes automatizados

```powershell
dotnet test tests/Quina.Tests/Quina.Tests.csproj --logger "trx;LogFileName=quina-tests.trx" --results-directory TestResults
```

A suíte usa AAA (Arrange/Act/Assert), `Fact` e `Theory`:

- Soma 10 retorna 2; soma 15 retorna 3; retornos e exceções com FluentAssertions.
- Orientação nas pontas, abertura, turno e decisões do bot.
- Moq prepara respostas do gateway e do armazenamento de intenções e verifica
  decisões de consultar/salvar/enviar, incluindo ausência de envio em falha do cofre.
- Idempotência, persistência antes de envio, retomada, 204, 409, 422 e falhas de rede.
- HTTP simulado verifica cabeçalhos, JSON camelCase, enum numérico, erros e `Retry-After`.
- Teste de ciclo completo configurado para **100 partidas simuladas** com sementes distintas.

**Estes testes C# ainda não foram executados no ambiente de criação.** O teste de
100 partidas está implementado, mas não se afirma que tenha passado. O workflow
`.github/workflows/build-test.yml` está preparado para compilar e testar no Windows
no GitHub; sua primeira execução também está pendente.

Relatório do que foi efetivamente verificado: [`docs/verificacao.md`](docs/verificacao.md).

## 11. Preparar apresentação e entrega

- [`docs/homologacao.md`](docs/homologacao.md): checklist de compilação, UI e API real.
- [`docs/apresentacao.md`](docs/apresentacao.md): roteiro de defesa e critérios de avaliação.
- Confirme integrantes e códigos da API; execute uma partida manual e uma autônoma
  completa com dois perfis antes de considerar o projeto pronto para avaliação.
- Publique o código no repositório do grupo e entregue o **link**, como pede o PDF.

Para um repositório vazio já criado no GitHub:

```powershell
git init
git add .
git commit -m "feat: cliente domino CP6 em C# com bot e testes"
git branch -M main
git remote add origin URL_DO_REPOSITORIO_DO_GRUPO
git push -u origin main
```

Substitua o marcador pela URL real. Se o grupo já tem um repositório, use uma
branch e preserve o histórico. Este pacote é um cliente independente; não
substitui automaticamente a API ou os projetos anteriores do grupo.

## 12. Solução de problemas

| Mensagem/situação | Próxima ação |
|---|---|
| `dotnet` não reconhecido | Instale .NET 8 SDK e reabra VS Code/terminal |
| F5 não encontra projeto | Abra a pasta `quina-csharp`, não a pasta pai |
| SDK/runtime ausente | Confira `dotnet --list-sdks`; instale o SDK 8 para Windows |
| Restore falha | Verifique rede/acesso ao NuGet; tente `dotnet restore Quina.sln` |
| Janela não abre no Linux/WSL | Execute no Windows; WPF é específico de Windows |
| `JOGADOR_JA_ATIVO` | Libere no cliente original; não apague a chave persistida |
| Ações desabilitadas | Confira turno, mapeamento, perfil e comando pendente |
| Mesa sumiu da listagem | Listagem contém mesas com vaga; use Retomar por ID |
| DPAPI falha após copiar arquivo | Cofre pertence ao usuário/máquina; use o cliente original |
| Token não chegou | Confira FIAP/spam e aguarde limite; contate professor se persistir |

Referências: contrato oficial incluído e apresentação do Checkpoint 6;
[documentação C# no VS Code](https://code.visualstudio.com/docs/languages/csharp) e
[visão geral do WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/).

### Hierarquia dos botões

Ações principais usam violeta com gradiente discreto; ações alternativas têm
fundo tonal e contorno. Acesso salvo e retorno usam botões de texto. Os controles
da janela ficam agrupados em uma barra discreta. Os botões compartilham raio,
tipografia e espaçamento, com realce suave ao passar o mouse, pressão visível e
anel de foco por teclado, sem deslocar o conteúdo.

### Seleção e encaixe direto

As peças disponíveis para jogar recebem contorno verde-menta. Selecione uma e
clique em um encaixe iluminado na mesa. Clique novamente na peça para desfazer a
seleção. Uma peça incompatível mostra orientação, sem enviar jogada. Durante
requisições, as peças ficam bloqueadas para impedir ações sobrepostas. Antes de
enviar, o cliente revalida turno, ação permitida, peça e ponta; a API continua
sendo a autoridade sobre a jogada. Para sequências longas, os encaixes acompanham a primeira e a última peça
no caminho em curvas; a mesa inteira se ajusta ao espaço disponível. Tab e Enter também acionam peças e encaixes.

A validação online completa com dois jogadores permanece pendente, conforme
combinado. A criação de partidas foi confirmada pela usuária.

### Mesa em curvas

A sequência muda de direção a cada sete peças. As peças das linhas de retorno
são giradas para preservar a leitura dos valores conectados; um traço curvo
liga as linhas sem representar pedras adicionais. O painel mantém a ordem do
estado recebido e ajusta a escala para mostrar a mesa completa. Os encaixes
ficam junto às duas pontas reais, inclusive em uma última linha incompleta.
Em mesas cheias, as peças ficam menores para caber na área disponível.

### Voltar e reiniciar

No treino, **Voltar ao início** pausa o bot e preserva as peças e o placar enquanto
o aplicativo estiver aberto. **Continuar minha partida**, na tela inicial, ou a aba
**Contra a CPU** retoma a mesa. **Reiniciar partida** começa uma nova partida com
placar zerado e peças redistribuídas; pede confirmação se a partida não acabou.
No modo online, **Voltar ao salão** mantém a partida existente. Não há reinício
local do estado online; criar outra mesa continua sendo uma ação no salão.
Durante uma operação, os botões aguardam sua conclusão.
