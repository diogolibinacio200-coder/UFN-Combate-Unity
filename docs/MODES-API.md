# Modos 0.7 — contrato de integração

Namespace `UFN.Core`, C# 9/netstandard2.1; o núcleo não usa Unity.

## Campanha, sobrevivência, torneio

```csharp
var run = CampaignRun.Create(CampaignMode.Tower, fighterId, seed);
// Tower aceita quarto argumento opcional int[] com adversários, terminando 14,15.
int enemy = run.OpponentId;
int stage = run.Stage; // zero-based
float hp = run.HealthMultiplier;
float attack = run.DamageMultiplier;
float startingMeter = run.MeterBonus;
run.RecordVictory(); // atualiza rodada; pode aguardar benefício
BenefitKind[] choices = run.BenefitChoices;
run.ChooseBenefit(0); // índice da opção, somente quando AwaitingBenefit=true
run.RecordDefeat();
bool ended = run.Complete;
bool won = run.Won;
CampaignSnapshot snapshot = run.Export();
var restored = CampaignRun.Restore(snapshot);
```

`StageLabel`, `Description`, `Wins`, `Losses`, `Difficulty`, `AwaitingBenefit` e `Complete` orientam a interface. Benefícios são `Vitality`, `Power`, `Focus`, `Recovery`, `Fortune`; `BenefitLabel/BenefitDescription` traduzem as opções. Sobrevivência continua até derrota; torneio usa chave de 16 participantes e quatro rounds. Todas as escolhas são locais, sem monetização.

## Perfil e persistência

`ProfileData` tem campos serializáveis `Version`, `TotalWins`, `TotalLosses`, `TowerClears`, `TournamentWins`, `BestSurvival`, `BestKartMilliseconds`, `SelectedCharacter`, `SelectedSkin`, `MusicVolume`, `EffectsVolume`, `Campaign`.

```csharp
sealed class Codec : IProfileCodec {
  public string Encode(ProfileData value) => JsonUtility.ToJson(value, true);
  public ProfileData Decode(string json) => JsonUtility.FromJson<ProfileData>(json);
}
var store = new ProfileStore(pathUnderPersistentDataPath, new Codec());
ProfileData profile = store.Load();
store.Save(profile);
profile.RecordCombatResult(run, true); // estatística do resultado da rodada
profile.RecordKartResult(race); // melhora recorde apenas se jogador terminou
```

`Load` valida e migra versões anteriores suportadas; tenta `.bak` quando o principal está inválido. `RecoveryMessage` explica recuperação/falha. Gravação usa arquivo temporário no mesmo diretório, flush e substituição atômica. Exceções de I/O em `Save` devem aparecer ao usuário.

## Kart

```csharp
var race = new KartRace(seed, selectedCharacterId, trackId: 0,
    mode: KartRaceMode.Quick, secondCharacterId: 2, playerSkin: 1,
    secondSkin: 0, difficulty: 1);
race.Step(new KartInput { Throttle=1, Steering=-1, Drift=true, UseItem=true },
    new KartInput { Throttle=1 });
// Step = exatamente 1/60 s; chamar no FixedUpdate ou acumulador fixo.
KartRacer player = race.Racers[0];
// Position (KartPoint.X/Y -> Unity x/z), Heading, Speed, Lap, Place,
// DriftCharge, BoostTicks, Item, Finished; CharacterId e Skin.
race.Track.Sample(distance); // KartTrackSample.Position/Tangent
race.Track.Points; race.Track.Width; race.Track.Length;
race.ElapsedTicks; race.Finished; race.Completed; race.PlayerPlace;
race.Track.Id; race.Track.Name; race.HumanCount;
race.ChampionshipPoints; race.ChampionshipComplete;
race.Recover(0); // resgate explícito ao último checkpoint, sem conceder progresso
KartChampionshipSnapshot save = race.ExportChampionship(); // somente Championship
var resumed = KartRace.RestoreChampionship(save);
// Campeonato, depois de Completed, enquanto !ChampionshipComplete:
// race = race.NextChampionshipRace();
```

Cinco pistas, três voltas, vinte checkpoints ordenados por volta, drift/turbo, itens e condução física da IA sem teletransporte. `KartInput` tem `Throttle`, `Brake`, `Steering` (faixa -1..1), `Drift`, `UseItem` e `LaunchBack`. `Finished` significa término do jogador 1; `Completed` significa que todos os humanos terminaram e congela a classificação e o relógio. CPUs que não chegaram ficam classificadas pelo progresso. `Reset` é feito criando outra corrida.

`Quick` e `Championship` têm oito pilotos e até dois humanos; `TimeAttack` força um piloto, sem itens. O campeonato usa quatro etapas (pistas 0–3), mantém o elenco e acumula pontos. A pista 4 continua disponível em corrida rápida e contra o relógio. `LapTicks[3]`, `FinishTick` e `BestLapMilliseconds` são tempos reais. `KartContent` lista pilotos, pistas e poderes; `KartAppearance.Get(character,skin)` identifica as quinze variantes visuais. Diogo Careca é personagem 3, skin 1, com a mesma física e poderes.

`Recover(index)` permite o resgate manual, com um segundo de imobilização e três segundos de intervalo. Recusa a chamada durante contagem regressiva, após chegada e durante o intervalo; retorna `bool`. Não altera voltas/checkpoints nem concede itens. Esta recuperação explícita é diferente da condução normal, na qual a CPU nunca teletransporta.

`ExportChampionship` salva o início da etapa atual ou a próxima etapa se a corrida terminou; depois da quarta etapa retorna `null`. Retomar durante uma prova reinicia essa etapa, conservando os pontos anteriores. O snapshot clona os pontos, preserva a semente do elenco, os dois humanos e suas skins. O restauro rejeita versões futuras, etapa fora de 0–3 ou pontuação incompatível com etapas concluídas. O aplicativo grava o snapshot junto ao progresso e oferece "Retomar campeonato" no menu de kart.

`UFN.Presentation.KartPresentation : MonoBehaviour`: `Bind(KartRace race)`, `UpdateView()` e limpeza ao destruir ou trocar corrida. Renderização de pistas/karts por geometria Unity, pilotos sentados e câmeras independentes para dois humanos. `HorizontalSplit` escolhe a orientação da divisão antes de `Bind`. Input, Step e HUD pertencem ao aplicativo. `CharacterPortraits` permanece apenas para compatibilidade da chamada; os pilotos não são retratos flutuantes. A apresentação não modifica a simulação. `KartModel` articula cabeça, tronco, braços e volante.

Testes independentes: `UFN.Tests.ModesTests.Run()` retorna `List<string>` com falhas e `ModesTests.Checks` total de verificações. Inclui `KartTests.Run()` para cinco pistas, quatro etapas, dois humanos, quatorze poderes, contra o relógio e identidade das skins. Consulte `KART-MIGRATION.md` e `kart-references/sources.json`.
