# Unity Edition 0.7.0 — notas de versão

## Estado da validação

Estado revisto em **22 de outubro de 2026**, com os resultados confirmados até esta revisão:

| Verificação | Resultado confirmado |
| --- | --- |
| Compilação C# externa | Sucesso, zero avisos e zero erros |
| Suite externa de lógica | **9.092 verificações, zero falhas** |
| Unity Test Framework, EditMode | **39 casos aprovados, zero falhas ou casos ignorados** |
| Compilação do executável Windows | Sucesso |
| Execução automatizada no Windows | **40 verificações aprovadas, zero erros; 26 capturas geradas** |
| Conferência após os últimos ajustes de interface | **Aprovada em nova execução: zero cortes detectados nas 26 telas capturadas, incluindo galeria com texto grande** |
| Controles físicos | Não verificados |

O teste do executável percorreu menus, seleção, combate, duplas, tutorial, treino, retomada de torre e Evolução, galeria, controles e opções. Confirmou replay sem divergência de estado, salvamento de perfil novo e preferências, avanço da simulação nas cinco pistas e inicialização da tela dividida horizontal e vertical. A execução levou cerca de 44 segundos; esse percurso não equivale a completar todos os modos nem a medir desempenho em diferentes computadores.

Relatórios distribuídos: [resumo dos testes](VALIDATION.json), [checagens do executável](RUNTIME-VALIDATION.json) e [capturas do player](screens/player). As 52 capturas de modelos e pistas em [screens/kart](screens/kart) também foram regeneradas e revisadas nesta versão.

Os números descrevem as execuções realizadas; não significam que todos os fluxos e combinações possíveis foram testados. Esta edição ainda não é apresentada como uma migração 100% verificada de todos os percursos da versão 0.6.

## Mudanças principais

- Projeto Unity 6000.3.26f1 em C#, sem dependência de Godot durante o jogo.
- Simulação de combate a 60 Hz separada de entrada, renderização e interface; suporte a replay por entradas e checkpoints determinísticos em memória.
- Preservação do elenco, golpes e ultimates do catálogo, oito cenários e arquivos originais de arte/áudio importados com manifestos de integridade.
- Diogo Careca continua sendo a skin de Diogo. Mirkos usa a arte de armadura depois da sequência de robôs. O ultimate de Vitor do Bem inicia sua perseguição e confirma quinze impactos.
- Arcade, torre clássica, duelos, duplas, torneio local, sobrevivência, treino, tutorial, galeria, estatísticas, configurações e controles integrados no aplicativo.
- Torre clássica com rota salva e escolha de 3/5/8/10 rivais mais dois chefes. Torre Evolução é uma opção adicional com benefícios, separada das regras clássicas.
- Kart com cinco pistas, oito corredores, três voltas, checkpoints ordenados, itens/poderes, drift, campeonato e contra o relógio. Tela dividida para duas pessoas, horizontal ou vertical.
- Pilotos sentados com aparência e cores inspiradas nas quinze referências fornecidas; Diogo e sua skin conservam física idêntica.
- Perfis Unity separados, cópias de segurança e importação compatível dos dados antigos sem sobrescrever o perfil Godot.

## Regressões cobertas

Os testes incluem perseguição de Vitor de ambos os lados e contra todo o elenco; sequência/armadura de Mirkos; recuperação dos ultimates; dano, guarda, combos e comandos; recursos de Maria, Leonardo, Felipe e Murilo; trocas de dupla com recursos individuais; dificuldade dos chefes; restauração de checkpoints; rotas clássicas; torneios com personagens espelhados; sobrevivência; perfis; corridas completas, classificação e checkpoints do kart.

O EditMode também carrega o catálogo distribuído com `JsonUtility`, verifica a serialização de torre, torneio e sobrevivência dentro da Unity e exercita os mapas separados de entrada da luta e do kart. As regressões de perfil verificam criação sem campanha, salvamento e recuperação da cópia de segurança, preservação de campanhas reais e rejeição de progresso corrompido. Os testes externos não precisam de uma instalação ou licença Unity. O workflow GitHub executa somente essa parte externa.

## Limitações conhecidas

- Os modelos de pilotos e karts usam geometria procedural. A semelhança artística com as referências é uma interpretação estilizada, não uma reprodução exata nem um conjunto de modelos esculpidos.
- Não há multiplayer online nem serviço de contas.
- Não foi realizado teste com controles físicos nesta máquina. Remapeamento e seleção de dispositivos precisam ser confirmados com o hardware do jogador.
- Replays pertencem à versão do catálogo e ao formato atual; não são prometidos entre versões ou arquiteturas diferentes.
- O resultado automatizado não garante ausência de problemas visuais, equilíbrio competitivo ou desempenho igual em todos os computadores.

## Arquivos da distribuição

- `UFN-Combate-Unity-v0.7.0-Windows.zip`: executável e arquivos necessários para jogar.
- `UFN-Combate-Unity-v0.7.0-Codigo.zip`: projeto editável e testes.
- As notas finais da release e os hashes dos arquivos devem acompanhar os downloads na [página de versões](https://github.com/diogolibinacio200-coder/UFN-Combate-Unity/releases).

A versão 0.6 em Godot permanece preservada no repositório anterior.
