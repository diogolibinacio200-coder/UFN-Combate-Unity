# Progressão original na Unity Edition

Estas classes em `UFN.Core` reproduzem a progressão da versão 0.6. A campanha com benefícios (`CampaignRun`) é uma modalidade adicional, independente das regras clássicas.

## Arcade e Torre UFN

```csharp
var run = ClassicRun.Create(tower: true, character: 3, skin: 1,
    count: 5, difficulty: 2, seed: 419u);
int opponent = run.Opponent;
// Chamar somente após a vitória na partida de melhor de três, não em cada round.
run.AdvanceVictory();
ClassicSnapshot saved = run.ExportSnapshot();
var resumed = ClassicRun.Restore(saved);
```

Torre admite 3, 5, 8 ou 10 adversários normais únicos, excluindo o próprio personagem, seguidos de André (14) e Kelvin (15). Arcade usa `tower:false, count:8`. A interface da torre oferece as três dificuldades originais (0..2); o núcleo aceita também a quarta dificuldade geral para importação/configuração.

`Order`, `Index`, `Character`, `Skin`, `Difficulty`, `Count`, `Tower`, `Opponent` e `Complete` são propriedades somente de leitura. `Order` retorna uma cópia. Derrota/tentativa não avança `Index`; portanto o aplicativo pode oferecer repetir e retomar sem gerar outra rota. Após a vitória final, `Complete=true` e `Opponent=-1`. Os snapshots são DTOs com campos serializáveis pelo `JsonUtility`.

## Torneio local

```csharp
var bracket = LocalBracket.Create(
    new[] { 3, 3, 4, 6 }, // IDs; espelhos são permitidos
    new[] { 0, 1, 0, 0 }, // skin de cada slot
    new[] { false, false, true, true }); // humano/CPU
int slotA = bracket.PairA, slotB = bracket.PairB;
int fighterA = bracket.Character(slotA);
bool cpuA = bracket.IsCpu(slotA);
bracket.RecordWinner(1); // lado vencedor da partida: 0 ou 1; empate exige desempate
```

Quatro ou oito slots, cada um com personagem, skin e controle próprios. Vencedores são identificados pelo slot, evitando misturar Diogo normal e sua skin em um espelho. `CurrentSlots`, `WinnerSlots`, `Round`, `MatchIndex` e `MatchesPlayed` orientam o chaveamento. `Champion` contém o slot vencedor somente ao terminar. Exportar/restaurar reconstrói a chave a partir dos resultados efetivamente jogados; históricos impossíveis são rejeitados.

## Sobrevivência

```csharp
var run = SurvivalRun.Create(character: 3, skin: 1, difficulty: 1);
// A luta usa um round; iniciar P1 com run.Health.
run.AdvanceVictory(remainingHp: 700.9f, timerFrames: 61);
// Vida: 880,9; pontos: 1720; próximo adversário conforme sequência original.
run.RecordDefeat();
```

Cada vitória soma `1000 + truncar(HP) + arredondarParaCima(segundos) * 10`, recupera 180 HP até o limite de 1000 e avança três posições na lista jogável, pulando o próprio personagem. `Wave`, `Score`, `Health`, `Opponent`, `Complete` e seleção sobrevivem à exportação. A pontuação usa `long` para sessões extensas; valores inválidos e overflow são rejeitados.

## Validação

`tests/UFN.Tests/ProgressionTests.cs` verifica todas as identidades, tamanhos de torre, dificuldades, retomadas, torneios com espelhos e controles mistos, contagem de vitórias por slot, sobrevivência e saves corrompidos. A suite é chamada pelo runner padrão; pode ser executada isoladamente passando `progression` após o caminho do projeto. O runner não substitui o teste da interface Unity.
