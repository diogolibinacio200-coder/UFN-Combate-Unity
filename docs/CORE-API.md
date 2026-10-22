# Núcleo de combate C# / contrato de integração

Namespace `UFN.Core`, C# 9 / netstandard2.1, sem Unity ou pacotes. Simulação em passos fixos de 60 Hz; renderer deve acumular tempo e chamar `Step` zero ou mais vezes por quadro. Coordenadas em pixels: arena 1280×720, X dos corpos limitado a 90..1190, solo Y=590, Y positivo para baixo.

## Dados

`GameCatalog`: campos públicos `CharacterDefinition[] Characters`, `MoveDefinition[] Normals`, `MoveDefinition[] Chains`. A seleção dos elos do combo usa `ChainStage=1/2`, não depende da posição no array.

`CharacterDefinition`: `int Id`, strings `Key, Name, Title, ColorHex, UltimateName, Quote, Ending`, `float WalkSpeed`, `MoveDefinition[] Specials`, `MoveDefinition Ultimate, Offensive, Defense`.

`MoveDefinition`: strings `Name, Kind, Level, Family, Visual, BodyPose`; floats `Damage, Range, Height, OffsetY, GuardDamage, Cost, Push, Launch, Speed, Chip, Pull, Gravity, Vy`; ints `Startup, Active, Recovery, Hitstun, Blockstun, Duration, Invuln, Freeze, Bounces, PulseCount, PulseInterval, Root, SetupTime, Slow, ChainStage`; bools `Cancel, Chargeable, Reflect`. Todos os DTOs usam CAMPOS PÚBLICOS PascalCase, compatíveis com Unity JsonUtility. Tipos/kinds da base são preservados.

## Entrada

`InputFrame` é struct com bools públicos `Left, Right, Up, Down, Attack, Heavy, Kick, Block, Dodge, Power, Tag`. `Step` detecta bordas comparando o estado anterior; envie botões mantidos, não eventos de teclado. `InputFrame.Empty` disponível. P1/P2 usam o mesmo contrato. Comandos: defesa+forte ou Power=ultimate; defesa+ataque=agarrão; trás+ataque=especial0; trás+baixo+ataque=especial1; frente+baixo+chute=especial2; frente+baixo+forte=offensive. Direções relativas calculadas a cada captura.

## Mundo

`new CombatWorld(GameCatalog catalog, int p1Id, int p2Id, uint seed = 1, int difficulty = 1)` cria round jogável. `Step(InputFrame p1, InputFrame p2)`, `ResetRound()`, `StartRound(int p1Id,int p2Id)`. Propriedades `FighterState[] Fighters`, `List<ProjectileState> Projectiles`, `List<CombatEvent> Events`, `CinematicState Cinematic`, `int Tick, TimerFrames, Hitstop, Winner, Round, Difficulty`, `string Phase`. `Winner=-1` em andamento, 0/1 vencedor, 2 empate; `Phase="fight"|"cinematic"|"result"`. `TrainingSettings Training` contém `Enabled, InfiniteHealth, InfiniteMeter`. `Catalog` acessível em leitura. A dificuldade 0..3 determina a vida original de André (1100/1150/1250/1300) e Kelvin (1150/1250/1300/1400), inclusive após reiniciar/restaurar um round.

### Duplas 2×2

`SetupTeams(int p1ReserveId,int p2ReserveId)` habilita as reservas dos dois líderes escolhidos e reinicia com 150 segundos. `ConfigureTeams(int p1LeadId,int p1ReserveId,int p2LeadId,int p2ReserveId)` configura os quatro IDs. `ResetRound()` mantém as equipes; `StartRound(p1,p2)` volta a duelo simples de 99 segundos. Estado público: `TeamMode`, `FighterState[][] Teams` (2×2), `int[] TeamActive`, `int[] TagCooldowns`; `Fighters` aponta para os dois membros atualmente em combate.

`Reserve(player)`, `TeamHealth(player)`, `TeamMaxHealth(player)` e `TryTag(player, bool forced=false)` são a API de troca. Input `Tag` ou defesa+chute troca quando o lutador está livre/no chão; cooldown de 300 passos de simulação, preservando HP, energia e buffs individuais. Projéteis do membro anterior são retirados. KO chama substituição forçada, mesmo durante cooldown; vitória exige derrotar os dois membros. O timeout compara a fração de vida total de cada equipe. `CaptureSnapshot/RestoreSnapshot/StateHash` incluem os quatro membros, reservas, índices e cooldowns. O renderer deve reler `World.Fighters` após cada passo/troca.

Campanha aplica `FighterState.DamageMultiplier` (padrão 1) antes de iniciar a luta. O multiplicador é capturado na cópia do golpe/projétil; nunca altera o catálogo. Em duplas, aplicar benefícios aos dois objetos de `Teams[player]`.

`FighterState`: `Id, Player, X, Y, VelocityX, VelocityY, Face, State, Frame, Move, Hp, MaxHp, Guard, Meter, Stun, Invuln, HitFlash, TransformFrames, UltimateTint, Combo, ComboDamage, ComboTimer, MaxCombo, DamageDealt, DamageReceived, Skin, Rage, Ammo, OverclockFrames, PreparedFrames, FeintFrames, Status, StatusFrames, DodgeFrames, DodgeCooldown, BearX, BearStun, BearPosture, Embalo, EmbaloFrames`. `State` segue strings da base: idle/walk/crouch/block/crouch_block/jump/attack/hitstun/blockstun/knockdown/wakeup/evade/throw/thrown/super/super_victim/victory/ko. `Animation` é a pose sugerida (`idle`, `walk`, `dash`, `guard`, `light`, `heavy`, etc); resolver frames no atlas é tarefa do renderer. `Move` nulo fora de ataques. `Grounded`, `ArmorActive` e `Hurtbox` são propriedades calculadas. `Skin` mantém seleção original; Mirkos usa atlas `mirkos_armor` quando `TransformFrames>0`, nunca muda `Id`.

`ProjectileState`: `Id, Owner, Kind, Visual, X, Y, VelocityX, VelocityY, Face, Age, Life, Radius, Move, Dead, Lane`. `CombatEvent`: `Type, Player, Target, Text, X, Y, Damage, Combo, Pulse, Final, Phase`. `Events` contém somente os eventos do Step atual. Tipos incluem attack, super, ultimate_phase, cinematic_start/end, super_hit, hit, block, perfect_block, parry, counter, projectile, clash, dodge, jump, land, combo, buff, no_meter, ko.

`Cinematic`: `Active, Frame, Duration, Attacker, Defender, Character, Phase, ImpactPause, Pulse, DamageTotal, DamageApplied, OriginAttackerX, OriginDefenderX`. Personagens ficam em super/super_victim e comandos/relógio são congelados. Mirkos usa seis impactos e revela armadura após a sequência; Vitor usa quinze impactos, vermelho somente no próprio atacante. Renderer pode reenquadrar clones sem alterar posições reais.

## CPU e replay

`new CombatAI(uint seed = 1, int difficulty = 1)`; `Decide(CombatWorld world, int player)` retorna InputFrame usando somente estado atual/histórico observado, reação limitada e RNG próprio. Chamar uma vez por passo de simulação, nunca por quadro de renderização.

`CombatSnapshot snapshot = world.CaptureSnapshot()`, `world.RestoreSnapshot(snapshot)` e `ulong world.StateHash()` fornecem checkpoints em memória e hash determinístico. Snapshot não é um formato JSON público: inclui campos internos de entrada/histórico necessários à continuação. `RestoreSnapshot` valida a estrutura antes de tocar a partida; uma estrutura inválida dispara `ArgumentException` e deixa o mundo atual intacto. `ReplayTape.Record(p1,p2)` armazena entradas; `ReplayTape.Play(world)` executa e retorna hashes por passo. Hash não substitui testes de equivalência entre runtimes/arquiteturas diferentes; usa representações IEEE de floats.

Este documento é o contrato inicial de integração; detalhes adicionais e limitações serão anotados após compilação e testes.
