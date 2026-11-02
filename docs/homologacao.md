# Checklist de homologação

Status inicial: **pendente**. Marque apenas depois de executar no Windows e na
API com os perfis do grupo. Não substitua uma partida real pelo treino local.

## VS Code e SDK

- [ ] Abrir a pasta `quina-csharp` no VS Code.
- [ ] Instalar .NET 8 SDK e C# Dev Kit.
- [ ] `dotnet restore Quina.sln` concluído.
- [ ] `dotnet build Quina.sln` concluído sem erros.
- [ ] `dotnet test tests/Quina.Tests/Quina.Tests.csproj` concluído sem falhas.
- [ ] F5 inicia a janela e permite colocar breakpoint no controlador.
- [ ] Salvar o relatório TRX e anotar o commit testado.

## Interface

- [ ] Testar resolução 1366×768 e escala de Windows 125%.
- [ ] Revisar acesso, perfis, listagem, mesa, mão, placar e rodapé.
- [ ] Testar rolagem de mão e mesa longas, sem pedras cortadas.
- [ ] Navegar com Tab, ativar botões por teclado e ler foco visível.
- [ ] Verificar que o token fica mascarado e não aparece no log.
- [ ] Rodar demonstração manual e demonstração com bot até o fim.

## Contrato e acesso real

- [ ] Confirmar nomes de `ProximaAcaoPartida` 0..5 com o professor.
- [ ] Configurar a ordem em ambos os clientes.
- [ ] Solicitar token FIAP, conferir recebimento e autenticar.
- [ ] Criar perfil e ativá-lo.
- [ ] Fechar, reabrir, recuperar token e retomar com a mesma chave.
- [ ] Testar liberação e transferência para outro cliente.
- [ ] Confirmar `JOGADOR_JA_ATIVO` com uma tentativa controlada em outra chave.

## Partida com dois perfis

- [ ] A cria, B ingressa e um jogador inicia explicitamente.
- [ ] Apenas mão própria aparece; oponente mostra somente quantidade.
- [ ] Abertura obrigatória e turno correspondem ao estado oficial.
- [ ] Confirmar orientação do array `tabuleiro` comparando as pontas com a API.
- [ ] Jogar em ambos os lados e verificar a atualização nos dois clientes.
- [ ] Conferir pontuação oficial em uma soma múltipla de cinco.
- [ ] Conferir compra de uma pedra e manutenção/troca do turno.
- [ ] Conferir passagem quando permitida.
- [ ] Terminar rodada, observar redistribuição e concluir partida.
- [ ] Consultar vencedor e histórico oficial.
- [ ] Executar partida completa bot versus humano e bot versus bot.

## Resiliência

- [ ] Sem mudança, 204 não limpa a mesa.
- [ ] Após timeout de comando, repetir a intenção sem duplicar jogada.
- [ ] Reabrir com intenção pendente e repetir os mesmos dados.
- [ ] Versão antiga não substitui uma visão nova.
- [ ] 409 aplica estado autoritativo; 422 não simula sucesso.
- [ ] Token inválido interrompe acompanhamento e permite reautenticar.
- [ ] Bot pausa em erro e não fica enviando comandos rejeitados.

## Entrega

- [ ] Confirmar integrantes/RMs.
- [ ] Publicar o repositório com README, diagrama e testes.
- [ ] Verificar execução do workflow no GitHub.
- [ ] Ensaiar apresentação com outro grupo e guardar evidências sem segredos.
- [ ] Entregar o link do repositório no prazo do professor.

| Verificação | Data | Ambiente/commit | Resultado |
|---|---|---|---|
| Build C# | Pendente | — | — |
| Testes xUnit | Pendente | — | — |
| Revisão visual Windows | Pendente | — | — |
| Partida manual real | Pendente | — | — |
| Partida autônoma real | Pendente | — | — |
| Retomada real | Pendente | — | — |

## Novos fluxos — resultado, CPU e feedback

- [ ] Escolher Fácil e Normal antes de começar; conferir o nível no cabeçalho.
- [ ] Completar uma partida em cada nível. Conferir vencedor e placar final.
- [ ] Jogar novamente: placar zerado, mão nova e painel de resultado oculto.
- [ ] Voltar ao início e iniciar outra partida sem avisos de pontos da anterior.
- [ ] Marcar pontos, comprar e jogar: conferir avisos e animações sem duplicatas.
- [ ] Conferir bônus local de rodada separadamente da pontuação das pontas.
- [ ] Desativar animações do Windows e conferir ausência de movimento de entrada.

## Roteiro online em dupla (pendente de execução)

1. Cada pessoa entra com seu próprio acesso e ativa um perfil diferente.
2. A cria uma mesa, copia o ID na sala de espera e compartilha com B.
3. B atualiza o salão, seleciona a mesa e entra. A tela de A deve indicar que está pronta.
4. Confirmar com o professor o mapeamento das ações e configurá-lo nos dois clientes.
5. Iniciar a partida. Jogar alternadamente; comparar turno, pontas e placar nos dois apps.
6. Quando permitido, comprar e passar; observar o resultado confirmado nos dois clientes.
7. A sai da sessão, entra de novo e usa Retomar pelo ID. Conferir que a mesma mesa é recuperada.
8. Terminar a partida; conferir o mesmo vencedor e placar nos dois resultados.
9. Voltar ao salão. Registrar erros, horário e código retornado, sem compartilhar tokens.

Não considerar esse roteiro aprovado apenas porque a criação da mesa funcionou.
